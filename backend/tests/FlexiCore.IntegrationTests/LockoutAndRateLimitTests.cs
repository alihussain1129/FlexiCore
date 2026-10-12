using System.Net;
using FlexiCore.IntegrationTests.Infrastructure;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LockoutAndRateLimitTests(PostgresFixture postgres)
{
    private static async Task FailAsync(IdentityTestApp app, int times, string email = Scenario.Email)
    {
        for (var i = 0; i < times; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync(email, Scenario.WrongPassword)).StatusCode);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_and_even_the_correct_password_is_then_refused_with_the_same_response()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = app.Db.OwnerConnectionString;

        var wrong = await app.LoginAsync(Scenario.Email, Scenario.WrongPassword);
        var wrongBody = await wrong.Content.ReadAsStringAsync();
        await FailAsync(app, 4);

        Assert.Equal(5, await Sql.ScalarAsync(owner, "select failed_login_count from identity.users"));
        Assert.Equal("true", await Sql.ScalarAsync(owner, "select (locked_until is not null and locked_until > now())::text from identity.users"));

        var correctWhileLocked = await app.LoginAsync(Scenario.Email, Scenario.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, correctWhileLocked.StatusCode);
        Assert.Equal(wrongBody, await correctWhileLocked.Content.ReadAsStringAsync());
        Assert.Equal(wrong.Content.Headers.ContentType, correctWhileLocked.Content.Headers.ContentType);

        // A locked account is indistinguishable from an unknown one.
        var unknown = await app.LoginAsync("nobody@example.com", Scenario.Password);
        Assert.Equal(wrongBody, await unknown.Content.ReadAsStringAsync());

        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from audit.events where action = 'auth.lockout' and outcome = 'denied'"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.login' and outcome = 'denied' and metadata->>'reason' = 'account_locked'"));
        Assert.Equal(0L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions")); // no session was issued
    }

    [Fact]
    public async Task A_successful_login_before_the_threshold_resets_the_counter()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        await FailAsync(app, 4);

        Assert.Equal(HttpStatusCode.OK, (await app.LoginAsync(Scenario.Email, Scenario.Password)).StatusCode);

        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(0, await Sql.ScalarAsync(owner, "select failed_login_count from identity.users"));
        Assert.Equal("true", await Sql.ScalarAsync(owner, "select (locked_until is null)::text from identity.users"));

        await FailAsync(app, 4); // the count starts again from zero, so this does not lock
        Assert.Equal(HttpStatusCode.OK, (await app.LoginAsync(Scenario.Email, Scenario.Password)).StatusCode);
    }

    [Fact]
    public async Task Email_casing_and_whitespace_variants_share_one_counter()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var variants = new[]
        {
            "alice.smith@example.com", "ALICE.SMITH@EXAMPLE.COM", "  Alice.Smith@Example.com  ",
            "aLiCe.SmItH@eXaMpLe.CoM", "ALICE.smith@example.COM"
        };

        foreach (var variant in variants)
            Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync(variant, Scenario.WrongPassword)).StatusCode);

        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.users"));
        Assert.Equal(5, await Sql.ScalarAsync(owner, "select failed_login_count from identity.users"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync(Scenario.Email, Scenario.Password)).StatusCode);
    }

    [Fact]
    public async Task The_lock_expires_and_the_correct_password_then_works_and_resets_the_counter()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        await FailAsync(app, 5);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, "update identity.users set locked_until = now() - interval '1 second'");

        Assert.Equal(HttpStatusCode.OK, (await app.LoginAsync(Scenario.Email, Scenario.Password)).StatusCode);

        Assert.Equal(0, await Sql.ScalarAsync(owner, "select failed_login_count from identity.users"));
        Assert.Equal("true", await Sql.ScalarAsync(owner, "select (locked_until is null)::text from identity.users"));
    }

    [Fact]
    public async Task A_failure_after_an_expired_lock_restarts_the_count_at_one()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        await FailAsync(app, 5);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, "update identity.users set locked_until = now() - interval '1 second'");

        await FailAsync(app, 1);

        Assert.Equal(1, await Sql.ScalarAsync(owner, "select failed_login_count from identity.users"));
        Assert.Equal("true", await Sql.ScalarAsync(owner, "select (locked_until is null)::text from identity.users"));
    }

    [Fact]
    public async Task A_lock_does_not_end_a_session_that_already_exists_but_blocks_new_logins()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        await FailAsync(app, 5);

        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(session.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync(Scenario.Email, Scenario.Password)).StatusCode);
    }

    [Fact]
    public async Task Auth_endpoints_return_429_with_Retry_After_when_the_per_IP_limit_is_exceeded()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres, rateLimitPerMinute: "3");

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await app.LoginAsync("nobody@example.com", Scenario.Password)).StatusCode);

        var limited = await app.LoginAsync("nobody@example.com", Scenario.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }
}