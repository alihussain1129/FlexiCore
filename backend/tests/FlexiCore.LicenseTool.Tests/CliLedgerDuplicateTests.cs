using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

public sealed class CliLedgerDuplicateTests
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

    private static string[] Args(string key, string output, string ledger, string id, params string[] extra)
    {
        var args = new List<string>
        {
            "issue", "--key", key, "--public-keys", Path.ChangeExtension(key, ".public.json"),
            "--license-id", id, "--licensee", "Test Customer",
            "--not-before", "2026-01-01", "--expires", "2027-01-01", "--installations", "3",
            "--out", output, "--ledger", ledger,
        };
        args.AddRange(extra);
        return args.ToArray();
    }

    private static string[] Records(string ledger) =>
        File.ReadAllLines(ledger).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

    [Fact]
    public void First_issuance_succeeds_and_is_recorded_once()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        var license = dir.Combine("a.lic");

        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-A")));

        Assert.True(File.Exists(license));
        Assert.Single(Records(ledger));
    }

    [Fact]
    public void Reissuing_the_same_license_id_fails_and_changes_nothing()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));
        var before = File.ReadAllBytes(ledger);
        var second = dir.Combine("b.lic");

        var code = Run(new FakeConsole(Pass), Args(key, second, ledger, "LIC-A"));

        Assert.NotEqual(0, code);
        Assert.False(File.Exists(second));
        Assert.Equal(before, File.ReadAllBytes(ledger));
    }

    [Fact]
    public void Different_license_ids_can_both_be_issued()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");

        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));
        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("b.lic"), ledger, "LIC-B")));

        Assert.Equal(2, Records(ledger).Length);
    }

    [Fact]
    public async Task Concurrent_issuance_of_the_same_id_records_exactly_one_license()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        var first = dir.Combine("race-1.lic");
        var second = dir.Combine("race-2.lic");

        var codes = await Task.WhenAll(
            Task.Run(() => Run(new FakeConsole(Pass), Args(key, first, ledger, "LIC-RACE"))),
            Task.Run(() => Run(new FakeConsole(Pass), Args(key, second, ledger, "LIC-RACE"))));

        Assert.Single(codes, c => c == 0);
        Assert.Single(codes, c => c != 0);
        Assert.Single(Records(ledger));
        Assert.Equal(1, new[] { File.Exists(first), File.Exists(second) }.Count(e => e));
    }

    [Fact]
    public void Force_replaces_output_files_but_never_bypasses_the_duplicate_check()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        var license = dir.Combine("a.lic");
        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-A")));
        var licenseBefore = File.ReadAllBytes(license);
        var ledgerBefore = File.ReadAllBytes(ledger);

        var code = Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-A", "--force"));

        Assert.NotEqual(0, code);
        Assert.Equal(licenseBefore, File.ReadAllBytes(license));
        Assert.Equal(ledgerBefore, File.ReadAllBytes(ledger));
    }

    [Fact]
    public void The_ledger_lock_is_released_after_a_refused_issuance()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Assert.Equal(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("a.lic"), ledger, "LIC-A")));
        Assert.NotEqual(0, Run(new FakeConsole(Pass), Args(key, dir.Combine("b.lic"), ledger, "LIC-A")));

        using var ledgerLock = LedgerFileLock.Acquire(ledger, TimeSpan.FromSeconds(2));

        ledgerLock.EnsureFor(ledger);
    }

    [Fact]
    public void A_corrupt_ledger_blocks_issuance_and_is_left_untouched()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(ledger)!);
        File.WriteAllText(ledger, "not json" + Environment.NewLine);
        var before = File.ReadAllBytes(ledger);
        var license = dir.Combine("a.lic");

        var code = Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-A"));

        Assert.NotEqual(0, code);
        Assert.False(File.Exists(license));
        Assert.Equal(before, File.ReadAllBytes(ledger));
    }

    [Fact]
    public void A_failed_ledger_append_leaves_no_new_license_file()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(ledger); // a directory where the ledger file should be: append must fail
        var license = dir.Combine("a.lic");

        var code = Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-A", "--format", "both"));

        Assert.NotEqual(0, code);
        Assert.False(File.Exists(license));
        Assert.False(File.Exists(license + ".code.txt"));
    }

    [Fact]
    public void A_failed_ledger_append_restores_a_file_that_force_had_replaced()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("owner/issue-ledger.jsonl");
        Directory.CreateDirectory(ledger);
        var license = dir.Combine("a.lic");
        File.WriteAllText(license, "keep me");

        var code = Run(new FakeConsole(Pass), Args(key, license, ledger, "LIC-A", "--force"));

        Assert.NotEqual(0, code);
        Assert.Equal("keep me", File.ReadAllText(license));
    }
}
