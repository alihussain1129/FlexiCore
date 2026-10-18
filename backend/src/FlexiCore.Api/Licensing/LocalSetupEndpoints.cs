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

/// <summary>First-owner setup body. Send <c>username</c> (local accounts); <c>email</c> is the legacy path.</summary>
internal sealed record LocalSetupRequest(string? Username, string? Email, string? Password);
internal sealed record LocalSetupResponse(Guid Id, string? Username, string? Email);

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
        LocalSetupRequest request, HttpContext http, LicenseService licenses,
        IPasswordService passwords, UserStore users, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!LocalActivationAuthorization.IsLocalAnonymousRequest(http))
            return LocalOnlyProblem();

        var license = await licenses.GetStatusAsync(ct);
        if (license.State is not (LicenseState.Valid or LicenseState.ValidInGrace))
            return LicenseRequiredProblem();

        var correlationId = http.Items.TryGetValue(CorrelationContext.ItemKey, out var correlation)
            ? correlation as string
            : null;

        var errors = new Dictionary<string, string[]>();
        var username = request.Username?.Trim();
        var email = request.Email?.Trim();
        Guid? userId;

        if (!string.IsNullOrWhiteSpace(username))
        {
            email = null;
            if (!UsernameRules.IsValid(username))
                errors["username"] = [$"Username must be {UsernameRules.MinLength}-{UsernameRules.MaxLength} characters: letters, digits, '.', '_' or '-'."];

            var normalizedUsername = UsernameRules.Normalize(username);
            var passwordErrors = PasswordPolicy.Validate(request.Password, normalizedUsername);
            if (passwordErrors.Count > 0) errors["password"] = [.. passwordErrors];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            userId = await users.TryCreateFirstWithUsernameAsync(
                username, normalizedUsername, passwords.Hash(request.Password!), correlationId, ct);
        }
        else
        {
            username = null;
            if (!EmailRules.IsValid(email)) errors["email"] = ["A valid email address is required."];

            var normalized = email is null ? string.Empty : EmailRules.Normalize(email);
            var passwordErrors = PasswordPolicy.Validate(request.Password, normalized);
            if (passwordErrors.Count > 0) errors["password"] = [.. passwordErrors];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            userId = await users.TryCreateFirstAsync(
                email!, normalized, passwords.Hash(request.Password!), correlationId, ct);
        }

        if (userId is null)
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Local owner setup is already complete",
                type: "urn:flexicore:problem:local-setup-complete");

        return Results.Json(new LocalSetupResponse(userId.Value, username, email), statusCode: StatusCodes.Status201Created);
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