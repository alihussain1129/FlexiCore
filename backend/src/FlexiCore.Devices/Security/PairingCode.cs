using System.Security.Cryptography;
using System.Text;

namespace FlexiCore.Devices.Security;

/// <summary>
/// A pairing code is 10 characters from a 32-symbol alphabet without look-alikes (50 bits), shown as XXXXX-XXXXX.
/// It is single use, expires within minutes, is stored only as a hash, and is guarded by a rate limit.
/// </summary>
public static class PairingCode
{
    public const int Length = 10;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    public static string Format(string code) => code.Length == Length ? code[..5] + "-" + code[5..] : code;

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input) || input.Length > 32) return false;

        var builder = new StringBuilder(Length);
        foreach (var ch in input)
        {
            if (ch == '-' || char.IsWhiteSpace(ch)) continue;
            var upper = char.ToUpperInvariant(ch);
            if (Alphabet.IndexOf(upper) < 0) return false;
            builder.Append(upper);
        }

        if (builder.Length != Length) return false;
        normalized = builder.ToString();
        return true;
    }

    public static byte[] Hash(string normalizedCode) =>
        SHA256.HashData(Encoding.ASCII.GetBytes("FlexiCore.PairingCode.v1|" + normalizedCode));
}