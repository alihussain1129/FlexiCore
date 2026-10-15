using Microsoft.Extensions.Configuration;

namespace FlexiCore.Tenancy;

/// <summary>Tenancy configuration, validated when it is read so a bad value fails at startup.</summary>
public sealed class TenancySettings
{
    public const int DefaultCreatePermitPerMinute = 10;

    /// <summary>Organization and company creations allowed per user per minute.</summary>
    public int CreatePermitPerMinute { get; init; } = DefaultCreatePermitPerMinute;

    public static TenancySettings From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var permit = configuration.GetValue("Tenancy:RateLimit:CreatePermitPerMinute", DefaultCreatePermitPerMinute);
        if (permit is < 1 or > 1_000_000)
        {
            throw new InvalidOperationException("Tenancy:RateLimit:CreatePermitPerMinute must be between 1 and 1000000.");
        }

        return new TenancySettings { CreatePermitPerMinute = permit };
    }
}