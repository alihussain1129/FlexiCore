using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace FlexiCore.Api.Licensing;

internal sealed class LocalActivationRequirement : IAuthorizationRequirement;

/// <summary>Succeeds for any authenticated user, or for an anonymous request that is provably local.</summary>
internal sealed class LocalActivationHandler(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration)
    : AuthorizationHandler<LocalActivationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, LocalActivationRequirement requirement)
    {
        var http = context.Resource as HttpContext ?? httpContextAccessor.HttpContext;
        if (context.User.Identity?.IsAuthenticated == true
            || (http is not null && LocalActivationAuthorization.IsLocalAnonymousRequest(http, configuration)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}

/// <summary>
/// Local-mode activation access. Registered only when Deployment:Mode is Local, and applied only to the license
/// status and import routes. Anonymous callers must come from loopback or an explicitly configured exact
/// local gateway IP, must have no forwarding headers, and must use a local Host header.
/// </summary>
internal static class LocalActivationAuthorization
{
    public const string PolicyName = "LocalActivation";

    private static readonly string[] ForwardingHeaders =
        ["Forwarded", "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto", "X-Real-IP"];

    public static void Register(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddAuthorization(options =>
            options.AddPolicy(PolicyName, policy => policy.AddRequirements(new LocalActivationRequirement())));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, LocalActivationHandler>());
    }

    public static bool IsLocalAnonymousRequest(HttpContext http, IConfiguration? configuration = null)
    {
        var address = http.Connection.RemoteIpAddress;
        if (address is null) return false;
        address = Normalize(address);

        var isLoopback = IPAddress.IsLoopback(address);
        var configuredAddresses = configuration?["License:LocalActivationAllowedRemoteAddresses"];
        var isExplicitlyAllowed = !string.IsNullOrWhiteSpace(configuredAddresses)
            && configuredAddresses
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => IPAddress.TryParse(value, out var allowed)
                    && Normalize(allowed).Equals(address));

        if (!isLoopback && !isExplicitlyAllowed) return false;

        foreach (var header in ForwardingHeaders)
            if (http.Request.Headers.ContainsKey(header)) return false;

        var host = http.Request.Host;
        if (!host.HasValue) return false;
        return string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.Host == "127.0.0.1"
            || host.Host == "[::1]";
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}