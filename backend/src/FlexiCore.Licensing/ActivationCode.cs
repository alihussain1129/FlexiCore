using System.Text;

namespace FlexiCore.Licensing;

/// <summary>
/// An activation code is the exact signed license file, base32-encoded for typing and pasting.
/// It is decoded back to the file and then verified like any file; it is not a second security system.
/// </summary>
public static class ActivationCode
{
    public const string Prefix = "FCL1";
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int MaxChars = 65536;

    public static string Encode(ReadOnlySpan<byte> file)
    {
        var body = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in file)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                body.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0) body.Append(Alphabet[(buffer << (5 - bits)) & 31]);

        var sb = new StringBuilder(Prefix);
        for (var i = 0; i < body.Length; i += 5)
            sb.Append('-').Append(body.ToString(i, Math.Min(5, body.Length - i)));
        return sb.ToString();
    }

    public static bool LooksLikeCode(ReadOnlySpan<byte> input)
    {
        var i = 0;
        while (i < input.Length && (input[i] == (byte)' ' || input[i] == (byte)'\t' || input[i] == (byte)'\r' || input[i] == (byte)'\n')) i++;
        return input.Length - i >= 4
               && (input[i] | 0x20) == 'f' && (input[i + 1] | 0x20) == 'c'
               && (input[i + 2] | 0x20) == 'l' && input[i + 3] == (byte)'1';
    }

    public static bool TryDecode(ReadOnlySpan<byte> input, out byte[] file)
    {
        file = [];
        if (input.Length > MaxChars) return false;
        var clean = new StringBuilder();
        foreach (var c in Encoding.UTF8.GetString(input))
            if (!char.IsWhiteSpace(c) && c != '-') clean.Append(char.ToUpperInvariant(c));
        var text = clean.ToString();
        if (!text.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var output = new List<byte>();
        int buffer = 0, bits = 0;
        for (var i = Prefix.Length; i < text.Length; i++)
        {
            var v = Alphabet.IndexOf(text[i]);
            if (v < 0) return false;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
                buffer &= (1 << bits) - 1;
            }
        }
        if (output.Count == 0) return false;
        file = output.ToArray();
        return true;
    }
}