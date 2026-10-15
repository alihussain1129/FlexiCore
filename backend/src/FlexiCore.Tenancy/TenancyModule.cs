using System.Threading.RateLimiting;
using FlexiCore.Tenancy.Data;
using FlexiCore.Tenancy.Endpoints;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Tenancy;

/// <summary>Entry points the host uses. Requires an NpgsqlDataSource to be registered by the host.</summary>
public static class TenancyModule
{
    public const string CreateRateLimitPolicy = "tenancy-create";

    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantConnectionFactory, TenantConnectionFactory>();
        services.TryAddSingleton<TenancyAuditWriter>();
        services.TryAddSingleton<TenancyStore>();

        // Adds a policy only. The 429 status code and the Retry-After header come from the global
        // rejection handler that the Identity module registers; this module must not replace it.
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var settings = TenancySettings.From(configuration);
            options.AddPolicy(CreateRateLimitPolicy, httpContext =>
            {
                // Per user when authenticated (needs the limiter to run after authentication), else per IP.
                var sub = httpContext.User.FindFirst("sub")?.Value;
                var key = !string.IsNullOrEmpty(sub)
                    ? "user:" + sub
                    : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.CreatePermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        TenancyEndpoints.Map(endpoints);
        return endpoints;
    }
}