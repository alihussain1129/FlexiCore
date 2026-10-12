using System.Security.Cryptography;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FlexiCore.UnitTests;

internal static class TestConfig
{
    public static IConfiguration Build(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    public static string RandomKey(int bytes = 32) => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes));
}

public class JwtSettingsTests
{
    [Fact]
    public void A_missing_key_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(TestConfig.Build()));

    [Fact]
    public void A_key_that_is_not_base64_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(TestConfig.Build(("Jwt:SigningKey", "not base64 !!"))));

    [Fact]
    public void A_key_shorter_than_256_bits_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(TestConfig.Build(("Jwt:SigningKey", TestConfig.RandomKey(16)))));

    [Fact]
    public void An_all_zero_key_is_rejected_and_never_echoed()
    {
        var zeros = Convert.ToBase64String(new byte[32]);

        var ex = Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(TestConfig.Build(("Jwt:SigningKey", zeros))));

        Assert.DoesNotContain(zeros, ex.Message);
    }

    [Fact]
    public void A_random_32_byte_key_loads_with_the_documented_defaults()
    {
        var settings = JwtSettings.Load(TestConfig.Build(("Jwt:SigningKey", TestConfig.RandomKey())));

        Assert.Equal("flexicore", settings.Issuer);
        Assert.Equal("flexicore-api", settings.Audience);
        Assert.Equal(15, settings.AccessTokenMinutes);
        Assert.Equal(30, settings.ClockSkewSeconds);
        Assert.Equal(32, settings.SigningKey.Length);
    }

    [Fact]
    public void Configured_values_override_the_defaults()
    {
        var settings = JwtSettings.Load(TestConfig.Build(
            ("Jwt:SigningKey", TestConfig.RandomKey()), ("Jwt:Issuer", "iss-x"), ("Jwt:Audience", "aud-x"),
            ("Jwt:AccessTokenMinutes", "5"), ("Jwt:ClockSkewSeconds", "10")));

        Assert.Equal("iss-x", settings.Issuer);
        Assert.Equal("aud-x", settings.Audience);
        Assert.Equal(5, settings.AccessTokenMinutes);
        Assert.Equal(10, settings.ClockSkewSeconds);
    }
}

public class IdentitySettingsTests
{
    [Fact]
    public void Fewer_than_the_minimum_iterations_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() =>
            IdentitySettings.Load(TestConfig.Build(("Identity:Pbkdf2Iterations", "99999")), isDevelopment: false));

    [Fact]
    public void Iterations_default_to_210000() =>
        Assert.Equal(210_000, IdentitySettings.Load(TestConfig.Build(), isDevelopment: false).Pbkdf2Iterations);

    [Fact]
    public void Registration_is_enabled_by_default_in_development() =>
        Assert.True(IdentitySettings.Load(TestConfig.Build(), isDevelopment: true).RegistrationEnabled);

    [Fact]
    public void Registration_is_disabled_by_default_outside_development() =>
        Assert.False(IdentitySettings.Load(TestConfig.Build(), isDevelopment: false).RegistrationEnabled);

    [Fact]
    public void Registration_can_be_disabled_explicitly_in_development() =>
        Assert.False(IdentitySettings.Load(TestConfig.Build(("Registration:Enabled", "false")), isDevelopment: true).RegistrationEnabled);

    [Fact]
    public void Registration_can_be_enabled_explicitly_outside_development() =>
        Assert.True(IdentitySettings.Load(TestConfig.Build(("Registration:Enabled", "true")), isDevelopment: false).RegistrationEnabled);
}

public class TokenServiceTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-12T10:00:00Z");

    private static TokenService NewService() =>
        new(JwtSettings.Load(TestConfig.Build(("Jwt:SigningKey", TestConfig.RandomKey()))), new FixedTime(Now));

    [Fact]
    public void Tokens_are_hs256_with_the_expected_claims_and_a_15_minute_lifetime()
    {
        var userId = Guid.NewGuid();
        var token = NewService().CreateAccessToken(userId, Guid.NewGuid());
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal("flexicore", jwt.Issuer);
        Assert.Contains("flexicore-api", jwt.Audiences);
        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.ValidFrom);
        Assert.Equal(Now.AddMinutes(15), token.ExpiresAt);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "email");
    }

    [Fact]
    public void Two_tokens_for_the_same_user_differ()
    {
        var service = NewService();
        var userId = Guid.NewGuid();

        Assert.NotEqual(service.CreateAccessToken(userId, Guid.NewGuid()).Value, service.CreateAccessToken(userId, Guid.NewGuid()).Value);
    }
}