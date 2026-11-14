using System.Text.Json;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Services;

public enum TermStatus { NotYetValid, Active, ActiveInGrace, Expired }

/// <summary>One line of the existing issue ledger. Status is computed from the recorded dates only.</summary>
public sealed record LedgerEntry(
    DateTimeOffset RecordedAt,
    string LicenseId,
    string Licensee,
    string KeyId,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    int GraceDays,
    int MaxInstallations,
    string FileSha256)
{
    public TermStatus StatusAt(DateTimeOffset now)
    {
        if (now < NotBefore) return TermStatus.NotYetValid;
        if (ExpiresAt is null || now <= ExpiresAt.Value) return TermStatus.Active;
        if (now <= LicenseVerifier.GraceEnd(ExpiresAt.Value, GraceDays)) return TermStatus.ActiveInGrace;
        return TermStatus.Expired;
    }
}

public sealed record LedgerSnapshot(IReadOnlyList<LedgerEntry> Entries, int UnreadableLines, string? Problem);

/// <summary>One non-blank ledger line: its exact text and, if it could be parsed, the record it holds.</summary>
internal sealed record LedgerLine(string Raw, LedgerEntry? Entry);

/// <summary>
/// Immutable capture of one ledger or backup file: the exact bytes read, plus the lines and records derived
/// from those same bytes. Nothing re-reads the file afterwards, so records and raw text cannot disagree.
/// </summary>
internal sealed class LedgerCapture
{
    public LedgerCapture(byte[] bytes, IReadOnlyList<LedgerLine> lines, string? problem)
    {
        Bytes = bytes;
        Lines = lines;
        Problem = problem;
        Entries = lines.Where(l => l.Entry is not null).Select(l => l.Entry!).ToList();
        UnreadableLines = lines.Count - Entries.Count;
    }

    public byte[] Bytes { get; }
    public IReadOnlyList<LedgerLine> Lines { get; }
    public IReadOnlyList<LedgerEntry> Entries { get; }
    public int UnreadableLines { get; }
    public string? Problem { get; }
}

public static class LedgerReader
{
    private const long MaxBytes = 50L * 1024 * 1024;

    /// <summary>
    /// Reads a consistent ledger snapshot while holding the exclusive ledger lock.
    /// Acquiring the lock creates the owner-data folder and the persistent sibling ".lock" file,
    /// even when the ledger itself does not exist yet; the lock file is intentionally left in place.
    /// A missing ledger is reported as an empty snapshot, and reading never creates the ledger file.
    /// Throws <see cref="LicenseToolException"/> if the lock cannot be acquired in time or the
    /// folder is inside a git working tree.
    /// </summary>
    public static LedgerSnapshot Read(string path)
    {
        var full = Path.GetFullPath(path);
        using var ledgerLock = LedgerFileLock.Acquire(full);
        return Read(full, ledgerLock);
    }

    /// <summary>Reads while the caller already owns the lock for this ledger; no second lock is taken.</summary>
    public static LedgerSnapshot Read(string path, LedgerFileLock ledgerLock)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
        var full = Path.GetFullPath(path);
        ledgerLock.EnsureFor(full);
        return ReadCore(full);
    }

    /// <summary>
    /// Reads a separately selected backup without creating a lock file beside it.
    /// The live ledger must still be protected by its own lock.
    /// </summary>
    internal static LedgerSnapshot ReadBackup(string path) =>
        ReadCore(Path.GetFullPath(path));

    private const string TooLarge = "The ledger is larger than 50 MB and was not loaded.";
    private const string ChangedWhileReading = "The file changed while it was being read. Try again.";

    /// <summary>
    /// Captures a separately selected backup in ONE bounded read, without creating a lock beside it.
    /// Decoding is strict UTF-8. The live ledger must still be protected by its own lock.
    /// </summary>
    internal static LedgerCapture CaptureBackup(string path) =>
        Capture(Path.GetFullPath(path), missingIsEmpty: false);

    /// <summary>Captures the live ledger in ONE read while the caller holds its lock. A missing ledger is empty.</summary>
    internal static LedgerCapture CaptureLive(string path, LedgerFileLock ledgerLock)
    {
        ArgumentNullException.ThrowIfNull(ledgerLock);
        var full = Path.GetFullPath(path);
        ledgerLock.EnsureFor(full);
        return Capture(full, missingIsEmpty: true);
    }

    /// <summary>Parses already-captured bytes. Raw line text is kept exactly as stored.</summary>
    internal static LedgerCapture Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length > MaxBytes) return Failed(TooLarge);

        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        string text;
        try
        {
            text = new System.Text.UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (System.Text.DecoderFallbackException)
        {
            return Failed("The ledger is not valid UTF-8 text and was not loaded.");
        }

        var lines = new List<LedgerLine>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            lines.Add(new LedgerLine(line, TryParse(line)));
        }
        return new LedgerCapture(bytes, lines, null);
    }

    private static LedgerCapture Capture(string full, bool missingIsEmpty)
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(full))
                return missingIsEmpty ? Parse(Array.Empty<byte>()) : Failed("The file was not found.");

            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxBytes) return Failed(TooLarge);
            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1) return Failed(ChangedWhileReading);
        }
        catch (EndOfStreamException)
        {
            return Failed(ChangedWhileReading);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failed("The ledger cannot be read (" + ex.GetType().Name + ").");
        }
        return Parse(bytes);
    }

    private static LedgerCapture Failed(string problem) =>
        new(Array.Empty<byte>(), Array.Empty<LedgerLine>(), problem);

    /// <summary>
    /// Shared by Read and ReadBackup. Uses the same strict capture as issuance and restore, so a ledger that is
    /// not valid UTF-8 is reported as a problem instead of being shown with replacement characters.
    /// A missing file is an empty snapshot.
    /// </summary>
    private static LedgerSnapshot ReadCore(string path)
    {
        var capture = Capture(path, missingIsEmpty: true);
        return new LedgerSnapshot(capture.Entries, capture.UnreadableLines, capture.Problem);
    }

    private static LedgerEntry? TryParse(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;

            if (!Text(r, "recordedAt", out var recordedText)
                || !LicenseIssuer.TryParseTimestamp(recordedText, out var recordedAt)) return null;
            if (!Text(r, "licenseId", out var licenseId)) return null;
            if (!Text(r, "licensee", out var licensee)) return null;
            if (!Text(r, "keyId", out var keyId)) return null;
            if (!Text(r, "notBefore", out var notBeforeText)
                || !LicenseIssuer.TryParseTimestamp(notBeforeText, out var notBefore)) return null;

            DateTimeOffset? expiresAt = null;
            if (!r.TryGetProperty("expiresAt", out var expiresElement)) return null;
            if (expiresElement.ValueKind == JsonValueKind.String)
            {
                if (!LicenseIssuer.TryParseTimestamp(expiresElement.GetString() ?? "", out var parsedExpiry)) return null;
                expiresAt = parsedExpiry;
            }
            else if (expiresElement.ValueKind != JsonValueKind.Null)
            {
                return null;
            }

            if (!Number(r, "graceDays", out var grace)) return null;
            if (!Number(r, "maxInstallations", out var installs)) return null;
            Text(r, "fileSha256", out var sha);

            return new LedgerEntry(recordedAt, licenseId, licensee, keyId, notBefore, expiresAt, grace, installs, sha);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Text(JsonElement r, string name, out string value)
    {
        value = "";
        if (r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String)
        {
            value = e.GetString() ?? "";
            return true;
        }
        return false;
    }

    private static bool Number(JsonElement r, string name, out int value)
    {
        value = 0;
        return r.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
    }
}
