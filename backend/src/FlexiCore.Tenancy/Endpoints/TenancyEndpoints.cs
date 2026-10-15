using System.Security.Claims;
using System.Text.RegularExpressions;
using FlexiCore.SharedKernel;
using FlexiCore.Tenancy.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Tenancy.Endpoints;

public sealed record CreateOrganizationRequest(string? Name, string? Slug);
public sealed record CreateCompanyRequest(string? Name, string? Slug, string? IndustryKey);
public sealed record AddMemberRequest(string? Email, string? Role);
public sealed record ChangeRoleRequest(string? Role);

internal static class TenancyEndpoints
{
    // These mirror the CHECK constraints in migration 0004 so callers get a clear 400 instead of a constraint error.
    private static readonly Regex SlugPattern =
        new(@"^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]\z", RegexOptions.CultureInvariant);

    private static readonly Regex IndustryKeyPattern =
        new(@"^[a-z][a-z0-9_]{1,63}\z", RegexOptions.CultureInvariant);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var organizations = endpoints.MapGroup("/api/v1/organizations").RequireAuthorization();
        organizations.MapPost("/", CreateOrganizationAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        organizations.MapGet("/", ListOrganizationsAsync);
        organizations.MapGet("/{organizationId:guid}/members", ListMembersAsync);
        organizations.MapPut("/{organizationId:guid}/members/{userId:guid}/role", ChangeMemberRoleAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        organizations.MapDelete("/{organizationId:guid}/members/{userId:guid}", RemoveMemberAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        organizations.MapPost("/{organizationId:guid}/members", AddMemberAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        organizations.MapPost("/{organizationId:guid}/companies", CreateCompanyAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);

        var companies = endpoints.MapGroup("/api/v1/companies").RequireAuthorization();
        companies.MapGet("/", ListCompaniesAsync);
        companies.MapGet("/{companyId:guid}", GetCompanyAsync);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request, HttpContext http, ClaimsPrincipal principal, TenancyStore store,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var name = request.Name?.Trim();
        var slug = request.Slug?.Trim();
        var errors = new Dictionary<string, string[]>();
        ValidateName(name, errors);
        ValidateSlug(slug, errors);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.CreateOrganizationAsync(userId, name!, slug!, CorrelationId(http), ct);
        return result.Outcome == CreateOutcome.Created
            ? Results.Json(new OrganizationSummary(result.Id!.Value, name!, slug!, "owner"),
                statusCode: StatusCodes.Status201Created)
            : Failure(result.Outcome, "An organization with this slug already exists.");
    }

    private static async Task<IResult> ListOrganizationsAsync(
        ClaimsPrincipal principal, TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        return Results.Ok(await store.ListOrganizationsAsync(userId, ct));
    }

    private static async Task<IResult> CreateCompanyAsync(
        Guid organizationId, CreateCompanyRequest request, HttpContext http, ClaimsPrincipal principal,
        TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var name = request.Name?.Trim();
        var slug = request.Slug?.Trim();
        var industryKey = request.IndustryKey?.Trim();
        var errors = new Dictionary<string, string[]>();
        ValidateName(name, errors);
        ValidateSlug(slug, errors);
        if (industryKey is null || !IndustryKeyPattern.IsMatch(industryKey))
        {
            errors["industryKey"] =
                ["Industry key must be 2 to 64 characters: lowercase letters, digits and underscores, starting with a letter."];
        }

        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.CreateCompanyAsync(
            userId, organizationId, name!, slug!, industryKey!, CorrelationId(http), ct);
        return result.Outcome == CreateOutcome.Created
            ? Results.Json(new CompanySummary(result.Id!.Value, organizationId, name!, slug!, industryKey!, "owner"),
                statusCode: StatusCodes.Status201Created)
            : Failure(result.Outcome, "A company with this slug already exists in this organization.");
    }

    private static async Task<IResult> ListCompaniesAsync(
        ClaimsPrincipal principal, TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        return Results.Ok(await store.ListCompaniesAsync(userId, ct));
    }

    private static async Task<IResult> GetCompanyAsync(
        Guid companyId, ClaimsPrincipal principal, TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var company = await store.GetCompanyAsync(userId, companyId, ct);
        return company is null ? Results.NotFound() : Results.Ok(company);
    }

    private static async Task<IResult> ListMembersAsync(
        Guid organizationId, ClaimsPrincipal principal, TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var members = await store.ListMembersAsync(userId, organizationId, ct);
        return members is null ? Failure(CreateOutcome.Forbidden, "") : Results.Ok(members);
    }

    private static async Task<IResult> AddMemberAsync(
        Guid organizationId, AddMemberRequest request, HttpContext http, ClaimsPrincipal principal,
        TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        // Same rule as Identity EmailRules.Normalize. Tenancy may not reference Identity, so a test pins the two together.
        var email = request.Email?.Trim().ToLowerInvariant();
        var role = request.Role?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(email) || email.Length < 3 || email.Length > 254 || !email.Contains('@'))
        {
            errors["email"] = ["A valid email address is required."];
        }

        if (role is not ("owner" or "admin" or "member"))
        {
            errors["role"] = ["Role must be owner, admin or member."];
        }

        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.AddMemberAsync(userId, organizationId, email!, role!, CorrelationId(http), ct);
        return result.Outcome == CreateOutcome.Created
            ? Results.Json(new { userId = result.Id!.Value, role }, statusCode: StatusCodes.Status201Created)
            : Failure(result.Outcome, "This user is already a member of this organization.");
    }

    private static async Task<IResult> ChangeMemberRoleAsync(
        Guid organizationId, Guid userId, ChangeRoleRequest request, HttpContext http, ClaimsPrincipal principal,
        TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var callerId)) return Results.Unauthorized();

        var role = request.Role?.Trim();
        if (role is not ("owner" or "admin" or "member"))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Role must be owner, admin or member."] });
        }

        var result = await store.ChangeMemberRoleAsync(callerId, organizationId, userId, role, CorrelationId(http), ct);
        return result.Outcome == CreateOutcome.Created ? Results.NoContent() : Failure(result.Outcome, "");
    }

    private static async Task<IResult> RemoveMemberAsync(
        Guid organizationId, Guid userId, HttpContext http, ClaimsPrincipal principal,
        TenancyStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var callerId)) return Results.Unauthorized();

        var result = await store.RemoveMemberAsync(callerId, organizationId, userId, CorrelationId(http), ct);
        return result.Outcome == CreateOutcome.Created ? Results.NoContent() : Failure(result.Outcome, "");
    }

    // The user comes only from the validated token. Nothing about identity is read from the request.
    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out userId) && userId != Guid.Empty;

    private static string? CorrelationId(HttpContext http) =>
        http.Items.TryGetValue(CorrelationContext.ItemKey, out var value) ? value as string : null;

    private static void ValidateName(string? name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 160)
        {
            errors["name"] = ["Name is required and must be at most 160 characters."];
        }
        else if (name.Any(char.IsControl))
        {
            errors["name"] = ["Name cannot contain control characters."];
        }
    }

    private static void ValidateSlug(string? slug, Dictionary<string, string[]> errors)
    {
        if (slug is null || !SlugPattern.IsMatch(slug))
        {
            errors["slug"] =
                ["Slug must be 3 to 80 characters: lowercase letters, digits and hyphens, starting and ending with a letter or digit."];
        }
    }

    // A 403 is the same whether the organization does not exist or the caller is not allowed to use it.
    private static IResult Failure(CreateOutcome outcome, string conflictDetail) => outcome switch
    {
        CreateOutcome.Forbidden => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Forbidden",
            detail: "You do not have permission to perform this action.",
            type: "urn:flexicore:problem:forbidden"),
        CreateOutcome.QuotaExceeded => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Limit reached",
            detail: "Your account has reached the maximum number of organizations it can own.",
            type: "urn:flexicore:problem:quota-exceeded"),
        CreateOutcome.LastOwner => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Last owner",
            detail: "An organization must keep at least one active owner.",
            type: "urn:flexicore:problem:last-owner"),
        CreateOutcome.Conflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Already exists",
            detail: conflictDetail,
            type: "urn:flexicore:problem:conflict"),
        CreateOutcome.Invalid => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid input",
            type: "urn:flexicore:problem:invalid-input"),
        CreateOutcome.Unauthenticated => Results.Unauthorized(),
        _ => throw new InvalidOperationException("Unexpected create outcome.")
    };
}