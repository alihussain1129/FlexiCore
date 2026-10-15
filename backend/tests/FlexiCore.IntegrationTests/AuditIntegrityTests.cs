using System.Net;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Tenancy.Data;
using Npgsql;
using static FlexiCore.IntegrationTests.SessionTestHelpers;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class AuditIntegrityTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_audit_table_has_no_foreign_keys_so_it_outlives_the_rows_it_describes()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);

        Assert.Equal(0L, await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            "select count(*) from pg_constraint where conrelid = 'audit.events'::regclass and contype = 'f'"));
    }

    [Fact]
    public async Task A_refusal_row_can_be_written_for_an_actor_that_does_not_exist()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        await using var dataSource = NpgsqlDataSource.Create(app.Db.AppConnectionString);
        var unknownActor = Guid.NewGuid();

        await new TenancyAuditWriter(dataSource).WriteAsync(
            new TenancyAuditEvent("tenancy.organization.create", "denied", unknownActor, "organization", null,
                "audit-actor-test", new Dictionary<string, string> { ["reason"] = "no_active_user" }),
            CancellationToken.None);

        Assert.Equal(1L, await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            $"select count(*) from audit.events where actor_user_id = '{unknownActor}'::uuid " +
            "and outcome = 'denied' and metadata->>'reason' = 'no_active_user'"));
    }

    [Fact]
    public async Task A_suspended_users_refresh_is_refused_audited_and_revokes_the_session()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, "update identity.users set status = 'suspended'");

        var refresh = await app.RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.refresh' and outcome = 'failure' and metadata->>'reason' = 'user_inactive'"));
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.session.revoked' and outcome = 'success' and metadata->>'reason' = 'user_inactive'"));
        Assert.Equal(0L, await UnrevokedAsync(app));
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(session.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task A_suspended_users_login_is_refused_with_the_generic_401_and_audited()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = app.Db.OwnerConnectionString;
        await Sql.ExecAsync(owner, "update identity.users set status = 'suspended'");

        var login = await app.LoginAsync(Scenario.Email, Scenario.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(1L, await Sql.ScalarAsync(owner,
            "select count(*) from audit.events where action = 'auth.login' and outcome = 'failure' and metadata->>'reason' = 'account_inactive'"));
        Assert.Equal(0L, await Sql.ScalarAsync(owner, "select count(*) from identity.refresh_sessions"));
    }
}