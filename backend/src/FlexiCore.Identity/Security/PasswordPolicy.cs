namespace FlexiCore.Identity.Security;

/// <summary>
/// Length 12-128 (counted in Unicode characters), no composition rules, and the password must not contain the email
/// local part. The local-part check applies from 4 characters up, because a 1-2 character local part would
/// otherwise reject most passwords.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;
    public const int MinLocalPartLengthForContainsCheck = 4;

    public static IReadOnlyList<string> Validate(string? password, string normalizedEmail)
    {
        var errors = new List<string>();
        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required.");
            return errors;
        }

        var length = password.EnumerateRunes().Count();
        if (length < MinLength) errors.Add($"Password must be at least {MinLength} characters.");
        if (length > MaxLength) errors.Add($"Password must be at most {MaxLength} characters.");

        var local = EmailRules.LocalPart(normalizedEmail);
        if (local.Length >= MinLocalPartLengthForContainsCheck &&
            password.Contains(local, StringComparison.OrdinalIgnoreCase))
            errors.Add("Password must not contain the local part of your email address.");

        return errors;
    }
}