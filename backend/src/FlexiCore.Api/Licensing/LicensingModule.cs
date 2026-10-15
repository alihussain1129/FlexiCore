using System.Threading.RateLimiting;
using FlexiCore.Licensing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FlexiCore.Api.Licensing;

/// <summary>
/// Hosts the license status and import endpoints. In Hosted mode only a "not applicable" status route exists and no
/// license file is read. In Local mode the verifier is built from the production keys embedded in the Licensing
/// assembly only; there is no configuration or environment override for keys.
/// </summary>
public static class LicensingModule
{
    public const string ImportRateLimitPolicy = "license-import";
    private const int DefaultImportPermitPerMinute = 10;

    public static IServiceCollection AddLicensingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = DeploymentSettings.From(configuration);
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);

        if (settings.Mode != DeploymentMode.Local) return services;

        var permit = DefaultImportPermitPerMinute;
        var rawPermit = configuration["License:ImportPermitPerMinute"];
        if (!string.IsNullOrEmpty(rawPermit) && (!int.TryParse(rawPermit, out permit) || permit < 1 || permit > 1000))
            throw new InvalidOperationException("License:ImportPermitPerMinute must be an integer from 1 to 1000.");

        services.AddSingleton<ILicenseStore>(_ => new FileLicenseStore(settings.LicenseDirectory!));
        services.AddSingleton(sp => new LicenseVerifier(ProductionKeyLoader.Load(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<LicenseService>();

        // Adds a policy only; the 429 handler is the global one registered by the Identity module.
        services.AddOptions<RateLimiterOptions>().Configure(options =>
        {
            options.AddPolicy(ImportRateLimitPolicy, httpContext =>
            {
                var sub = httpContext.User.FindFirst("sub")?.Value;
                var key = !string.IsNullOrEmpty(sub)
                    ? "user:" + sub
                    : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }

    public static IEndpointRouteBuilder MapLicenseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.ServiceProvider;
        var settings = services.GetRequiredService<DeploymentSettings>();

        if (settings.Mode == DeploymentMode.Local)
        {
            var verifier = services.GetRequiredService<LicenseVerifier>();
            if (verifier.TrustedKeyCount == 0)
            {
                services.GetRequiredService<ILoggerFactory>().CreateLogger("FlexiCore.Licensing").LogWarning(
                    "No trusted production license keys are embedded in this build. License status will report 'unavailable' and every import will be refused.");
            }
        }

        LicenseEndpoints.Map(endpoints, settings);
        return endpoints;
    }
}