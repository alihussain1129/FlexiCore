using System.Security.Claims;
using System.Text;
using FlexiCore.Identity.Data;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using FlexiCore.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Identity.Endpoints;

public sealed record RegisterRequest(string? Email, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RegisterResponse(Guid Id, string Email);
public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn);
public sealed record MeResponse(Guid Id, string Email);

internal static class AuthEndpoints
{
    private const int MaxLoginPasswordLength = 1024;

    // One fixed body for every failed login (unknown email, wrong password, inactive account), so responses are byte-identical.
    private const string InvalidCredentialsBody =
        "{\"type\":\"urn:flexicore:problem:invalid-credentials\",\"title\":\"Invalid credentials\",\"status\":401," +
        "\"detail\":\"The email or password is incorrect.\"}";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", MeAsync).RequireAuthorization();
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request, HttpContext http, IdentitySettings settings, IPasswordService passwords,
        UserStore users, AuditWriter audit, CancellationToken ct)
    {
        if (!settings.RegistrationEnabled)
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Registration is disabled",
                type: "urn:flexicore:problem:registration-disabled");

        var email = request.Email?.Trim();
        var errors = new Dictionary<string, string[]>();
        if (!EmailRules.IsValid(email)) errors["email"] = ["A valid email address is required."];

        var normalized = email is null ? string.Empty : EmailRules.Normalize(email);
        var passwordErrors = PasswordPolicy.Validate(request.Password, normalized);
        if (passwordErrors.Count > 0) errors["password"] = [.. passwordErrors];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var correlationId = CorrelationId(http);
        var userId = await users.TryCreateAsync(email!, normalized, passwords.Hash(request.Password!), correlationId, ct);
        if (userId is null)
        {
            await audit.WriteAsync(
                new AuditEvent("auth.register", "failure", CorrelationId: correlationId, Metadata: Reason("duplicate_email")), ct);
            // Known limitation (documented in DECISIONS.md): without email verification this reveals registered emails.
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Email already in use",
                detail: "An account with this email address already exists.",
                type: "urn:flexicore:problem:email-in-use");
        }

        return Results.Json(new RegisterResponse(userId.Value, email!), statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, HttpContext http, JwtSettings jwt, IPasswordService passwords, ITokenService tokens,
        UserStore users, AuditWriter audit, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email)) errors["email"] = ["Email is required."];
        if (string.IsNullOrEmpty(request.Password)) errors["password"] = ["Password is required."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        http.Response.Headers.CacheControl = "no-store";
        var correlationId = CorrelationId(http);
        var email = request.Email!.Trim();
        var password = request.Password!;

        // Reject absurd input before spending any hashing effort on it.
        if (email.Length > EmailRules.MaxLength || password.Length > MaxLoginPasswordLength)
        {
            await audit.WriteAsync(
                new AuditEvent("auth.login", "failure", CorrelationId: correlationId, Metadata: Reason("oversized_input")), ct);
            return InvalidCredentials();
        }

        var user = await users.FindByNormalizedEmailAsync(EmailRules.Normalize(email), ct);
        if (user is null)
        {
            passwords.BurnVerificationTime(password);
            await audit.WriteAsync(
                new AuditEvent("auth.login", "failure", CorrelationId: correlationId, Metadata: Reason("unknown_account")), ct);
            return InvalidCredentials();
        }

        var check = passwords.Verify(user.PasswordHash, password);
        if (check == PasswordCheck.Failed)
        {
            await audit.WriteAsync(
                new AuditEvent("auth.login", "failure", user.Id, "user", user.Id.ToString(), correlationId, Reason("bad_password")), ct);
            return InvalidCredentials();
        }

        if (!user.IsActive)
        {
            await audit.WriteAsync(
                new AuditEvent("auth.login", "failure", user.Id, "user", user.Id.ToString(), correlationId, Reason("account_inactive")), ct);
            return InvalidCredentials();
        }

        var newHash = check == PasswordCheck.SuccessRehashNeeded ? passwords.Hash(password) : null;
        await users.UpdateAfterLoginAsync(user.Id, newHash, ct);

        var token = tokens.CreateAccessToken(user.Id);
        await audit.WriteAsync(
            new AuditEvent("auth.login", "success", user.Id, "user", user.Id.ToString(), correlationId), ct);

        return Results.Ok(new LoginResponse(token.Value, "Bearer", jwt.AccessTokenMinutes * 60));
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, UserStore users, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var id)) return Results.Unauthorized();

        var user = await users.FindByIdAsync(id, ct);
        return user is { IsActive: true }
            ? Results.Ok(new MeResponse(user.Id, user.Email))
            : Results.Unauthorized();
    }

    private static IResult InvalidCredentials() =>
        Results.Content(InvalidCredentialsBody, "application/problem+json", Encoding.UTF8, StatusCodes.Status401Unauthorized);

    private static string? CorrelationId(HttpContext http) =>
        http.Items.TryGetValue(CorrelationContext.ItemKey, out var value) ? value as string : null;

    private static Dictionary<string, string> Reason(string reason) => new() { ["reason"] = reason };
}