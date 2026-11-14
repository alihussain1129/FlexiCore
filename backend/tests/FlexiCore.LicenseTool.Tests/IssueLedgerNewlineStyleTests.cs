using System.Text;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

/// <summary>Append newline handling for CRLF-terminated and lone-CR-terminated ledgers (direct IssueLedger.Append).</summary>
public sealed class IssueLedgerNewlineStyleTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static LicenseRequest Request(string id) => new(
        LicenseId: id,
        Licensee: "Direct Customer",
        IssuedAt: Now,
        NotBefore: Now,
        ExpiresAt: null,
        GraceDays: 0,
        MaxInstallations: 1,
        InstallationId: null);

    [Fact]
    public void A_crlf_terminated_ledger_gets_the_next_record_directly_after_it_with_no_extra_line_break()
    {
        using var dir = new TempDir();
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(ledger)!);
        File.WriteAllBytes(ledger, Encoding.UTF8.GetBytes("{\"old\":1}\r\n"));
        var before = File.ReadAllBytes(ledger);

        IssueLedger.Append(ledger, Request("LIC-CR"), "fc-test-a", new byte[] { 1 }, Now);

        var after = File.ReadAllBytes(ledger);
        Assert.Equal(before, after[..before.Length]);
        Assert.Equal((byte)'{', after[before.Length]);
    }

    [Fact]
    public void A_ledger_ending_in_a_lone_cr_is_treated_as_unterminated_and_every_byte_is_kept()
    {
        using var dir = new TempDir();
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(ledger)!);
        File.WriteAllBytes(ledger, Encoding.UTF8.GetBytes("{\"old\":1}\r"));
        var before = File.ReadAllBytes(ledger);
        var separator = Encoding.UTF8.GetBytes("\n");

        IssueLedger.Append(ledger, Request("LIC-CR"), "fc-test-a", new byte[] { 1 }, Now);

        var after = File.ReadAllBytes(ledger);
        Assert.Equal(before, after[..before.Length]);
        Assert.Equal(separator, after[before.Length..(before.Length + separator.Length)]);
        Assert.Equal((byte)'{', after[before.Length + separator.Length]);
    }
}
