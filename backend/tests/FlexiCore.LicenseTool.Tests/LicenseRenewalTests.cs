using System.Text.Json;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.LicenseTool.Tests;

public sealed class LicenseRenewalTests
{
    private const string Passphrase = "correct horse battery staple";
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Scenario : IDisposable
    {
        public Scenario()
        {
            Root = new TempDir();
            Source = Path.Combine(Root.Path, "keys");
            KeyStore.CreateForTesting("fc-renewal-test", Source, Passphrase, false, _ => true);
            KeyPath = Path.Combine(Source, "fc-renewal-test.p8");
            Keys = PublicKeyExport.Read(Path.Combine(Source, "fc-renewal-test.public.json"));
            LedgerPath = Path.Combine(Root.Path, "issued.jsonl");
            OldPath = Path.Combine(Root.Path, "old.lic");
            RevocationsPath = Path.Combine(Root.Path, "revocations.signed.json");
            Installation = new string('a', 64);
            OldRequest = new LicenseRequest("LIC-OLD-1", "Renewal Customer",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), 5, 3,
                InstallationId: Installation);
            using var signer = KeyStore.Load(KeyPath, Passphrase);
            var old = LicenseIssuer.Issue(OldRequest, signer);
            File.WriteAllBytes(OldPath, old);
            using (var lockHandle = LedgerFileLock.Acquire(LedgerPath))
                IssueLedger.Append(LedgerPath, OldRequest, signer.KeyId, old, Now.AddDays(-31), lockHandle,
                    Guid.NewGuid(), CustomerId, "owner-reviewer");
            var snapshot = RevocationSnapshotIssuer.Issue(Keys, signer, Now);
            File.WriteAllBytes(RevocationsPath, snapshot);
        }

        public TempDir Root { get; }
        public string Source { get; }
        public string KeyPath { get; }
        public IReadOnlyCollection<TrustedKey> Keys { get; }
        public string LedgerPath { get; }
        public string OldPath { get; }
        public string RevocationsPath { get; }
        public string Installation { get; }
        public LicenseRequest OldRequest { get; }
        public Guid CustomerId { get; } = Guid.NewGuid();

        public void Dispose() => Root.Dispose();
    }

    [Fact]
    public void Expired_license_renews_with_same_customer_installation_and_audited_predecessor()
    {
        using var s = new Scenario();
        var output = Path.Combine(s.Root.Path, "renewed.lic");
        var result = LicenseToolApp.Run(
            ["renew", "--key", s.KeyPath, "--public-keys", Path.Combine(s.Source, "fc-renewal-test.public.json"),
             "--license", s.OldPath, "--revocations", s.RevocationsPath, "--ledger", s.LedgerPath,
             "--license-id", "LIC-NEW-1", "--expires", "2027-01-01", "--out", output],
            new FakeConsole(Passphrase), new TestClock(Now));

        Assert.Equal(0, result);
        var verifier = new LicenseVerifier(s.Keys, new TestClock(Now));
        var renewed = verifier.Verify(File.ReadAllBytes(output));
        Assert.Equal(LicenseStatus.Valid, renewed.Status);
        Assert.Equal("Renewal Customer", renewed.Payload!.Licensee);
        Assert.Equal(s.Installation, renewed.Payload.InstallationId);
        Assert.Equal(3, renewed.Payload.MaxInstallations);
        Assert.Equal(5, renewed.Payload.GraceDays);

        var last = File.ReadLines(s.LedgerPath).Last();
        using var row = JsonDocument.Parse(last);
        Assert.Equal("LIC-OLD-1", row.RootElement.GetProperty("renewedFromLicenseId").GetString());
        Assert.Equal(s.CustomerId, row.RootElement.GetProperty("customerId").GetGuid());
        Assert.Equal(Environment.UserName, row.RootElement.GetProperty("issuedBy").GetString());
    }

    [Fact]
    public void Revoked_license_cannot_renew_and_output_is_not_created()
    {
        using var s = new Scenario();
        using var signer = KeyStore.Load(s.KeyPath, Passphrase);
        var now = Now.AddSeconds(1);
        File.WriteAllBytes(s.RevocationsPath, RevocationSnapshotIssuer.Issue(s.Keys, signer, now,
            previousSnapshot: File.ReadAllBytes(s.RevocationsPath), revokeLicenseId: "LIC-OLD-1", reason: "fraud"));
        var output = Path.Combine(s.Root.Path, "must-not-exist.lic");

        var result = LicenseToolApp.Run(
            ["renew", "--key", s.KeyPath, "--public-keys", Path.Combine(s.Source, "fc-renewal-test.public.json"),
             "--license", s.OldPath, "--revocations", s.RevocationsPath, "--ledger", s.LedgerPath,
             "--license-id", "LIC-NEW-2", "--expires", "2027-01-01", "--out", output],
            new FakeConsole(Passphrase), new TestClock(now));

        Assert.Equal(2, result);
        Assert.False(File.Exists(output));
        Assert.Single(File.ReadLines(s.LedgerPath));
    }

    [Fact]
    public void A_predecessor_cannot_have_two_renewal_successors()
    {
        using var s = new Scenario();
        var args = new[]
        {
            "renew", "--key", s.KeyPath, "--public-keys", Path.Combine(s.Source, "fc-renewal-test.public.json"),
            "--license", s.OldPath, "--revocations", s.RevocationsPath, "--ledger", s.LedgerPath,
            "--license-id", "LIC-NEW-3", "--expires", "2027-01-01", "--out", Path.Combine(s.Root.Path, "first.lic")
        };
        Assert.Equal(0, LicenseToolApp.Run(args, new FakeConsole(Passphrase), new TestClock(Now)));
        var secondArgs = (string[])args.Clone();
        secondArgs[Array.IndexOf(secondArgs, "LIC-NEW-3")] = "LIC-NEW-4";
        secondArgs[Array.IndexOf(secondArgs, args[^1])] = Path.Combine(s.Root.Path, "second.lic");

        Assert.Equal(1, LicenseToolApp.Run(secondArgs, new FakeConsole(Passphrase), new TestClock(Now)));
        Assert.False(File.Exists(Path.Combine(s.Root.Path, "second.lic")));
        Assert.Equal(2, File.ReadLines(s.LedgerPath).Count());
    }
}
