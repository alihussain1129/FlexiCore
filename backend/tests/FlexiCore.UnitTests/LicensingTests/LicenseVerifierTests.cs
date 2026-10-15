using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.UnitTests.LicensingTests;

public class LicenseVerifierTests : IDisposable
{
    private readonly TestSigner _signer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    public void Dispose() => _signer.Dispose();

    private LicenseVerifier Verifier() => new([_signer.Trusted()], _time);

    private static string Mod(string old, string replacement)
    {
        var o = Q.Fix(old);
        Assert.Contains(o, TestSigner.ValidPayload);
        return TestSigner.ValidPayload.Replace(o, Q.Fix(replacement));
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    // ---- accepted ----

    [Fact]
    public void ValidLicense_IsAccepted()
    {
        var r = Verifier().Verify(_signer.Build(TestSigner.ValidPayload));
        Assert.Equal(LicenseStatus.Valid, r.Status);
        Assert.True(r.IsAccepted);
        Assert.NotNull(r.Payload);
        Assert.Equal("LIC-0001", r.Payload!.LicenseId);
        Assert.Equal(0, r.Payload.GraceDays);
        Assert.Equal(1, r.Payload.MaxInstallations);
    }

    [Fact]
    public void PerpetualLicense_NullExpiry_IsAcceptedFarInFuture()
    {
        _time.Now = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var r = Verifier().Verify(_signer.Build(Mod("'expiresAt':'2027-01-01T00:00:00Z'", "'expiresAt':null")));
        Assert.Equal(LicenseStatus.Valid, r.Status);
        Assert.Null(r.Payload!.ExpiresAt);
    }

    [Fact]
    public void GraceDays_IsOptionalAndDefaultsToZero()
    {
        var r = Verifier().Verify(_signer.Build(Mod("'graceDays':0,", "")));
        Assert.Equal(LicenseStatus.Valid, r.Status);
        Assert.Equal(0, r.Payload!.GraceDays);
    }

    [Fact]
    public void Verification_IsDeterministic()
    {
        var file = _signer.Build(TestSigner.ValidPayload);
        var a = Verifier().Verify(file);
        var b = Verifier().Verify(file);
        Assert.Equal(a, b);
    }

    // ---- signature ----

    [Fact]
    public void TamperedPayload_IsInvalidSignature()
    {
        var signature = _signer.Sign(Utf8(TestSigner.ValidPayload));
        var tampered = Utf8(Mod("'graceDays':0", "'graceDays':300"));
        var r = Verifier().Verify(TestSigner.Envelope(tampered, signature));
        Assert.Equal(LicenseStatus.InvalidSignature, r.Status);
        Assert.Null(r.Payload);
    }

    [Fact]
    public void FlippedSignatureByte_IsInvalidSignature()
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var signature = _signer.Sign(payload);
        signature[0] ^= 0xFF;
        Assert.Equal(LicenseStatus.InvalidSignature, Verifier().Verify(TestSigner.Envelope(payload, signature)).Status);
    }

    [Fact]
    public void DerEncodedSignature_IsRejected()
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var der = _signer.SignDer(payload);
        Assert.NotEqual(LicenseLimits.SignatureBytes, der.Length);
        Assert.Equal(LicenseStatus.InvalidSignature, Verifier().Verify(TestSigner.Envelope(payload, der)).Status);
    }

    [Fact]
    public void SignedByDifferentKeyWithKnownId_IsInvalidSignature()
    {
        using var other = new TestSigner();
        Assert.Equal(LicenseStatus.InvalidSignature, Verifier().Verify(other.Build(TestSigner.ValidPayload)).Status);
    }

    [Fact]
    public void UnknownKeyId_IsUnknownKeyId()
    {
        var file = _signer.Build(TestSigner.ValidPayload, keyId: "some-other-key");
        Assert.Equal(LicenseStatus.UnknownKeyId, Verifier().Verify(file).Status);
    }

    [Fact]
    public void NoTrustedKeys_FailsClosed_EvenForValidAndGarbageInput()
    {
        var empty = new LicenseVerifier([], _time);
        Assert.Equal(LicenseStatus.NoTrustedKeys, empty.Verify(_signer.Build(TestSigner.ValidPayload)).Status);
        Assert.Equal(LicenseStatus.NoTrustedKeys, empty.Verify(Utf8("not json")).Status);
    }

    // ---- product and time ----

    [Fact]
    public void WrongProduct_IsRejected()
    {
        var r = Verifier().Verify(_signer.Build(Mod("'product':'FlexiCore'", "'product':'Other'")));
        Assert.Equal(LicenseStatus.WrongProduct, r.Status);
        Assert.Null(r.Payload);
    }

    [Fact]
    public void Expired_WithNoGrace_IsExpired()
    {
        var r = Verifier().Verify(_signer.Build(Mod("'expiresAt':'2027-01-01T00:00:00Z'", "'expiresAt':'2026-05-01T00:00:00Z'")));
        Assert.Equal(LicenseStatus.Expired, r.Status);
        Assert.False(r.IsAccepted);
        Assert.NotNull(r.Payload); // authentic but expired: details stay available for display, never for access
    }

    [Theory]
    [InlineData("2025-12-31T23:59:59Z", LicenseStatus.NotYetValid)]
    [InlineData("2026-01-01T00:00:00Z", LicenseStatus.Valid)]
    [InlineData("2027-01-01T00:00:00Z", LicenseStatus.Valid)]
    [InlineData("2027-01-01T00:00:01Z", LicenseStatus.ValidInGrace)]
    [InlineData("2027-01-04T00:00:00Z", LicenseStatus.ValidInGrace)]
    [InlineData("2027-01-04T00:00:01Z", LicenseStatus.Expired)]
    public void TimeBoundaries_WithThreeGraceDays(string now, LicenseStatus expected)
    {
        _time.Now = DateTimeOffset.Parse(now, CultureInfo.InvariantCulture);
        var file = _signer.Build(Mod("'graceDays':0", "'graceDays':3"));
        Assert.Equal(expected, Verifier().Verify(file).Status);
    }

    // ---- schema version ----

    [Theory]
    [InlineData(2)]
    [InlineData(99)]
    public void UnsupportedSchemaVersion_IsReported(int version)
    {
        var file = _signer.Build(TestSigner.ValidPayload, schemaVersion: version);
        Assert.Equal(LicenseStatus.UnsupportedSchemaVersion, Verifier().Verify(file).Status);
    }

    [Fact]
    public void FutureSchemaWithExtraFields_IsUnsupportedNotMalformed()
    {
        var file = Utf8(Q.Fix("{'schemaVersion':2,'extra':true}"));
        Assert.Equal(LicenseStatus.UnsupportedSchemaVersion, Verifier().Verify(file).Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveSchemaVersion_IsMalformed(int version)
    {
        var file = _signer.Build(TestSigner.ValidPayload, schemaVersion: version);
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(file).Status);
    }

    // ---- malformed envelope ----

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{'schemaVersion':1}")]
    [InlineData("{'schemaVersion':'1','keyId':'test-key-1','payload':'AA','signature':'AA'}")]
    [InlineData("{'schemaVersion':1,'keyId':'test-key-1'")]
    [InlineData("{'schemaVersion':1,'keyId':'bad key','payload':'AA','signature':'AA'}")]
    [InlineData("{'schemaVersion':1,'keyId':'test-key-1','payload':'!!!','signature':'AA'}")]
    [InlineData("{'schemaVersion':1,'keyId':'test-key-1','payload':'','signature':'AA'}")]
    [InlineData("{'schemaVersion':1,'keyId':'test-key-1','payload':'AA','signature':'AA','extra':1}")]
    [InlineData("{'schemaVersion':1,'schemaVersion':1,'keyId':'test-key-1','payload':'AA','signature':'AA'}")]
    public void CorruptEnvelope_IsMalformed(string json)
    {
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(Utf8(Q.Fix(json))).Status);
    }

    [Fact]
    public void OversizedFile_IsFileTooLarge()
    {
        var big = new byte[LicenseLimits.MaxFileBytes + 1];
        Array.Fill(big, (byte)'a');
        Assert.Equal(LicenseStatus.FileTooLarge, Verifier().Verify(big).Status);
    }

    [Fact]
    public void FileAtExactLimit_IsNotTooLarge()
    {
        var atLimit = new byte[LicenseLimits.MaxFileBytes];
        Array.Fill(atLimit, (byte)'a');
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(atLimit).Status);
    }

    [Fact]
    public void OversizedPayload_IsMalformed()
    {
        var payload = Mod("'licensee':'Test Customer'", "'licensee':'" + new string('x', 5000) + "'");
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(_signer.Build(payload)).Status);
    }

    // ---- invalid (but correctly signed) payload ----

    [Theory]
    [InlineData("'licenseId':'LIC-0001',", "")]
    [InlineData("'graceDays':0", "'graceDays':366")]
    [InlineData("'graceDays':0", "'graceDays':-1")]
    [InlineData("'graceDays':0", "'graceDays':'0'")]
    [InlineData("'graceDays':0", "'graceDays':1.5")]
    [InlineData("'maxInstallations':1", "'maxInstallations':0")]
    [InlineData("'maxInstallations':1", "'maxInstallations':100001")]
    [InlineData("'issuedAt':'2026-01-01T00:00:00Z'", "'issuedAt':'2026-01-01'")]
    [InlineData("'issuedAt':'2026-01-01T00:00:00Z'", "'issuedAt':'2026-01-01T00:00:00+00:00'")]
    [InlineData("'notBefore':'2026-01-01T00:00:00Z'", "'notBefore':'not a date'")]
    [InlineData("'expiresAt':'2027-01-01T00:00:00Z'", "'expiresAt':'2025-12-31T00:00:00Z'")]
    [InlineData("'expiresAt':'2027-01-01T00:00:00Z'", "'expiresAt':5")]
    [InlineData("'expiresAt':'2027-01-01T00:00:00Z',", "")]
    [InlineData("'schemaVersion':1,", "'schemaVersion':2,")]
    [InlineData("'licensee':'Test Customer'", "'licensee':''")]
    [InlineData("'product':'FlexiCore'", "'product':null")]
    [InlineData("'licenseId':'LIC-0001'", "'licenseId':'LIC-0001','licenseId':'LIC-0002'")]
    [InlineData("'licenseId':'LIC-0001'", "'licenseId':'LIC-0001','extra':1")]
    public void SignedButInvalidPayload_IsInvalidPayload(string old, string replacement)
    {
        var r = Verifier().Verify(_signer.Build(Mod(old, replacement)));
        Assert.Equal(LicenseStatus.InvalidPayload, r.Status);
        Assert.Null(r.Payload);
    }

    [Fact]
    public void OverlongLicensee_IsInvalidPayload()
    {
        var payload = Mod("'licensee':'Test Customer'", "'licensee':'" + new string('x', LicenseLimits.MaxLicenseeLength + 1) + "'");
        Assert.Equal(LicenseStatus.InvalidPayload, Verifier().Verify(_signer.Build(payload)).Status);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public void SignedNonObjectPayload_IsInvalidPayload(string payload)
    {
        Assert.Equal(LicenseStatus.InvalidPayload, Verifier().Verify(_signer.Build(payload)).Status);
    }

    [Fact]
    public void SignedInvalidUtf8Payload_IsInvalidPayload()
    {
        var payload = new byte[] { 0xFF, 0xFE, 0xFD };
        var file = TestSigner.Envelope(payload, _signer.Sign(payload));
        Assert.Equal(LicenseStatus.InvalidPayload, Verifier().Verify(file).Status);
    }

    // ---- verifier construction ----

    [Fact]
    public void Constructor_RejectsNonP256Key()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var key = new TrustedKey("k384", p384.ExportSubjectPublicKeyInfo());
        Assert.Throws<ArgumentException>(() => new LicenseVerifier([key], _time));
    }

    [Fact]
    public void Constructor_RejectsGarbageKeyBytes()
    {
        var key = new TrustedKey("junk", [1, 2, 3]);
        Assert.Throws<ArgumentException>(() => new LicenseVerifier([key], _time));
    }

    [Fact]
    public void Constructor_RejectsDuplicateAndInvalidKeyIds()
    {
        Assert.Throws<ArgumentException>(() => new LicenseVerifier([_signer.Trusted("dup"), _signer.Trusted("dup")], _time));
        Assert.Throws<ArgumentException>(() => new LicenseVerifier([_signer.Trusted("bad id")], _time));
    }

    // ---- production key loader ----

    [Fact]
    public void ParseKeySet_AcceptsValidSet_AndVerifierWorksWithIt()
    {
        var b64 = Convert.ToBase64String(_signer.Trusted().SubjectPublicKeyInfo);
        var json = Q.Fix("{'keys':[{'keyId':'" + TestSigner.KeyId + "','publicKey':'" + b64 + "'}]}");
        var keys = ProductionKeyLoader.ParseKeySet(Utf8(json));
        Assert.Single(keys);
        var verifier = new LicenseVerifier(keys, _time);
        Assert.Equal(LicenseStatus.Valid, verifier.Verify(_signer.Build(TestSigner.ValidPayload)).Status);
    }

    [Fact]
    public void ParseKeySet_ReturnsEmpty_ForAnyDefect()
    {
        var good = Convert.ToBase64String(_signer.Trusted().SubjectPublicKeyInfo);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var bad384 = Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo());

        string[] defects =
        [
            "",
            "{}",
            "{'keys':[]}",
            "{'keys':[{'keyId':'k','publicKey':'!!!'}]}",
            "{'keys':[{'keyId':'k','publicKey':'" + bad384 + "'}]}",
            "{'keys':[{'keyId':'k','publicKey':'" + good + "'},{'keyId':'k','publicKey':'" + good + "'}]}",
            "{'keys':[{'keyId':'k','publicKey':'" + good + "','extra':'x'}]}",
            "{'keys':[{'keyId':'bad id','publicKey':'" + good + "'}]}",
            "{'keys':[{'keyId':'k','publicKey':'" + good + "'}],'extra':1}",
        ];

        foreach (var defect in defects)
            Assert.Empty(ProductionKeyLoader.ParseKeySet(Utf8(Q.Fix(defect))));
    }

    [Fact]
    public void Loader_ReturnsEmpty_WhenAssemblyHasNoKeyResource()
    {
        Assert.Empty(ProductionKeyLoader.Load(typeof(LicenseVerifierTests).Assembly));
        var verifier = new LicenseVerifier(ProductionKeyLoader.Load(typeof(LicenseVerifierTests).Assembly), _time);
        Assert.Equal(LicenseStatus.NoTrustedKeys, verifier.Verify(_signer.Build(TestSigner.ValidPayload)).Status);
    }

    [Fact]
    public void LicensingAssembly_EmbedsOnlyTheProductionKeyFile()
    {
        var names = typeof(LicenseVerifier).Assembly.GetManifestResourceNames();
        Assert.All(names, n => Assert.EndsWith(ProductionKeyLoader.ResourceFileName, n));
    }
}