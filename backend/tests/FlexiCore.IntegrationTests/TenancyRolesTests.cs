using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Migrations;
using FlexiCore.Tenancy;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenancyRolesTests(PostgresFixture postgres)
{
    private const string OwnerId = "10000000-0000-0000-0000-000000000001";
    private const string AdminId = "10000000-0000-0000-0000-000000000002";
    private const string MemberId = "10000000-0000-0000-0000-000000000003";
    private const string OutsiderId = "10000000-0000-0000-0000-000000000004";

    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanyA = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid CompanyB = Guid.Parse("30000000-0000-0000-0000-000000000002");

    private async Task<TestDatabase> CreateSeededDatabaseAsync()
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();

        const string seed = """
            INSERT INTO identity.users (id, email, email_normalized, password_hash)
            VALUES
                ('10000000-0000-0000-0000-000000000001', 'owner@example.test', 'OWNER@EXAMPLE.TEST', 'test-hash'),
                ('10000000-0000-0000-0000-000000000002', 'admin@example.test', 'ADMIN@EXAMPLE.TEST', 'test-hash'),
                ('10000000-0000-0000-0000-000000000003', 'member@example.test', 'MEMBER@EXAMPLE.TEST', 'test-hash'),
                ('10000000-0000-0000-0000-000000000004', 'outsider@example.test', 'OUTSIDER@EXAMPLE.TEST', 'test-hash');

            INSERT INTO tenancy.organizations (id, name, slug)
            VALUES ('20000000-0000-0000-0000-000000000001', 'Test Organization', 'test-organization');

            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            VALUES
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'owner'),
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'admin'),
                ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', 'member');

            INSERT INTO tenancy.companies (id, organization_id, name, slug, industry_key)
            VALUES
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', 'Company A', 'company-a', 'retail'),
                ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', 'Company B', 'company-b', 'retail');

            INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role)
            VALUES
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'owner'),
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'admin'),
                ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', 'member'),
                ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'owner');

            INSERT INTO tenancy.branches (id, company_id, name, code)
            VALUES
                ('40000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 'Branch A', 'A-1'),
                ('40000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000002', 'Branch B', 'B-1');
            """;

        await Sql.ExecAsync(db.OwnerConnectionString, seed);
        return db;
    }

    private static async Task<T> InScopeAsync<T>(
        string appConnectionString,
        TenantContext context,
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work,
        bool commit = false)
    {
        await using var dataSource = NpgsqlDataSource.Create(appConnectionString);
        var factory = new TenantConnectionFactory(dataSource);
        await using var scope = await factory.OpenAsync(context);
        var result = await work(scope.Connection, scope.Transaction);
        if (commit)
        {
            await scope.CommitAsync();
        }

        return result;
    }

    private static async Task<int> ExecAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql,
        params (string Name, object Value)[] args)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql,
        params (string Name, object Value)[] args)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in args)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteScalarAsync();
    }

    private static async Task<List<string>> ReadCodesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
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

    private static Task<Guid> CreateCompanyAsync(string appConnectionString, string userId, string suffix) =>
        InScopeAsync(
            appConnectionString,
            TenantContext.For(Guid.Parse(userId)),
            async (conn, tx) =>
            {
                var value = await ScalarAsync(
                    conn, tx,
                    "SELECT tenancy.create_company(@org, @name, @slug, @industry)",
                    ("org", OrganizationId), ("name", "New Company"), ("slug", "co-" + suffix), ("industry", "retail"));
                return (Guid)value!;
            },
            commit: true);

    [Fact]
    public async Task A_plain_member_can_read_branches_but_cannot_insert_one()
    {
        var db = await CreateSeededDatabaseAsync();
        var context = TenantContext.For(Guid.Parse(MemberId), CompanyA);

        var codes = await InScopeAsync(db.AppConnectionString, context, (conn, tx) => ReadCodesAsync(conn, tx));
        Assert.Equal(new List<string> { "A-1" }, codes);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InScopeAsync(
            db.AppConnectionString, context,
            (conn, tx) => ExecAsync(
                conn, tx,
                "INSERT INTO tenancy.branches (company_id, name, code) VALUES (@c, 'X', 'A-9')",
                ("c", CompanyA))));
        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task A_plain_member_cannot_update_or_delete_branches()
    {
        var db = await CreateSeededDatabaseAsync();
        var context = TenantContext.For(Guid.Parse(MemberId), CompanyA);

        var updated = await InScopeAsync(db.AppConnectionString, context,
            (conn, tx) => ExecAsync(conn, tx, "UPDATE tenancy.branches SET name = 'Hacked' WHERE code = 'A-1'"));
        var deleted = await InScopeAsync(db.AppConnectionString, context,
            (conn, tx) => ExecAsync(conn, tx, "DELETE FROM tenancy.branches WHERE code = 'A-1'"));

        Assert.Equal(0, updated);
        Assert.Equal(0, deleted);
        Assert.Equal("Branch A", await Sql.ScalarAsync(
            db.OwnerConnectionString, "SELECT name FROM tenancy.branches WHERE code = 'A-1'"));
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData(AdminId)]
    public async Task Company_owners_and_admins_can_insert_update_and_delete_branches(string userId)
    {
        var db = await CreateSeededDatabaseAsync();
        var context = TenantContext.For(Guid.Parse(userId), CompanyA);

        var result = await InScopeAsync(db.AppConnectionString, context, async (conn, tx) =>
        {
            var inserted = await ExecAsync(
                conn, tx,
                "INSERT INTO tenancy.branches (company_id, name, code) VALUES (@c, 'Second', 'A-2')",
                ("c", CompanyA));
            var updated = await ExecAsync(conn, tx, "UPDATE tenancy.branches SET name = 'Renamed' WHERE code = 'A-2'");
            var deleted = await ExecAsync(conn, tx, "DELETE FROM tenancy.branches WHERE code = 'A-2'");
            return (inserted, updated, deleted);
        });

        Assert.Equal((1, 1, 1), result);
    }

    [Fact]
    public async Task An_admin_cannot_insert_a_branch_into_a_different_company()
    {
        var db = await CreateSeededDatabaseAsync();
        var context = TenantContext.For(Guid.Parse(AdminId), CompanyA);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InScopeAsync(
            db.AppConnectionString, context,
            (conn, tx) => ExecAsync(
                conn, tx,
                "INSERT INTO tenancy.branches (company_id, name, code) VALUES (@c, 'Sneaky', 'B-9')",
                ("c", CompanyB))));

        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Creating_an_organization_makes_the_caller_its_only_owner_and_it_is_visible_to_them()
    {
        var db = await CreateSeededDatabaseAsync();
        var outsider = TenantContext.For(Guid.Parse(OutsiderId));

        var orgId = await InScopeAsync(db.AppConnectionString, outsider, async (conn, tx) =>
            (Guid)(await ScalarAsync(
                conn, tx, "SELECT tenancy.create_organization(@n, @s)",
                ("n", "New Org"), ("s", "new-org")))!,
            commit: true);

        Assert.Equal("owner", await Sql.ScalarAsync(db.OwnerConnectionString,
            $"SELECT role FROM tenancy.organization_memberships WHERE organization_id = '{orgId}' AND user_id = '{OutsiderId}'"));
        Assert.Equal(1L, await Sql.ScalarAsync(db.OwnerConnectionString,
            $"SELECT count(*) FROM tenancy.organization_memberships WHERE organization_id = '{orgId}'"));

        var visible = await InScopeAsync(db.AppConnectionString, outsider,
            (conn, tx) => ScalarAsync(conn, tx, "SELECT count(*) FROM tenancy.organizations"));
        Assert.Equal(1L, visible);
    }

    [Fact]
    public async Task Creating_an_organization_with_an_invalid_slug_is_rejected_by_the_database()
    {
        var db = await CreateSeededDatabaseAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => InScopeAsync(
            db.AppConnectionString, TenantContext.For(Guid.Parse(OutsiderId)),
            (conn, tx) => ScalarAsync(
                conn, tx, "SELECT tenancy.create_organization(@n, @s)", ("n", "Bad"), ("s", "A"))));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task Creating_an_organization_without_a_user_context_is_rejected()
    {
        var db = await CreateSeededDatabaseAsync();
        await using var connection = new NpgsqlConnection(db.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT tenancy.create_organization('Nope', 'nope-org')", connection);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteScalarAsync());

        Assert.Equal("28000", ex.SqlState);
    }

    [Theory]
    [InlineData(OwnerId)]
    [InlineData(AdminId)]
    public async Task Organization_owners_and_admins_can_create_a_company_and_become_its_owner(string userId)
    {
        var db = await CreateSeededDatabaseAsync();

        var companyId = await CreateCompanyAsync(db.AppConnectionString, userId, "ok");

        Assert.Equal("owner", await Sql.ScalarAsync(db.OwnerConnectionString,
            $"SELECT role FROM tenancy.company_memberships WHERE company_id = '{companyId}' AND user_id = '{userId}'"));
        Assert.Equal(1L, await Sql.ScalarAsync(db.OwnerConnectionString,
            $"SELECT count(*) FROM tenancy.company_memberships WHERE company_id = '{companyId}'"));

        var inserted = await InScopeAsync(
            db.AppConnectionString, TenantContext.For(Guid.Parse(userId), companyId),
            (conn, tx) => ExecAsync(
                conn, tx,
                "INSERT INTO tenancy.branches (company_id, name, code) VALUES (@c, 'Main', 'M-1')",
                ("c", companyId)));
        Assert.Equal(1, inserted);
    }

    [Theory]
    [InlineData(MemberId)]
    [InlineData(OutsiderId)]
    public async Task Only_organization_owners_and_admins_can_create_companies(string userId)
    {
        var db = await CreateSeededDatabaseAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => CreateCompanyAsync(db.AppConnectionString, userId, "denied"));

        Assert.Equal("42501", ex.SqlState);
        Assert.Equal(0L, await Sql.ScalarAsync(db.OwnerConnectionString,
            "SELECT count(*) FROM tenancy.companies WHERE slug = 'co-denied'"));
    }

    [Fact]
    public async Task The_app_role_cannot_insert_memberships_directly()
    {
        var db = await CreateSeededDatabaseAsync();
        var context = TenantContext.For(Guid.Parse(OwnerId), CompanyA);

        var orgEx = await Assert.ThrowsAsync<PostgresException>(() => InScopeAsync(
            db.AppConnectionString, context,
            (conn, tx) => ExecAsync(
                conn, tx,
                "INSERT INTO tenancy.organization_memberships (organization_id, user_id, role) VALUES (@o, @u, 'owner')",
                ("o", OrganizationId), ("u", Guid.Parse(OutsiderId)))));
        Assert.Equal("42501", orgEx.SqlState);

        var companyEx = await Assert.ThrowsAsync<PostgresException>(() => InScopeAsync(
            db.AppConnectionString, context,
            (conn, tx) => ExecAsync(
                conn, tx,
                "INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role) VALUES (@c, @o, @u, 'owner')",
                ("c", CompanyA), ("o", OrganizationId), ("u", Guid.Parse(OutsiderId)))));
        Assert.Equal("42501", companyEx.SqlState);
    }
}