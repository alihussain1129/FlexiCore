namespace FlexiCore.Tenancy;

/// <summary>
/// Email normalization for membership requests. Tenancy must not reference Identity, so this repeats
/// Identity's EmailRules.Normalize (trim, then invariant lowercase). A unit test pins the two together.
/// </summary>
public static class MemberEmail
{
    public static string? Normalize(string? email) => email?.Trim().ToLowerInvariant();
}