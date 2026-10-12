using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace FlexiCore.Identity.Security;

/// <summary>
/// Refresh tokens are 32 random bytes, base64url-encoded (43 characters). Only the SHA-256 of the token string is stored.
/// A plain hash is appropriate here because the token is a 256-bit random secret, so it cannot be guessed or brute-forced.
/// </summary>
public static class RefreshTokenCodec
{
    public const int TokenBytes = 32;
    public const int EncodedLength = 43;

    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));

    /// <summary>True only for the canonical encoding of 32 bytes: exactly 43 url-safe characters, last character canonical.</summary>
    public static bool IsWellFormed([NotNullWhen(true)] string? token)
    {
        if (token is null || token.Length != EncodedLength) return false;

        for (var i = 0; i < token.Length; i++)
        {
            var value = Value(token[i]);
            if (value < 0) return false;
            // 43 characters carry 258 bits for 256 bits of data, so the last character's low two bits must be zero.
            if (i == token.Length - 1 && (value & 0b11) != 0) return false;
        }

        return true;
    }

    private static int Value(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '-' => 62,
        '_' => 63,
        _ => -1
    };
}