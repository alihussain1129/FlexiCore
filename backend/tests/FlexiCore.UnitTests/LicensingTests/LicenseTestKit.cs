using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FlexiCore.Licensing;

namespace FlexiCore.UnitTests.LicensingTests;

// TEST ONLY. Keys are generated in memory per run; nothing here is, or resembles, a production key.

internal static class Q
{
    /// <summary>Lets test JSON be written with single quotes.</summary>
    public static string Fix(string s) => s.Replace('\'', '"');
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class TestSigner : IDisposable
{
    public const string KeyId = "test-key-1";

    public static string ValidPayload => Q.Fix(
        "{'schemaVersion':1,'licenseId':'LIC-0001','product':'FlexiCore','licensee':'Test Customer'," +
        "'issuedAt':'2026-01-01T00:00:00Z','notBefore':'2026-01-01T00:00:00Z'," +
        "'expiresAt':'2027-01-01T00:00:00Z','graceDays':0,'maxInstallations':1}");

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public TrustedKey Trusted(string keyId = KeyId) => new(keyId, _key.ExportSubjectPublicKeyInfo());

    public byte[] Sign(byte[] data) =>
        _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public byte[] SignDer(byte[] data) =>
        _key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

    public byte[] Build(string payloadJson, int schemaVersion = 1, string keyId = KeyId)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        return Envelope(payload, Sign(payload), schemaVersion, keyId);
    }

    public static byte[] Envelope(byte[] payload, byte[] signature, int schemaVersion = 1, string keyId = KeyId)
    {
        var json = "{\"schemaVersion\":" + schemaVersion + ",\"keyId\":\"" + keyId + "\",\"payload\":\""
                   + Base64Url.EncodeToString(payload) + "\",\"signature\":\"" + Base64Url.EncodeToString(signature) + "\"}";
        return Encoding.UTF8.GetBytes(json);
    }

    public void Dispose() => _key.Dispose();
}