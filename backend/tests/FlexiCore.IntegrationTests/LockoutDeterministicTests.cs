using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;
using static FlexiCore.IntegrationTests.SessionTestHelpers;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LockoutDeterministicTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_failed_login_queued_behind_another_update_of_the_user_row_is_still_counted_under_a_repeatable_read_default()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        await UseRepeatableReadByDefaultAsync(app);
        var owner = app.Db.OwnerConnectionString;

        await using var holder = new NpgsqlConnection(owner);
        await holder.OpenAsync();
        await using var tx = await holder.BeginTransactionAsync();
        await using (var update = new NpgsqlCommand(
            "update identity.users set failed_login_count = failed_login_count + 1", holder, tx))
        {
            await update.ExecuteNonQueryAsync();
        }

        var login = app.LoginAsync(Scenario.Email, "wrong-password-" + Guid.NewGuid());

        var waiting = false;
        for (var i = 0; i < 200 && !waiting; i++)
        {
            waiting = Convert.ToInt64(await Sql.ScalarAsync(owner,
                "select count(*) from pg_locks where not granted and locktype in ('transactionid', 'tuple')")) > 0;
            if (!waiting) await Task.Delay(50);
        }
        var activity = await Sql.ScalarAsync(owner,
            "select string_agg(pid || ' | ' || coalesce(state, '-') || ' | ' || coalesce(wait_event_type, '-') || ' | ' || coalesce(wait_event, '-') || ' | ' || left(replace(query, chr(10), ' '), 70), chr(10)) " +
            "from pg_stat_activity where datname = current_database() and pid <> pg_backend_pid()");
        Assert.True(waiting, $"the login never reached the row lock. loginCompleted={login.IsCompleted}{Environment.NewLine}{activity}");

        await tx.CommitAsync();
        var response = await login;

        var count = Convert.ToInt64(await Sql.ScalarAsync(owner, "select failed_login_count from identity.users"));
        Assert.True(count == 2, $"expected 2 (holder + login), got {count}; login status {(int)response.StatusCode}");
    }
}