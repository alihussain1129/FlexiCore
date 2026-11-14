using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlexiCore.Licensing;

public sealed record RevokedLicense(string LicenseId, DateTimeOffset RevokedAt, string Reason);

public sealed record SignedRevocationList(
    string ListId,
    DateTimeOffset IssuedAt,
    DateTimeOffset NextUpdate,
    IReadOnlyList<RevokedLicense> RevokedLicenses);

public enum RevocationListError
{
    None,
    Malformed,
    UnsupportedVersion,
    UnknownKey,
    InvalidSignature,
    InvalidPayload,
    NoTrustedKeys,
}

public sealed record RevocationListVerification(RevocationListError Error, SignedRevocationList? List)
{
    public bool IsValid => Error == RevocationListError.None && List is not null;
}

/// <summary>Verifies versioned ES256 signed revocation snapshots using the same configured trust set as licenses.</summary>
public sealed class SignedRevocationListVerifier
{
    public const int MaxFileBytes = 1024 * 1024;
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private static readonly Regex IdPattern = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);

    public SignedRevocationListVerifier(IEnumerable<TrustedKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            if (key is null || !LicenseVerifier.KeyIdPattern.IsMatch(key.KeyId ?? "")
                || !LicenseVerifier.IsValidP256PublicKey(key.SubjectPublicKeyInfo))
                throw new ArgumentException("Invalid revocation trust key.", nameof(keys));
            if (!_keys.TryAdd(key.KeyId!, (byte[])key.SubjectPublicKeyInfo.Clone()))
                throw new ArgumentException("Duplicate revocation trust key id.", nameof(keys));
        }
    }

    public RevocationListVerification Verify(ReadOnlyMemory<byte> file)
    {
        if (_keys.Count == 0) return new(RevocationListError.NoTrustedKeys, null);
        if (file.Length is 0 or > MaxFileBytes) return new(RevocationListError.Malformed, null);
        try
        {
            using var doc = JsonDocument.Parse(file, new JsonDocumentOptions { MaxDepth = 8 });
            var root = doc.RootElement;
            if (!ExactObject(root, "schemaVersion", "keyId", "payload", "signature"))
                return new(RevocationListError.Malformed, null);
            if (!root.GetProperty("schemaVersion").TryGetInt32(out var schema) || schema < 1)
                return new(RevocationListError.Malformed, null);
            if (schema != 1) return new(RevocationListError.UnsupportedVersion, null);
            var keyId = RequiredString(root, "keyId", 64);
            var payloadText = RequiredString(root, "payload", 1_300_000);
            var signatureText = RequiredString(root, "signature", 128);
            if (keyId is null || payloadText is null || signatureText is null)
                return new(RevocationListError.Malformed, null);
            if (!_keys.TryGetValue(keyId, out var spki)) return new(RevocationListError.UnknownKey, null);
            if (!TryBase64Url(payloadText, out var payload) || !TryBase64Url(signatureText, out var signature)
                || signature.Length != LicenseLimits.SignatureBytes)
                return new(RevocationListError.Malformed, null);
            using (var ecdsa = ECDsa.Create())
            {
                ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
                if (!ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                    return new(RevocationListError.InvalidSignature, null);
            }

            using var body = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 8 });
            var p = body.RootElement;
            if (!ExactObject(p, "schemaVersion", "listId", "issuedAt", "nextUpdate", "revokedLicenses"))
                return new(RevocationListError.InvalidPayload, null);
            if (!p.GetProperty("schemaVersion").TryGetInt32(out var payloadSchema) || payloadSchema != 1)
                return new(RevocationListError.InvalidPayload, null);
            var listId = RequiredString(p, "listId", 64);
            if (listId is null || !Guid.TryParseExact(listId, "N", out _))
                return new(RevocationListError.InvalidPayload, null);
            if (!TryTime(p, "issuedAt", out var issuedAt) || !TryTime(p, "nextUpdate", out var nextUpdate)
                || nextUpdate <= issuedAt || nextUpdate - issuedAt > TimeSpan.FromDays(31))
                return new(RevocationListError.InvalidPayload, null);
            var entriesJson = p.GetProperty("revokedLicenses");
            if (entriesJson.ValueKind != JsonValueKind.Array || entriesJson.GetArrayLength() > 20_000)
                return new(RevocationListError.InvalidPayload, null);
            var entries = new List<RevokedLicense>(entriesJson.GetArrayLength());
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entriesJson.EnumerateArray())
            {
                if (!ExactObject(entry, "licenseId", "revokedAt", "reason"))
                    return new(RevocationListError.InvalidPayload, null);
                var id = RequiredString(entry, "licenseId", LicenseLimits.MaxLicenseIdLength);
                var reason = RequiredString(entry, "reason", 500);
                if (id is null || !IdPattern.IsMatch(id) || reason is null || string.IsNullOrWhiteSpace(reason)
                    || !ids.Add(id) || !TryTime(entry, "revokedAt", out var revokedAt) || revokedAt > issuedAt)
                    return new(RevocationListError.InvalidPayload, null);
                entries.Add(new RevokedLicense(id, revokedAt, reason));
            }
            return new(RevocationListError.None, new SignedRevocationList(listId, issuedAt, nextUpdate, entries));
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            return new(RevocationListError.Malformed, null);
        }
    }

    private static bool ExactObject(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) return false;
        return seen.Count == names.Length;
    }

    private static string? RequiredString(JsonElement element, string name, int max)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var result = value.GetString();
        return !string.IsNullOrEmpty(result) && result.Length <= max ? result : null;
    }

    private static bool TryTime(JsonElement element, string name, out DateTimeOffset value)
    {
        value = default;
        return element.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParseExact(item.GetString(), TimestampFormat, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out value);
    }

    private static bool TryBase64Url(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Contains('=') || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return false;
        try
        {
            var base64 = text.Replace('-', '+').Replace('_', '/');
            base64 += new string('=', (4 - base64.Length % 4) % 4);
            bytes = Convert.FromBase64String(base64);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') == text;
        }
        catch (FormatException) { return false; }
    }
}
