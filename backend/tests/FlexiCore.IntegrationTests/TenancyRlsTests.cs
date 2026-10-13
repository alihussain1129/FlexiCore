using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Migrations;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenancyRlsTests(PostgresFixture postgres)
{
    private const string UserA = "10000000-0000-0000-0000-000000000001";
    private const string UserB = "10000000-0000-0000-0000-000000000002";
    private const string Organization = "20000000-0000-0000-0000-000000000001";
    private const string CompanyA = "30000000-0000-0000-0000-000000000001";
    private const string CompanyB = "30000000-0000-0000-0000-000000000002";
    private const string BranchA = "40000000-0000-0000-0000-000000000001";
    private const string BranchB = "40000000-0000-0000-0000-000000000002";

    private async Task<TestDatabase> CreateSeededDatabaseAsync()
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();

        const string seed = """
            INSERT INTO identity.users
                (id, email, email_normalized, password_hash)
            VALUES
                ('10000000-0000-0000-0000-000000000001',
                 'user-a@example.test', 'USER-A@EXAMPLE.TEST', 'test-hash-a'),
                ('10000000-0000-0000-0000-000000000002',
                 'user-b@example.test', 'USER-B@EXAMPLE.TEST', 'test-hash-b');

            INSERT INTO tenancy.organizations (id, name, slug)
            VALUES ('20000000-0000-0000-0000-000000000001',
                    'Test Organization', 'test-organization');

            INSERT INTO tenancy.organization_memberships
                (organization_id, user_id, role)
            VALUES
                ('20000000-0000-0000-0000-000000000001',
                 '10000000-0000-0000-0000-000000000001', 'owner'),
                ('20000000-0000-0000-0000-000000000001',
                 '10000000-0000-0000-0000-000000000002', 'member');

            INSERT INTO tenancy.companies
                (id, organization_id, name, slug, industry_key)
            VALUES
                ('30000000-0000-0000-0000-000000000001',
                 '20000000-0000-0000-0000-000000000001',
                 'Company A', 'company-a', 'retail'),
                ('30000000-0000-0000-0000-000000000002',
                 '20000000-0000-0000-0000-000000000001',
                 'Company B', 'company-b', 'retail');

            INSERT INTO tenancy.company_memberships
                (company_id, organization_id, user_id, role)
            VALUES
                ('30000000-0000-0000-0000-000000000001',
                 '20000000-0000-0000-0000-000000000001',
                 '10000000-0000-0000-0000-000000000001', 'owner'),
                ('30000000-0000-0000-0000-000000000002',
                 '20000000-0000-0000-0000-000000000001',
                 '10000000-0000-0000-0000-000000000002', 'owner');

            INSERT INTO tenancy.branches (id, company_id, name, code)
            VALUES
                ('40000000-0000-0000-0000-000000000001',
                 '30000000-0000-0000-0000-000000000001',
                 'Branch A', 'A-1'),
                ('40000000-0000-0000-0000-000000000002',
                 '30000000-0000-0000-0000-000000000002',
                 'Branch B', 'B-1');
            """;

        await Sql.ExecAsync(db.OwnerConnectionString, seed);
        return db;
    }

    private static async Task SetContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string userId,
        string? companyId)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT set_config('app.user_id', @user_id, true),
                   set_config('app.company_id', @company_id, true);
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue(
            "company_id", companyId ?? string.Empty);

        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task User_sees_only_branches_for_selected_company_and_membership()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetContextAsync(connection, transaction, UserA, CompanyA);

            await using var command = new NpgsqlCommand(
                "SELECT code FROM tenancy.branches ORDER BY code",
                connection, transaction);

            await using var reader = await command.ExecuteReaderAsync();
            var codes = new List<string>();

            while (await reader.ReadAsync())
                codes.Add(reader.GetString(0));

            Assert.Equal(["A-1"], codes);
            await reader.CloseAsync();
            await transaction.CommitAsync();
        }

        // User A is not a member of Company B. Changing the selected company
        // must not grant access to its branch.
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetContextAsync(connection, transaction, UserA, CompanyB);

            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM tenancy.branches",
                connection, transaction);

            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            await transaction.CommitAsync();
        }
    }

    [Fact]
    public async Task Missing_context_exposes_no_branch_rows()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.branches", connection);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Transaction_local_context_does_not_leak_to_next_transaction()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetContextAsync(connection, transaction, UserA, CompanyA);

            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM tenancy.branches",
                connection, transaction);

            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
            await transaction.CommitAsync();
        }

        // Reuse the same open connection, but start a new transaction without
        // restoring context. Transaction-local settings should now be absent.
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM tenancy.branches",
                connection, transaction);

            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
            await transaction.CommitAsync();
        }
    }

    [Fact]
    public async Task Runtime_role_cannot_bypass_rls_or_truncate_branches()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            SELECT r.rolbypassrls,
                   pg_has_role(current_user, c.relowner, 'MEMBER'),
                   has_table_privilege(current_user,
                       'tenancy.branches', 'TRUNCATE')
            FROM pg_roles r
            CROSS JOIN pg_class c
            WHERE r.rolname = current_user
              AND c.oid = 'tenancy.branches'::regclass;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
    }

    [Fact]
    public async Task User_cannot_insert_branch_for_another_company()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyB);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO tenancy.branches (company_id, name, code)
            VALUES (@company_id, 'Unauthorized Branch', 'BAD-1');
            """,
            connection, transaction);

        command.Parameters.AddWithValue("company_id", Guid.Parse(CompanyB));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync());

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
    [Fact]
    public async Task Suspended_company_exposes_no_branch_rows()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.companies SET status = 'suspended' " +
            "WHERE id = '30000000-0000-0000-0000-000000000001'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.branches",
            connection, transaction);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Suspended_organization_exposes_no_branch_rows()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.organizations SET status = 'suspended' " +
            "WHERE id = '20000000-0000-0000-0000-000000000001'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.branches",
            connection, transaction);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Suspended_organization_membership_exposes_no_branch_rows()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.organization_memberships SET status = 'suspended' " +
            "WHERE organization_id = '" + Organization + "' " +
            "AND user_id = '" + UserA + "'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.branches",
            connection, transaction);

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Active_company_is_visible_to_its_member()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.companies WHERE id = @company_id",
            connection, transaction);
        command.Parameters.AddWithValue("company_id", Guid.Parse(CompanyA));

        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Suspended_company_membership_hides_company()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.company_memberships SET status = 'suspended' " +
            "WHERE company_id = '" + CompanyA + "' " +
            "AND user_id = '" + UserA + "'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.companies WHERE id = @company_id",
            connection, transaction);
        command.Parameters.AddWithValue("company_id", Guid.Parse(CompanyA));

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Suspended_parent_organization_hides_company()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.organizations SET status = 'suspended' " +
            "WHERE id = '" + Organization + "'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.companies WHERE id = @company_id",
            connection, transaction);
        command.Parameters.AddWithValue("company_id", Guid.Parse(CompanyA));

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Suspended_organization_membership_hides_company()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.organization_memberships SET status = 'suspended' " +
            "WHERE organization_id = '" + Organization + "' " +
            "AND user_id = '" + UserA + "'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenancy.companies WHERE id = @company_id",
            connection, transaction);
        command.Parameters.AddWithValue("company_id", Guid.Parse(CompanyA));

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task Unauthorized_user_cannot_update_another_company_branch()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "UPDATE tenancy.branches SET name = 'Tampered' WHERE id = @branch_id",
            connection, transaction);
        command.Parameters.AddWithValue("branch_id", Guid.Parse(BranchB));

        var affected = await command.ExecuteNonQueryAsync();

        Assert.Equal(0, affected);
        await transaction.CommitAsync();

        var name = await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT name FROM tenancy.branches WHERE id = '" + BranchB + "'");

        Assert.Equal("Branch B", name);
    }

    [Fact]
    public async Task Unauthorized_user_cannot_delete_another_company_branch()
    {
        var db = await CreateSeededDatabaseAsync();

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var command = new NpgsqlCommand(
            "DELETE FROM tenancy.branches WHERE id = @branch_id",
            connection, transaction);
        command.Parameters.AddWithValue("branch_id", Guid.Parse(BranchB));

        var affected = await command.ExecuteNonQueryAsync();

        Assert.Equal(0, affected);
        await transaction.CommitAsync();

        var count = await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT count(*) FROM tenancy.branches WHERE id = '" + BranchB + "'");

        Assert.Equal(1L, Convert.ToInt64(count));
    }

    [Fact]
    public async Task Suspended_company_branch_cannot_be_modified()
    {
        var db = await CreateSeededDatabaseAsync();

        await Sql.ExecAsync(db.OwnerConnectionString,
            "UPDATE tenancy.companies SET status = 'suspended' " +
            "WHERE id = '" + CompanyA + "'");

        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await SetContextAsync(connection, transaction, UserA, CompanyA);

        await using var update = new NpgsqlCommand(
            "UPDATE tenancy.branches SET name = 'Tampered' WHERE id = @branch_id",
            connection, transaction);
        update.Parameters.AddWithValue("branch_id", Guid.Parse(BranchA));

        var updated = await update.ExecuteNonQueryAsync();
        Assert.Equal(0, updated);

        await using var delete = new NpgsqlCommand(
            "DELETE FROM tenancy.branches WHERE id = @branch_id",
            connection, transaction);
        delete.Parameters.AddWithValue("branch_id", Guid.Parse(BranchA));

        var deleted = await delete.ExecuteNonQueryAsync();
        Assert.Equal(0, deleted);

        await transaction.CommitAsync();

        var name = await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT name FROM tenancy.branches WHERE id = '" + BranchA + "'");

        Assert.Equal("Branch A", name);
    }
}