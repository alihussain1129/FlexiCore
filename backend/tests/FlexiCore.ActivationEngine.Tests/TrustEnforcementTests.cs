using System.Security.Cryptography;
using System.Text;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

/// <summary>
/// Regression tests for signing-key trust. Temporary folders and throwaway keys only.
/// No real production private key is used or needed.
/// </summary>
public sealed class TrustEnforcementTests : IDisposable
{
    private const string Pass = "Tr0ub4dor&3-horse-staple-battery-9";
    private const string ProdId = "fc-2026-a";
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fc-trust-" + Guid.NewGuid().ToString("N"));
    private readonly string _outDir;
    private readonly OwnerPaths _paths;
    private readonly KeyService _keys;
    private readonly PinnedTimeProvider _time = new PinnedTimeProvider(Now);

    public TrustEnforcementTests()
    {
        Directory.CreateDirectory(_root);
        _outDir = Path.Combine(_root, "out");
        Directory.CreateDirectory(_outDir);
        _paths = new OwnerPaths(Path.Combine(_root, "owner"));
        _keys = new KeyService(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private KeyManagementService Manager(string? erpFile) => new KeyManagementService(_paths, _keys, _time, () => erpFile);
    private IssueService Issuer(KeyManagementService m) => new IssueService(_paths, _time, _keys, m);

    private TrustedKey LocalPublic(string id) =>
        PublicKeyExport.Read(Path.Combine(_paths.KeysDirectory, id + ".public.json")).Single(k => k.KeyId == id);

    private static TrustedKey Stranger(string id)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new TrustedKey(id, ec.ExportSubjectPublicKeyInfo());
    }

    private string WriteErp(params TrustedKey[] keys)
    {
        var path = Path.Combine(_root, "erp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllBytes(path, PublicKeyExport.Build(keys));
        return path;
    }

    private string WriteRaw(string text)
    {
        var path = Path.Combine(_root, "erp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return path;
    }

    private string LicensePath(string licenseId = "FC-TRUST-0001") => Path.Combine(_outDir, licenseId + ".lic");

    private IssueInput Input(string keyId, string licenseId = "FC-TRUST-0001")
    {
        var start = LicenseIssuer.Truncate(Now);
        return new IssueInput(keyId, Pass, licenseId, "Test Customer", start, start.AddYears(1), 0, 1, null,
            LicensePath(licenseId), false);
    }

    private static string Short(byte[] spki) =>
        Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant()[..16];

    private void AssertBlocked(string? erpFile)
    {
        _keys.Create(ProdId, Pass, Pass);
        var manager = Manager(erpFile);
        Assert.False(manager.Load().IsSelectable(ProdId));
        Assert.Throws<LicenseToolException>(() => Issuer(manager).Issue(Input(ProdId)));
        Assert.False(File.Exists(LicensePath()));
        Assert.False(File.Exists(_paths.LedgerPath));
    }

    [Fact]
    public void Embedded_production_trust_keeps_the_three_known_public_keys()
    {
        var embedded = ProductionKeyLoader.Load().ToDictionary(k => k.KeyId, k => Short(k.SubjectPublicKeyInfo));
        Assert.True(embedded.Count >= 3);
        Assert.Equal("53844eef91a95d10", embedded["fc-2026-a"]);
        Assert.Equal("6df7f3fa54f9cde0", embedded["fc-2026-b"]);
        Assert.Equal("36d72e03d36f4516", embedded["flexicore-owner-2026-01"]);
    }

    [Fact]
    public void Local_key_with_a_production_id_but_another_fingerprint_cannot_issue()
    {
        _keys.Create(ProdId, Pass, Pass);
        var manager = Manager(WriteErp(Stranger(ProdId)));

        var row = manager.Load().Keys.Single(k => k.KeyId == ProdId);
        Assert.False(row.InErpFile == true);
        Assert.True(row.ErpIdPresent);
        Assert.Equal("Fingerprint mismatch", row.State);
        Assert.False(manager.Load().IsSelectable(ProdId));

        var ex = Assert.Throws<LicenseToolException>(() => Issuer(manager).Issue(Input(ProdId)));
        Assert.Contains("not eligible", ex.Message);
        Assert.False(File.Exists(LicensePath()));
        Assert.False(File.Exists(_paths.LedgerPath));
    }

    [Fact]
    public void Key_that_matches_the_trusted_entry_issues_and_verifies_and_tampering_is_rejected()
    {
        _keys.Create(ProdId, Pass, Pass);
        var erp = WriteErp(LocalPublic(ProdId));
        var manager = Manager(erp);
        Assert.True(manager.Load().IsSelectable(ProdId));

        var outcome = Issuer(manager).Issue(Input(ProdId));
        var bytes = File.ReadAllBytes(outcome.LicensePath);

        var trusted = PublicKeyExport.Read(erp);
        Assert.Equal(LicenseStatus.Valid, new LicenseVerifier(trusted, _time).Verify(bytes).Status);

        var tampered = (byte[])bytes.Clone();
        tampered[tampered.Length / 2] ^= 0x01;
        Assert.NotEqual(LicenseStatus.Valid, new LicenseVerifier(trusted, _time).Verify(tampered).Status);

        var otherTrust = new[] { Stranger(ProdId) };
        Assert.NotEqual(LicenseStatus.Valid, new LicenseVerifier(otherTrust, _time).Verify(bytes).Status);
    }

    [Fact]
    public void Ledger_records_one_entry_and_refuses_a_repeated_license_id()
    {
        _keys.Create(ProdId, Pass, Pass);
        var manager = Manager(WriteErp(LocalPublic(ProdId)));
        Issuer(manager).Issue(Input(ProdId));

        var snapshot = LedgerReader.Read(_paths.LedgerPath);
        Assert.Null(snapshot.Problem);
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal("FC-TRUST-0001", entry.LicenseId);
        Assert.Equal(ProdId, entry.KeyId);

        Assert.Throws<LicenseToolException>(() => Issuer(manager).Issue(Input(ProdId)));
        Assert.Single(LedgerReader.Read(_paths.LedgerPath).Entries);
    }

    [Fact]
    public void Retired_key_cannot_issue_even_when_trusted()
    {
        _keys.Create(ProdId, Pass, Pass);
        var manager = Manager(WriteErp(LocalPublic(ProdId)));
        manager.Retire(ProdId);

        Assert.False(manager.Load().IsSelectable(ProdId));
        Assert.Throws<LicenseToolException>(() => Issuer(manager).Issue(Input(ProdId)));
        Assert.False(File.Exists(LicensePath()));
    }

    [Fact]
    public void Missing_trust_file_blocks_issuance() => AssertBlocked(Path.Combine(_root, "does-not-exist.json"));

    [Fact]
    public void Malformed_trust_file_blocks_issuance() => AssertBlocked(WriteRaw("{ this is not json"));

    [Fact]
    public void Unknown_trust_file_blocks_issuance() => AssertBlocked(null);

    [Fact]
    public void Trust_file_without_the_key_id_blocks_issuance() => AssertBlocked(WriteErp(Stranger("fc-2026-b")));

    [Fact]
    public void Duplicate_trust_ids_are_rejected_by_the_loader_and_block_issuance()
    {
        var one = Convert.ToBase64String(Stranger(ProdId).SubjectPublicKeyInfo);
        var two = Convert.ToBase64String(Stranger(ProdId).SubjectPublicKeyInfo);
        var json = "{\"keys\":[{\"keyId\":\"" + ProdId + "\",\"publicKey\":\"" + one + "\"},"
                 + "{\"keyId\":\"" + ProdId + "\",\"publicKey\":\"" + two + "\"}]}";

        Assert.Empty(ProductionKeyLoader.ParseKeySet(Encoding.UTF8.GetBytes(json)));
        AssertBlocked(WriteRaw(json));
    }

    [Fact]
    public void Key_ids_already_in_the_embedded_trust_list_are_never_offered_again()
    {
        // No ERP file can be found here, which is how the old fc-2026-a collision happened.
        var manager = Manager(null);
        Assert.True(manager.IsKeyIdTaken("fc-2026-a"));
        Assert.True(manager.IsKeyIdTaken("fc-2026-b"));
        Assert.Throws<LicenseToolException>(() => manager.EnsureCanCreate("fc-2026-a"));

        var next = manager.NextKeyId();
        Assert.DoesNotContain(next, ProductionKeyLoader.Load().Select(k => k.KeyId));
    }

    [Fact]
    public void Marking_a_key_as_built_into_the_erp_is_refused_unless_the_public_keys_match()
    {
        _keys.Create(ProdId, Pass, Pass);
        var mismatched = Manager(WriteErp(Stranger(ProdId)));
        mismatched.RecordManualKey(ProdId, false);

        Assert.Throws<LicenseToolException>(() => mismatched.MarkBuiltIntoErp(ProdId));
        Assert.True(mismatched.Load().Keys.Single(k => k.KeyId == ProdId).PendingErp);

        var matching = Manager(WriteErp(LocalPublic(ProdId)));
        matching.MarkBuiltIntoErp(ProdId);
        Assert.False(matching.Load().Keys.Single(k => k.KeyId == ProdId).PendingErp);
    }

    [Fact]
    public void Encrypted_backup_restores_the_same_public_key_and_never_overwrites()
    {
        _keys.Create(ProdId, Pass, Pass);
        var backupDir = Path.Combine(_root, "backup");
        Directory.CreateDirectory(backupDir);
        var backup = Path.Combine(backupDir, ProdId + ".p8");

        _keys.ExportEncryptedPrivateKey(ProdId, backup);
        Assert.Throws<LicenseToolException>(() => _keys.ExportEncryptedPrivateKey(ProdId, backup));
        Assert.Throws<LicenseToolException>(() => _keys.ImportEncryptedKey(backup, Pass));

        var paths2 = new OwnerPaths(Path.Combine(_root, "owner2"));
        var keys2 = new KeyService(paths2);
        keys2.ImportEncryptedKey(backup, Pass);

        var restored = PublicKeyExport.Read(Path.Combine(paths2.KeysDirectory, ProdId + ".public.json")).Single();
        Assert.True(restored.SubjectPublicKeyInfo.AsSpan().SequenceEqual(LocalPublic(ProdId).SubjectPublicKeyInfo));
    }

    [Fact]
    public void Retiring_a_key_keeps_its_licenses_valid_and_the_standby_key_keeps_issuing()
    {
        const string oldId = "rot-old-1";
        const string newId = "rot-new-1";
        _keys.Create(oldId, Pass, Pass);
        _keys.Create(newId, Pass, Pass);
        var erp = WriteErp(LocalPublic(oldId), LocalPublic(newId));
        var manager = Manager(erp);
        var issuer = Issuer(manager);

        var first = issuer.Issue(Input(oldId, "FC-ROT-0001"));
        Assert.Equal(oldId, first.KeyId);
        var firstBytes = File.ReadAllBytes(first.LicensePath);

        manager.Retire(oldId);

        // The retired key can no longer sign new licenses.
        Assert.Throws<LicenseToolException>(() => issuer.Issue(Input(oldId, "FC-ROT-0002")));

        // The standby key keeps issuing.
        var second = issuer.Issue(Input(newId, "FC-ROT-0003"));
        Assert.Equal(newId, second.KeyId);
        var secondBytes = File.ReadAllBytes(second.LicensePath);

        // The ERP trust file is unchanged, so both licenses still verify.
        var verifier = new LicenseVerifier(ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(erp)), _time);
        Assert.Equal(LicenseStatus.Valid, verifier.Verify(firstBytes).Status);
        Assert.Equal(LicenseStatus.Valid, verifier.Verify(secondBytes).Status);
    }

    [Fact]
    public void Embedded_production_trust_has_all_four_keys_unchanged_and_rejects_unknown_ids()
    {
        var embedded = ProductionKeyLoader.Load().ToDictionary(k => k.KeyId, k => Short(k.SubjectPublicKeyInfo));
        Assert.Equal("53844eef91a95d10", embedded["fc-2026-a"]);
        Assert.Equal("6df7f3fa54f9cde0", embedded["fc-2026-b"]);
        Assert.Equal("36d72e03d36f4516", embedded["flexicore-owner-2026-01"]);
        Assert.Equal("f1f052bc005fc011", embedded["fc-2026-c"]);
        Assert.False(embedded.ContainsKey("rogue-key-1"));
    }
}
