using FlexiCore.Identity.Data;
using FlexiCore.Identity.Endpoints;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace FlexiCore.Identity;

/// <summary>Entry points the host uses. Requires an NpgsqlDataSource to be registered by the host.</summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var jwt = JwtSettings.Load(configuration);
        var identity = IdentitySettings.Load(configuration, environment.IsDevelopment());

        services.AddSingleton(jwt);
        services.AddSingleton(identity);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordService>(new PasswordService(identity.Pbkdf2Iterations));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<AuditWriter>();
        services.AddSingleton<UserStore>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep "sub" as "sub"
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
            });
        services.AddAuthorization();

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        AuthEndpoints.Map(endpoints);
        return endpoints;
    }
}