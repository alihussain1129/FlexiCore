using FlexiCore.Identity;
using FlexiCore.Identity.Data;
using FlexiCore.Identity.Endpoints;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using FlexiCore.Licensing;
using FlexiCore.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Api.Licensing;

internal static class LocalSetupEndpoints
{
    private const string SetupUrl = "/api/v1/local/setup";

    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints, DeploymentSettings settings)
    {
        if (settings.Mode != DeploymentMode.Local) return endpoints;

        endpoints.MapGet(SetupUrl, StatusAsync)
            .RequireRateLimiting(IdentityModule.RateLimitPolicy);
        endpoints.MapPost(SetupUrl, CreateFirstOwnerAsync)
            .RequireRateLimiting(IdentityModule.RateLimitPolicy);
        return endpoints;
    }

    private static async Task<IResult> StatusAsync(
        HttpContext http, LicenseService licenses, UserStore users, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!LocalActivationAuthorization.IsLocalAnonymousRequest(http))
            return LocalOnlyProblem();

        var license = await licenses.GetStatusAsync(ct);
        if (license.State is not (LicenseState.Valid or LicenseState.ValidInGrace))
            return LicenseRequiredProblem();

        return Results.Ok(new { setupRequired = !await users.HasAnyUsersAsync(ct) });
    }

    private static async Task<IResult> CreateFirstOwnerAsync(
        RegisterRequest request, HttpContext http, LicenseService licenses,
        IPasswordService passwords, UserStore users, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!LocalActivationAuthorization.IsLocalAnonymousRequest(http))
            return LocalOnlyProblem();

        var license = await licenses.GetStatusAsync(ct);
        if (license.State is not (LicenseState.Valid or LicenseState.ValidInGrace))
            return LicenseRequiredProblem();

        var email = request.Email?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (!EmailRules.IsValid(email)) errors["email"] = ["A valid email address is required."];

        var normalized = email is null ? string.Empty : EmailRules.Normalize(email);
        var passwordErrors = PasswordPolicy.Validate(request.Password, normalized);
        if (passwordErrors.Count > 0) errors["password"] = [.. passwordErrors];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var userId = await users.TryCreateFirstAsync(
            email!, normalized, passwords.Hash(request.Password!),
            http.Items.TryGetValue(CorrelationContext.ItemKey, out var correlationId)
                ? correlationId as string
                : null,
            ct);
        if (userId is null)
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Local owner setup is already complete",
                type: "urn:flexicore:problem:local-setup-complete");

        return Results.Json(new RegisterResponse(userId.Value, email!), statusCode: StatusCodes.Status201Created);
    }

    private static IResult LocalOnlyProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Local setup is only available on this computer",
            type: "urn:flexicore:problem:local-setup-local-only");

    private static IResult LicenseRequiredProblem() =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "A valid license is required before local setup",
            type: "urn:flexicore:problem:license-required");
}
