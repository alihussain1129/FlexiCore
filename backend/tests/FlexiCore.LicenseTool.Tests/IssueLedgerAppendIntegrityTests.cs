using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

/// <summary>
/// Append integrity: a new record must never join an unterminated final line, and no existing byte may change.
/// Driven through the CLI so the real lock, duplicate check and append path are all exercised.
/// </summary>
public sealed class IssueLedgerAppendIntegrityTests
{
    private const string Pass = "correct horse battery staple";
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static int Run(FakeConsole console, params string[] args) =>
        LicenseToolApp.Run(args, console, new PinnedTimeProvider(Now));

    private static string KeyGen(TempDir dir)
    {
        Assert.Equal(0, Run(new FakeConsole(Pass, Pass), "keygen", "--key-id", "fc-test-a", "--out-dir", dir.Path));
        return dir.Combine("fc-test-a.p8");
    }

    private static string[] Args(string key, string output, string ledger, string id) => new[]
    {
        "issue", "--key", key, "--public-keys", Path.ChangeExtension(key, ".public.json"),
        "--license-id", id, "--licensee", "Test Customer",
        "--not-before", "2026-01-01", "--expires", "2027-01-01", "--installations", "3",
        "--out", output, "--ledger", ledger,
    };

    private static string[] Records(string ledger) =>
        File.ReadAllLines(ledger).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

    [Fact]
    public void An_empty_ledger_file_gets_its_first_record_without_a_leading_blank_line()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(ledger)!);
        File.WriteAllBytes(ledger, Array.Empty<byte>());

        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));

        var bytes = File.ReadAllBytes(ledger);
        Assert.Equal((byte)'{', bytes[0]);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Single(File.ReadAllLines(ledger));
    }

    [Fact]
    public void A_terminated_ledger_gets_the_next_record_on_the_next_line_with_no_blank_line()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));
        var before = File.ReadAllBytes(ledger);

        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("b.lic"), ledger, "LIC-B")));

        var after = File.ReadAllBytes(ledger);
        Assert.Equal(before, after[..before.Length]);
        Assert.Equal(2, File.ReadAllLines(ledger).Length);
    }

    [Fact]
    public void A_complete_record_missing_its_final_newline_is_kept_byte_for_byte_and_the_next_record_starts_a_new_line()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));
        var terminated = File.ReadAllBytes(ledger);
        var unterminated = terminated[..^Environment.NewLine.Length];
        File.WriteAllBytes(ledger, unterminated);

        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("b.lic"), ledger, "LIC-B")));

        var after = File.ReadAllBytes(ledger);
        Assert.Equal(unterminated, after[..unterminated.Length]);
        Assert.Equal((byte)'\n', after[unterminated.Length]);
        var lines = File.ReadAllLines(ledger);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, l =>
        {
            Assert.StartsWith("{", l);
            Assert.EndsWith("}", l);
        });
    }

    [Fact]
    public void A_truncated_final_record_blocks_issuance_and_every_byte_is_preserved()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(ledger)!);
        File.WriteAllText(ledger, "{\"recordedAt\":\"2026-01-01T00:00:00Z\",\"licenseId\":\"LIC-TR\"");
        var before = File.ReadAllBytes(ledger);
        var license = dir.Combine("a.lic");

        var code = Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-NEW"));

        Assert.NotEqual(0, code);
        Assert.False(File.Exists(license));
        Assert.Equal(before, File.ReadAllBytes(ledger));
    }

    [Fact]
    public void The_ledger_lock_is_released_after_a_failed_append()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(ledger); // a directory where the ledger file should be: append must fail

        Assert.NotEqual(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));

        Assert.True(Directory.Exists(ledger));
        using var ledgerLock = LedgerFileLock.Acquire(ledger, TimeSpan.FromSeconds(2));
        ledgerLock.EnsureFor(ledger);
    }
}
