using System.Security.Claims;
using FlexiCore.Identity.Settings;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlexiCore.Identity.Security;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    /// <param name="sessionId">The refresh-token family id; it becomes the "sid" claim.</param>
    AccessToken CreateAccessToken(Guid userId, Guid sessionId);
}

/// <summary>Issues short-lived HS256 access tokens carrying only sub, sid and jti (no email or other personal data).</summary>
public sealed class TokenService(JwtSettings settings, TimeProvider time) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials =
        new(new SymmetricSecurityKey(settings.SigningKey), SecurityAlgorithms.HmacSha256);

    public AccessToken CreateAccessToken(Guid userId, Guid sessionId)
    {
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(settings.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sid, sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            }),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _credentials
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}