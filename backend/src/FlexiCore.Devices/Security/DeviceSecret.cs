using System.Security.Cryptography;
using System.Text;

namespace FlexiCore.Devices.Security;

/// <summary>
/// A device secret is 32 random bytes (256 bits), shown to the device once. Only its SHA-256 hash is stored; a slow
/// password hash is unnecessary for a high-entropy random secret. The credential on the wire is
/// "Authorization: Device &lt;deviceId as 32 hex&gt;.&lt;43 base64url characters&gt;".
/// </summary>
public static class DeviceSecret
{
    public const int SecretByteLength = 32;
    public const int SecretTextLength = 43;
    public const string AuthorizationScheme = "Device";

    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(SecretByteLength))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string secret) =>
        SHA256.HashData(Encoding.ASCII.GetBytes("FlexiCore.DeviceSecret.v1|" + secret));

    public static bool Verify(string secret, byte[] expectedHash) =>
        expectedHash.Length == SecretByteLength && CryptographicOperations.FixedTimeEquals(Hash(secret), expectedHash);

    public static string FormatCredential(Guid deviceId, string secret) => deviceId.ToString("N") + "." + secret;

    public static bool IsDeviceHeader(string? header) =>
        !string.IsNullOrEmpty(header) &&
        header.StartsWith(AuthorizationScheme + " ", StringComparison.OrdinalIgnoreCase);

    public static bool TryParseAuthorizationHeader(string? header, out Guid deviceId, out string secret)
    {
        deviceId = Guid.Empty;
        secret = "";

        if (string.IsNullOrEmpty(header) || header.Length > 200 || !IsDeviceHeader(header)) return false;

        var credential = header[(AuthorizationScheme.Length + 1)..].Trim();
        if (credential.Length != 32 + 1 + SecretTextLength || credential[32] != '.') return false;

        if (!Guid.TryParseExact(credential[..32], "N", out var id) || id == Guid.Empty) return false;

        var text = credential[33..];
        foreach (var c in text)
        {
            if (!IsBase64Url(c)) return false;
        }

        deviceId = id;
        secret = text;
        return true;
    }

    private static bool IsBase64Url(char c) =>
        c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
}