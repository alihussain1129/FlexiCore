using System.Reflection;
using System.Text.Json;

namespace FlexiCore.Licensing;

/// <summary>
/// Loads the production public keys embedded in this assembly. Any problem (no resource, oversized,
/// malformed, bad key, duplicate id, too many keys) yields an empty list, so verification fails closed.
/// This library embeds no private key and no test key.
/// </summary>
public static class ProductionKeyLoader
{
    public const string ResourceFileName = "production-public-keys.json";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        MaxDepth = LicenseLimits.MaxJsonDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    public static IReadOnlyList<TrustedKey> Load() => Load(typeof(ProductionKeyLoader).Assembly);

    public static IReadOnlyList<TrustedKey> Load(Assembly assembly)
    {
        try
        {
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(ResourceFileName, StringComparison.Ordinal));
            if (name is null) return [];

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null || stream.Length == 0 || stream.Length > LicenseLimits.MaxKeySetBytes) return [];

            var buffer = new byte[stream.Length];
            stream.ReadExactly(buffer);
            return ParseKeySet(buffer);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [];
        }
    }

    /// <summary>Format: {"keys":[{"keyId":"...","publicKey":"&lt;standard base64 of DER SubjectPublicKeyInfo&gt;"}]}</summary>
    public static IReadOnlyList<TrustedKey> ParseKeySet(ReadOnlyMemory<byte> json)
    {
        if (json.IsEmpty || json.Length > LicenseLimits.MaxKeySetBytes) return [];
        try
        {
            using var doc = JsonDocument.Parse(json, JsonOptions);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return [];

            JsonElement keysElement = default;
            var seenKeys = false;
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "keys" || seenKeys) return [];
                seenKeys = true;
                keysElement = property.Value;
            }
            if (!seenKeys || keysElement.ValueKind != JsonValueKind.Array) return [];

            var count = keysElement.GetArrayLength();
            if (count < 1 || count > LicenseLimits.MaxTrustedKeys) return [];

            var result = new List<TrustedKey>(count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in keysElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) return [];
                string? keyId = null, publicKey = null;
                foreach (var property in item.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.String) return [];
                    switch (property.Name)
                    {
                        case "keyId" when keyId is null: keyId = property.Value.GetString(); break;
                        case "publicKey" when publicKey is null: publicKey = property.Value.GetString(); break;
                        default: return [];
                    }
                }
                if (keyId is null || publicKey is null) return [];
                if (!LicenseVerifier.KeyIdPattern.IsMatch(keyId) || !ids.Add(keyId)) return [];

                var spki = Convert.FromBase64String(publicKey);
                if (!LicenseVerifier.IsValidP256PublicKey(spki)) return [];
                result.Add(new TrustedKey(keyId, spki));
            }
            return result;
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return [];
        }
    }
}