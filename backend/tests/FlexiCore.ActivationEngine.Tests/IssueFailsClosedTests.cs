using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class IssueFailsClosedTests
{
    [Fact]
    public void A_damaged_ledger_line_blocks_issuance_even_if_it_might_hide_the_same_id()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        Directory.CreateDirectory(o.Paths.Root);
        File.WriteAllText(o.Paths.LedgerPath, "{\"licenseId\":\"FC-F-9\", broken" + Environment.NewLine);
        var before = File.ReadAllBytes(o.Paths.LedgerPath);
        var output = o.Out("blocked.lic");

        var ex = Assert.Throws<LicenseToolException>(
            () => o.Issue.Issue(o.Input(key, "FC-F-9", output: output)));

        Assert.Contains("nothing was issued", ex.Message);
        Assert.False(File.Exists(output));
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
    }

    [Fact]
    public void The_ledger_lock_is_released_after_a_blocked_issuance()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        Directory.CreateDirectory(o.Paths.Root);
        File.WriteAllText(o.Paths.LedgerPath, "not json" + Environment.NewLine);

        Assert.Throws<LicenseToolException>(
            () => o.Issue.Issue(o.Input(key, "FC-F-1", output: o.Out("x.lic"))));

        using var ledgerLock = LedgerFileLock.Acquire(o.Paths.LedgerPath, TimeSpan.FromSeconds(2));
        ledgerLock.EnsureFor(o.Paths.LedgerPath);
    }
}
