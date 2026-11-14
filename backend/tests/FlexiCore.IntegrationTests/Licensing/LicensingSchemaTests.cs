using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Migrations;
using Npgsql;

namespace FlexiCore.IntegrationTests.Licensing;

[Collection("postgres")]
public sealed class LicensingSchemaTests(PostgresFixture postgres)
{
    private static readonly Guid UserA = Guid.Parse("10000000-0000-0000-0000-0000000000c1");
    private static readonly Guid UserB = Guid.Parse("10000000-0000-0000-0000-0000000000c2");
    private static readonly Guid MemberA = Guid.Parse("10000000-0000-0000-0000-0000000000c3");
    private static readonly Guid OrgA = Guid.Parse("20000000-0000-0000-0000-0000000000c1");
    private static readonly Guid OrgB = Guid.Parse("20000000-0000-0000-0000-0000000000c2");
    private static readonly Guid SnapshotA = Guid.Parse("30000000-0000-0000-0000-0000000000c1");

    private const string CountPending = "SELECT count(*) FROM licensing.pending_revocations";

    [Fact]
    public async Task Fresh_database_has_the_pending_revocation_table_and_migrations_can_run_again()
    {
        var db = await StartAsync();
        Assert.Equal("licensing.pending_revocations", Convert.ToString(
            await Sql.ScalarAsync(db.OwnerConnectionString, "SELECT to_regclass('licensing.pending_revocations')::text")));

        var version = Convert.ToString(
            await Sql.ScalarAsync(db.OwnerConnectionString, "SELECT current_setting('server_version')")) ?? "";
        Assert.StartsWith("18", version);

        await new MigrationRunner(db.OwnerConnectionString).RunAsync();
        Assert.Equal("licensing.pending_revocations", Convert.ToString(
            await Sql.ScalarAsync(db.OwnerConnectionString, "SELECT to_regclass('licensing.pending_revocations')::text")));
    }

    [Fact]
    public async Task Keys_constraints_and_indexes_match_the_migrations()
    {
        var db = await StartAsync();
        var names = new[]
        {
            "fk_licenses_predecessor", "fk_activation_events_license", "fk_renewals_previous", "fk_renewals_new",
            "fk_revocations_license", "fk_revocations_snapshot", "fk_entitlements_license",
            "uq_licenses_id_organization", "uq_revocation_snapshots_id_org", "uq_revocation_snapshots_list",
        };
        var found = Convert.ToInt64(await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid=c.connamespace WHERE n.nspname='licensing' AND c.conname IN ("
            + string.Join(",", names.Select(x => "'" + x + "'")) + ")"));
        Assert.Equal(names.Length, (int)found);

        Assert.Equal(1L, Convert.ToInt64(await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT count(*) FROM pg_indexes WHERE schemaname='licensing' AND indexname='uq_licenses_one_successor'")));

        var definitions = Convert.ToString(await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT string_agg(pg_get_constraintdef(oid), ' | ' ORDER BY contype) FROM pg_constraint "
            + "WHERE conrelid='licensing.pending_revocations'::regclass AND contype IN ('p','f')")) ?? "";
        Assert.Contains("PRIMARY KEY (organization_id, license_id)", definitions);
        Assert.Contains("REFERENCES licensing.revocation_snapshots(id, organization_id)", definitions);
        Assert.Contains("REFERENCES identity.users(id)", definitions);
    }

    [Fact]
    public async Task Every_licensing_table_enforces_row_level_security()
    {
        var db = await StartAsync();
        var withoutSecurity = await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT string_agg(c.relname, ',') FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace "
            + "WHERE n.nspname='licensing' AND c.relkind='r' AND NOT c.relrowsecurity");
        Assert.Null(withoutSecurity);
    }

    [Fact]
    public async Task The_application_role_can_only_read_and_append_licensing_data()
    {
        var db = await StartAsync();
        var writable = await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT string_agg(c.relname || ':' || priv.p, ',') FROM pg_class c "
            + "JOIN pg_namespace n ON n.oid=c.relnamespace "
            + "CROSS JOIN unnest(ARRAY['UPDATE','DELETE','TRUNCATE']) AS priv(p) "
            + "WHERE n.nspname='licensing' AND c.relkind='r' AND has_table_privilege('flexicore_app', c.oid, priv.p)");
        Assert.Null(writable);

        Assert.True(Convert.ToBoolean(await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT has_table_privilege('flexicore_app','licensing.pending_revocations','SELECT') "
            + "AND has_table_privilege('flexicore_app','licensing.pending_revocations','INSERT')")));
    }

    [Fact]
    public async Task Pending_revocation_policies_isolate_tenants_and_admit_only_owners_and_admins()
    {
        var db = await StartAsync();
        await AsUserAsync(db, UserA, InsertFor("LIC-SCHEMA-1"));

        Assert.Equal(1L, Convert.ToInt64(await AsUserAsync(db, UserA, CountPending)));
        Assert.Equal(1L, Convert.ToInt64(await AsUserAsync(db, MemberA, CountPending)));
        Assert.Equal(0L, Convert.ToInt64(await AsUserAsync(db, UserB, CountPending)));
        Assert.Equal(0L, Convert.ToInt64(await AsUserAsync(db, null, CountPending)));

        var outsider = await Assert.ThrowsAsync<PostgresException>(() => AsUserAsync(db, UserB, InsertFor("LIC-SCHEMA-2")));
        Assert.Equal("42501", outsider.SqlState);
        var member = await Assert.ThrowsAsync<PostgresException>(() => AsUserAsync(db, MemberA, InsertFor("LIC-SCHEMA-3")));
        Assert.Equal("42501", member.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(
            () => AsUserAsync(db, UserA, "DELETE FROM licensing.pending_revocations"));
        Assert.Equal("42501", delete.SqlState);
        var update = await Assert.ThrowsAsync<PostgresException>(
            () => AsUserAsync(db, UserA, "UPDATE licensing.pending_revocations SET reason='changed'"));
        Assert.Equal("42501", update.SqlState);
    }

    [Fact]
    public async Task Duplicate_revocations_are_rejected_and_a_rolled_back_insert_leaves_nothing()
    {
        var db = await StartAsync();

        await using (var connection = new NpgsqlConnection(db.AppConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var setting = new NpgsqlCommand("SELECT set_config('app.user_id', @user, true)", connection, transaction))
            {
                setting.Parameters.AddWithValue("user", UserA.ToString());
                await setting.ExecuteScalarAsync();
            }
            await using (var insert = new NpgsqlCommand(InsertFor("LIC-ROLLBACK"), connection, transaction))
                await insert.ExecuteNonQueryAsync();
            await transaction.RollbackAsync();
        }
        Assert.Equal(0L, Convert.ToInt64(await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT count(*) FROM licensing.pending_revocations WHERE license_id='LIC-ROLLBACK'")));

        await AsUserAsync(db, UserA, InsertFor("LIC-DUP"));
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => AsUserAsync(db, UserA, InsertFor("LIC-DUP")));
        Assert.Equal("23505", duplicate.SqlState);
    }

    private async Task<TestDatabase> StartAsync()
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();
        await Sql.ExecAsync(db.OwnerConnectionString, $"""
            INSERT INTO identity.users (id,email,email_normalized,password_hash) VALUES
              ('{UserA}','a@example.test','a@example.test','test'),
              ('{UserB}','b@example.test','b@example.test','test'),
              ('{MemberA}','m@example.test','m@example.test','test');
            INSERT INTO tenancy.organizations(id,name,slug) VALUES
              ('{OrgA}','Schema A','schema-a'),
              ('{OrgB}','Schema B','schema-b');
            INSERT INTO tenancy.organization_memberships(organization_id,user_id,role) VALUES
              ('{OrgA}','{UserA}','owner'),
              ('{OrgA}','{MemberA}','member'),
              ('{OrgB}','{UserB}','owner');
            INSERT INTO licensing.revocation_snapshots
              (id, organization_id, list_id, signing_key_id, issued_at, next_update, signed_snapshot, snapshot_sha256, imported_by)
            VALUES ('{SnapshotA}','{OrgA}','{SnapshotA}','test-key-1', now() - interval '1 hour',
                    now() + interval '6 days', '\x00'::bytea, repeat('a',64), '{UserA}');
            """);
        return db;
    }

    private static string InsertFor(string licenseId) =>
        "INSERT INTO licensing.pending_revocations(organization_id, license_id, snapshot_id, revoked_at, reason, authorized_by) "
        + "VALUES ('" + OrgA + "','" + licenseId + "','" + SnapshotA + "', now(), 'schema test', '" + UserA + "')";

    private static async Task<object?> AsUserAsync(TestDatabase db, Guid? user, string sql)
    {
        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        if (user is Guid id)
        {
            await using var setting = new NpgsqlCommand("SELECT set_config('app.user_id', @user, true)", connection, transaction);
            setting.Parameters.AddWithValue("user", id.ToString());
            await setting.ExecuteScalarAsync();
        }
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var result = await command.ExecuteScalarAsync();
        await transaction.CommitAsync();
        return result is DBNull ? null : result;
    }
}
