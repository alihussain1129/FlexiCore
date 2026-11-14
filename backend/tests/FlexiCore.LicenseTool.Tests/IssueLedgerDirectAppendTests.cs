using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

/// <summary>Calls IssueLedger.Append directly (no CLI), so the append guard itself is proven.</summary>
public sealed class IssueLedgerDirectAppendTests
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
    public void Append_after_a_truncated_final_record_keeps_that_record_and_starts_a_new_line()
    {
        using var dir = new TempDir();
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(ledger)!);
        var truncated = "{\"recordedAt\":\"2026-01-01T00:00:00Z\",\"licenseId\":\"LIC-TR\"";
        File.WriteAllText(ledger, truncated);
        var before = File.ReadAllBytes(ledger);

        IssueLedger.Append(ledger, Request("LIC-D"), "fc-test-a", new byte[] { 1, 2, 3 }, Now);

        var after = File.ReadAllBytes(ledger);
        Assert.Equal(before, after[..before.Length]);
        Assert.Equal((byte)'\n', after[before.Length]);
        var lines = File.ReadAllLines(ledger);
        Assert.Equal(2, lines.Length);
        Assert.Equal(truncated, lines[0]);
        Assert.Contains("\"licenseId\":\"LIC-D\"", lines[1]);
    }

    [Fact]
    public void Append_creates_a_missing_ledger_with_exactly_one_record_and_no_blank_line()
    {
        using var dir = new TempDir();
        var ledger = dir.Combine("owner/issue-ledger.jsonl");

        IssueLedger.Append(ledger, Request("LIC-D"), "fc-test-a", new byte[] { 1, 2, 3 }, Now);

        var bytes = File.ReadAllBytes(ledger);
        Assert.Equal((byte)'{', bytes[0]);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Single(File.ReadAllLines(ledger));
    }
}
