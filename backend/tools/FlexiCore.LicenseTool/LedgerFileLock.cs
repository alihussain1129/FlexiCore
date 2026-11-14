using System.Diagnostics;

namespace FlexiCore.LicenseTool;

/// <summary>
/// Cross-process exclusive lock shared by issuance and ledger restoration.
/// The lock file is intentionally persistent; only its open handle represents ownership.
/// </summary>
public sealed class LedgerFileLock : IDisposable
{
    private readonly FileStream _stream;

    private LedgerFileLock(string ledgerPath, FileStream stream)
    {
        LedgerPath = ledgerPath;
        _stream = stream;
    }

    public string LedgerPath { get; }

    public static LedgerFileLock Acquire(string ledgerPath, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(ledgerPath) || !Path.IsPathFullyQualified(ledgerPath))
            throw new LicenseToolException("The ledger lock path must be absolute.");

        var full = Path.GetFullPath(ledgerPath);
        var directory = Path.GetDirectoryName(full)
            ?? throw new LicenseToolException("The ledger folder could not be determined.");

        OutputPathGuard.EnsureOutsideGitWorkingTree(directory, "The ledger folder");
        Directory.CreateDirectory(directory);

        var wait = timeout ?? TimeSpan.FromSeconds(30);
        if (wait < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var lockPath = full + ".lock";
        var timer = Stopwatch.StartNew();

        while (true)
        {
            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);

                return new LedgerFileLock(full, stream);
            }
            catch (IOException) when (timer.Elapsed < wait)
            {
                Thread.Sleep(50);
            }
            catch (IOException ex)
            {
                throw new LicenseToolException(
                    "Timed out waiting for the issue ledger lock (" + ex.GetType().Name
                    + "). Another issuance or restore operation may still be running.", 2);
            }
        }
    }

    public void EnsureFor(string ledgerPath)
    {
        var full = Path.GetFullPath(ledgerPath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!string.Equals(LedgerPath, full, comparison))
            throw new InvalidOperationException(
                "The supplied ledger lock does not protect this ledger path.");
    }

    public void Dispose() => _stream.Dispose();
}
