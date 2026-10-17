using Microsoft.Extensions.Configuration;

namespace FlexiCore.Identity.Settings;

public sealed class IdentitySettings
{
    public const int MinPbkdf2Iterations = 100_000;
    public const int DefaultPbkdf2Iterations = 210_000;

    /// <summary>Absolute lifetime of a refresh-token family, from login to forced re-login. Fixed, not configurable.</summary>
    public const int FamilyLifetimeDays = 90;
    public const int DefaultRefreshTokenDays = 30;
    public const int DefaultLockoutMaxFailedAttempts = 5;
    public const int DefaultLockoutMinutes = 15;
    public const int DefaultRateLimitPermitPerMinute = 20;

    public int Pbkdf2Iterations { get; init; } = DefaultPbkdf2Iterations;
    public bool RegistrationEnabled { get; init; }
    public int RefreshTokenDays { get; init; } = DefaultRefreshTokenDays;
    public int LockoutMaxFailedAttempts { get; init; } = DefaultLockoutMaxFailedAttempts;
    public int LockoutMinutes { get; init; } = DefaultLockoutMinutes;
    public int RateLimitPermitPerMinute { get; init; } = DefaultRateLimitPermitPerMinute;

    /// <summary>Registration defaults to enabled in Development only; any other environment must opt in explicitly.</summary>
    public static IdentitySettings Load(IConfiguration configuration, bool isDevelopment)
    {
        var iterations = configuration.GetValue("Identity:Pbkdf2Iterations", DefaultPbkdf2Iterations);
        if (iterations < MinPbkdf2Iterations)
            throw new InvalidOperationException($"Identity:Pbkdf2Iterations must be at least {MinPbkdf2Iterations}.");

        var refreshDays = configuration.GetValue("Identity:RefreshTokenDays", DefaultRefreshTokenDays);
        if (refreshDays is < 1 or > FamilyLifetimeDays)
            throw new InvalidOperationException($"Identity:RefreshTokenDays must be between 1 and {FamilyLifetimeDays}.");

        var maxAttempts = configuration.GetValue("Identity:Lockout:MaxFailedAttempts", DefaultLockoutMaxFailedAttempts);
        if (maxAttempts is < 3 or > 20)
            throw new InvalidOperationException("Identity:Lockout:MaxFailedAttempts must be between 3 and 20.");

        var lockMinutes = configuration.GetValue("Identity:Lockout:Minutes", DefaultLockoutMinutes);
        if (lockMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Identity:Lockout:Minutes must be between 1 and 1440.");

        var permit = configuration.GetValue("Identity:RateLimit:PermitPerMinute", DefaultRateLimitPermitPerMinute);
        if (permit is < 1 or > 1_000_000)
            throw new InvalidOperationException("Identity:RateLimit:PermitPerMinute must be between 1 and 1000000.");

        var isLocalDeployment = string.Equals(
            configuration["Deployment:Mode"], "Local", StringComparison.OrdinalIgnoreCase);
        var enabled = !isLocalDeployment
            && (configuration.GetValue<bool?>("Registration:Enabled") ?? isDevelopment);
        return new IdentitySettings
        {
            Pbkdf2Iterations = iterations,
            RegistrationEnabled = enabled,
            RefreshTokenDays = refreshDays,
            LockoutMaxFailedAttempts = maxAttempts,
            LockoutMinutes = lockMinutes,
            RateLimitPermitPerMinute = permit
        };
    }
}