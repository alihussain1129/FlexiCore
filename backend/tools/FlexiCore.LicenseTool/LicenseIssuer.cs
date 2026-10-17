using System.Buffers.Text;
using System.Globalization;
using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool;

public sealed record LicenseRequest(
    string LicenseId,
    string Licensee,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    int GraceDays,
    int MaxInstallations,
    string Product = LicenseLimits.ExpectedProduct,
    string? InstallationId = null);

/// <summary>
/// Builds licenses in the exact format LicenseVerifier accepts: compact UTF-8 payload JSON in a fixed field order,
/// ES256 signature over those exact bytes (raw 64-byte r||s), base64url without padding, inside the envelope.
/// </summary>
public static class LicenseIssuer
{
    public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public static DateTimeOffset Truncate(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    public static string FormatTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>Accepts yyyy-MM-ddTHH:mm:ssZ or a plain date (midnight UTC).</summary>
    public static bool TryParseTimestamp(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(
            text, new[] { TimestampFormat, "yyyy-MM-dd" }, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);

    /// <summary>Applies the same rules the verifier enforces, and normalises timestamps to whole seconds.</summary>
    public static LicenseRequest Validate(LicenseRequest request, bool allowOtherProduct)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckText("The license id", request.LicenseId, LicenseLimits.MaxLicenseIdLength);
        CheckText("The licensee", request.Licensee, LicenseLimits.MaxLicenseeLength);

        if (allowOtherProduct) CheckText("The product", request.Product, LicenseLimits.MaxProductLength);
        else if (!string.Equals(request.Product, LicenseLimits.ExpectedProduct, StringComparison.Ordinal))
            throw new LicenseToolException($"The product must be '{LicenseLimits.ExpectedProduct}'.");

        if (request.InstallationId is not null && !InstallationIds.IsValid(request.InstallationId))
            throw new LicenseToolException("The installation id is not valid.");

        var issuedAt = Truncate(request.IssuedAt);
        var notBefore = Truncate(request.NotBefore);
        DateTimeOffset? expiresAt = request.ExpiresAt is { } e ? Truncate(e) : null;

        if (expiresAt is { } expires && expires <= notBefore)
            throw new LicenseToolException("The expiry must be later than the not-before time.");
        if (request.GraceDays < 0 || request.GraceDays > LicenseLimits.MaxGraceDays)
            throw new LicenseToolException($"Grace days must be 0 to {LicenseLimits.MaxGraceDays}.");
        if (request.MaxInstallations < 1 || request.MaxInstallations > LicenseLimits.MaxInstallationsLimit)
            throw new LicenseToolException($"Installations must be 1 to {LicenseLimits.MaxInstallationsLimit}.");

        return request with { IssuedAt = issuedAt, NotBefore = notBefore, ExpiresAt = expiresAt };
    }

    /// <summary>Strict issuing: product must be FlexiCore, and the result is verified with the real verifier before it is returned.</summary>
    public static byte[] Issue(LicenseRequest request, ILicenseSigner signer)
    {
        var r = Validate(request, allowOtherProduct: false);
        var file = Build(r, signer);
        SelfCheck(r, signer, file);
        return file;
    }

    /// <summary>For test vectors only: allows another product and skips the self-check.</summary>
    public static byte[] IssueUnchecked(LicenseRequest request, ILicenseSigner signer) =>
        Build(Validate(request, allowOtherProduct: true), signer);

    public static byte[] BuildPayload(LicenseRequest r)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", LicenseLimits.SupportedSchemaVersion);
            writer.WriteString("licenseId", r.LicenseId);
            writer.WriteString("product", r.Product);
            writer.WriteString("licensee", r.Licensee);
            writer.WriteString("issuedAt", FormatTimestamp(r.IssuedAt));
            writer.WriteString("notBefore", FormatTimestamp(r.NotBefore));
            if (r.ExpiresAt is { } expires) writer.WriteString("expiresAt", FormatTimestamp(expires));
            else writer.WriteNull("expiresAt");
            writer.WriteNumber("graceDays", r.GraceDays);
            writer.WriteNumber("maxInstallations", r.MaxInstallations);
            if (r.InstallationId is not null) writer.WriteString("installationId", r.InstallationId);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static byte[] Build(LicenseRequest r, ILicenseSigner signer)
    {
        var payload = BuildPayload(r);
        if (payload.Length > LicenseLimits.MaxPayloadBytes)
            throw new LicenseToolException("The payload is larger than the verifier allows.");

        var signature = signer.Sign(payload);
        if (signature.Length != LicenseLimits.SignatureBytes)
            throw new LicenseToolException("The signer returned a signature that is not 64 bytes (raw r||s).", 2);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", LicenseLimits.SupportedSchemaVersion);
            writer.WriteString("keyId", signer.KeyId);
            writer.WriteString("payload", Base64Url.EncodeToString(payload));
            writer.WriteString("signature", Base64Url.EncodeToString(signature));
            writer.WriteEndObject();
        }
        var file = stream.ToArray();
        if (file.Length > LicenseLimits.MaxFileBytes)
            throw new LicenseToolException("The license file is larger than the verifier allows.");
        return file;
    }

    private static void SelfCheck(LicenseRequest r, ILicenseSigner signer, byte[] file)
    {
        var verifier = new LicenseVerifier(
            new[] { new TrustedKey(signer.KeyId, signer.SubjectPublicKeyInfo) },
            new PinnedTimeProvider(r.NotBefore));
        var result = verifier.Verify(file);

        var p = result.Payload;
        var same = result.Status == LicenseStatus.Valid
                   && p is not null
                   && p.LicenseId == r.LicenseId
                   && p.Product == r.Product
                   && p.Licensee == r.Licensee
                   && p.IssuedAt == r.IssuedAt
                   && p.NotBefore == r.NotBefore
                   && p.ExpiresAt == r.ExpiresAt
                   && p.GraceDays == r.GraceDays
                   && p.MaxInstallations == r.MaxInstallations
                   && p.InstallationId == r.InstallationId;
        if (!same)
            throw new LicenseToolException($"Self-check failed ({result.Status}). Nothing was written.", 2);
    }

    private static void CheckText(string what, string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) throw new LicenseToolException($"{what} must not be empty.");
        if (value.Length > maxLength) throw new LicenseToolException($"{what} must be at most {maxLength} characters.");
        if (value.Any(char.IsControl)) throw new LicenseToolException($"{what} must not contain control characters.");
    }
}