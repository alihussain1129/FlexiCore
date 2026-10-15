using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlexiCore.Licensing;

/// <summary>
/// Verifies ES256 (ECDSA P-256 + SHA-256, raw 64-byte r||s signature) license files against a set of
/// trusted public keys. It never throws for bad license input; it returns a <see cref="LicenseStatus"/>.
/// With no trusted keys it always returns NoTrustedKeys (fail closed).
/// </summary>
public sealed class LicenseVerifier
{
    private const string P256Oid = "1.2.840.10045.3.1.7";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = LicenseLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    private static readonly string[] EnvelopeFields = ["schemaVersion", "keyId", "payload", "signature"];

    private static readonly string[] PayloadFields =
    [
        "schemaVersion", "licenseId", "product", "licensee", "issuedAt",
        "notBefore", "expiresAt", "graceDays", "maxInstallations",
    ];

    internal static readonly Regex KeyIdPattern = new(
        @"^[A-Za-z0-9._-]{1,64}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;

    /// <summary>Number of trusted keys loaded. Zero means every verification fails closed with NoTrustedKeys.</summary>
    public int TrustedKeyCount => _keys.Count;

    /// <exception cref="ArgumentException">A key id is invalid or duplicated, or a key is not a valid P-256 public key.</exception>
    public LicenseVerifier(IEnumerable<TrustedKey> trustedKeys, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _time = timeProvider;

        foreach (var key in trustedKeys)
        {
            if (key is null || key.SubjectPublicKeyInfo is null || !KeyIdPattern.IsMatch(key.KeyId ?? ""))
                throw new ArgumentException("Trusted key has an invalid key id.", nameof(trustedKeys));
            if (!IsValidP256PublicKey(key.SubjectPublicKeyInfo))
                throw new ArgumentException($"Trusted key '{key.KeyId}' is not a valid ECDSA P-256 public key.", nameof(trustedKeys));
            if (!_keys.TryAdd(key.KeyId!, (byte[])key.SubjectPublicKeyInfo.Clone()))
                throw new ArgumentException($"Duplicate trusted key id '{key.KeyId}'.", nameof(trustedKeys));
        }
    }

    public LicenseVerificationResult Verify(ReadOnlyMemory<byte> file)
    {
        try
        {
            return VerifyCore(file);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Bad input must never surface as an exception; anything unexpected is treated as a malformed file.
            return Fail(LicenseStatus.Malformed);
        }
    }

    private LicenseVerificationResult VerifyCore(ReadOnlyMemory<byte> file)
    {
        if (file.Length > LicenseLimits.MaxFileBytes) return Fail(LicenseStatus.FileTooLarge);
        if (_keys.Count == 0) return Fail(LicenseStatus.NoTrustedKeys);
        if (file.IsEmpty) return Fail(LicenseStatus.Malformed);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(file, JsonOptions); }
        catch (JsonException) { return Fail(LicenseStatus.Malformed); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schemaVersion", out var sv)
                || sv.ValueKind != JsonValueKind.Number
                || !sv.TryGetInt32(out var version)
                || version < 1)
                return Fail(LicenseStatus.Malformed);

            // Checked before the strict field check so a future format with new fields is reported as unsupported.
            if (version != LicenseLimits.SupportedSchemaVersion) return Fail(LicenseStatus.UnsupportedSchemaVersion);

            if (!TryReadProperties(root, EnvelopeFields, out var env)) return Fail(LicenseStatus.Malformed);
            if (!TryGetString(env, "keyId", out var keyId) || !KeyIdPattern.IsMatch(keyId)) return Fail(LicenseStatus.Malformed);
            if (!TryGetString(env, "payload", out var payloadText) || payloadText.Length > LicenseLimits.MaxPayloadBase64Chars)
                return Fail(LicenseStatus.Malformed);
            if (!TryGetString(env, "signature", out var signatureText) || signatureText.Length > LicenseLimits.MaxSignatureBase64Chars)
                return Fail(LicenseStatus.Malformed);
            if (!TryDecode(payloadText, out var payloadBytes) || payloadBytes.Length > LicenseLimits.MaxPayloadBytes)
                return Fail(LicenseStatus.Malformed);
            if (!TryDecode(signatureText, out var signature)) return Fail(LicenseStatus.Malformed);

            if (!_keys.TryGetValue(keyId, out var spki)) return Fail(LicenseStatus.UnknownKeyId);
            if (!VerifySignature(spki, payloadBytes, signature)) return Fail(LicenseStatus.InvalidSignature);

            return ValidatePayload(payloadBytes);
        }
    }

    private LicenseVerificationResult ValidatePayload(byte[] bytes)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(bytes, JsonOptions); }
        catch (JsonException) { return Fail(LicenseStatus.InvalidPayload); }

        using (doc)
        {
            if (!TryReadProperties(doc.RootElement, PayloadFields, out var p)) return Fail(LicenseStatus.InvalidPayload);

            if (!TryGetInt(p, "schemaVersion", out var version) || version != LicenseLimits.SupportedSchemaVersion)
                return Fail(LicenseStatus.InvalidPayload);
            if (!TryGetBoundedString(p, "licenseId", LicenseLimits.MaxLicenseIdLength, out var licenseId)) return Fail(LicenseStatus.InvalidPayload);
            if (!TryGetBoundedString(p, "product", LicenseLimits.MaxProductLength, out var product)) return Fail(LicenseStatus.InvalidPayload);
            if (!TryGetBoundedString(p, "licensee", LicenseLimits.MaxLicenseeLength, out var licensee)) return Fail(LicenseStatus.InvalidPayload);
            if (!TryGetTimestamp(p, "issuedAt", out var issuedAt)) return Fail(LicenseStatus.InvalidPayload);
            if (!TryGetTimestamp(p, "notBefore", out var notBefore)) return Fail(LicenseStatus.InvalidPayload);

            DateTimeOffset? expiresAt = null;
            if (!p.TryGetValue("expiresAt", out var expiresElement)) return Fail(LicenseStatus.InvalidPayload);
            if (expiresElement.ValueKind != JsonValueKind.Null)
            {
                if (expiresElement.ValueKind != JsonValueKind.String
                    || !TryParseTimestamp(expiresElement.GetString() ?? "", out var parsed))
                    return Fail(LicenseStatus.InvalidPayload);
                expiresAt = parsed;
            }

            var graceDays = 0;
            if (p.ContainsKey("graceDays")
                && (!TryGetInt(p, "graceDays", out graceDays) || graceDays < 0 || graceDays > LicenseLimits.MaxGraceDays))
                return Fail(LicenseStatus.InvalidPayload);

            if (!TryGetInt(p, "maxInstallations", out var maxInstallations)
                || maxInstallations < 1 || maxInstallations > LicenseLimits.MaxInstallationsLimit)
                return Fail(LicenseStatus.InvalidPayload);

            if (expiresAt is { } e && e <= notBefore) return Fail(LicenseStatus.InvalidPayload);

            if (!string.Equals(product, LicenseLimits.ExpectedProduct, StringComparison.Ordinal))
                return Fail(LicenseStatus.WrongProduct);

            var now = _time.GetUtcNow();
            if (now < notBefore)
                return new LicenseVerificationResult(
                    LicenseStatus.NotYetValid,
                    new LicensePayload(licenseId, product, licensee, issuedAt, notBefore, expiresAt, graceDays, maxInstallations));

            var payload = new LicensePayload(licenseId, product, licensee, issuedAt, notBefore, expiresAt, graceDays, maxInstallations);

            if (expiresAt is null || now <= expiresAt.Value)
                return new LicenseVerificationResult(LicenseStatus.Valid, payload);
            if (now <= GraceEnd(expiresAt.Value, graceDays))
                return new LicenseVerificationResult(LicenseStatus.ValidInGrace, payload);
            return new LicenseVerificationResult(LicenseStatus.Expired, payload);
        }
    }

    internal static bool IsValidP256PublicKey(byte[] spki)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            if (ecdsa.KeySize != 256) return false;
            var curve = ecdsa.ExportParameters(false).Curve;
            return curve.IsNamed
                && (curve.Oid?.Value == P256Oid
                    || curve.Oid?.FriendlyName is "nistP256" or "ECDSA_P256");
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool VerifySignature(byte[] spki, byte[] payload, byte[] signature)
    {
        if (signature.Length != LicenseLimits.SignatureBytes) return false;
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            return ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static LicenseVerificationResult Fail(LicenseStatus status) => new(status, null);

    /// <summary>Last instant of the grace period. Clamps at the maximum date instead of overflowing.</summary>
    public static DateTimeOffset GraceEnd(DateTimeOffset expiresAt, int graceDays) =>
        expiresAt > DateTimeOffset.MaxValue.AddDays(-graceDays)
            ? DateTimeOffset.MaxValue
            : expiresAt.AddDays(graceDays);

    private static bool TryReadProperties(JsonElement root, string[] allowed, out Dictionary<string, JsonElement> props)
    {
        props = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in root.EnumerateObject())
        {
            if (Array.IndexOf(allowed, property.Name) < 0) return false;      // unknown property
            if (!props.TryAdd(property.Name, property.Value)) return false;   // duplicate property
        }
        return true;
    }

    private static bool TryGetString(Dictionary<string, JsonElement> p, string name, out string value)
    {
        value = "";
        if (!p.TryGetValue(name, out var e) || e.ValueKind != JsonValueKind.String) return false;
        value = e.GetString() ?? "";
        return true;
    }

    private static bool TryGetBoundedString(Dictionary<string, JsonElement> p, string name, int maxLength, out string value)
    {
        if (!TryGetString(p, name, out value)) return false;
        if (value.Length == 0 || value.Length > maxLength) return false;
        foreach (var ch in value)
            if (char.IsControl(ch)) return false;
        return true;
    }

    private static bool TryGetInt(Dictionary<string, JsonElement> p, string name, out int value)
    {
        value = 0;
        return p.TryGetValue(name, out var e) && e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out value);
    }

    private static bool TryGetTimestamp(Dictionary<string, JsonElement> p, string name, out DateTimeOffset value)
    {
        value = default;
        return TryGetString(p, name, out var text) && TryParseTimestamp(text, out value);
    }

    private static bool TryParseTimestamp(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(text, TimestampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);

    private static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length == 0) return false;
        try
        {
            bytes = Base64Url.DecodeFromChars(text);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}