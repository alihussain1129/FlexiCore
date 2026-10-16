using System.Text.Encodings.Web;
using System.Text.Json;
using FlexiCore.Licensing;

namespace FlexiCore.LicenseTool;

/// <summary>Writes and reads public key sets in exactly the format ProductionKeyLoader parses.</summary>
public static class PublicKeyExport
{
    public static byte[] Build(IReadOnlyList<TrustedKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count < 1 || keys.Count > LicenseLimits.MaxTrustedKeys)
            throw new LicenseToolException($"A key set must hold 1 to {LicenseLimits.MaxTrustedKeys} keys.");

        using var stream = new MemoryStream();
        var options = new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("keys");
            foreach (var key in keys)
            {
                writer.WriteStartObject();
                writer.WriteString("keyId", key.KeyId);
                writer.WriteString("publicKey", Convert.ToBase64String(key.SubjectPublicKeyInfo));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var bytes = stream.ToArray();

        // The production loader is the judge: if it does not accept this exact output, nothing is written.
        var parsed = ProductionKeyLoader.ParseKeySet(bytes);
        var matches = parsed.Count == keys.Count;
        for (var i = 0; matches && i < keys.Count; i++)
        {
            matches = parsed[i].KeyId == keys[i].KeyId
                      && parsed[i].SubjectPublicKeyInfo.AsSpan().SequenceEqual(keys[i].SubjectPublicKeyInfo);
        }
        if (!matches)
            throw new LicenseToolException("The generated key set did not pass the production key loader. Nothing was written.", 2);

        return bytes;
    }

    public static IReadOnlyList<TrustedKey> Read(string path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new LicenseToolException($"Key set file not found: {full}");
        var parsed = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(full));
        if (parsed.Count == 0)
            throw new LicenseToolException($"{full} is not a valid key set (expected a keys array of P-256 public keys).");
        return parsed;
    }
}