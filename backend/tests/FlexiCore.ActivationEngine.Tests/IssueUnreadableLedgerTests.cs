using System.Text;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

/// <summary>Issuance must fail closed when the ledger cannot be read or is not strict UTF-8, same as restore.</summary>
public sealed class IssueUnreadableLedgerTests
{
    [Fact]
    public void A_ledger_that_cannot_be_opened_blocks_issuance_and_nothing_is_written()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-U-0"));
        var before = File.ReadAllBytes(o.Paths.LedgerPath);
        var output = o.Out("blocked.lic");

        using (new FileStream(o.Paths.LedgerPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, "FC-U-1", output: output)));
        }

        Assert.False(File.Exists(output));
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
    }

    [Fact]
    public void A_ledger_with_invalid_utf8_inside_an_otherwise_valid_record_blocks_issuance()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-U-0"));
        var text = File.ReadAllText(o.Paths.LedgerPath);
        Assert.Contains("Test Customer", text);
        var marked = Encoding.UTF8.GetBytes(text.Replace("Test Customer", "Test @Customer"));
        var at = Array.IndexOf(marked, (byte)'@');
        Assert.True(at > 0);
        var damaged = marked.Take(at).Concat(new byte[] { 0xC3, 0x28 }).Concat(marked.Skip(at + 1)).ToArray();
        File.WriteAllBytes(o.Paths.LedgerPath, damaged);
        var output = o.Out("blocked.lic");

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, "FC-U-1", output: output)));

        Assert.False(File.Exists(output));
        Assert.Equal(damaged, File.ReadAllBytes(o.Paths.LedgerPath));
    }
}
