using System.Threading.RateLimiting;
using FlexiCore.Devices.Data;
using FlexiCore.Devices.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Devices;

/// <summary>
/// Entry points the host uses (Local mode only). Requires an NpgsqlDataSource and an IDeviceActivationGate
/// to be registered by the host. Devices never depends on Identity, Tenancy or Licensing.
/// </summary>
public static class DevicesModule
{
    public const string AuthenticationScheme = "Device";
    public const string SelectorScheme = "FlexiCoreSelector";
    public const string AnyDevicePolicy = "device";
    public const string OwnerDevicePolicy = "device-owner";
    public const string PairRateLimitPolicy = "devices-pair";

    public static readonly TimeSpan AdminPairingCodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DevicePairingCodeLifetime = TimeSpan.FromMinutes(10);

    private const string BearerScheme = "Bearer"; // the JWT scheme registered by Identity
    private const int PairPermitPerMinute = 10;

    public static IServiceCollection AddDevicesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DeviceAuditWriter>();
        services.AddSingleton<DeviceStore>();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, DeviceAuthenticationHandler>(AuthenticationScheme, null)
            .AddPolicyScheme(SelectorScheme, null, options =>
            {
                options.ForwardDefaultSelector = context =>
                    DeviceSecret.IsDeviceHeader(context.Request.Headers.Authorization.ToString())
                        ? AuthenticationScheme
                        : BearerScheme;
            });

        // Identity registers JWT as the default scheme; after all registrations, route the default through the selector.
        services.PostConfigure<AuthenticationOptions>(options => options.DefaultScheme = SelectorScheme);

        services.AddAuthorizationBuilder()
            .AddPolicy(AnyDevicePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(DeviceClaims.ActorKind, DeviceClaims.ActorKindDevice))
            .AddPolicy(OwnerDevicePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(DeviceClaims.ActorKind, DeviceClaims.ActorKindDevice)
                .RequireClaim(DeviceClaims.DeviceOwner, "true"));

        // Rejection status and Retry-After handling come from the limiter options Identity already configured.
        services.Configure<RateLimiterOptions>(options =>
            options.AddPolicy(PairRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = PairPermitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })));

        return services;
    }
}