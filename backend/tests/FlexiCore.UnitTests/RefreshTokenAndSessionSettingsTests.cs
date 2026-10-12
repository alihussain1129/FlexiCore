using System.Security.Cryptography;
using System.Text;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FlexiCore.UnitTests;

public class RefreshTokenCodecTests
{
    public static IEnumerable<object?[]> Malformed() => new[]
    {
        new object?[] { null },
        new object?[] { "" },
        new object?[] { "abc" },
        new object?[] { new string('A', 42) },
        new object?[] { new string('A', 44) },
        new object?[] { new string('A', 42) + "+" },
        new object?[] { new string('A', 42) + "=" },
        new object?[] { new string('A', 42) + " " },
        new object?[] { new string('A', 42) + "B" } // non-canonical final character
    };

    [Fact]
    public void Generated_tokens_are_43_url_safe_characters_and_well_formed()
    {
        var token = RefreshTokenCodec.Generate();

        Assert.Equal(RefreshTokenCodec.EncodedLength, token.Length);
        Assert.True(token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        Assert.True(RefreshTokenCodec.IsWellFormed(token));
    }

    [Fact]
    public void Generated_tokens_do_not_repeat_and_are_always_well_formed()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 2000; i++)
        {
            var token = RefreshTokenCodec.Generate();
            Assert.True(RefreshTokenCodec.IsWellFormed(token));
            Assert.True(seen.Add(token));
        }
    }

    [Fact]
    public void Hash_is_the_sha256_of_the_ascii_token_and_32_bytes_long()
    {
        var token = RefreshTokenCodec.Generate();
        var hash = RefreshTokenCodec.Hash(token);

        Assert.Equal(32, hash.Length);
        Assert.Equal(SHA256.HashData(Encoding.ASCII.GetBytes(token)), hash);
        Assert.Equal(hash, RefreshTokenCodec.Hash(token));
        Assert.NotEqual(hash, RefreshTokenCodec.Hash(RefreshTokenCodec.Generate()));
    }

    [Fact]
    public void An_all_A_token_of_the_right_length_is_well_formed() =>
        Assert.True(RefreshTokenCodec.IsWellFormed(new string('A', 43)));

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Malformed_tokens_are_rejected(string? token) => Assert.False(RefreshTokenCodec.IsWellFormed(token));
}

public class SessionSettingsTests
{
    [Fact]
    public void Defaults_are_30_day_tokens_a_90_day_family_5_failures_15_minutes_and_20_requests_per_minute()
    {
        var settings = IdentitySettings.Load(TestConfig.Build(), isDevelopment: false);

        Assert.Equal(30, settings.RefreshTokenDays);
        Assert.Equal(90, IdentitySettings.FamilyLifetimeDays);
        Assert.Equal(5, settings.LockoutMaxFailedAttempts);
        Assert.Equal(15, settings.LockoutMinutes);
        Assert.Equal(20, settings.RateLimitPermitPerMinute);
    }

    [Fact]
    public void Configured_values_override_the_defaults()
    {
        var settings = IdentitySettings.Load(TestConfig.Build(
            ("Identity:RefreshTokenDays", "7"), ("Identity:Lockout:MaxFailedAttempts", "3"),
            ("Identity:Lockout:Minutes", "60"), ("Identity:RateLimit:PermitPerMinute", "5")), isDevelopment: false);

        Assert.Equal(7, settings.RefreshTokenDays);
        Assert.Equal(3, settings.LockoutMaxFailedAttempts);
        Assert.Equal(60, settings.LockoutMinutes);
        Assert.Equal(5, settings.RateLimitPermitPerMinute);
    }

    [Theory]
    [InlineData("Identity:RefreshTokenDays", "0")]
    [InlineData("Identity:RefreshTokenDays", "91")]
    [InlineData("Identity:Lockout:MaxFailedAttempts", "2")]
    [InlineData("Identity:Lockout:MaxFailedAttempts", "21")]
    [InlineData("Identity:Lockout:Minutes", "0")]
    [InlineData("Identity:Lockout:Minutes", "1441")]
    [InlineData("Identity:RateLimit:PermitPerMinute", "0")]
    public void Out_of_range_session_settings_are_rejected(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() =>
            IdentitySettings.Load(TestConfig.Build((key, value)), isDevelopment: false));
}

public class AccessTokenSessionClaimTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void The_access_token_carries_the_session_id_as_sid_and_stays_15_minutes()
    {
        var now = DateTimeOffset.Parse("2026-10-12T10:00:00Z");
        var service = new TokenService(JwtSettings.Load(TestConfig.Build(("Jwt:SigningKey", TestConfig.RandomKey()))), new FixedTime(now));
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var token = service.CreateAccessToken(userId, sessionId);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal(sessionId.ToString(), jwt.GetPayloadValue<string>("sid"));
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.ValidFrom);
        Assert.Equal(now.AddMinutes(15), token.ExpiresAt);
    }
}