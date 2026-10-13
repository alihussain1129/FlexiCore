using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Migrations;
using FlexiCore.Tenancy;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenantContextTests(PostgresFixture postgres)
{
    private static readonly Guid UserA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserB = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CompanyA = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanyB = Guid.Parse("30000000-0000-0000-0000-000000000002");

    private async Task<TestDatabase> CreateSeededDatabaseAsync()
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();

        const string seed = """
            INSERT INTO identity.users (id, email, email_normalized, password_hash)
            VALUES
                ('10000000-0000-0000-0000-000000000001', 'user-a@example.test', 'USER-A@EXAMPLE.TEST', 'test-hash-a'),
                ('10000000-0000-0000-0000-000000000002', 'user-b@example.test', 'USER-B@EXAMPLE.TEST', 'test-hash-b');

            INSERT INTO tenancy.organizations (id, name, slug)
            VALUES ('20000000-0000-0000-0000-000000000001', 'Test Organization', 'test-organization');

            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            VALUES
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'owner'),
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'member');

            INSERT INTO tenancy.companies (id, organization_id, name, slug, industry_key)
            VALUES
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Company A', 'company-a', 'retail'),
                ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', 'Company B', 'company-b', 'retail');

            INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role)
            VALUES
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'owner'),
                ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'owner');

            INSERT INTO tenancy.branches (id, company_id, name, code)
            VALUES
                ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 'Branch A', 'A-1'),
                ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000002', 'Branch B', 'B-1');
            """;

        await Sql.ExecAsync(db.OwnerConnectionString, seed);
        return db;
    }

    private static async Task<List<string>> ReadBranchCodesAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(
            "SELECT code FROM tenancy.branches ORDER BY code", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var codes = new List<string>();
        while (await reader.ReadAsync())
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }

    private static async Task<List<string>> BranchCodesForAsync(
        string appConnectionString, TenantContext context)
    {
        await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
        var factory = new TenantConnectionFactory(dataSource);
        await using var scope = await factory.OpenAsync(context);
        var codes = await ReadBranchCodesAsync(scope.Connection, scope.Transaction);
        await scope.CommitAsync();
        return codes;
    }

    private static string PooledConnectionString(string appConnectionString) =>
        new NpgsqlConnectionStringBuilder(appConnectionString)
        {
            Pooling = true,
            MinPoolSize = 0,
            MaxPoolSize = 1,       // guarantees the next open reuses the same physical connection
            NoResetOnClose = true  // Npgsql must not hide a leak by resetting session state itself
        }.ConnectionString;

    private static async Task<(int Pid, string? UserSetting)> ReadPidAndUserSettingAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_backend_pid(), current_setting('app.user_id', true)", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    [Fact]
    public async Task Factory_context_exposes_only_the_selected_companys_branch()
    {
        var db = await CreateSeededDatabaseAsync();

        var codes = await BranchCodesForAsync(db.AppConnectionString, TenantContext.For(UserA, CompanyA));

        Assert.Equal(new List<string> { "A-1" }, codes);
    }

    [Fact]
    public async Task Selecting_a_company_the_user_does_not_belong_to_exposes_nothing()
    {
        var db = await CreateSeededDatabaseAsync();

        var codes = await BranchCodesForAsync(db.AppConnectionString, TenantContext.For(UserA, CompanyB));

        Assert.Empty(codes);
    }

    [Fact]
    public async Task No_selected_company_exposes_no_branches()
    {
        var db = await CreateSeededDatabaseAsync();

        var codes = await BranchCodesForAsync(db.AppConnectionString, TenantContext.For(UserA));

        Assert.Empty(codes);
    }

    [Fact]
    public async Task A_different_user_cannot_read_another_companys_branch_by_selecting_it()
    {
        var db = await CreateSeededDatabaseAsync();

        var codes = await BranchCodesForAsync(db.AppConnectionString, TenantContext.For(UserB, CompanyA));

        Assert.Empty(codes);
    }

    [Fact]
    public async Task Context_does_not_leak_to_the_next_use_of_a_pooled_connection_after_commit()
    {
        var db = await CreateSeededDatabaseAsync();
        await using var dataSource = NpgsqlDataSource.Create(PooledConnectionString(db.AppConnectionString));
        var factory = new TenantConnectionFactory(dataSource);

        int firstPid;
        await using (var scope = await factory.OpenAsync(TenantContext.For(UserA, CompanyA)))
        {
            var inside = await ReadPidAndUserSettingAsync(scope.Connection, scope.Transaction);
            firstPid = inside.Pid;
            Assert.Equal(UserA.ToString(), inside.UserSetting);
            Assert.Equal(new List<string> { "A-1" }, await ReadBranchCodesAsync(scope.Connection, scope.Transaction));
            await scope.CommitAsync();
        }

        await using var next = await dataSource.OpenConnectionAsync();
        var after = await ReadPidAndUserSettingAsync(next, null);

        Assert.Equal(firstPid, after.Pid); // proves this really is the same physical connection
        Assert.True(string.IsNullOrEmpty(after.UserSetting), "app.user_id leaked to the next pooled use.");
        Assert.Empty(await ReadBranchCodesAsync(next, null));
    }

    [Fact]
    public async Task Context_does_not_leak_to_the_next_use_of_a_pooled_connection_after_rollback()
    {
        var db = await CreateSeededDatabaseAsync();
        await using var dataSource = NpgsqlDataSource.Create(PooledConnectionString(db.AppConnectionString));
        var factory = new TenantConnectionFactory(dataSource);

        int firstPid;
        await using (var scope = await factory.OpenAsync(TenantContext.For(UserA, CompanyA)))
        {
            firstPid = (await ReadPidAndUserSettingAsync(scope.Connection, scope.Transaction)).Pid;
            // Disposed without commit: the transaction rolls back.
        }

        await using var next = await dataSource.OpenConnectionAsync();
        var after = await ReadPidAndUserSettingAsync(next, null);

        Assert.Equal(firstPid, after.Pid);
        Assert.True(string.IsNullOrEmpty(after.UserSetting), "app.user_id leaked after rollback.");
        Assert.Empty(await ReadBranchCodesAsync(next, null));
    }

    [Fact]
    public void An_empty_user_id_is_rejected() =>
        Assert.Throws<ArgumentException>(() => TenantContext.For(Guid.Empty));

    [Fact]
    public void An_empty_company_id_is_rejected() =>
        Assert.Throws<ArgumentException>(() => TenantContext.For(UserA, Guid.Empty));
}