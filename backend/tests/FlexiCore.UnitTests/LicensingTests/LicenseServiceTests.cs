using System.Text;
using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.UnitTests.LicensingTests;

internal sealed class InMemoryLicenseStore : ILicenseStore
{
    public byte[]? Content { get; set; }
    public bool FailReads { get; set; }
    public bool FailWrites { get; set; }
    public int Writes { get; private set; }

    public Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        if (FailReads) throw new LicenseStorageException("read failed");
        return Task.FromResult(Content is null ? null : (byte[]?)Content.Clone());
    }

    public Task WriteAsync(byte[] content, CancellationToken cancellationToken)
    {
        if (FailWrites) throw new LicenseStorageException("write failed");
        Content = (byte[])content.Clone();
        Writes++;
        return Task.CompletedTask;
    }
}

public sealed class LicenseServiceTests : IDisposable
{
    private readonly TestSigner _signer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flexicore-license-svc-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _signer.Dispose();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private LicenseService Service(ILicenseStore store, bool withKey = true)
    {
        var keys = withKey ? new[] { _signer.Trusted() } : Array.Empty<TrustedKey>();
        return new LicenseService(store, new LicenseVerifier(keys, _time), _time);
    }

    private static string P(
        string issuedAt = "2026-01-01T00:00:00Z",
        string notBefore = "2026-01-01T00:00:00Z",
        string? expires = "2027-01-01T00:00:00Z",
        int grace = 0)
    {
        var expiresJson = expires is null ? "null" : "'" + expires + "'";
        return Q.Fix("{'schemaVersion':1,'licenseId':'LIC-0001','product':'FlexiCore','licensee':'Test Customer',"
                     + "'issuedAt':'" + issuedAt + "','notBefore':'" + notBefore + "','expiresAt':" + expiresJson
                     + ",'graceDays':" + grace + ",'maxInstallations':1}");
    }

    private byte[] Lic(string payload) => _signer.Build(payload);

    private byte[] Tampered()
    {
        var original = Encoding.UTF8.GetBytes(P());
        var altered = Encoding.UTF8.GetBytes(P(grace: 300));
        return TestSigner.Envelope(altered, _signer.Sign(original));
    }

    // ---- status ----

    [Fact]
    public async Task Status_WithNoTrustedKeys_IsUnavailable_EvenWhenAFileIsStored()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P()) };
        var report = await Service(store, withKey: false).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Unavailable, report.State);
        Assert.Equal("no_trusted_keys", report.Reason);
        Assert.Null(report.License);
    }

    [Fact]
    public async Task Status_EmptyStore_IsMissing()
    {
        var report = await Service(new InMemoryLicenseStore()).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Missing, report.State);
        Assert.Null(report.Reason);
    }

    [Fact]
    public async Task Status_ValidStored_ReportsDetails()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P()) };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Valid, report.State);
        Assert.Equal("LIC-0001", report.License!.LicenseId);
        Assert.Null(report.GraceEndsAt);
        Assert.Equal(_time.Now, report.CheckedAt);
    }

    [Fact]
    public async Task Status_ValidWithGraceConfigured_ReportsGraceEnd()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P(expires: "2027-01-01T00:00:00Z", grace: 3)) };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Valid, report.State);
        Assert.Equal(new DateTimeOffset(2027, 1, 4, 0, 0, 0, TimeSpan.Zero), report.GraceEndsAt);
    }

    [Fact]
    public async Task Status_PerpetualLicense_IsValidWithNoGraceEnd()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P(expires: null)) };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Valid, report.State);
        Assert.Null(report.License!.ExpiresAt);
        Assert.Null(report.GraceEndsAt);
    }

    [Fact]
    public async Task Status_Expired_KeepsDetails()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P(expires: "2026-05-01T00:00:00Z")) };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Expired, report.State);
        Assert.Equal("expired", report.Reason);
        Assert.NotNull(report.License);
    }

    [Fact]
    public async Task Status_InGrace_IsValidInGrace()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P(expires: "2026-05-30T00:00:00Z", grace: 5)) };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.ValidInGrace, report.State);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 0, 0, 0, TimeSpan.Zero), report.GraceEndsAt);
    }

    [Fact]
    public async Task Status_NotYetValid_KeepsDetails()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P(notBefore: "2026-07-01T00:00:00Z")) };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.NotYetValid, report.State);
        Assert.NotNull(report.License);
    }

    [Fact]
    public async Task Status_TamperedFile_IsInvalid_WithNoDetails()
    {
        var store = new InMemoryLicenseStore { Content = Tampered() };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Invalid, report.State);
        Assert.Equal("invalid_signature", report.Reason);
        Assert.Null(report.License);
    }

    [Fact]
    public async Task Status_GarbageFile_IsInvalidMalformed()
    {
        var store = new InMemoryLicenseStore { Content = Encoding.UTF8.GetBytes("not json") };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Invalid, report.State);
        Assert.Equal("malformed", report.Reason);
    }

    [Fact]
    public async Task Status_StorageReadFailure_IsUnavailable_NotInvalid()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P()), FailReads = true };
        var report = await Service(store).GetStatusAsync(CancellationToken.None);
        Assert.Equal(LicenseState.Unavailable, report.State);
        Assert.Equal("storage_error", report.Reason);
    }

    // ---- import ----

    [Fact]
    public async Task Import_Valid_StoresTheExactBytes()
    {
        var store = new InMemoryLicenseStore();
        var file = Lic(P());
        var result = await Service(store).ImportAsync(file, CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Imported, result.Outcome);
        Assert.Equal(LicenseState.Valid, result.Report!.State);
        Assert.Equal(file, store.Content);
    }

    [Fact]
    public async Task Import_Tampered_IsRejected_AndPreviousFileIsUnchanged()
    {
        var original = Lic(P());
        var store = new InMemoryLicenseStore { Content = original };
        var result = await Service(store).ImportAsync(Tampered(), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Rejected, result.Outcome);
        Assert.Equal(LicenseStatus.InvalidSignature, result.Verification);
        Assert.Equal(original, store.Content);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task Import_Expired_IsRejected_AndNothingIsWritten()
    {
        var store = new InMemoryLicenseStore();
        var result = await Service(store).ImportAsync(Lic(P(expires: "2026-05-01T00:00:00Z")), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Rejected, result.Outcome);
        Assert.Equal(LicenseStatus.Expired, result.Verification);
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task Import_NotYetValid_IsRejected()
    {
        var store = new InMemoryLicenseStore();
        var result = await Service(store).ImportAsync(Lic(P(notBefore: "2026-07-01T00:00:00Z")), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Rejected, result.Outcome);
        Assert.Equal(LicenseStatus.NotYetValid, result.Verification);
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task Import_Garbage_IsRejectedMalformed()
    {
        var store = new InMemoryLicenseStore();
        var result = await Service(store).ImportAsync(Encoding.UTF8.GetBytes("{"), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Rejected, result.Outcome);
        Assert.Equal(LicenseStatus.Malformed, result.Verification);
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task Import_Oversized_IsRejectedFileTooLarge()
    {
        var store = new InMemoryLicenseStore();
        var result = await Service(store).ImportAsync(new byte[LicenseLimits.MaxFileBytes + 1], CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Rejected, result.Outcome);
        Assert.Equal(LicenseStatus.FileTooLarge, result.Verification);
    }

    [Fact]
    public async Task Import_WithNoTrustedKeys_IsVerificationUnavailable_NeverAccepted()
    {
        var store = new InMemoryLicenseStore();
        var result = await Service(store, withKey: false).ImportAsync(Lic(P()), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.VerificationUnavailable, result.Outcome);
        Assert.Null(store.Content);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task Import_OlderIssuedAtThanCurrent_IsRefused_AndCurrentIsUnchanged()
    {
        var current = Lic(P(issuedAt: "2026-03-01T00:00:00Z"));
        var store = new InMemoryLicenseStore { Content = current };
        var result = await Service(store).ImportAsync(Lic(P(issuedAt: "2026-02-01T00:00:00Z")), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.OlderThanCurrent, result.Outcome);
        Assert.Equal(current, store.Content);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task Import_NewerIssuedAt_ReplacesCurrent()
    {
        var store = new InMemoryLicenseStore { Content = Lic(P(issuedAt: "2026-02-01T00:00:00Z")) };
        var newer = Lic(P(issuedAt: "2026-03-01T00:00:00Z"));
        var result = await Service(store).ImportAsync(newer, CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Imported, result.Outcome);
        Assert.Equal(newer, store.Content);
    }

    [Fact]
    public async Task Import_IdenticalBytes_IsUnchanged_AndNothingIsWritten()
    {
        var file = Lic(P());
        var store = new InMemoryLicenseStore { Content = file };
        var result = await Service(store).ImportAsync(file, CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Unchanged, result.Outcome);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task Import_ReplacesAnInvalidCurrentFile()
    {
        var store = new InMemoryLicenseStore { Content = Encoding.UTF8.GetBytes("garbage") };
        var file = Lic(P());
        var result = await Service(store).ImportAsync(file, CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.Imported, result.Outcome);
        Assert.Equal(file, store.Content);
    }

    [Fact]
    public async Task Import_WriteFailure_KeepsPreviousBytes()
    {
        var current = Lic(P(issuedAt: "2026-02-01T00:00:00Z"));
        var store = new InMemoryLicenseStore { Content = current, FailWrites = true };
        var result = await Service(store).ImportAsync(Lic(P(issuedAt: "2026-03-01T00:00:00Z")), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.StorageFailed, result.Outcome);
        Assert.Equal(current, store.Content);
    }

    [Fact]
    public async Task Import_ReadFailure_IsStorageFailed_AndNothingIsWritten()
    {
        var store = new InMemoryLicenseStore { FailReads = true };
        var result = await Service(store).ImportAsync(Lic(P()), CancellationToken.None);
        Assert.Equal(LicenseImportOutcome.StorageFailed, result.Outcome);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task ConcurrentImports_EndWithTheNewestLicense()
    {
        var store = new FileLicenseStore(_dir);
        var service = Service(store);
        var older = Lic(P(issuedAt: "2026-02-01T00:00:00Z"));
        var newer = Lic(P(issuedAt: "2026-03-01T00:00:00Z"));

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => Task.Run(() => service.ImportAsync(i % 2 == 0 ? older : newer, CancellationToken.None))));

        Assert.All(results, r => Assert.True(
            r.Outcome is LicenseImportOutcome.Imported or LicenseImportOutcome.Unchanged or LicenseImportOutcome.OlderThanCurrent,
            $"Unexpected outcome {r.Outcome}"));
        Assert.Equal(newer, await store.ReadAsync(CancellationToken.None));
        Assert.Equal(FileLicenseStore.FileName, Assert.Single(Directory.GetFiles(_dir).Select(Path.GetFileName)));
    }
}