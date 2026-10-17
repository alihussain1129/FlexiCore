using System.Security.Cryptography;
using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.UnitTests.LicensingTests;

public class InstallationBindingTests : IDisposable
{
    private static readonly string IdA = new('a', 64);
    private static readonly string IdB = new('b', 64);

    private readonly TestSigner _signer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    public void Dispose() => _signer.Dispose();

    private sealed class FakeIdentity(string? id) : IInstallationIdentity
    {
        public string? InstallationId { get; } = id;
    }

    private sealed class MemStore : ILicenseStore
    {
        public byte[]? Data;
        public Task<byte[]?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Data);
        public Task WriteAsync(byte[] content, CancellationToken cancellationToken)
        {
            Data = content;
            return Task.CompletedTask;
        }
    }

    private static string Bound(string id) =>
        TestSigner.ValidPayload.Replace("\"maxInstallations\":1}", "\"maxInstallations\":1,\"installationId\":\"" + id + "\"}");

    private LicenseVerifier Verifier() => new([_signer.Trusted()], _time);

    private LicenseService Service(MemStore store, string? machineId, bool requireBinding = false) =>
        new(store, Verifier(), _time, new FakeIdentity(machineId), requireBinding);

    [Fact]
    public void RequestCode_RoundTrips_AndRejectsTampering()
    {
        var code = InstallationIds.ToRequestCode(IdA);
        Assert.StartsWith("FCR1-", code);
        Assert.True(InstallationIds.TryParseRequestCode(code, out var id));
        Assert.Equal(IdA, id);
        Assert.True(InstallationIds.TryParseRequestCode(code.ToLowerInvariant().Replace("-", " "), out _));

        var tampered = code.Remove(code.Length - 1) + (code[^1] == '0' ? '1' : '0');
        Assert.False(InstallationIds.TryParseRequestCode(tampered, out _));
        Assert.False(InstallationIds.TryParseRequestCode("FCR1-AAAA", out _));
        Assert.False(InstallationIds.TryParseRequestCode(null, out _));
    }

    [Fact]
    public void ActivationCode_RoundTrips_ForManyLengths()
    {
        for (var length = 1; length <= 300; length++)
        {
            var data = RandomNumberGenerator.GetBytes(length);
            var code = ActivationCode.Encode(data);
            Assert.True(ActivationCode.LooksLikeCode(System.Text.Encoding.ASCII.GetBytes(code)));
            Assert.True(ActivationCode.TryDecode(System.Text.Encoding.ASCII.GetBytes(code), out var back));
            Assert.Equal(data, back);
        }
        Assert.False(ActivationCode.TryDecode("FCL1-!!!!!"u8, out _));
        Assert.False(ActivationCode.TryDecode("hello"u8, out _));
    }

    [Fact]
    public void Verifier_ReadsInstallationId_AndRejectsBadOnes()
    {
        var ok = Verifier().Verify(_signer.Build(Bound(IdA)));
        Assert.Equal(LicenseStatus.Valid, ok.Status);
        Assert.Equal(IdA, ok.Payload!.InstallationId);

        var bad = Verifier().Verify(_signer.Build(Bound("not-hex")));
        Assert.Equal(LicenseStatus.InvalidPayload, bad.Status);
        Assert.Null(Verifier().Verify(_signer.Build(TestSigner.ValidPayload)).Payload!.InstallationId);
    }

    [Fact]
    public async Task BoundLicense_ImportsOnSameMachine_AndSurvivesReload()
    {
        var store = new MemStore();
        var result = await Service(store, IdA).ImportAsync(_signer.Build(Bound(IdA)), default);
        Assert.Equal(LicenseImportOutcome.Imported, result.Outcome);

        var status = await Service(store, IdA).GetStatusAsync(default);
        Assert.Equal(LicenseState.Valid, status.State);
    }

    [Fact]
    public async Task BoundLicense_IsRefusedOnAnotherMachine_AndStoreStaysEmpty()
    {
        var store = new MemStore();
        var result = await Service(store, IdB).ImportAsync(_signer.Build(Bound(IdA)), default);
        Assert.Equal(LicenseImportOutcome.Rejected, result.Outcome);
        Assert.Equal(LicenseStatus.WrongInstallation, result.Verification);
        Assert.Null(store.Data);
    }

    [Fact]
    public async Task CopiedLicenseFile_IsInvalidOnTheOtherMachine()
    {
        var store = new MemStore { Data = _signer.Build(Bound(IdA)) };
        var status = await Service(store, IdB).GetStatusAsync(default);
        Assert.Equal(LicenseState.Invalid, status.State);
        Assert.Equal("wrong_installation", status.Reason);
        Assert.Null(status.License);
    }

    [Fact]
    public async Task UnreadableMachineIdentity_FailsClosedForBoundLicenses()
    {
        var store = new MemStore();
        var result = await Service(store, null).ImportAsync(_signer.Build(Bound(IdA)), default);
        Assert.Equal(LicenseStatus.WrongInstallation, result.Verification);
    }

    [Fact]
    public async Task UnboundLicense_IsAcceptedByDefault_AndRefusedWhenBindingRequired()
    {
        var file = _signer.Build(TestSigner.ValidPayload);
        Assert.Equal(LicenseImportOutcome.Imported,
            (await Service(new MemStore(), IdA).ImportAsync(file, default)).Outcome);

        var strict = await Service(new MemStore(), IdA, requireBinding: true).ImportAsync(file, default);
        Assert.Equal(LicenseStatus.NotBound, strict.Verification);
    }

    [Fact]
    public async Task ActivationCode_AndFile_GiveTheSameResult_AndStoreTheFileBytes()
    {
        var file = _signer.Build(Bound(IdA));
        var code = System.Text.Encoding.ASCII.GetBytes(ActivationCode.Encode(file));

        var fileStore = new MemStore();
        var codeStore = new MemStore();
        var a = await Service(fileStore, IdA).ImportAsync(file, default);
        var b = await Service(codeStore, IdA).ImportAsync(code, default);

        Assert.Equal(a.Outcome, b.Outcome);
        Assert.Equal(LicenseImportOutcome.Imported, b.Outcome);
        Assert.Equal(fileStore.Data, codeStore.Data);
    }

    [Fact]
    public async Task TamperedActivationCode_IsRejected()
    {
        var code = ActivationCode.Encode(_signer.Build(Bound(IdA)));
        var tampered = code.Remove(40, 1).Insert(40, code[40] == 'A' ? "B" : "A");
        var result = await Service(new MemStore(), IdA).ImportAsync(System.Text.Encoding.ASCII.GetBytes(tampered), default);
        Assert.NotEqual(LicenseImportOutcome.Imported, result.Outcome);
    }
}