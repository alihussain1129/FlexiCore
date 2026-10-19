using FlexiCore.Licensing;
using Microsoft.AspNetCore.Http;

namespace FlexiCore.Api.Licensing;

/// <summary>
/// Local mode only. Unless the installed license is Valid or ValidInGrace, every request outside a short allow-list
/// is answered with 403. Fail closed: Missing, Invalid, Expired, NotYetValid and Unavailable all block.
/// The license is re-evaluated on each request (a small file read and one signature check), so activation takes
/// effect immediately and expiry is never cached.
/// </summary>
internal sealed class LicenseEnforcementMiddleware(RequestDelegate next)
{
    private static readonly PathString[] Open =
    [
        new("/health"),
        new("/api/v1/auth/register"),
        new("/api/v1/auth/login"),
        new("/api/v1/auth/refresh"),
        new("/api/v1/auth/logout"),
        new("/api/v1/license"),
        new("/openapi"),
    ];
    private const string LocalSetupPath = "/api/v1/local/setup";
    // Anonymous by design; the endpoint itself requires a valid license, a one-time code and a rate limit.
    private const string DevicePairPath = "/api/v1/devices/pair";

    public async Task InvokeAsync(HttpContext context, LicenseService service)
    {
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsPost(context.Request.Method))
            && string.Equals(context.Request.Path.Value, LocalSetupPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method)
            && string.Equals(context.Request.Path.Value, DevicePairPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        foreach (var open in Open)
        {
            if (context.Request.Path.StartsWithSegments(open, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var report = await service.GetStatusAsync(context.RequestAborted);
            if (report.State is LicenseState.Valid or LicenseState.ValidInGrace)
            {
                await next(context);
                return;
            }

            var name = report.State.ToString();
            context.Response.Headers.CacheControl = "no-store";
            await Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "A valid license is required",
                type: "urn:flexicore:problem:license-required",
                extensions: new Dictionary<string, object?>
                {
                    ["state"] = char.ToLowerInvariant(name[0]) + name[1..],
                    ["reason"] = report.Reason,
                }).ExecuteAsync(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
    }
}