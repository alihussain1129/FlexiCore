using System.Buffers.Text;
using System.Text;
using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.UnitTests.LicensingTests;

public class LicenseHardeningTests : IDisposable
{
    private readonly TestSigner _signer = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

    public void Dispose() => _signer.Dispose();

    private LicenseVerifier Verifier() => new([_signer.Trusted()], _time);

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    private static byte[] PaddedPayload(int totalBytes)
    {
        var text = TestSigner.ValidPayload;
        var pad = totalBytes - Encoding.UTF8.GetByteCount(text);
        Assert.True(pad >= 0);
        return Utf8(text + new string(' ', pad)); // trailing whitespace is valid JSON
    }

    [Fact]
    public void LoneSurrogateInKeyId_IsMalformed_NotAnException()
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var json = "{\"schemaVersion\":1,\"keyId\":\"\\ud800\",\"payload\":\""
                   + Base64Url.EncodeToString(payload) + "\",\"signature\":\""
                   + Base64Url.EncodeToString(_signer.Sign(payload)) + "\"}";
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(Utf8(json)).Status);
    }

    [Fact]
    public void PayloadExactlyAtLimit_IsAccepted()
    {
        var payload = PaddedPayload(LicenseLimits.MaxPayloadBytes);
        Assert.Equal(LicenseLimits.MaxPayloadBytes, payload.Length);
        var file = TestSigner.Envelope(payload, _signer.Sign(payload));
        Assert.Equal(LicenseStatus.Valid, Verifier().Verify(file).Status);
    }

    [Fact]
    public void PayloadOneByteOverLimit_IsMalformed()
    {
        var payload = PaddedPayload(LicenseLimits.MaxPayloadBytes + 1);
        var file = TestSigner.Envelope(payload, _signer.Sign(payload));
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(file).Status);
    }

    [Fact]
    public void KeyIdAtMaxLength_IsUnknownKeyId()
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var file = TestSigner.Envelope(payload, _signer.Sign(payload), keyId: new string('a', LicenseLimits.MaxKeyIdLength));
        Assert.Equal(LicenseStatus.UnknownKeyId, Verifier().Verify(file).Status);
    }

    [Fact]
    public void KeyIdOverMaxLength_IsMalformed()
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var file = TestSigner.Envelope(payload, _signer.Sign(payload), keyId: new string('a', LicenseLimits.MaxKeyIdLength + 1));
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(file).Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(65)]
    public void SignatureOfWrongLength_IsInvalidSignature(int length)
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var file = TestSigner.Envelope(payload, new byte[length]);
        Assert.Equal(LicenseStatus.InvalidSignature, Verifier().Verify(file).Status);
    }

    [Fact]
    public void EmptySignature_IsMalformed()
    {
        var payload = Utf8(TestSigner.ValidPayload);
        var file = TestSigner.Envelope(payload, []);
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(file).Status);
    }

    [Fact]
    public void SeveralTrustedKeys_EachOnlyValidatesItsOwnKeyId()
    {
        using var other = new TestSigner();
        var verifier = new LicenseVerifier([_signer.Trusted("key-a"), other.Trusted("key-b")], _time);

        Assert.Equal(LicenseStatus.Valid, verifier.Verify(_signer.Build(TestSigner.ValidPayload, keyId: "key-a")).Status);
        Assert.Equal(LicenseStatus.Valid, verifier.Verify(other.Build(TestSigner.ValidPayload, keyId: "key-b")).Status);
        // Signed by key B but claiming key A must not verify.
        Assert.Equal(LicenseStatus.InvalidSignature, verifier.Verify(other.Build(TestSigner.ValidPayload, keyId: "key-a")).Status);
    }

    [Fact]
    public void JsonNestedBeyondDepthLimit_IsMalformed()
    {
        var json = "{\"schemaVersion\":1,\"x\":[[[[[[[[1]]]]]]]]}";
        Assert.Equal(LicenseStatus.Malformed, Verifier().Verify(Utf8(json)).Status);
    }

    [Theory]
    [InlineData("=")]
    [InlineData("==")]
    [InlineData(" ")]
    [InlineData("\n")]
    public void NonCanonicalBase64_IsNeverMisread(string suffix)
    {
        // Whatever the decoder does with padding or whitespace, the outcome must be a clean
        // Valid (same bytes) or Malformed. It must not throw or produce any other state.
        var payload = Utf8(TestSigner.ValidPayload);
        var json = "{\"schemaVersion\":1,\"keyId\":\"" + TestSigner.KeyId + "\",\"payload\":\""
                   + Base64Url.EncodeToString(payload) + suffix + "\",\"signature\":\""
                   + Base64Url.EncodeToString(_signer.Sign(payload)) + "\"}";
        var status = Verifier().Verify(Utf8(json)).Status;
        Assert.True(status is LicenseStatus.Valid or LicenseStatus.Malformed, $"Unexpected status {status}");
    }

    [Fact]
    public void GraceEnd_NearMaxDate_ClampsInsteadOfThrowing()
    {
        var expires = new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);
        Assert.Equal(DateTimeOffset.MaxValue, LicenseVerifier.GraceEnd(expires, 365));
        Assert.Equal(expires, LicenseVerifier.GraceEnd(expires, 0));

        var payload = TestSigner.ValidPayload
            .Replace(Q.Fix("'expiresAt':'2027-01-01T00:00:00Z'"), Q.Fix("'expiresAt':'9999-12-31T23:59:59Z'"))
            .Replace(Q.Fix("'graceDays':0"), Q.Fix("'graceDays':365"));
        _time.Now = DateTimeOffset.MaxValue;
        Assert.Equal(LicenseStatus.ValidInGrace, Verifier().Verify(_signer.Build(payload)).Status);
    }
}