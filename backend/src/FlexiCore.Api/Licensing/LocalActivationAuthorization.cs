using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.Api.Licensing;

internal sealed class LocalActivationRequirement : IAuthorizationRequirement;

/// <summary>Succeeds for any authenticated user, or for an anonymous request that is provably local.</summary>
internal sealed class LocalActivationHandler : AuthorizationHandler<LocalActivationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, LocalActivationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            || (context.Resource is HttpContext http && LocalActivationAuthorization.IsLocalAnonymousRequest(http)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Local-mode activation access. Registered only when Deployment:Mode is Local, and applied only to the license
/// status and import routes. An anonymous caller is accepted only when the connection itself is loopback, no
/// forwarding header is present, and the Host header names the local machine. A missing remote address is refused.
/// </summary>
internal static class LocalActivationAuthorization
{
    public const string PolicyName = "LocalActivation";

    private static readonly string[] ForwardingHeaders =
        ["Forwarded", "X-Forwarded-For", "X-Forwarded-Host", "X-Forwarded-Proto", "X-Real-IP"];

    public static void Register(IServiceCollection services)
    {
        services.AddAuthorization(options =>
            options.AddPolicy(PolicyName, policy => policy.AddRequirements(new LocalActivationRequirement())));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, LocalActivationHandler>());
    }

    public static bool IsLocalAnonymousRequest(HttpContext http)
    {
        var address = http.Connection.RemoteIpAddress;
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (!System.Net.IPAddress.IsLoopback(address)) return false;

        foreach (var header in ForwardingHeaders)
            if (http.Request.Headers.ContainsKey(header)) return false;

        var host = http.Request.Host;
        if (!host.HasValue) return false;
        return string.Equals(host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.Host == "127.0.0.1"
            || host.Host == "[::1]";
    }
}