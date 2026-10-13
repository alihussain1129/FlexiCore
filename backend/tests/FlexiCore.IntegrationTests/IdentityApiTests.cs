using System.Buffers.Binary;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FlexiCore.Identity.Security;
using FlexiCore.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class IdentityApiTests(PostgresFixture postgres)
{
    private const string Email = "Alice.Smith@Example.com"; // the password below must not contain "alice.smith"
    private const string Password = "Tr0ub4dor&3-horse-staple";
    private const string NormalizedEmail = "alice.smith@example.com";

    private static async Task<(Guid Id, string Token)> RegisterAndLoginAsync(IdentityTestApp app)
    {
        var register = await app.RegisterAsync(Email, Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var id = (await IdentityTestApp.ReadJsonAsync(register)).GetProperty("id").GetGuid();

        var login = await app.LoginAsync(Email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await IdentityTestApp.ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
        return (id, token);
    }

    private static string Mint(
        Guid sub, DateTime? expires = null, string issuer = "flexicore", string audience = "flexicore-api",
        byte[]? key = null, string algorithm = SecurityAlgorithms.HmacSha256, string? sid = null)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(sid is null ? new[] { new Claim("sub", sub.ToString()) } : new[] { new Claim("sub", sub.ToString()), new Claim("sid", sid) }),
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.AddMinutes(-30),
            NotBefore = now.AddMinutes(-30),
            Expires = expires ?? now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key ?? TestSecrets.SigningKey), algorithm)
        });
    }

    // Signed by hand with the server's real key: IdentityModel refuses HS512 with a 256-bit key,
    // and this test must prove that the server pins HS256 even when the signature itself is valid.
    private static string HandSigned(Guid sub, string alg, string sid)
    {
        var header = Base64UrlEncoder.Encode($"{{\"alg\":\"{alg}\",\"typ\":\"JWT\"}}");
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = Base64UrlEncoder.Encode($"{{\"sub\":\"{sub}\",\"sid\":\"{sid}\",\"iss\":\"flexicore\",\"aud\":\"flexicore-api\",\"exp\":{exp}}}");
        var signingInput = $"{header}.{payload}";
        var signature = HMACSHA512.HashData(TestSecrets.SigningKey, Encoding.ASCII.GetBytes(signingInput));
        return $"{signingInput}.{Base64UrlEncoder.Encode(signature)}";
    }

    private static string AlgNone(Guid sub, string sid)
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = Base64UrlEncoder.Encode($"{{\"sub\":\"{sub}\",\"sid\":\"{sid}\",\"iss\":\"flexicore\",\"aud\":\"flexicore-api\",\"exp\":{exp}}}");
        return $"{header}.{payload}.";
    }

    private static string TamperSignature(string token)
    {
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'A' ? 'B' : 'A') + parts[2][1..];
        return string.Join('.', parts);
    }

    [Fact]
    public async Task Register_creates_a_user_with_a_hashed_password_and_an_audit_event()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        var response = await app.RegisterAsync("  " + Email + "  ", Password);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal(Email, json.GetProperty("email").GetString()); // as entered, trimmed

        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(Email + "|" + NormalizedEmail,
            await Sql.ScalarAsync(owner, "select email || '|' || email_normalized from identity.users"));

        var hash = (string)(await Sql.ScalarAsync(owner, "select password_hash from identity.users"))!;
        Assert.DoesNotContain(Password, hash);
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4)));
        Assert.Equal(210_000u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)));

        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.register' and outcome = 'success'"));
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_rejected_case_insensitively()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(Email, Password)).StatusCode);

        var duplicate = await app.RegisterAsync(Email.ToUpperInvariant(), Password);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.users"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.register' and outcome = 'failure'"));
    }

    [Theory]
    [InlineData("short-pw-1")]
    [InlineData("my-Alice.Smith-pw1")]
    public async Task Weak_passwords_are_rejected_with_a_field_error(string password)
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        var response = await app.RegisterAsync(Email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.True(json.GetProperty("errors").TryGetProperty("password", out _));
        Assert.Equal(0L, await Sql.ScalarAsync(app.Db.OwnerConnectionString, "select count(*) from identity.users"));
    }

    [Fact]
    public async Task A_password_over_128_characters_is_rejected()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        var response = await app.RegisterAsync(Email, new string('x', 129));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_invalid_email_is_rejected_with_a_field_error()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        var response = await app.RegisterAsync("not-an-email", Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.True(json.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Registration_is_forbidden_when_explicitly_disabled()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres, registrationEnabled: "false");

        Assert.Equal(HttpStatusCode.Forbidden, (await app.RegisterAsync(Email, Password)).StatusCode);
    }

    [Fact]
    public async Task Registration_is_disabled_by_default_outside_development()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres, registrationEnabled: null);

        Assert.Equal(HttpStatusCode.Forbidden, (await app.RegisterAsync(Email, Password)).StatusCode);
    }

    [Fact]
    public async Task Register_then_login_then_me_returns_the_current_user()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var (id, token) = await RegisterAndLoginAsync(app);

        var me = await app.MeAsync(token);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(me);
        Assert.Equal(id, json.GetProperty("id").GetGuid());
        Assert.Equal(Email, json.GetProperty("email").GetString());

        var upper = await app.LoginAsync(Email.ToUpperInvariant(), Password);
        Assert.Equal(HttpStatusCode.OK, upper.StatusCode);
        var loginJson = await IdentityTestApp.ReadJsonAsync(upper);
        Assert.Equal("Bearer", loginJson.GetProperty("tokenType").GetString());
        Assert.Equal(900, loginJson.GetProperty("expiresIn").GetInt32());
    }

    [Fact]
    public async Task Me_without_a_token_or_with_garbage_is_401()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync("not.a.token")).StatusCode);
    }

    [Theory]
    [InlineData("tampered-signature")]
    [InlineData("expired")]
    [InlineData("wrong-key")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("hs512")]
    [InlineData("alg-none")]
    public async Task Tokens_that_fail_validation_are_rejected_even_for_a_real_user(string kind)
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var (id, good) = await RegisterAndLoginAsync(app);
        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(good)).StatusCode); // control: the valid token works
        var sid = new JsonWebTokenHandler().ReadJsonWebToken(good).GetPayloadValue<string>("sid");

        var bad = kind switch
        {
            "tampered-signature" => TamperSignature(good),
            "expired" => Mint(id, expires: DateTime.UtcNow.AddMinutes(-10), sid: sid),
            "wrong-key" => Mint(id, key: SHA256.HashData(Encoding.UTF8.GetBytes("another-key")), sid: sid),
            "wrong-issuer" => Mint(id, issuer: "someone-else", sid: sid),
            "wrong-audience" => Mint(id, audience: "another-api", sid: sid),
            "hs512" => HandSigned(id, "HS512", sid),
            "alg-none" => AlgNone(id, sid),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(bad)).StatusCode);
    }

    [Fact]
    public async Task A_validly_signed_token_for_a_user_that_does_not_exist_is_rejected()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(Mint(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task A_suspended_user_loses_access_immediately_and_cannot_log_in()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var (_, token) = await RegisterAndLoginAsync(app);

        await Sql.ExecAsync(app.Db.OwnerConnectionString, "update identity.users set status = 'suspended'");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync(Email, Password)).StatusCode);
    }

    [Fact]
    public async Task Failed_logins_return_byte_identical_responses_whatever_the_cause()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(Email, Password)).StatusCode);

        var wrongPassword = await app.LoginAsync(Email, "wrong-password-123");
        var unknownEmail = await app.LoginAsync("nobody@example.com", Password);
        await Sql.ExecAsync(app.Db.OwnerConnectionString, "update identity.users set status = 'suspended'");
        var suspended = await app.LoginAsync(Email, Password);

        Assert.All(new[] { wrongPassword, unknownEmail, suspended },
            r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        var body = await wrongPassword.Content.ReadAsStringAsync();
        Assert.Equal(body, await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal(body, await suspended.Content.ReadAsStringAsync());
        Assert.Equal(wrongPassword.Content.Headers.ContentType, unknownEmail.Content.Headers.ContentType);
        Assert.Equal("application/problem+json", wrongPassword.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_with_missing_fields_is_a_validation_error()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        var response = await app.LoginAsync(Email, "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.True(json.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Login_rehashes_a_hash_made_with_fewer_iterations()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(Email, Password)).StatusCode);

        var oldHash = new PasswordService(100_000).Hash(Password); // base64 only, safe to inline
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, $"update identity.users set password_hash = '{oldHash}'");

        Assert.Equal(HttpStatusCode.OK, (await app.LoginAsync(Email, Password)).StatusCode);

        var bytes = Convert.FromBase64String((string)(await Sql.ScalarAsync(owner, "select password_hash from identity.users"))!);
        Assert.Equal(210_000u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)));
    }

    [Fact]
    public async Task Audit_events_carry_the_correlation_id_and_never_contain_secrets()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        const string corr = "corr-abc123";

        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(Email, Password, corr)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync("nobody@example.com", Password, corr)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync(Email, "wrong-password-123", corr)).StatusCode);
        var ok = await app.LoginAsync(Email, Password, corr);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var token = (await IdentityTestApp.ReadJsonAsync(ok)).GetProperty("accessToken").GetString()!;

        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from audit.events where action = 'auth.register' and outcome = 'success'"));
        Assert.Equal(2L, await Sql.ScalarAsync(owner, "select count(*) from audit.events where action = 'auth.login' and outcome = 'failure'"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from audit.events where action = 'auth.login' and outcome = 'success'"));
        Assert.Equal(4L, await Sql.ScalarAsync(owner, $"select count(*) from audit.events where correlation_id = '{corr}'"));
        Assert.Equal("bad_password,unknown_account", await Sql.ScalarAsync(owner,
            "select string_agg(metadata->>'reason', ',' order by metadata->>'reason') from audit.events where outcome = 'failure'"));

        var dump = (string)(await Sql.ScalarAsync(owner, "select coalesce(string_agg(e::text, ' '), '') from audit.events e"))!;
        Assert.DoesNotContain(Password, dump);
        Assert.DoesNotContain("wrong-password-123", dump);
        Assert.DoesNotContain(token, dump);
    }

}

