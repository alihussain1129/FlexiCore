using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.LicenseTool.Tests;

public sealed class AuthorizedSignerProvisioningTests
{
    private const string Passphrase = "correct horse battery staple";
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void Provision_requires_exact_trust_match_and_refuses_overwrite()
    {
        using var root = new TempDir();
        var source = Path.Combine(root.Path, "incoming");
        var target = Path.Combine(root.Path, "owner-keys");
        var unauthorized = Path.Combine(root.Path, "unauthorized");
        KeyStore.CreateForTesting("fc-authorized-a", source, Passphrase, false, _ => true);
        KeyStore.CreateForTesting("fc-authorized-b", unauthorized, Passphrase, false, _ => true);
        var authorized = PublicKeyExport.Read(Path.Combine(source, "fc-authorized-a.public.json"));

        var provisioned = KeyStore.ProvisionAuthorized(
            Path.Combine(source, "fc-authorized-a.p8"), target, Passphrase, authorized);
        using var loaded = KeyStore.Load(provisioned.PrivateKeyPath, Passphrase);

        Assert.Equal("fc-authorized-a", provisioned.KeyId);
        Assert.Equal(authorized.Single().SubjectPublicKeyInfo, loaded.SubjectPublicKeyInfo);
        var audit = File.ReadAllLines(Path.Combine(target, "key-provisioning-audit.jsonl"));
        Assert.Equal("provision-authorized-key", System.Text.Json.JsonDocument.Parse(audit.Single()).RootElement.GetProperty("action").GetString());
        Assert.Throws<LicenseToolException>(() => KeyStore.ProvisionAuthorized(
            Path.Combine(source, "fc-authorized-a.p8"), target, Passphrase, authorized));
        Assert.Throws<LicenseToolException>(() => KeyStore.ProvisionAuthorized(
            Path.Combine(unauthorized, "fc-authorized-b.p8"), Path.Combine(root.Path, "rejected"), Passphrase, authorized));
        Assert.False(Directory.Exists(Path.Combine(root.Path, "rejected")));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(provisioned.PrivateKeyPath));
    }

    [Fact]
    public void Cli_provisions_authorized_key_then_creates_initial_and_cumulative_snapshots()
    {
        using var root = new TempDir();
        var source = Path.Combine(root.Path, "incoming");
        var owner = Path.Combine(root.Path, "owner");
        KeyStore.CreateForTesting("fc-snapshot-test", source, Passphrase, false, _ => true);
        var keyset = Path.Combine(root.Path, "authorized-keys.json");
        File.Copy(Path.Combine(source, "fc-snapshot-test.public.json"), keyset);
        var clock = new TestClock(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var provisionConsole = new FakeConsole(Passphrase);
        var provisionCode = LicenseToolApp.Run(
            ["provision-key", "--key", Path.Combine(source, "fc-snapshot-test.p8"), "--public-keys", keyset,
             "--out-dir", owner], provisionConsole, clock);

        Assert.Equal(0, provisionCode);
        Assert.Contains("Trust set unchanged", provisionConsole.AllOutput, StringComparison.Ordinal);

        var initialPath = Path.Combine(root.Path, "initial.signed.json");
        var initialConsole = new FakeConsole(Passphrase);
        var initialCode = LicenseToolApp.Run(
            ["revocation-snapshot", "--key", Path.Combine(owner, "fc-snapshot-test.p8"), "--public-keys", keyset,
             "--out", initialPath], initialConsole, clock);
        Assert.Equal(0, initialCode);
        Assert.Empty(new SignedRevocationListVerifier(PublicKeyExport.Read(keyset)).Verify(File.ReadAllBytes(initialPath)).List!.RevokedLicenses);

        var updatedPath = Path.Combine(root.Path, "updated.signed.json");
        clock.Now = clock.Now.AddSeconds(1);
        var updateCode = LicenseToolApp.Run(
            ["revocation-snapshot", "--key", Path.Combine(owner, "fc-snapshot-test.p8"), "--public-keys", keyset,
             "--out", updatedPath, "--previous", initialPath, "--revoke-id", "LIC-1001", "--reason", "approved request"],
            new FakeConsole(Passphrase), clock);

        Assert.Equal(0, updateCode);
        var updated = new SignedRevocationListVerifier(PublicKeyExport.Read(keyset)).Verify(File.ReadAllBytes(updatedPath));
        Assert.True(updated.IsValid);
        Assert.Equal("LIC-1001", Assert.Single(updated.List!.RevokedLicenses).LicenseId);
    }
}
