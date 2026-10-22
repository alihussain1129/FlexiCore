using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class IssueServiceTests
{
    [Fact]
    public void Issued_license_is_accepted_by_the_existing_verifier_and_recorded_in_the_ledger()
    {
        using var o = new Owner();
        var key = o.CreateKey();

        var outcome = o.Issue.Issue(o.Input(key, licensee: "Acme Ltd", installs: 3, grace: 7));

        Assert.True(File.Exists(outcome.LicensePath));
        var result = o.Verify(key, outcome.LicensePath);
        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal("FC-TEST-0001", result.Payload!.LicenseId);
        Assert.Equal("Acme Ltd", result.Payload.Licensee);
        Assert.Equal(3, result.Payload.MaxInstallations);
        Assert.Equal(7, result.Payload.GraceDays);

        var ledger = LedgerReader.Read(o.Paths.LedgerPath);
        Assert.Null(ledger.Problem);
        var entry = Assert.Single(ledger.Entries);
        Assert.Equal("FC-TEST-0001", entry.LicenseId);
        Assert.Equal(key, entry.KeyId);
        Assert.Equal(outcome.Sha256, entry.FileSha256);
    }

    [Fact]
    public void Bound_license_carries_the_installation_id_and_unbound_license_does_not()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var id = InstallationIds.FromMachineSource("unit-test-machine");
        Assert.True(InstallationIds.IsValid(id));

        var bound = o.Issue.Issue(o.Input(key, "FC-BOUND", installationId: id));
        var unbound = o.Issue.Issue(o.Input(key, "FC-UNBOUND"));

        Assert.True(bound.Bound);
        Assert.Equal(id, o.Verify(key, bound.LicensePath).Payload!.InstallationId);
        Assert.False(unbound.Bound);
        Assert.Null(o.Verify(key, unbound.LicensePath).Payload!.InstallationId);
    }

    [Fact]
    public void Perpetual_license_has_no_expiry()
    {
        using var o = new Owner();
        var key = o.CreateKey();

        var outcome = o.Issue.Issue(o.Input(key, perpetual: true));

        Assert.Null(o.Verify(key, outcome.LicensePath).Payload!.ExpiresAt);
        Assert.Null(Assert.Single(LedgerReader.Read(o.Paths.LedgerPath).Entries).ExpiresAt);
    }

    [Fact]
    public void Activation_code_file_decodes_to_the_same_license()
    {
        using var o = new Owner();
        var key = o.CreateKey();

        var outcome = o.Issue.Issue(o.Input(key, code: true));

        Assert.NotNull(outcome.ActivationCodePath);
        Assert.True(ActivationCode.TryDecode(File.ReadAllBytes(outcome.ActivationCodePath!), out var decoded));
        Assert.Equal(File.ReadAllBytes(outcome.LicensePath), decoded);
    }

    [Fact]
    public void Existing_license_file_is_never_overwritten()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var target = o.Out("existing.lic");
        File.WriteAllText(target, "keep me");

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, output: target)));

        Assert.Equal("keep me", File.ReadAllText(target));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Fact]
    public void Existing_activation_code_file_is_never_overwritten()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var target = o.Out("x.lic");
        File.WriteAllText(target + ".code.txt", "keep me too");

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, output: target, code: true)));

        Assert.Equal("keep me too", File.ReadAllText(target + ".code.txt"));
        Assert.False(File.Exists(target));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Fact]
    public void Duplicate_license_id_is_refused_and_not_recorded_twice()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-DUP", output: o.Out("a.lic")));

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, "FC-DUP", output: o.Out("b.lic"))));

        Assert.False(File.Exists(o.Out("b.lic")));
        Assert.Single(LedgerReader.Read(o.Paths.LedgerPath).Entries);
    }

    [Fact]
    public void Wrong_passphrase_issues_nothing()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var input = o.Input(key) with { Passphrase = "a completely different passphrase" };

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(input));

        Assert.False(File.Exists(input.OutputPath));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Fact]
    public void Missing_signing_key_issues_nothing()
    {
        using var o = new Owner();
        var input = o.Input("no-such-key");

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(input));

        Assert.False(File.Exists(input.OutputPath));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(366, 1)]
    [InlineData(0, 0)]
    [InlineData(0, 100001)]
    public void Invalid_limits_are_rejected_and_nothing_is_written(int grace, int installs)
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var input = o.Input(key, grace: grace, installs: installs);

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(input));

        Assert.False(File.Exists(input.OutputPath));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Fact]
    public void Expiry_that_is_not_after_the_start_is_rejected()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var input = o.Input(key, expires: LicenseIssuer.Truncate(Owner.Now));

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(input));
        Assert.False(File.Exists(input.OutputPath));
    }

    [Theory]
    [InlineData("", "Customer")]
    [InlineData("FC-1", "")]
    [InlineData("FC-1", "Line\nbreak")]
    public void Invalid_text_fields_are_rejected(string licenseId, string licensee)
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var input = o.Input(key, licenseId: "FC-OUT", licensee: licensee) with { LicenseId = licenseId };

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(input));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Fact]
    public void Invalid_installation_id_is_rejected()
    {
        using var o = new Owner();
        var key = o.CreateKey();

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, installationId: "not-a-valid-id")));
    }

    [Fact]
    public void Relative_or_missing_output_folder_is_rejected()
    {
        using var o = new Owner();
        var key = o.CreateKey();

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, output: "relative.lic")));
        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, output: Path.Combine(o.Root, "no-folder", "x.lic"))));
        Assert.False(File.Exists(o.Paths.LedgerPath));
    }

    [Fact]
    public void A_ledger_failure_removes_the_license_file_and_reports_failure()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        Directory.CreateDirectory(o.Paths.LedgerPath);   // a folder where the ledger file should be: appending must fail
        var input = o.Input(key, code: true);

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(input));

        Assert.False(File.Exists(input.OutputPath));
        Assert.False(File.Exists(input.OutputPath + ".code.txt"));
    }

    [Fact]
    public void Customer_artifacts_and_ledger_contain_no_private_key_material_or_passphrase()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var outcome = o.Issue.Issue(o.Input(key, code: true));

        var texts = new[]
        {
            File.ReadAllText(outcome.LicensePath),
            File.ReadAllText(outcome.ActivationCodePath!),
            File.ReadAllText(Path.Combine(o.Paths.KeysDirectory, key + ".public.json")),
            File.ReadAllText(o.Paths.LedgerPath),
        };
        foreach (var text in texts)
        {
            Assert.DoesNotContain("PRIVATE KEY", text, StringComparison.Ordinal);
            Assert.DoesNotContain(Owner.Pass, text, StringComparison.Ordinal);
        }
        Assert.StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----",
            File.ReadAllText(Path.Combine(o.Paths.KeysDirectory, key + ".p8")), StringComparison.Ordinal);
    }

    [Fact]
    public void Tampered_signature_and_unknown_key_id_are_rejected_by_the_verifier()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var outcome = o.Issue.Issue(o.Input(key));
        var bytes = File.ReadAllBytes(outcome.LicensePath);
        var trusted = PublicKeyExport.Read(Path.Combine(o.Paths.KeysDirectory, key + ".public.json"));

        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        var signature = Base64Url.DecodeFromChars(root.GetProperty("signature").GetString()!);
        signature[10] ^= 0xFF;
        var tampered = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"keyId\":\"" + key + "\",\"payload\":\"" + root.GetProperty("payload").GetString()
            + "\",\"signature\":\"" + Base64Url.EncodeToString(signature) + "\"}");

        var verifier = new LicenseVerifier(trusted, new PinnedTimeProvider(Owner.Now));
        Assert.Equal(LicenseStatus.InvalidSignature, verifier.Verify(tampered).Status);

        var otherId = new LicenseVerifier(new[] { new TrustedKey("other-key", trusted[0].SubjectPublicKeyInfo) },
            new PinnedTimeProvider(Owner.Now));
        Assert.Equal(LicenseStatus.UnknownKeyId, otherId.Verify(bytes).Status);
    }
}

public sealed class KeyServiceTests
{
    [Fact]
    public void Created_key_is_listed_with_a_fingerprint_and_no_problem()
    {
        using var o = new Owner();
        var created = o.Keys.Create("test-key-1", Owner.Pass, Owner.Pass);

        var keys = o.Keys.List(out var problem);

        Assert.Null(problem);
        var key = Assert.Single(keys);
        Assert.Equal("test-key-1", key.KeyId);
        Assert.Null(key.Problem);
        Assert.Equal(created.Fingerprint, key.Fingerprint);
    }

    [Fact]
    public void Listing_a_missing_key_folder_is_empty_and_not_an_error()
    {
        using var o = new Owner();
        Assert.Empty(o.Keys.List(out var problem));
        Assert.Null(problem);
    }

    [Fact]
    public void Weak_passphrase_mismatch_reserved_id_and_duplicate_are_refused()
    {
        using var o = new Owner();

        Assert.Throws<LicenseToolException>(() => o.Keys.Create("k-weak", "short", "short"));
        Assert.Throws<LicenseToolException>(() => o.Keys.Create("k-mismatch", Owner.Pass, Owner.Pass + "x"));
        Assert.Throws<LicenseToolException>(() => o.Keys.Create("interop-test-9", Owner.Pass, Owner.Pass));
        Assert.Throws<LicenseToolException>(() => o.Keys.Create("bad id", Owner.Pass, Owner.Pass));
        Assert.Empty(o.Keys.List(out _));

        o.CreateKey();
        Assert.Throws<LicenseToolException>(() => o.Keys.Create("test-key-1", Owner.Pass, Owner.Pass));
    }

    [Fact]
    public void Public_key_export_holds_only_public_material_and_never_overwrites()
    {
        using var o = new Owner();
        var created = o.Keys.Create("test-key-1", Owner.Pass, Owner.Pass);
        var target = o.Out("keys.json");

        var exported = o.Keys.ExportKeySet(new[] { "test-key-1" }, target);

        Assert.Equal(("test-key-1", created.Fingerprint), Assert.Single(exported));
        var text = File.ReadAllText(target);
        Assert.DoesNotContain("PRIVATE", text, StringComparison.Ordinal);
        var parsed = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(target));
        Assert.Equal("test-key-1", Assert.Single(parsed).KeyId);

        Assert.Throws<LicenseToolException>(() => o.Keys.ExportKeySet(new[] { "test-key-1" }, target));
        Assert.Equal(text, File.ReadAllText(target));
    }

    [Fact]
    public void Export_refuses_test_vector_ids_and_empty_lists()
    {
        using var o = new Owner();
        o.CreateKey();

        Assert.Throws<LicenseToolException>(() => o.Keys.ExportKeySet(new[] { "interop-test-1" }, o.Out("a.json")));
        Assert.Throws<LicenseToolException>(() => o.Keys.ExportKeySet(Array.Empty<string>(), o.Out("b.json")));
        Assert.False(File.Exists(o.Out("a.json")));
        Assert.False(File.Exists(o.Out("b.json")));
    }
}

public sealed class RequestServiceTests
{
    private static string Id() => InstallationIds.FromMachineSource("unit-test-machine");
    private static string Code() => InstallationIds.ToRequestCode(Id());

    [Fact]
    public void Valid_request_code_is_parsed()
    {
        var parsed = RequestService.Parse(Code());

        Assert.Equal(Id(), parsed.InstallationId);
        Assert.Equal(Code(), parsed.RequestCode);
        Assert.Equal(Id().Substring(0, 12), parsed.ShortId);
    }

    [Fact]
    public void Request_code_is_found_inside_surrounding_text()
    {
        var parsed = RequestService.Parse("Hello,\r\nmy request code is " + Code() + "\r\nThanks");
        Assert.Equal(Id(), parsed.InstallationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("FCR1-ZZZZ-ZZZZ")]
    public void Empty_or_malformed_input_is_rejected(string? text)
    {
        Assert.Throws<LicenseToolException>(() => RequestService.Parse(text));
    }

    [Fact]
    public void Altered_code_and_oversized_input_are_rejected()
    {
        var code = Code();
        var altered = code[..^1] + (code[^1] == '0' ? '1' : '0');

        Assert.Throws<LicenseToolException>(() => RequestService.Parse(altered));
        Assert.Throws<LicenseToolException>(() => RequestService.Parse(new string('A', 4001)));
    }

    [Fact]
    public void Request_file_is_read_but_missing_or_oversized_files_are_rejected()
    {
        using var dir = new TempDir();
        var good = dir.Combine("good.txt");
        File.WriteAllText(good, Code());
        var big = dir.Combine("big.txt");
        File.WriteAllText(big, new string('A', 5000));

        Assert.Equal(Id(), RequestService.ParseFile(good).InstallationId);
        Assert.Throws<LicenseToolException>(() => RequestService.ParseFile(big));
        Assert.Throws<LicenseToolException>(() => RequestService.ParseFile(dir.Combine("missing.txt")));
    }
}

public sealed class SafeFileTests
{
    [Fact]
    public void WriteNew_creates_a_file_and_refuses_to_replace_it()
    {
        using var dir = new TempDir();
        var path = dir.Combine("a.bin");

        SafeFile.WriteNew(path, new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));

        Assert.Throws<LicenseToolException>(() => SafeFile.WriteNew(path, new byte[] { 9 }));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void WriteNew_rejects_relative_paths_and_missing_folders()
    {
        using var dir = new TempDir();

        Assert.Throws<LicenseToolException>(() => SafeFile.WriteNew("relative.bin", new byte[] { 1 }));
        Assert.Throws<LicenseToolException>(() => SafeFile.WriteNew(dir.Combine("nofolder\\x.bin"), new byte[] { 1 }));
    }
}

public sealed class LedgerReaderTests
{
    private const string Line =
        "{\"recordedAt\":\"2026-01-01T00:00:00Z\",\"licenseId\":\"A\",\"licensee\":\"X\",\"keyId\":\"k\","
        + "\"notBefore\":\"2026-01-01T00:00:00Z\",\"expiresAt\":\"2026-02-01T00:00:00Z\",\"graceDays\":5,"
        + "\"maxInstallations\":1,\"fileSha256\":\"ab\"}";

    [Fact]
    public void Missing_ledger_is_empty_and_not_a_problem()
    {
        using var dir = new TempDir();
        var snapshot = LedgerReader.Read(dir.Combine("none.jsonl"));
        Assert.Empty(snapshot.Entries);
        Assert.Null(snapshot.Problem);
        Assert.Equal(0, snapshot.UnreadableLines);
    }

    [Fact]
    public void Unreadable_lines_are_counted_and_good_lines_are_kept()
    {
        using var dir = new TempDir();
        var path = dir.Combine("l.jsonl");
        File.WriteAllLines(path, new[] { Line, "not json", "", "{\"licenseId\":\"only\"}" });

        var snapshot = LedgerReader.Read(path);

        Assert.Single(snapshot.Entries);
        Assert.Equal(2, snapshot.UnreadableLines);
    }

    [Fact]
    public void Status_is_computed_from_the_recorded_dates_only()
    {
        using var dir = new TempDir();
        var path = dir.Combine("l.jsonl");
        File.WriteAllLines(path, new[] { Line });
        var entry = Assert.Single(LedgerReader.Read(path).Entries);

        Assert.Equal(TermStatus.NotYetValid, entry.StatusAt(new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TermStatus.Active, entry.StatusAt(new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TermStatus.ActiveInGrace, entry.StatusAt(new DateTimeOffset(2026, 2, 3, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(TermStatus.Expired, entry.StatusAt(new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)));
    }
}
