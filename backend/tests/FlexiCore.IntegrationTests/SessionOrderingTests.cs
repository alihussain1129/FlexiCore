using System.Net;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;
using NpgsqlTypes;
using static FlexiCore.IntegrationTests.SessionTestHelpers;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class SessionOrderingTests(PostgresFixture postgres)
{
    private const string BlockedAdvisoryLocksSql =
        "select count(*) from pg_locks where locktype = 'advisory' and not granted " +
        "and database = (select oid from pg_database where datname = current_database())";

    private static async Task<string> FamilyIdAsync(IdentityTestApp app) =>
        (string)(await Sql.ScalarAsync(app.Db.OwnerConnectionString, "select family_id::text from identity.refresh_sessions"))!;

    private static async Task ExecuteWithFamilyAsync(NpgsqlConnection connection, string sql, string family)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("family", NpgsqlDbType.Text) { Value = family });
        await command.ExecuteNonQueryAsync();
    }

    private static async Task WaitForBlockedAsync(IdentityTestApp app, long expected)
    {
        for (var i = 0; i < 200; i++)
        {
            var blocked = (long)(await Sql.ScalarAsync(app.Db.OwnerConnectionString, BlockedAdvisoryLocksSql))!;
            if (blocked >= expected) return;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Expected {expected} request(s) waiting on the session family lock.");
    }

    /// <summary>
    /// Holds the family's advisory lock from a test connection, starts the two requests one at a time (each is seen
    /// waiting in pg_locks before the next starts), then releases the lock. The first request therefore wins.
    /// </summary>
    private static async Task<(HttpResponseMessage First, HttpResponseMessage Second)> RunInOrderAsync(
        IdentityTestApp app, Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second)
    {
        var family = await FamilyIdAsync(app);
        await using var holder = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await holder.OpenAsync();
        await ExecuteWithFamilyAsync(holder, "select pg_advisory_lock(hashtextextended(@family, 0))", family);

        Task<HttpResponseMessage> firstTask;
        Task<HttpResponseMessage> secondTask;
        try
        {
            firstTask = first();
            await WaitForBlockedAsync(app, 1);
            secondTask = second();
            await WaitForBlockedAsync(app, 2);
        }
        finally
        {
            await ExecuteWithFamilyAsync(holder, "select pg_advisory_unlock(hashtextextended(@family, 0))", family);
        }

        return (await firstTask, await secondTask);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_refresh_that_wins_the_lock_is_revoked_by_the_logout_queued_behind_it(bool repeatableReadDefault)
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        if (repeatableReadDefault) await UseRepeatableReadByDefaultAsync(app);
        var session = await Scenario.LoginAsync(app);

        var (refresh, logout) = await RunInOrderAsync(app,
            () => app.RefreshAsync(session.RefreshToken),
            () => app.LogoutAsync(session.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var issued = await Scenario.ReadSessionAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.RefreshAsync(issued.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.MeAsync(issued.AccessToken)).StatusCode);
        Assert.Equal(0L, await UnrevokedAsync(app));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_logout_that_wins_the_lock_makes_the_refresh_queued_behind_it_fail(bool repeatableReadDefault)
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        if (repeatableReadDefault) await UseRepeatableReadByDefaultAsync(app);
        var session = await Scenario.LoginAsync(app);

        var (logout, refresh) = await RunInOrderAsync(app,
            () => app.LogoutAsync(session.RefreshToken),
            () => app.RefreshAsync(session.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal(0L, await UnrevokedAsync(app));
    }
}