using System.Text;
using FlexiCore.Licensing;

namespace FlexiCore.UnitTests.LicensingTests;

public sealed class RevocationListTests : IDisposable
{
    private readonly TestSigner _signer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flexicore-revocations-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _signer.Dispose();
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); } catch (IOException) { }
    }

    private byte[] Snapshot(string issuedAt, string nextUpdate, params string[] revokedIds)
    {
        var entries = string.Join(',', revokedIds.Select(id =>
            "{\"licenseId\":\"" + id + "\",\"revokedAt\":\"" + issuedAt + "\",\"reason\":\"chargeback\"}"));
        var payload = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"listId\":\"" + Guid.NewGuid().ToString("N") + "\",\"issuedAt\":\"" + issuedAt
            + "\",\"nextUpdate\":\"" + nextUpdate + "\",\"revokedLicenses\":[" + entries + "]}");
        return TestSigner.Envelope(payload, _signer.Sign(payload));
    }

    private RevocationListService Service(IRevocationListStore store) =>
        new(store, new SignedRevocationListVerifier([_signer.Trusted()]), _time);

    [Fact]
    public void Verifier_AcceptsTrustedSnapshot_AndRejectsTamperedPayload()
    {
        var verifier = new SignedRevocationListVerifier([_signer.Trusted()]);
        var bytes = Snapshot("2026-05-31T00:00:00Z", "2026-06-08T00:00:00Z", "LIC-0001");
        Assert.True(verifier.Verify(bytes).IsValid);

        var text = Encoding.UTF8.GetString(bytes);
        var signatureStart = text.LastIndexOf("\"signature\":\"", StringComparison.Ordinal) + "\"signature\":\"".Length;
        var modified = text[..signatureStart] + (text[signatureStart] == 'A' ? 'B' : 'A') + text[(signatureStart + 1)..];
        Assert.Equal(RevocationListError.InvalidSignature, verifier.Verify(Encoding.UTF8.GetBytes(modified)).Error);
    }

    [Fact]
    public async Task ImportedRevocationBlocksCurrentStatusAndReinstallation()
    {
        var snapshotStore = new InMemoryRevocationStore();
        var revocations = Service(snapshotStore);
        Assert.Equal(RevocationImportOutcome.Imported,
            (await revocations.ImportAsync(Snapshot("2026-05-31T00:00:00Z", "2026-06-08T00:00:00Z", "LIC-0001"), default)).Outcome);

        var licenseStore = new InMemoryLicenseStore { Content = _signer.Build(TestSigner.ValidPayload) };
        var service = new LicenseService(licenseStore, new LicenseVerifier([_signer.Trusted()], _time), _time,
            revocations: revocations);
        var status = await service.GetStatusAsync(default);
        var reinstall = await service.ImportAsync(_signer.Build(TestSigner.ValidPayload), default);

        Assert.Equal(LicenseState.Revoked, status.State);
        Assert.Equal("license_revoked", status.Reason);
        Assert.Equal(LicenseImportOutcome.Revoked, reinstall.Outcome);
        Assert.Equal(LicenseStatus.Revoked, reinstall.Verification);
    }

    [Fact]
    public async Task LaterSnapshotCannotRemoveOrAlterExistingRevocations()
    {
        var store = new InMemoryRevocationStore();
        var service = Service(store);
        Assert.Equal(RevocationImportOutcome.Imported,
            (await service.ImportAsync(Snapshot("2026-05-31T00:00:00Z", "2026-06-08T00:00:00Z", "LIC-0001"), default)).Outcome);

        var rollback = await service.ImportAsync(Snapshot("2026-06-01T00:00:00Z", "2026-06-09T00:00:00Z"), default);
        var preserved = await service.CheckAsync("LIC-0001", default);

        Assert.Equal(RevocationImportOutcome.RollbackAttempt, rollback.Outcome);
        Assert.Equal(RevocationCheckState.Revoked, preserved.State);
    }

    [Fact]
    public async Task OldOrEqualTimestampSnapshotCannotReplaceCurrentSnapshot()
    {
        var store = new InMemoryRevocationStore();
        var service = Service(store);
        var first = Snapshot("2026-05-31T00:00:00Z", "2026-06-08T00:00:00Z", "LIC-0001");
        Assert.Equal(RevocationImportOutcome.Imported, (await service.ImportAsync(first, default)).Outcome);

        var replay = await service.ImportAsync(Snapshot("2026-05-31T00:00:00Z", "2026-06-09T00:00:00Z", "LIC-0001"), default);

        Assert.Equal(RevocationImportOutcome.OlderSnapshot, replay.Outcome);
        Assert.Equal(first, store.Content);
    }

    [Fact]
    public async Task ExpiredSnapshotFailsClosed()
    {
        var service = Service(new InMemoryRevocationStore
        {
            Content = Snapshot("2026-05-01T00:00:00Z", "2026-05-31T00:00:00Z")
        });

        var result = await service.CheckAsync("LIC-0001", default);

        Assert.Equal(RevocationCheckState.Unavailable, result.State);
        Assert.Equal("revocation_snapshot_stale", result.Reason);
    }

    [Fact]
    public async Task FileStoreUsesOwnerOnlyPermissionsOnUnix()
    {
        var store = new FileRevocationListStore(_directory);
        await store.WriteAsync(Snapshot("2026-05-31T00:00:00Z", "2026-06-08T00:00:00Z"), default);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(_directory, "revocations.signed.json")));
    }

    private sealed class InMemoryRevocationStore : IRevocationListStore
    {
        public byte[]? Content { get; set; }
        public Task<byte[]?> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Content is null ? null : (byte[])Content.Clone());
        public Task WriteAsync(byte[] bytes, CancellationToken cancellationToken)
        { Content = (byte[])bytes.Clone(); return Task.CompletedTask; }
    }
}
