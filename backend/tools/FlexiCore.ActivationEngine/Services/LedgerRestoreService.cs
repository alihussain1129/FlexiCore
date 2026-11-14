using System.Globalization;
using System.Text;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public sealed record LedgerRestoreResult(int Added, int AlreadyPresent, int TotalAfter, string? SafetyCopyPath);

/// <summary>
/// Restores issue-ledger records from a backup by merging. Existing ledger bytes are never changed or removed:
/// only records whose license id is missing are appended. The current ledger is copied aside first and the
/// result is written atomically. Do not issue licenses while a restore is running.
///
/// Consistency: the backup and the live ledger are each read ONCE into a byte snapshot. Parsing, validation,
/// duplicate detection and the raw JSONL text that gets written all come from those same bytes.
///
/// Failure cleanup: the temp file is removed in a finally block for every exception type. The live ledger is only
/// ever changed by the final atomic move, and the .before-restore safety copy is never deleted.
///
/// Duplicate-id policy (nothing is ever silently chosen, merged or discarded):
///  - A backup that lists a license id more than once is refused, whether the copies are identical or not.
///  - If a backup id appears more than once in the current ledger, the restore is refused, whether the copies
///    are identical or not. Duplicates the backup does not mention are left alone (existing bytes never change).
///  - An id that appears once in the current ledger must match the backup record exactly (counted as already
///    present); any difference refuses the whole restore.
/// </summary>
public sealed class LedgerRestoreService
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly OwnerPaths _paths;
    private readonly TimeProvider _time;

    public LedgerRestoreService(OwnerPaths paths, TimeProvider time)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Test seam: called with the temp file path after it is fully written and before it replaces the ledger.</summary>
    internal Action<string>? BeforeReplaceForTests { get; set; }

    /// <summary>Best-effort removal of the temp file. A cleanup failure must never replace the original error.</summary>
    private static void TryDeleteTemp(string temp)
    {
        try { File.Delete(temp); }
        catch (Exception) { /* intentionally ignored: the original failure is the one the caller must see */ }
    }

    private sealed record Plan(IReadOnlyList<string> ToAdd, int AlreadyPresent, int TotalAfter, byte[] NewContent);

    /// <summary>Checks the backup and reports what a restore would do. Writes nothing.</summary>
    public LedgerRestoreResult Preview(string backupPath)
    {
        using var ledgerLock = LedgerFileLock.Acquire(_paths.LedgerPath);
        var plan = Prepare(backupPath, ledgerLock);
        return new LedgerRestoreResult(plan.ToAdd.Count, plan.AlreadyPresent, plan.TotalAfter, null);
    }

    public LedgerRestoreResult Restore(string backupPath)
    {
        // Hold the same lock as issuance from planning through the atomic replacement.
        using var ledgerLock = LedgerFileLock.Acquire(_paths.LedgerPath);
        var plan = Prepare(backupPath, ledgerLock);
        if (plan.ToAdd.Count == 0)
            return new LedgerRestoreResult(0, plan.AlreadyPresent, plan.TotalAfter, null);

        var ledgerPath = _paths.LedgerPath;
        var temp = ledgerPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string? safetyCopy = null;
        var replaced = false;
        try
        {
            Directory.CreateDirectory(_paths.Root);
            if (File.Exists(ledgerPath))
            {
                var stamp = _time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                safetyCopy = ledgerPath + ".before-restore-" + stamp + "-" + Guid.NewGuid().ToString("N")[..6];
                File.Copy(ledgerPath, safetyCopy, overwrite: false);
            }
            // Write through a stream and flush to disk BEFORE the rename, so a crash or power loss cannot leave a
            // renamed but incomplete ledger. CreateNew refuses to reuse an existing temp name.
            using (var tempStream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                tempStream.Write(plan.NewContent, 0, plan.NewContent.Length);
                tempStream.Flush(true);
            }
            BeforeReplaceForTests?.Invoke(temp);
            File.Move(temp, ledgerPath, overwrite: true);
            replaced = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LicenseToolException(
                "The ledger could not be restored (" + ex.GetType().Name + "). The existing ledger was not changed.", 2);
        }
        finally
        {
            // Runs on every exit path (I/O errors, cancellation, unexpected exceptions). It only removes the temp
            // file. The live ledger is never touched here, and the safety copy is kept on purpose as evidence.
            if (!replaced) TryDeleteTemp(temp);
        }
        return new LedgerRestoreResult(plan.ToAdd.Count, plan.AlreadyPresent, plan.TotalAfter, safetyCopy);
    }

    private Plan Prepare(string backupPath, LedgerFileLock ledgerLock)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || !Path.IsPathFullyQualified(backupPath))
            throw new LicenseToolException("The ledger backup path must be absolute.");
        var backup = Path.GetFullPath(backupPath);
        var ledger = Path.GetFullPath(_paths.LedgerPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(backup, ledger, comparison))
            throw new LicenseToolException("Choose a backup file, not the live ledger itself.");
        if (!File.Exists(backup))
            throw new LicenseToolException("The ledger backup file was not found.");

        OutputPathGuard.EnsureOutsideGitWorkingTree(_paths.Root, "The owner data folder");

        // ONE read of the backup. Records and raw lines below are derived from the same captured bytes.
        var fromBackup = LedgerReader.CaptureBackup(backup);
        if (fromBackup.Problem is not null)
            throw new LicenseToolException("The backup cannot be used. " + fromBackup.Problem);
        if (fromBackup.UnreadableLines > 0)
            throw new LicenseToolException(
                "The backup has " + fromBackup.UnreadableLines.ToString(CultureInfo.InvariantCulture)
                + " line(s) that cannot be read, so nothing was restored. Use an undamaged backup.");
        if (fromBackup.Lines.Count == 0)
            throw new LicenseToolException("The backup has no ledger records.");

        var repeated = fromBackup.Entries
            .GroupBy(e => e.LicenseId, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (repeated is not null)
            throw new LicenseToolException(
                "The backup lists license id '" + repeated.Key + "' more than once, so nothing was restored.");

        // ONE read of the live ledger, under the lock. These same bytes are the base of the new content.
        var current = LedgerReader.CaptureLive(ledger, ledgerLock);
        if (current.Problem is not null)
            throw new LicenseToolException(
                "The current ledger cannot be read, so nothing was restored. " + current.Problem, 2);

        var currentById = current.Entries.ToLookup(e => e.LicenseId, StringComparer.Ordinal);
        var toAdd = new List<string>();
        var already = 0;
        foreach (var line in fromBackup.Lines)
        {
            var entry = line.Entry!;
            var matches = currentById[entry.LicenseId].ToList();
            if (matches.Count == 0)
            {
                toAdd.Add(line.Raw);
            }
            else if (matches.Count > 1)
            {
                throw new LicenseToolException(
                    "License id '" + entry.LicenseId + "' appears more than once in the current ledger, so it cannot "
                    + "be compared safely. Nothing was restored. Resolve the duplicate by hand first.");
            }
            else if (matches[0] == entry)
            {
                already++;
            }
            else
            {
                throw new LicenseToolException(
                    "License id '" + entry.LicenseId + "' is in both the ledger and the backup with different details. "
                    + "Nothing was restored. Resolve this by hand before restoring.");
            }
        }

        var existing = current.Bytes;
        var nl = "\n";
        var added = new StringBuilder();
        if (existing.Length > 0 && existing[^1] != (byte)'\n') added.Append(nl);
        foreach (var line in toAdd) added.Append(line).Append(nl);
        var content = existing.Concat(Utf8.GetBytes(added.ToString())).ToArray();

        return new Plan(toAdd, already, current.Entries.Count + toAdd.Count, content);
    }
}
