using System.Text.RegularExpressions;

namespace FlexiCore.Identity.Security;

/// <summary>
/// Username policy for local sign-in: 3-32 characters, letters, digits, '.', '_' or '-',
/// starting with a letter or digit. Case-insensitive; no '@', so it can never be confused with an email.
/// </summary>
public static partial class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NormalizedPattern();

    public static string Normalize(string username) =>
        username.Trim().ToLowerInvariant();

    public static bool IsValid(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;
        var normalized = Normalize(username);
        return normalized.Length is >= MinLength and <= MaxLength
            && NormalizedPattern().IsMatch(normalized);
    }
}