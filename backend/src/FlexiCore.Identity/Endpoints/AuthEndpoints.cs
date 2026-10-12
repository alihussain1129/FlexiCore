using System.Security.Claims;
using System.Text;
using FlexiCore.Identity.Data;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using FlexiCore.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Identity.Endpoints;

public sealed record RegisterRequest(string? Email, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record LogoutRequest(string? RefreshToken);
public sealed record RegisterResponse(Guid Id, string Email);
/// <summary>Returned by both login and refresh.</summary>
public sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken);
public sealed record MeResponse(Guid Id, string Email);

internal static class AuthEndpoints
{
    private const int MaxLoginPasswordLength = 1024;

    // One fixed body for every failed login (unknown email, wrong password, locked or inactive account), so responses are byte-identical.
    private const string InvalidCredentialsBody =
        "{\"type\":\"urn:flexicore:problem:invalid-credentials\",\"title\":\"Invalid credentials\",\"status\":401," +
        "\"detail\":\"The email or password is incorrect.\"}";

    // One fixed body for every refresh failure (malformed, unknown, expired, revoked, reused, inactive user).
    private const string InvalidRefreshBody =
        "{\"type\":\"urn:flexicore:problem:invalid-refresh-token\",\"title\":\"Invalid refresh token\",\"status\":401," +
        "\"detail\":\"The refresh token is invalid, expired or has been revoked.\"}";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");
        group.MapPost("/register", RegisterAsync).RequireRateLimiting(IdentityModule.RateLimitPolicy);
        group.MapPost("/login", LoginAsync).RequireRateLimiting(IdentityModule.RateLimitPolicy);
        group.MapPost("/refresh", RefreshAsync).RequireRateLimiting(IdentityModule.RateLimitPolicy);
        group.MapPost("/logout", LogoutAsync).RequireRateLimiting(IdentityModule.RateLimitPolicy);
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
        LoginRequest request, HttpContext http, JwtSettings jwt, IdentitySettings identity, IPasswordService passwords,
        ITokenService tokens, UserStore users, RefreshSessionStore sessions, AuditWriter audit, CancellationToken ct)
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

        if (user.IsLocked)
        {
            // The password is deliberately not checked while locked, so a lock cannot be used to test guesses.
            // The response is identical to every other failed login, so it does not reveal that the account exists.
            passwords.BurnVerificationTime(password);
            await audit.WriteAsync(
                new AuditEvent("auth.login", "denied", user.Id, "user", user.Id.ToString(), correlationId, Reason("account_locked")), ct);
            return InvalidCredentials();
        }

        var check = passwords.Verify(user.PasswordHash, password);
        if (check == PasswordCheck.Failed)
        {
            await users.RegisterFailedLoginAsync(user.Id, identity.LockoutMaxFailedAttempts, identity.LockoutMinutes, correlationId, ct);
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
        var refreshToken = RefreshTokenCodec.Generate();
        var familyId = await sessions.CompleteLoginAsync(
            user.Id, newHash, RefreshTokenCodec.Hash(refreshToken), identity.RefreshTokenDays, correlationId, ct);

        var token = tokens.CreateAccessToken(user.Id, familyId);
        return Results.Ok(new LoginResponse(token.Value, "Bearer", jwt.AccessTokenMinutes * 60, refreshToken));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request, HttpContext http, JwtSettings jwt, IdentitySettings identity, ITokenService tokens,
        RefreshSessionStore sessions, AuditWriter audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var correlationId = CorrelationId(http);

        if (!RefreshTokenCodec.IsWellFormed(request.RefreshToken))
        {
            await audit.WriteAsync(
                new AuditEvent("auth.refresh", "failure", CorrelationId: correlationId, Metadata: Reason("malformed")), ct);
            return InvalidRefreshToken();
        }

        var newToken = RefreshTokenCodec.Generate();
        var result = await sessions.RotateAsync(
            RefreshTokenCodec.Hash(request.RefreshToken!), RefreshTokenCodec.Hash(newToken), identity.RefreshTokenDays, correlationId, ct);
        if (result.Outcome != RefreshOutcome.Rotated) return InvalidRefreshToken();

        var access = tokens.CreateAccessToken(result.UserId!.Value, result.FamilyId!.Value);
        return Results.Ok(new LoginResponse(access.Value, "Bearer", jwt.AccessTokenMinutes * 60, newToken));
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request, HttpContext http, RefreshSessionStore sessions, AuditWriter audit, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var correlationId = CorrelationId(http);

        // Always 204 for a well-formed JSON body, so logout cannot be used to test whether a token exists.
        if (!RefreshTokenCodec.IsWellFormed(request.RefreshToken))
        {
            await audit.WriteAsync(
                new AuditEvent("auth.logout", "failure", CorrelationId: correlationId, Metadata: Reason("malformed")), ct);
            return Results.NoContent();
        }

        await sessions.LogoutAsync(RefreshTokenCodec.Hash(request.RefreshToken!), correlationId, ct);
        return Results.NoContent();
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

    private static IResult InvalidRefreshToken() =>
        Results.Content(InvalidRefreshBody, "application/problem+json", Encoding.UTF8, StatusCodes.Status401Unauthorized);

    private static string? CorrelationId(HttpContext http) =>
        http.Items.TryGetValue(CorrelationContext.ItemKey, out var value) ? value as string : null;

    private static Dictionary<string, string> Reason(string reason) => new() { ["reason"] = reason };
}