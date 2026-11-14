using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;
using System.Text;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class LedgerReaderStrictReadTests
{
    [Fact]
    public void Read_refuses_a_ledger_that_is_not_valid_utf8_instead_of_showing_replacement_characters()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-V-1", output: o.Out("v.lic")));

        var good = File.ReadAllBytes(o.Paths.LedgerPath);
        var marker = System.Text.Encoding.UTF8.GetBytes("FC-V-1");
        var at = System.MemoryExtensions.IndexOf(good.AsSpan(), marker.AsSpan());
        Assert.True(at > 0);
        var damaged = good.Take(at).Concat(new byte[] { 0xC3, 0x28 }).Concat(good.Skip(at)).ToArray();
        File.WriteAllBytes(o.Paths.LedgerPath, damaged);

        var snapshot = LedgerReader.Read(o.Paths.LedgerPath);

        Assert.NotNull(snapshot.Problem);
        Assert.Contains("UTF-8", snapshot.Problem);
        Assert.Empty(snapshot.Entries);
        Assert.Equal(damaged, File.ReadAllBytes(o.Paths.LedgerPath));
    }

    [Fact]
    public void Read_returns_the_issued_record_from_a_valid_ledger()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-V-2", output: o.Out("v2.lic")));

        var snapshot = LedgerReader.Read(o.Paths.LedgerPath);

        Assert.Null(snapshot.Problem);
        Assert.Equal(0, snapshot.UnreadableLines);
        Assert.Equal("FC-V-2", Assert.Single(snapshot.Entries).LicenseId);
    }

    [Fact]
    public void Read_of_a_missing_ledger_is_empty_without_a_problem()
    {
        using var o = new Owner();

        var snapshot = LedgerReader.Read(o.Paths.LedgerPath);

        Assert.Null(snapshot.Problem);
        Assert.Empty(snapshot.Entries);
        Assert.Equal(0, snapshot.UnreadableLines);
    }
}
