using System.Net;
using FlexiCore.IntegrationTests.Infrastructure;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class RefreshApiTests(PostgresFixture postgres)
{
    private const string InvalidRefreshType = "urn:flexicore:problem:invalid-refresh-token";

    public static IEnumerable<object?[]> MalformedTokens() => new[]
    {
        new object?[] { null },
        new object?[] { "" },
        new object?[] { "abc" },
        new object?[] { new string('A', 42) },
        new object?[] { new string('A', 44) },
        new object?[] { new string('A', 42) + "+" },
        new object?[] { new string('A', 43) } // well-formed but unknown
    };

    [Fact]
    public async Task Login_returns_a_refresh_token_and_the_database_keeps_only_its_sha256_hash()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;

        Assert.Equal(43, session.RefreshToken.Length);
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions"));
        Assert.Equal(Scenario.HashHex(session.RefreshToken),
            await Sql.ScalarAsync(owner, "select encode(token_hash, 'hex') from identity.refresh_sessions"));

        // No column can hold a plaintext token, and the plaintext appears nowhere in the table's text form.
        Assert.Equal("id,user_id,family_id,token_hash,created_at,expires_at,family_expires_at,rotated_at,revoked_at,revoked_reason,device_label",
            await Sql.ScalarAsync(owner,
                "select string_agg(column_name, ',' order by ordinal_position) from information_schema.columns " +
                "where table_schema = 'identity' and table_name = 'refresh_sessions'"));
        var dump = (string)(await Sql.ScalarAsync(owner, "select coalesce(string_agg(s::text, ' '), '') from identity.refresh_sessions s"))!;
        Assert.DoesNotContain(session.RefreshToken, dump);

        // 30-day token inside a 90-day family.
        Assert.Equal("true", await Sql.ScalarAsync(owner,
            "select (extract(epoch from (family_expires_at - created_at)) = 7776000)::text from identity.refresh_sessions"));
        Assert.Equal("true", await Sql.ScalarAsync(owner,
            "select (extract(epoch from (expires_at - created_at)) = 2592000)::text from identity.refresh_sessions"));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_new_credentials_work()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var first = await Scenario.LoginAsync(app);

        var response = await app.RefreshAsync(first.RefreshToken);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var second = await Scenario.ReadSessionAsync(response);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(second.AccessToken)).StatusCode);

        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(2L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(distinct family_id) from identity.refresh_sessions"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions where rotated_at is not null"));
        Assert.Equal(Scenario.HashHex(second.RefreshToken),
            await Sql.ScalarAsync(owner, "select encode(token_hash, 'hex') from identity.refresh_sessions where rotated_at is null"));
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family_and_kills_the_access_token()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var first = await Scenario.LoginAsync(app);
        var second = await Scenario.ReadSessionAsync(await app.RefreshAsync(first.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(second.AccessToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(first.RefreshToken)).StatusCode); // reuse

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(second.RefreshToken)).StatusCode); // sibling is dead too
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(second.AccessToken)).StatusCode);

        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(0L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions where revoked_at is null"));
        Assert.Equal(2L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions where revoked_reason = 'reuse_detected'"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.refresh' and outcome = 'failure' and metadata->>'reason' = 'reuse_detected'"));
    }

    [Fact]
    public async Task Twenty_concurrent_refreshes_of_one_token_produce_exactly_one_success()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => app.RefreshAsync(session.RefreshToken)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(19, results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));

        // The losers are reuse attempts, so the family ends up fully revoked: exactly the two rows, none usable.
        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(2L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions"));
        Assert.Equal(0L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions where revoked_at is null"));
    }

    [Fact]
    public async Task A_logout_racing_a_refresh_never_leaves_a_usable_token_behind()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);

        for (var i = 0; i < 5; i++)
        {
            var session = await Scenario.LoginAsync(app);
            var refresh = app.RefreshAsync(session.RefreshToken);
            var logout = app.LogoutAsync(session.RefreshToken);
            await Task.WhenAll(refresh, logout);

            if ((await refresh).StatusCode == HttpStatusCode.OK)
            {
                var issued = await Scenario.ReadSessionAsync(await refresh);
                Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(issued.RefreshToken)).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(issued.AccessToken)).StatusCode);
            }
        }

        Assert.Equal(0L, await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            "select count(*) from identity.refresh_sessions where revoked_at is null"));
    }

    [Theory]
    [MemberData(nameof(MalformedTokens))]
    public async Task Malformed_or_unknown_refresh_tokens_get_the_same_generic_401(string? token)
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);

        var response = await app.RefreshAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(InvalidRefreshType, await response.Content.ReadAsStringAsync());
        Assert.Equal(0L, await Sql.ScalarAsync(app.Db.OwnerConnectionString, "select count(*) from identity.refresh_sessions where rotated_at is not null"));
    }

    [Fact]
    public async Task Every_refresh_failure_returns_a_byte_identical_body()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = app.Db.OwnerConnectionString;

        var unknown = await (await app.RefreshAsync(new string('A', 43))).Content.ReadAsStringAsync();
        var malformed = await (await app.RefreshAsync("nope")).Content.ReadAsStringAsync();

        var first = await Scenario.LoginAsync(app);
        await Scenario.ReadSessionAsync(await app.RefreshAsync(first.RefreshToken));
        var reused = await (await app.RefreshAsync(first.RefreshToken)).Content.ReadAsStringAsync();

        var second = await Scenario.LoginAsync(app);
        await Sql.ExecAsync(owner, $"update identity.refresh_sessions set revoked_at = now(), revoked_reason = 'test' where encode(token_hash, 'hex') = '{Scenario.HashHex(second.RefreshToken)}'");
        var revoked = await (await app.RefreshAsync(second.RefreshToken)).Content.ReadAsStringAsync();

        Assert.Equal(unknown, malformed);
        Assert.Equal(unknown, reused);
        Assert.Equal(unknown, revoked);
    }

    [Fact]
    public async Task An_expired_token_is_rejected_and_not_rotated()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner,
            "update identity.refresh_sessions set created_at = now() - interval '2 hours', expires_at = now() - interval '1 second'");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(session.RefreshToken)).StatusCode);

        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.refresh' and metadata->>'reason' = 'expired'"));
    }

    [Fact]
    public async Task An_expired_family_is_rejected_even_though_it_was_never_revoked()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner,
            "update identity.refresh_sessions set created_at = now() - interval '91 days', " +
            "expires_at = now() - interval '2 days', family_expires_at = now() - interval '1 day'");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(session.AccessToken)).StatusCode); // sid check enforces the cap too
    }

    [Fact]
    public async Task Rotation_never_extends_the_family_past_the_90_day_cap()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        // One day of family lifetime left, while a normal token would live 30 more days.
        await Sql.ExecAsync(owner,
            "update identity.refresh_sessions set expires_at = now() + interval '1 day', family_expires_at = now() + interval '1 day'");

        await Scenario.ReadSessionAsync(await app.RefreshAsync(session.RefreshToken));

        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(distinct family_expires_at) from identity.refresh_sessions"));
        Assert.Equal("true", await Sql.ScalarAsync(owner,
            "select (expires_at = family_expires_at)::text from identity.refresh_sessions where rotated_at is null"));
    }

    [Fact]
    public async Task A_revoked_token_is_rejected_without_raising_a_reuse_alarm()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, "update identity.refresh_sessions set revoked_at = now(), revoked_reason = 'test'");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(session.RefreshToken)).StatusCode);

        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.refresh' and metadata->>'reason' = 'revoked'"));
        Assert.Equal(0L, await Sql.ScalarAsync(owner, "select count(*) from audit.events where action = 'auth.session.revoked'"));
    }

    [Fact]
    public async Task A_suspended_user_cannot_refresh_and_the_family_is_revoked()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, "update identity.users set status = 'suspended'");

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(session.RefreshToken)).StatusCode);

        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions where revoked_reason = 'user_inactive'"));
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_ends_the_access_token_immediately()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(session.AccessToken)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(session.RefreshToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(session.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(session.RefreshToken)).StatusCode);
        var owner = app.Db.OwnerConnectionString;
        Assert.Equal(1L, await Sql.ScalarAsync(owner, "select count(*) from audit.events where action = 'auth.logout' and outcome = 'success'"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.session.revoked' and metadata->>'reason' = 'logout'"));
    }

    [Fact]
    public async Task Logout_is_idempotent_and_never_reveals_whether_a_token_exists()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);

        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(session.RefreshToken)).StatusCode);   // already revoked
        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(new string('A', 43))).StatusCode);    // unknown
        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync("garbage")).StatusCode);              // malformed
        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(null)).StatusCode);                   // missing

        Assert.Equal("auth.logout:failure:already_revoked,auth.logout:failure:malformed,auth.logout:failure:malformed,auth.logout:failure:unknown_token,auth.logout:success:",
            await Sql.ScalarAsync(app.Db.OwnerConnectionString,
                "select string_agg(e, ',' order by e collate \"C\") from (select action || ':' || outcome || ':' || coalesce(metadata->>'reason', '') as e " +
                "from audit.events where action = 'auth.logout') x"));
    }

    [Fact]
    public async Task Logging_out_with_a_rotated_token_still_ends_the_whole_session()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var first = await Scenario.LoginAsync(app);
        var second = await Scenario.ReadSessionAsync(await app.RefreshAsync(first.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(first.RefreshToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(second.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(second.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Logout_ends_only_its_own_session_not_the_users_other_sessions()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var phone = await Scenario.LoginAsync(app);
        var laptop = await Scenario.LoginAsync(app);

        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(phone.RefreshToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(phone.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.MeAsync(laptop.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.RefreshAsync(laptop.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task The_refresh_lifecycle_audit_trail_has_the_expected_events_and_never_contains_credentials()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var first = await Scenario.LoginAsync(app);
        var second = await Scenario.ReadSessionAsync(await app.RefreshAsync(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(first.RefreshToken)).StatusCode); // reuse
        var owner = app.Db.OwnerConnectionString;

        Assert.Equal(
            "auth.login:success:,auth.refresh:failure:reuse_detected,auth.refresh:success:,auth.register:success:,auth.session.revoked:success:reuse_detected",
            await Sql.ScalarAsync(owner,
                "select string_agg(e, ',' order by e collate \"C\") from (select action || ':' || outcome || ':' || coalesce(metadata->>'reason', '') as e from audit.events) x"));

        var dump = (string)(await Sql.ScalarAsync(owner, "select coalesce(string_agg(e::text, ' '), '') from audit.events e"))!;
        var secrets = new[]
        {
            Scenario.Password, first.AccessToken, first.RefreshToken, second.AccessToken, second.RefreshToken,
            Scenario.HashHex(first.RefreshToken), Scenario.HashHex(second.RefreshToken)
        };
        for (var i = 0; i < secrets.Length; i++)
            Assert.False(dump.Contains(secrets[i], StringComparison.OrdinalIgnoreCase), $"secret #{i} found in the audit trail");
    }
}