using System.Net;
using System.Security.Claims;
using FlexiCore.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class SessionClaimTests(PostgresFixture postgres)
{
    private static string Mint(Guid sub, string? sid)
    {
        var now = DateTime.UtcNow;
        var claims = sid is null
            ? new[] { new Claim("sub", sub.ToString()) }
            : new[] { new Claim("sub", sub.ToString()), new Claim("sid", sid) };
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = "flexicore",
            Audience = "flexicore-api",
            IssuedAt = now.AddMinutes(-1),
            NotBefore = now.AddMinutes(-1),
            Expires = now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(TestSecrets.SigningKey), SecurityAlgorithms.HmacSha256)
        });
    }

    private static string SidOf(string accessToken) =>
        new JsonWebTokenHandler().ReadJsonWebToken(accessToken).GetPayloadValue<string>("sid");

    [Fact]
    public async Task The_access_token_sid_is_the_session_family_and_survives_rotation()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var first = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        var family = (string)(await Sql.ScalarAsync(owner, "select family_id::text from identity.refresh_sessions"))!;

        Assert.Equal(family, SidOf(first.AccessToken));

        var second = await Scenario.ReadSessionAsync(await app.RefreshAsync(first.RefreshToken));
        Assert.Equal(family, SidOf(second.AccessToken));
    }

    [Fact]
    public async Task A_validly_signed_token_without_a_sid_is_rejected()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var userId = Guid.Parse((string)(await Sql.ScalarAsync(app.Db.OwnerConnectionString, "select id::text from identity.users"))!);
        var session = await Scenario.LoginAsync(app);

        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(Mint(userId, SidOf(session.AccessToken)))).StatusCode); // control: the helper makes valid tokens
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(Mint(userId, null))).StatusCode);
    }

    [Fact]
    public async Task A_validly_signed_token_with_an_unknown_sid_is_rejected()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var userId = Guid.Parse((string)(await Sql.ScalarAsync(app.Db.OwnerConnectionString, "select id::text from identity.users"))!);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(Mint(userId, Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(Mint(userId, "not-a-guid"))).StatusCode);
    }

    [Fact]
    public async Task A_token_cannot_borrow_the_session_of_a_different_user()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync("bob.jones@example.com", "Another-long-passphrase-9")).StatusCode);
        var alice = await Scenario.LoginAsync(app);
        var bob = await Scenario.LoginAsync(app, "bob.jones@example.com", "Another-long-passphrase-9");
        var aliceId = Guid.Parse((string)(await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            "select id::text from identity.users where email_normalized = 'alice.smith@example.com'"))!);

        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(Mint(aliceId, SidOf(alice.AccessToken)))).StatusCode); // control
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(Mint(aliceId, SidOf(bob.AccessToken)))).StatusCode);
    }
}