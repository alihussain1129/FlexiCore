using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlexiCore.LicenseTool;

/// <summary>
/// Append-only record of what was issued (one JSON line each). There is no revocation, so this is the owner's only
/// answer to "what did I sign?". It holds customer names, so it must live outside every git working tree.
/// </summary>
public static class IssueLedger
{
    public static void Append(string path, LicenseRequest request, string keyId, byte[] licenseFile, DateTimeOffset recordedAt)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)!;
        OutputPathGuard.EnsureOutsideGitWorkingTree(directory, "The ledger folder");
        Directory.CreateDirectory(directory);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("recordedAt", LicenseIssuer.FormatTimestamp(recordedAt));
            writer.WriteString("licenseId", request.LicenseId);
            writer.WriteString("licensee", request.Licensee);
            writer.WriteString("keyId", keyId);
            writer.WriteString("notBefore", LicenseIssuer.FormatTimestamp(request.NotBefore));
            if (request.ExpiresAt is { } expires) writer.WriteString("expiresAt", LicenseIssuer.FormatTimestamp(expires));
            else writer.WriteNull("expiresAt");
            writer.WriteNumber("graceDays", request.GraceDays);
            writer.WriteNumber("maxInstallations", request.MaxInstallations);
            writer.WriteString("fileSha256", Convert.ToHexString(SHA256.HashData(licenseFile)).ToLowerInvariant());
            writer.WriteEndObject();
        }

        File.AppendAllText(full, Encoding.UTF8.GetString(stream.ToArray()) + Environment.NewLine, new UTF8Encoding(false));
    }
}