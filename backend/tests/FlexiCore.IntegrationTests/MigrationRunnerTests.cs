using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Migrations;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class MigrationRunnerTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Applies_baseline_once_and_is_idempotent()
    {
        var db = await postgres.CreateDatabaseAsync();
        var runner = new MigrationRunner(db.OwnerConnectionString);

        var first = await runner.RunAsync();
        var second = await runner.RunAsync();

        Assert.True(first >= 1);
        Assert.Equal(0, second);
        Assert.Equal("0001_baseline",
            (string?)await Sql.ScalarAsync(db.OwnerConnectionString,
                "select version from public.schema_migrations order by version limit 1"));
    }

    [Fact]
    public async Task App_role_can_use_owner_tables_but_cannot_create_its_own()
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();

        await Sql.ExecAsync(db.OwnerConnectionString, "create table public.widget (id int primary key, name text)");
        await Sql.ExecAsync(db.AppConnectionString, "insert into public.widget values (1, 'a')");
        Assert.Equal("a", (string?)await Sql.ScalarAsync(db.AppConnectionString, "select name from public.widget where id = 1"));

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => Sql.ExecAsync(db.AppConnectionString, "create table public.nope (x int)"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task A_failing_script_rolls_back_completely_and_is_not_journaled()
    {
        var db = await postgres.CreateDatabaseAsync();
        var runner = new MigrationRunner(db.OwnerConnectionString);

        await Assert.ThrowsAsync<PostgresException>(() => runner.RunAsync(
            [new("0001_bad", "create table public.should_rollback (x int); select 1/0;")]));

        Assert.Null(await Sql.ScalarAsync(db.OwnerConnectionString, "select to_regclass('public.should_rollback')::text"));
        Assert.Equal(0L, await Sql.ScalarAsync(db.OwnerConnectionString, "select count(*) from public.schema_migrations"));
    }

    [Fact]
    public async Task A_script_changed_after_it_was_applied_is_rejected()
    {
        var db = await postgres.CreateDatabaseAsync();
        var runner = new MigrationRunner(db.OwnerConnectionString);
        await runner.RunAsync([new("0001_x", "select 1")]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync([new("0001_x", "select 2")]));
        Assert.Contains("changed after it was applied", ex.Message);
    }

    [Fact]
    public async Task A_database_ahead_of_this_build_is_refused()
    {
        var db = await postgres.CreateDatabaseAsync();
        var runner = new MigrationRunner(db.OwnerConnectionString);
        await runner.RunAsync([new("0001_x", "select 1"), new("0002_y", "select 1")]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync([new("0001_x", "select 1")]));
        Assert.Contains("0002_y", ex.Message);
    }

    [Fact]
    public async Task Concurrent_runners_apply_each_migration_exactly_once()
    {
        var db = await postgres.CreateDatabaseAsync();
        var a = new MigrationRunner(db.OwnerConnectionString);
        var b = new MigrationRunner(db.OwnerConnectionString);

        var counts = await Task.WhenAll(a.RunAsync(), b.RunAsync());

        Assert.Equal(MigrationRunner.LoadEmbeddedScripts().Count, counts.Sum());
    }
}