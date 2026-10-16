using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool.Tests;

public sealed class IssuerTests
{
    private static DateTimeOffset T(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static EcdsaSigner NewSigner(string keyId = "test-key-1") =>
        new(ECDsa.Create(ECCurve.NamedCurves.nistP256), keyId);

    private static LicenseVerifier VerifierFor(ILicenseSigner signer, string now = "2026-06-01T00:00:00Z", string? asKeyId = null) =>
        new(new[] { new TrustedKey(asKeyId ?? signer.KeyId, signer.SubjectPublicKeyInfo) }, new PinnedTimeProvider(T(now)));

    private static LicenseRequest Req(
        string id = "LIC-0001", string licensee = "Test Customer", string notBefore = "2026-01-01T00:00:00Z",
        string? expires = "2027-01-01T00:00:00Z", int grace = 0, int installations = 1, string product = "FlexiCore") =>
        new(id, licensee, T("2026-01-01T00:00:00Z"), T(notBefore), expires is null ? null : T(expires), grace, installations, product);

    private static (string KeyId, byte[] Payload, byte[] Signature) Open(byte[] file)
    {
        using var doc = JsonDocument.Parse(file);
        var root = doc.RootElement;
        return (
            root.GetProperty("keyId").GetString()!,
            Base64Url.DecodeFromChars(root.GetProperty("payload").GetString()!),
            Base64Url.DecodeFromChars(root.GetProperty("signature").GetString()!));
    }

    private static byte[] Repack(string keyId, byte[] payload, byte[] signature) =>
        Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"keyId\":\"" + keyId + "\",\"payload\":\"" + Base64Url.EncodeToString(payload)
            + "\",\"signature\":\"" + Base64Url.EncodeToString(signature) + "\"}");

    [Fact]
    public void Issued_license_verifies_as_Valid_with_the_matching_public_key()
    {
        using var signer = NewSigner();
        var file = LicenseIssuer.Issue(Req(), signer);

        var result = VerifierFor(signer).Verify(file);

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal("LIC-0001", result.Payload!.LicenseId);
        Assert.Equal("Test Customer", result.Payload.Licensee);
        Assert.Equal(1, result.Payload.MaxInstallations);
    }

    [Fact]
    public void Payload_is_compact_with_the_fixed_field_order_and_exact_timestamp_format()
    {
        using var signer = NewSigner();
        var (_, payload, _) = Open(LicenseIssuer.Issue(Req(), signer));

        Assert.Equal(
            "{\"schemaVersion\":1,\"licenseId\":\"LIC-0001\",\"product\":\"FlexiCore\",\"licensee\":\"Test Customer\","
            + "\"issuedAt\":\"2026-01-01T00:00:00Z\",\"notBefore\":\"2026-01-01T00:00:00Z\","
            + "\"expiresAt\":\"2027-01-01T00:00:00Z\",\"graceDays\":0,\"maxInstallations\":1}",
            Encoding.UTF8.GetString(payload));
    }

    [Fact]
    public void Signature_is_the_raw_64_byte_r_s_form()
    {
        using var signer = NewSigner();
        var (_, _, signature) = Open(LicenseIssuer.Issue(Req(), signer));
        Assert.Equal(LicenseLimits.SignatureBytes, signature.Length);
    }

    [Fact]
    public void The_envelope_carries_the_key_id_and_unpadded_base64url()
    {
        using var signer = NewSigner("fc-2026-a");
        var file = LicenseIssuer.Issue(Req(), signer);
        var text = Encoding.UTF8.GetString(file);

        Assert.Equal("fc-2026-a", Open(file).KeyId);
        Assert.DoesNotContain("=", text.Replace("\":", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void Perpetual_license_has_a_null_expiry_and_verifies()
    {
        using var signer = NewSigner();
        var file = LicenseIssuer.Issue(Req(expires: null), signer);

        var result = VerifierFor(signer, now: "2099-01-01T00:00:00Z").Verify(file);

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Null(result.Payload!.ExpiresAt);
        Assert.Contains("\"expiresAt\":null", Encoding.UTF8.GetString(Open(file).Payload), StringComparison.Ordinal);
    }

    [Fact]
    public void Sub_second_timestamps_are_truncated_to_whole_seconds()
    {
        using var signer = NewSigner();
        var request = Req() with { NotBefore = T("2026-01-01T00:00:00Z").AddMilliseconds(999) };

        var (_, payload, _) = Open(LicenseIssuer.Issue(request, signer));

        Assert.Contains("\"notBefore\":\"2026-01-01T00:00:00Z\"", Encoding.UTF8.GetString(payload), StringComparison.Ordinal);
    }

    [Fact]
    public void A_modified_payload_is_InvalidSignature()
    {
        using var signer = NewSigner();
        var (keyId, payload, signature) = Open(LicenseIssuer.Issue(Req(), signer));
        var changed = Encoding.UTF8.GetString(payload).Replace("\"graceDays\":0", "\"graceDays\":300", StringComparison.Ordinal);

        var result = VerifierFor(signer).Verify(Repack(keyId, Encoding.UTF8.GetBytes(changed), signature));

        Assert.Equal(LicenseStatus.InvalidSignature, result.Status);
    }

    [Fact]
    public void A_modified_signature_is_InvalidSignature()
    {
        using var signer = NewSigner();
        var (keyId, payload, signature) = Open(LicenseIssuer.Issue(Req(), signer));
        signature[10] ^= 0xFF;

        Assert.Equal(LicenseStatus.InvalidSignature, VerifierFor(signer).Verify(Repack(keyId, payload, signature)).Status);
    }

    [Fact]
    public void A_license_checked_after_its_expiry_is_Expired()
    {
        using var signer = NewSigner();
        var file = LicenseIssuer.Issue(Req(expires: "2026-05-01T00:00:00Z"), signer);

        Assert.Equal(LicenseStatus.Expired, VerifierFor(signer, now: "2026-06-01T00:00:00Z").Verify(file).Status);
        Assert.Equal(LicenseStatus.Valid, VerifierFor(signer, now: "2026-04-01T00:00:00Z").Verify(file).Status);
    }

    [Fact]
    public void A_license_inside_its_grace_period_is_ValidInGrace()
    {
        using var signer = NewSigner();
        var file = LicenseIssuer.Issue(Req(expires: "2026-05-30T00:00:00Z", grace: 5), signer);

        Assert.Equal(LicenseStatus.ValidInGrace, VerifierFor(signer, now: "2026-06-01T00:00:00Z").Verify(file).Status);
    }

    [Fact]
    public void A_wrong_product_license_is_WrongProduct_and_strict_issuing_refuses_it()
    {
        using var signer = NewSigner();
        var request = Req(product: "OtherProduct");

        Assert.Throws<LicenseToolException>(() => LicenseIssuer.Issue(request, signer));

        var file = LicenseIssuer.IssueUnchecked(request, signer);
        Assert.Equal(LicenseStatus.WrongProduct, VerifierFor(signer).Verify(file).Status);
    }

    [Fact]
    public void A_key_id_the_verifier_does_not_trust_is_UnknownKeyId()
    {
        using var signer = NewSigner("fc-2026-a");
        var file = LicenseIssuer.Issue(Req(), signer);

        Assert.Equal(LicenseStatus.UnknownKeyId, VerifierFor(signer, asKeyId: "fc-2026-b").Verify(file).Status);
    }

    [Fact]
    public void A_different_key_with_the_same_id_is_InvalidSignature()
    {
        using var signer = NewSigner("fc-2026-a");
        using var other = NewSigner("fc-2026-a");
        var file = LicenseIssuer.Issue(Req(), signer);

        Assert.Equal(LicenseStatus.InvalidSignature, VerifierFor(other).Verify(file).Status);
    }

    [Theory]
    [InlineData("", "Customer", "2027-01-01T00:00:00Z", 0, 1)]
    [InlineData("LIC-1", "", "2027-01-01T00:00:00Z", 0, 1)]
    [InlineData("LIC-1", "Customer", "2025-12-31T00:00:00Z", 0, 1)]
    [InlineData("LIC-1", "Customer", "2026-01-01T00:00:00Z", 0, 1)]
    [InlineData("LIC-1", "Customer", "2027-01-01T00:00:00Z", 366, 1)]
    [InlineData("LIC-1", "Customer", "2027-01-01T00:00:00Z", -1, 1)]
    [InlineData("LIC-1", "Customer", "2027-01-01T00:00:00Z", 0, 0)]
    [InlineData("LIC-1", "Customer", "2027-01-01T00:00:00Z", 0, 100001)]
    [InlineData("LIC-1", "Line\nbreak", "2027-01-01T00:00:00Z", 0, 1)]
    public void Requests_the_verifier_would_reject_are_refused_before_signing(
        string id, string licensee, string expires, int grace, int installations)
    {
        using var signer = NewSigner();
        Assert.Throws<LicenseToolException>(() =>
            LicenseIssuer.Issue(Req(id, licensee, expires: expires, grace: grace, installations: installations), signer));
    }

    [Fact]
    public void Over_long_text_is_refused()
    {
        using var signer = NewSigner();
        Assert.Throws<LicenseToolException>(() =>
            LicenseIssuer.Issue(Req(licensee: new string('x', LicenseLimits.MaxLicenseeLength + 1)), signer));
        Assert.Throws<LicenseToolException>(() =>
            LicenseIssuer.Issue(Req(id: new string('x', LicenseLimits.MaxLicenseIdLength + 1)), signer));
    }

    [Fact]
    public void Non_ascii_and_quote_characters_survive_a_round_trip()
    {
        using var signer = NewSigner();
        const string licensee = "Caf\u00e9 \"Noor\" & Sons <Ltd>";

        var result = VerifierFor(signer).Verify(LicenseIssuer.Issue(Req(licensee: licensee), signer));

        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(licensee, result.Payload!.Licensee);
    }

    [Fact]
    public void Public_key_set_output_is_accepted_by_the_production_key_loader()
    {
        using var signer = NewSigner("fc-2026-a");
        var bytes = PublicKeyExport.Build(new[] { new TrustedKey(signer.KeyId, signer.SubjectPublicKeyInfo) });

        var parsed = ProductionKeyLoader.ParseKeySet(bytes);

        Assert.Single(parsed);
        Assert.Equal("fc-2026-a", parsed[0].KeyId);
        Assert.True(parsed[0].SubjectPublicKeyInfo.AsSpan().SequenceEqual(signer.SubjectPublicKeyInfo));
    }
}