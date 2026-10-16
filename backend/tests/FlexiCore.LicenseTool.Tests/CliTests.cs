using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

public sealed class CliTests
{
    private const string Pass = "correct horse battery staple";
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static int Run(FakeConsole console, params string[] args) =>
        LicenseToolApp.Run(args, console, new PinnedTimeProvider(Now));

    private static string KeyGen(TempDir dir, string id = "fc-test-a")
    {
        var console = new FakeConsole(Pass, Pass);
        Assert.Equal(0, Run(console, "keygen", "--key-id", id, "--out-dir", dir.Path));
        return dir.Combine(id + ".p8");
    }

    private static string[] IssueArgs(string key, string output, string expires = "2027-01-01", params string[] extra)
    {
        var args = new List<string>
        {
            "issue", "--key", key, "--license-id", "LIC-0007", "--licensee", "Test Customer",
            "--not-before", "2026-01-01", "--expires", expires, "--installations", "3", "--out", output,
        };
        args.AddRange(extra);
        return args.ToArray();
    }

    [Fact]
    public void Keygen_issue_and_verify_work_end_to_end()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var license = dir.Combine("license.lic");
        var pub = dir.Combine("fc-test-a.public.json");

        var issue = new FakeConsole(Pass);
        Assert.Equal(0, Run(issue, IssueArgs(key, license)));

        var ok = new FakeConsole();
        Assert.Equal(0, Run(ok, "verify", license, "--public-keys", pub, "--at", "2026-06-01"));
        Assert.Contains("Status: Valid", ok.AllOutput, StringComparison.Ordinal);
        Assert.Contains("LIC-0007", ok.AllOutput, StringComparison.Ordinal);

        var late = new FakeConsole();
        Assert.Equal(3, Run(late, "verify", license, "--public-keys", pub, "--at", "2028-01-01"));
        Assert.Contains("Expired", late.AllOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_private_key_file_is_encrypted_and_no_output_leaks_secrets()
    {
        using var dir = new TempDir();
        var console = new FakeConsole(Pass, Pass);
        Assert.Equal(0, Run(console, "keygen", "--key-id", "fc-test-a", "--out-dir", dir.Path));

        var pem = File.ReadAllText(dir.Combine("fc-test-a.p8"));
        Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----", pem, StringComparison.Ordinal);
        Assert.DoesNotContain("-----BEGIN PRIVATE KEY-----", pem, StringComparison.Ordinal);

        var issue = new FakeConsole(Pass);
        Assert.Equal(0, Run(issue, IssueArgs(dir.Combine("fc-test-a.p8"), dir.Combine("l.lic"))));

        foreach (var text in new[] { console.AllOutput, issue.AllOutput })
        {
            Assert.DoesNotContain(Pass, text, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE KEY", text, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("PRIVATE KEY", File.ReadAllText(dir.Combine("fc-test-a.public.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void Keygen_refuses_a_folder_inside_a_git_working_tree()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Combine(".git"));
        var target = dir.Combine("keys");

        var console = new FakeConsole(Pass, Pass);
        Assert.Equal(1, Run(console, "keygen", "--key-id", "fc-test-a", "--out-dir", target));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Keygen_refuses_a_relative_folder()
    {
        var console = new FakeConsole(Pass, Pass);
        Assert.Equal(1, Run(console, "keygen", "--key-id", "fc-test-a", "--out-dir", "keys"));
    }

    [Fact]
    public void Keygen_refuses_a_weak_passphrase_and_writes_nothing()
    {
        using var dir = new TempDir();
        var target = dir.Combine("keys");

        Assert.Equal(1, Run(new FakeConsole("short", "short"), "keygen", "--key-id", "fc-test-a", "--out-dir", target));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Keygen_refuses_mismatched_confirmation_and_writes_nothing()
    {
        using var dir = new TempDir();
        var target = dir.Combine("keys");

        Assert.Equal(1, Run(new FakeConsole(Pass, Pass + "x"), "keygen", "--key-id", "fc-test-a", "--out-dir", target));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Keygen_needs_an_interactive_passphrase()
    {
        using var dir = new TempDir();
        Assert.Equal(1, Run(new FakeConsole(), "keygen", "--key-id", "fc-test-a", "--out-dir", dir.Combine("keys")));
    }

    [Fact]
    public void Keygen_refuses_an_invalid_key_id()
    {
        using var dir = new TempDir();
        Assert.Equal(1, Run(new FakeConsole(Pass, Pass), "keygen", "--key-id", "bad id", "--out-dir", dir.Path));
    }

    [Fact]
    public void Keygen_does_not_overwrite_without_force()
    {
        using var dir = new TempDir();
        KeyGen(dir);

        Assert.Equal(1, Run(new FakeConsole(Pass, Pass), "keygen", "--key-id", "fc-test-a", "--out-dir", dir.Path));
        Assert.Equal(0, Run(new FakeConsole(Pass, Pass), "keygen", "--key-id", "fc-test-a", "--out-dir", dir.Path, "--force"));
    }

    [Fact]
    public void A_wrong_passphrase_issues_nothing()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var license = dir.Combine("license.lic");

        var code = Run(new FakeConsole("a different long passphrase"), IssueArgs(key, license));

        Assert.Equal(2, code);
        Assert.False(File.Exists(license));
    }

    [Fact]
    public void Issue_requires_an_explicit_expiry_and_never_makes_a_perpetual_license_by_default()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var license = dir.Combine("license.lic");

        var code = Run(new FakeConsole(Pass),
            "issue", "--key", key, "--license-id", "LIC-1", "--licensee", "X", "--out", license);

        Assert.Equal(1, code);
        Assert.False(File.Exists(license));
    }

    [Fact]
    public void Expires_never_makes_a_perpetual_license_that_verifies_far_in_the_future()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var license = dir.Combine("license.lic");

        Assert.Equal(0, Run(new FakeConsole(Pass), IssueArgs(key, license, expires: "never")));
        Assert.Equal(0, Run(new FakeConsole(), "verify", license,
            "--public-keys", dir.Combine("fc-test-a.public.json"), "--at", "2099-01-01"));
    }

    [Fact]
    public void Issue_does_not_overwrite_an_existing_license_without_force()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var license = dir.Combine("license.lic");
        File.WriteAllText(license, "keep me");

        Assert.Equal(1, Run(new FakeConsole(Pass), IssueArgs(key, license)));
        Assert.Equal("keep me", File.ReadAllText(license));
    }

    [Fact]
    public void Export_public_matches_the_public_file_written_by_keygen()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var exported = dir.Combine("exported.json");

        Assert.Equal(0, Run(new FakeConsole(Pass), "export-public", "--key", key, "--out", exported));

        var a = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(exported));
        var b = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(dir.Combine("fc-test-a.public.json")));
        Assert.Single(a);
        Assert.Equal(b[0].KeyId, a[0].KeyId);
        Assert.True(a[0].SubjectPublicKeyInfo.AsSpan().SequenceEqual(b[0].SubjectPublicKeyInfo));
    }

    [Fact]
    public void Build_keyset_merges_two_public_keys_and_the_loader_accepts_the_result()
    {
        using var dirA = new TempDir();
        using var dirB = new TempDir();
        KeyGen(dirA, "fc-test-a");
        KeyGen(dirB, "fc-test-b");
        var output = dirA.Combine("keyset.json");

        var code = Run(new FakeConsole(), "build-keyset", "--out", output,
            dirA.Combine("fc-test-a.public.json"), dirB.Combine("fc-test-b.public.json"));

        Assert.Equal(0, code);
        Assert.Equal(2, ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(output)).Count);
    }

    [Fact]
    public void Build_keyset_refuses_duplicate_key_ids()
    {
        using var dirA = new TempDir();
        using var dirB = new TempDir();
        KeyGen(dirA, "fc-test-a");
        KeyGen(dirB, "fc-test-a");
        var output = dirA.Combine("keyset.json");

        var code = Run(new FakeConsole(), "build-keyset", "--out", output,
            dirA.Combine("fc-test-a.public.json"), dirB.Combine("fc-test-a.public.json"));

        Assert.Equal(1, code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Build_keyset_refuses_test_key_ids()
    {
        using var dir = new TempDir();
        KeyGen(dir, "interop-test-1");
        var output = dir.Combine("keyset.json");

        var code = Run(new FakeConsole(), "build-keyset", "--out", output, dir.Combine("interop-test-1.public.json"));

        Assert.Equal(1, code);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void The_ledger_records_each_license_without_secrets()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var ledger = dir.Combine("issued.jsonl");

        var console = new FakeConsole(Pass);
        Assert.Equal(0, Run(console, IssueArgs(key, dir.Combine("l1.lic"), "2027-01-01", "--ledger", ledger)));

        var lines = File.ReadAllLines(ledger);
        Assert.Single(lines);
        using var doc = JsonDocument.Parse(lines[0]);
        Assert.Equal("LIC-0007", doc.RootElement.GetProperty("licenseId").GetString());
        Assert.Equal("fc-test-a", doc.RootElement.GetProperty("keyId").GetString());
        Assert.Equal(64, doc.RootElement.GetProperty("fileSha256").GetString()!.Length);
        Assert.DoesNotContain(Pass, lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_ledger_is_refused_inside_a_git_working_tree()
    {
        using var dir = new TempDir();
        var key = KeyGen(dir);
        var repo = dir.Combine("repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var license = dir.Combine("l1.lic");

        var code = Run(new FakeConsole(Pass), IssueArgs(key, license, "2027-01-01", "--ledger", Path.Combine(repo, "issued.jsonl")));

        Assert.Equal(1, code);
        Assert.False(File.Exists(license));
    }

    [Fact]
    public void Keygen_stops_and_removes_temporary_file_when_permission_restriction_fails()
    {
        using var dir = new TempDir();

        var error = Assert.Throws<LicenseToolException>(() =>
            KeyStore.CreateForTesting("fc-test-acl", dir.Path, Pass, false, _ => false));

        Assert.Contains("Could not restrict", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(dir.Combine("fc-test-acl.p8")));
        Assert.False(File.Exists(dir.Combine("fc-test-acl.public.json")));
        Assert.Empty(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void Keygen_acl_failure_does_not_overwrite_existing_private_key_when_force_is_set()
    {
        using var dir = new TempDir();
        var original = KeyStore.Create("fc-test-acl", dir.Path, Pass, false);
        var originalPrivate = File.ReadAllBytes(original.PrivateKeyPath);
        var originalPublic = File.ReadAllBytes(original.PublicKeyPath);

        Assert.Throws<LicenseToolException>(() =>
            KeyStore.CreateForTesting("fc-test-acl", dir.Path, Pass, true, _ => false));

        Assert.Equal(originalPrivate, File.ReadAllBytes(original.PrivateKeyPath));
        Assert.Equal(originalPublic, File.ReadAllBytes(original.PublicKeyPath));
        Assert.Empty(Directory.GetFiles(dir.Path, "*.tmp"));
    }
    [Fact]
    public void Unknown_commands_and_options_are_usage_errors()
    {
        Assert.Equal(1, Run(new FakeConsole(), "frobnicate"));
        Assert.Equal(1, Run(new FakeConsole(), "keygen", "--nope", "x"));
        Assert.Equal(1, Run(new FakeConsole()));
        Assert.Equal(0, Run(new FakeConsole(), "help"));
    }
}
