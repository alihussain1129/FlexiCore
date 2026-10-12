namespace FlexiCore.Identity.Security;

public static class EmailRules
{
    public const int MaxLength = 254;
    private const int MaxLocalPartLength = 64;

    /// <summary>Application-side normalization: trimmed and invariant-lowercased. This is what uniqueness is enforced on.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public static string LocalPart(string normalizedEmail)
    {
        var at = normalizedEmail.LastIndexOf('@');
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var value = email.Trim();
        if (value.Length > MaxLength) return false;

        var at = value.IndexOf('@');
        if (at < 1 || at != value.LastIndexOf('@') || at == value.Length - 1) return false;

        var local = value[..at];
        var domain = value[(at + 1)..];
        if (local.Length > MaxLocalPartLength) return false;
        if (value.Any(IsForbidden)) return false;
        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..")) return false;

        var labels = domain.Split('.');
        return labels.Length >= 2 && labels.All(IsValidDomainLabel);
    }

    private static bool IsForbidden(char c) =>
        char.IsWhiteSpace(c) || char.IsControl(c) || c is '"' or '<' or '>' or ',' or ';' or '(' or ')' or '\\' or '[' or ']' or ':';

    private static bool IsValidDomainLabel(string label) =>
        label.Length is >= 1 and <= 63 &&
        !label.StartsWith('-') && !label.EndsWith('-') &&
        label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}