namespace FlexiCore.LicenseTool;

public static class PassphrasePolicy
{
    public const int MinLength = 16;

    /// <summary>Returns a problem description, or null when the passphrase is acceptable.</summary>
    public static string? Validate(string? passphrase)
    {
        if (string.IsNullOrEmpty(passphrase)) return "A passphrase is required.";
        if (passphrase.Length < MinLength) return $"The passphrase must be at least {MinLength} characters.";
        if (passphrase.Distinct().Count() < 6) return "The passphrase is too repetitive.";
        return null;
    }
}