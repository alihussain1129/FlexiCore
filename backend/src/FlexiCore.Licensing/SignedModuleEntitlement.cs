using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlexiCore.Licensing;

public sealed record ModuleGrant(
    string ModuleKey, string FeatureKey, bool Enabled,
    int? MaxUsers = null, int? MaxDevices = null, int? MaxCompanies = null, int? MaxBranches = null);

public sealed record ModuleEntitlementPayload(
    string LicenseId, Guid OrganizationId, string CustomerId, int Revision,
    DateTimeOffset IssuedAt, DateTimeOffset ValidFrom, DateTimeOffset? ValidUntil,
    IReadOnlyList<ModuleGrant> Modules);

public enum ModuleEntitlementError
{
    None, Malformed, UnsupportedVersion, UnknownKey, InvalidSignature, InvalidPayload,
    NotYetValid, Expired, NoTrustedKeys
}

public sealed record ModuleEntitlementVerification(ModuleEntitlementError Error, string? KeyId, ModuleEntitlementPayload? Payload)
{
    public bool IsValid => Error == ModuleEntitlementError.None && Payload is not null;
}

/// <summary>Verifies versioned ES256 entitlement artifacts. The signed payload bytes are the canonical claim source.</summary>
public sealed class SignedModuleEntitlementVerifier
{
    public const int MaxFileBytes = 256 * 1024;
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private static readonly Regex IdPattern = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex ModulePattern = new("^[a-z][a-z0-9_.-]{1,63}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex FeaturePattern = new("^(\\*|[a-z][a-z0-9_.-]{0,95})$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;

    public SignedModuleEntitlementVerifier(IEnumerable<TrustedKey> keys, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _time = time ?? throw new ArgumentNullException(nameof(time));
        foreach (var key in keys)
        {
            if (key is null || !LicenseVerifier.KeyIdPattern.IsMatch(key.KeyId ?? "")
                || !LicenseVerifier.IsValidP256PublicKey(key.SubjectPublicKeyInfo))
                throw new ArgumentException("Invalid entitlement trust key.", nameof(keys));
            if (!_keys.TryAdd(key.KeyId!, (byte[])key.SubjectPublicKeyInfo.Clone()))
                throw new ArgumentException("Duplicate entitlement trust key id.", nameof(keys));
        }
    }

    public ModuleEntitlementVerification Verify(ReadOnlyMemory<byte> file)
    {
        if (_keys.Count == 0) return new(ModuleEntitlementError.NoTrustedKeys, null, null);
        if (file.IsEmpty || file.Length > MaxFileBytes) return new(ModuleEntitlementError.Malformed, null, null);
        try
        {
            using var envelope = JsonDocument.Parse(file, new JsonDocumentOptions { MaxDepth = 12 });
            var root = envelope.RootElement;
            if (!ExactObject(root, "schemaVersion", "keyId", "payload", "signature"))
                return new(ModuleEntitlementError.Malformed, null, null);
            if (!root.GetProperty("schemaVersion").TryGetInt32(out var version) || version < 1)
                return new(ModuleEntitlementError.Malformed, null, null);
            if (version != 1) return new(ModuleEntitlementError.UnsupportedVersion, null, null);
            var keyId = String(root, "keyId", 64);
            var payloadText = String(root, "payload", 300_000);
            var signatureText = String(root, "signature", 128);
            if (keyId is null || payloadText is null || signatureText is null
                || !LicenseVerifier.KeyIdPattern.IsMatch(keyId))
                return new(ModuleEntitlementError.Malformed, null, null);
            if (!_keys.TryGetValue(keyId, out var spki)) return new(ModuleEntitlementError.UnknownKey, keyId, null);
            if (!Decode(payloadText, out var payloadBytes) || !Decode(signatureText, out var signature)
                || signature.Length != LicenseLimits.SignatureBytes)
                return new(ModuleEntitlementError.Malformed, keyId, null);
            using (var signer = ECDsa.Create())
            {
                signer.ImportSubjectPublicKeyInfo(spki, out _);
                if (!signer.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256,
                        DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                    return new(ModuleEntitlementError.InvalidSignature, keyId, null);
            }

            var parsed = ParsePayload(payloadBytes);
            if (parsed is null) return new(ModuleEntitlementError.InvalidPayload, keyId, null);
            var now = _time.GetUtcNow();
            if (now < parsed.ValidFrom || now < parsed.IssuedAt)
                return new(ModuleEntitlementError.NotYetValid, keyId, parsed);
            if (parsed.ValidUntil is { } until && now >= until)
                return new(ModuleEntitlementError.Expired, keyId, parsed);
            return new(ModuleEntitlementError.None, keyId, parsed);
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            return new(ModuleEntitlementError.Malformed, null, null);
        }
    }

    private static ModuleEntitlementPayload? ParsePayload(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 10 });
        var p = doc.RootElement;
        if (!ExactObject(p, "schemaVersion", "licenseId", "organizationId", "customerId", "revision", "issuedAt", "validFrom", "validUntil", "modules")
            || !p.GetProperty("schemaVersion").TryGetInt32(out var schema) || schema != 1
            || !p.GetProperty("revision").TryGetInt32(out var revision) || revision < 1)
            return null;
        var licenseId = String(p, "licenseId", LicenseLimits.MaxLicenseIdLength);
        var orgText = String(p, "organizationId", 36);
        var customerId = String(p, "customerId", 200);
        if (licenseId is null || !IdPattern.IsMatch(licenseId) || orgText is null
            || !Guid.TryParseExact(orgText, "D", out var organizationId) || organizationId == Guid.Empty
            || customerId is null || string.IsNullOrWhiteSpace(customerId) || customerId.Any(char.IsControl)
            || !Timestamp(p, "issuedAt", out var issuedAt) || !Timestamp(p, "validFrom", out var validFrom))
            return null;
        DateTimeOffset? validUntil = null;
        var until = p.GetProperty("validUntil");
        if (until.ValueKind != JsonValueKind.Null)
        {
            if (until.ValueKind != JsonValueKind.String || !TryTimestamp(until.GetString() ?? "", out var parsedUntil)) return null;
            validUntil = parsedUntil;
        }
        if (issuedAt > validFrom || (validUntil is { } expires && expires <= validFrom)) return null;
        var modulesJson = p.GetProperty("modules");
        if (modulesJson.ValueKind != JsonValueKind.Array || modulesJson.GetArrayLength() is 0 or > 256) return null;
        var modules = new List<ModuleGrant>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in modulesJson.EnumerateArray())
        {
            if (!ExactObject(item, "moduleKey", "featureKey", "enabled", "maxUsers", "maxDevices", "maxCompanies", "maxBranches")) return null;
            var module = String(item, "moduleKey", 64);
            var feature = String(item, "featureKey", 96);
            if (module is null || !ModulePattern.IsMatch(module) || feature is null || !FeaturePattern.IsMatch(feature)
                || item.GetProperty("enabled").ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !unique.Add(module + "\0" + feature)) return null;
            if (!Limit(item, "maxUsers", out var users) || !Limit(item, "maxDevices", out var devices)
                || !Limit(item, "maxCompanies", out var companies) || !Limit(item, "maxBranches", out var branches)) return null;
            modules.Add(new(module, feature, item.GetProperty("enabled").GetBoolean(), users, devices, companies, branches));
        }
        return new(licenseId, organizationId, customerId, revision, issuedAt, validFrom, validUntil, modules);
    }

    private static bool Limit(JsonElement p, string name, out int? value)
    {
        value = null;
        var item = p.GetProperty(name);
        if (item.ValueKind == JsonValueKind.Null) return true;
        if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var parsed) || parsed < 0) return false;
        value = parsed;
        return true;
    }

    private static bool ExactObject(JsonElement e, params string[] names)
    {
        if (e.ValueKind != JsonValueKind.Object) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in e.EnumerateObject()) if (!names.Contains(prop.Name, StringComparer.Ordinal) || !seen.Add(prop.Name)) return false;
        return seen.Count == names.Length;
    }
    private static string? String(JsonElement p, string name, int max) => p.TryGetProperty(name, out var e)
        && e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } value && value.Length <= max ? value : null;
    private static bool Timestamp(JsonElement p, string name, out DateTimeOffset value)
    {
        value = default;
        return p.TryGetProperty(name, out var e) && e.ValueKind == JsonValueKind.String
            && TryTimestamp(e.GetString() ?? "", out value);
    }
    private static bool TryTimestamp(string s, out DateTimeOffset value) => DateTimeOffset.TryParseExact(s, TimestampFormat,
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    private static bool Decode(string text, out byte[] bytes)
    {
        bytes = [];
        if (text.Length == 0 || text.Contains('=') || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return false;
        try
        {
            var b64 = text.Replace('-', '+').Replace('_', '/');
            b64 += new string('=', (4 - b64.Length % 4) % 4);
            bytes = Convert.FromBase64String(b64);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') == text;
        }
        catch (FormatException) { return false; }
    }
}
