using FlexiCore.LicenseTool;

// Test-only helper. Holds the ledger lock in this separate OS process until told to release.
// usage: FlexiCore.LedgerLockHelper <ledgerPath> <timeoutMs>
if (args.Length < 2 || !int.TryParse(args[1], out var timeoutMs))
{
    Console.Error.WriteLine("usage: <ledgerPath> <timeoutMs>");
    return 64;
}

LedgerFileLock held;
try
{
    held = LedgerFileLock.Acquire(args[0], TimeSpan.FromMilliseconds(timeoutMs));
}
catch (LicenseToolException ex)
{
    Console.WriteLine("FAILED: " + ex.Message);
    return 2;
}

using (held)
{
    Console.WriteLine("LOCKED");
    Console.Out.Flush();
    // Returns on a line from the parent, or at EOF when the parent closes stdin or dies.
    Console.In.ReadLine();
}

Console.WriteLine("RELEASED");
return 0;
