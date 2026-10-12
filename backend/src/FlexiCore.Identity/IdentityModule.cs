using System.Globalization;
using System.Threading.RateLimiting;
using FlexiCore.Identity.Data;
using FlexiCore.Identity.Endpoints;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace FlexiCore.Identity;

/// <summary>Entry points the host uses. Requires an NpgsqlDataSource to be registered by the host.</summary>
public static class IdentityModule
{
    public const string RateLimitPolicy = "identity-auth";

    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = JwtSettings.Load(configuration);
        var identity = IdentitySettings.Load(configuration, environment.IsDevelopment());

        // Never let IdentityModel diagnostics include token contents or other personal data.
        IdentityModelEventSource.ShowPII = false;

        services.AddSingleton(jwt);
        services.AddSingleton(identity);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService>(new PasswordService(identity.Pbkdf2Iterations));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<AuditWriter>();
        services.AddSingleton<UserStore>();
        services.AddSingleton<RefreshSessionStore>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep "sub" and "sid" as they are
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.SigningKey),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = "sub"
                };
                options.Events = new JwtBearerEvents { OnTokenValidated = ValidateSessionAsync };
            });
        services.AddAuthorization();

        // Per-IP limit shared by register, login, refresh and logout. The in-memory limiter is per instance.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            options.AddPolicy(RateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = identity.RateLimitPermitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        AuthEndpoints.Map(endpoints);
        return endpoints;
    }

    /// <summary>A valid signature is not enough: the token must carry a sid whose session family is still live for that user.</summary>
    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst("sub")?.Value, out var userId) ||
            !Guid.TryParse(principal?.FindFirst("sid")?.Value, out var sessionId))
        {
            context.Fail("The token has no valid session.");
            return;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<RefreshSessionStore>();
        if (!await sessions.IsSessionActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
            context.Fail("The session is no longer active.");
    }
}