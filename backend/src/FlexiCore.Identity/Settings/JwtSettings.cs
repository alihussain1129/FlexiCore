using Microsoft.Extensions.Configuration;

namespace FlexiCore.Identity.Settings;

/// <summary>Validated JWT settings. Loading throws (so the host does not start) if the signing key is missing or weak.</summary>
public sealed class JwtSettings
{
    public const string Section = "Jwt";
    public const int MinSigningKeyBytes = 32;
    private const int MinDistinctKeyBytes = 16;

    public string Issuer { get; init; } = "flexicore";
    public string Audience { get; init; } = "flexicore-api";
    public int AccessTokenMinutes { get; init; } = 15;
    public int ClockSkewSeconds { get; init; } = 30;
    public byte[] SigningKey { get; init; } = [];

    public static JwtSettings Load(IConfiguration configuration)
    {
        var section = configuration.GetSection(Section);

        var minutes = section.GetValue("AccessTokenMinutes", 15);
        if (minutes is < 1 or > 60)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 60.");
        var skew = section.GetValue("ClockSkewSeconds", 30);
        if (skew is < 0 or > 300)
            throw new InvalidOperationException("Jwt:ClockSkewSeconds must be between 0 and 300.");

        var keyText = section["SigningKey"];
        if (string.IsNullOrWhiteSpace(keyText))
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. In development use 'dotnet user-secrets'; elsewhere use the Jwt__SigningKey " +
                "environment variable. It must be 32 or more cryptographically random bytes, base64-encoded.");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(keyText);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be base64-encoded.");
        }

        if (key.Length < MinSigningKeyBytes)
            throw new InvalidOperationException("Jwt:SigningKey must decode to at least 32 bytes (256 bits).");
        if (key.Distinct().Count() < MinDistinctKeyBytes)
            throw new InvalidOperationException(
                "Jwt:SigningKey looks low-entropy (for example all zero bytes). Generate it with a cryptographic random number generator.");

        return new JwtSettings
        {
            Issuer = string.IsNullOrWhiteSpace(section["Issuer"]) ? "flexicore" : section["Issuer"]!,
            Audience = string.IsNullOrWhiteSpace(section["Audience"]) ? "flexicore-api" : section["Audience"]!,
            AccessTokenMinutes = minutes,
            ClockSkewSeconds = skew,
            SigningKey = key
        };
    }
}