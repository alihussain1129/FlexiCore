using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace FlexiCore.IntegrationTests;

/// <summary>
/// Talks to the database directly as the application role (no API), to prove that row-level security and the
/// column grants hold on their own. Every call runs in a transaction that is rolled back.
/// </summary>
[Collection("postgres")]
public sealed class CatalogRlsTests(PostgresFixture postgres)
{
    private const string InsufficientPrivilege = "42501";
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ForeignKeyViolation = "23503";

    private static async Task<T> AsAppAsync<T>(
        CatalogWorld w, Guid? userId, Guid? companyId, Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> body)
    {
        await using var connection = new NpgsqlConnection(w.App.Db.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand(
            "select set_config('app.user_id', @u, true), set_config('app.company_id', @c, true)", connection, transaction))
        {
            set.Parameters.AddWithValue("u", userId?.ToString() ?? string.Empty);
            set.Parameters.AddWithValue("c", companyId?.ToString() ?? string.Empty);
            await set.ExecuteNonQueryAsync();
        }

        var result = await body(connection, transaction);
        await transaction.RollbackAsync();
        return result;
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection c, NpgsqlTransaction t, string sql)
    {
        await using var command = new NpgsqlCommand(sql, c, t);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ExecAsync(NpgsqlConnection c, NpgsqlTransaction t, string sql)
    {
        await using var command = new NpgsqlCommand(sql, c, t);
        return await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertFailsAsync(CatalogWorld w, Guid? user, Guid? company, string sql, string expectedState)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => AsAppAsync(w, user, company, async (c, t) => await ExecAsync(c, t, sql)));
        Assert.True(expectedState == ex.SqlState, $"Expected {expectedState} but got {ex.SqlState} ({ex.MessageText}) for: {sql}");
    }

    private const string VisibleRows =
        "select (select count(*) from catalog.units) + (select count(*) from catalog.products)";

    private static string InsertUnit(Guid company, string code = "T1", string name = "Test") =>
        $"insert into catalog.units (id, company_id, code, name) values (gen_random_uuid(), '{company}', '{code}', '{name}')";

    private static string InsertProduct(Guid company, string sku = "T-1", string name = "Test", string extra = "") =>
        $"insert into catalog.products (id, company_id, sku, name{(extra.Length > 0 ? ", " + extra.Split('=')[0] : "")}) " +
        $"values (gen_random_uuid(), '{company}', '{sku}', '{name}'{(extra.Length > 0 ? ", " + extra.Split('=')[1] : "")})";

    [Fact]
    public async Task A_direct_query_with_another_companys_context_returns_nothing()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk");

        Task<long> Visible(Guid? user, Guid? company) =>
            AsAppAsync(w, user, company, (c, t) => ScalarAsync(c, t, VisibleRows));

        Assert.Equal(2L, await Visible(w.OwnerId, w.CompanyId));
        Assert.Equal(2L, await Visible(w.AdminId, w.CompanyId));
        Assert.Equal(2L, await Visible(w.MemberId, w.CompanyId)); // members may read

        Assert.Equal(0L, await Visible(w.OutsiderId, w.CompanyId));      // not a member of the company
        Assert.Equal(0L, await Visible(w.OwnerId, w.OtherCompanyId));    // company they do not belong to
        Assert.Equal(0L, await Visible(w.OutsiderId, w.OtherCompanyId)); // theirs, but it holds nothing
        Assert.Equal(0L, await Visible(Guid.NewGuid(), w.CompanyId));    // unknown user
        Assert.Equal(0L, await Visible(w.OwnerId, null));                // no company selected
        Assert.Equal(0L, await Visible(null, null));                     // no context at all
    }

    [Fact]
    public async Task Members_cannot_write_at_the_database_level()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk");

        await AssertFailsAsync(w, w.MemberId, w.CompanyId, InsertUnit(w.CompanyId, "M1", "Member"), InsufficientPrivilege);
        await AssertFailsAsync(w, w.MemberId, w.CompanyId, InsertProduct(w.CompanyId, "M-1", "Member"), InsufficientPrivilege);

        // Updates by a member match no rows (the update policy hides them), and nothing changes.
        Assert.Equal(0, await AsAppAsync(w, w.MemberId, w.CompanyId, (c, t) => ExecAsync(c, t, "update catalog.units set name = 'Hacked'")));
        Assert.Equal(0, await AsAppAsync(w, w.MemberId, w.CompanyId, (c, t) => ExecAsync(c, t, "update catalog.products set deleted_at = now()")));

        // The same updates work for an admin.
        Assert.Equal(1, await AsAppAsync(w, w.AdminId, w.CompanyId, (c, t) => ExecAsync(c, t, "update catalog.units set name = 'Renamed'")));

        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where name = 'Kilogram'"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products where deleted_at is null"));
    }

    [Fact]
    public async Task Owners_and_admins_cannot_write_into_another_company()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        Assert.Equal(1, await AsAppAsync(w, w.AdminId, w.CompanyId, (c, t) => ExecAsync(c, t, InsertUnit(w.CompanyId))));
        Assert.Equal(1, await AsAppAsync(w, w.OwnerId, w.CompanyId, (c, t) => ExecAsync(c, t, InsertProduct(w.CompanyId))));

        // Company context A, row for company B.
        await AssertFailsAsync(w, w.OwnerId, w.CompanyId, InsertUnit(w.OtherCompanyId), InsufficientPrivilege);
        await AssertFailsAsync(w, w.OwnerId, w.CompanyId, InsertProduct(w.OtherCompanyId), InsufficientPrivilege);
        // Context set to company B, where this user has no membership.
        await AssertFailsAsync(w, w.OwnerId, w.OtherCompanyId, InsertUnit(w.OtherCompanyId), InsufficientPrivilege);
        await AssertFailsAsync(w, w.AdminId, w.OtherCompanyId, InsertProduct(w.OtherCompanyId), InsufficientPrivilege);
        // No context.
        await AssertFailsAsync(w, null, null, InsertUnit(w.CompanyId), InsufficientPrivilege);

        Assert.Equal(0L, await w.CountAsync("select count(*) from catalog.units"));
        Assert.Equal(0L, await w.CountAsync("select count(*) from catalog.products"));
    }

    [Fact]
    public async Task The_app_role_cannot_write_server_stamped_columns_or_delete()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk");

        var c = w.CompanyId;
        var statements = new[]
        {
            "update catalog.units set version = 99",
            "update catalog.units set change_seq = 1",
            "update catalog.units set updated_at = now()",
            "update catalog.units set origin_device_id = gen_random_uuid()",
            "update catalog.units set id = gen_random_uuid()",
            $"update catalog.units set company_id = '{w.OtherCompanyId}'",
            $"insert into catalog.units (id, company_id, code, name, version) values (gen_random_uuid(), '{c}', 'V1', 'V', 5)",
            $"insert into catalog.units (id, company_id, code, name, change_seq) values (gen_random_uuid(), '{c}', 'V2', 'V', 5)",
            $"insert into catalog.units (id, company_id, code, name, origin_device_id) values (gen_random_uuid(), '{c}', 'V3', 'V', gen_random_uuid())",
            "delete from catalog.units",
            "update catalog.products set version = 99",
            "update catalog.products set change_seq = 1",
            "update catalog.products set updated_at = now()",
            "update catalog.products set origin_device_id = gen_random_uuid()",
            "update catalog.products set id = gen_random_uuid()",
            $"update catalog.products set company_id = '{w.OtherCompanyId}'",
            $"insert into catalog.products (id, company_id, sku, name, version) values (gen_random_uuid(), '{c}', 'V-1', 'V', 5)",
            $"insert into catalog.products (id, company_id, sku, name, change_seq) values (gen_random_uuid(), '{c}', 'V-2', 'V', 5)",
            "delete from catalog.products",
            "truncate catalog.units",
            "truncate catalog.products"
        };

        foreach (var sql in statements)
            await AssertFailsAsync(w, w.OwnerId, w.CompanyId, sql, InsufficientPrivilege);

        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where version = 1"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products where version = 1"));
    }

    [Fact]
    public async Task A_trigger_stamps_version_updated_at_and_change_sequence()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var (v1, s1, v2, s2, pv1, pv2) = await AsAppAsync(w, w.OwnerId, w.CompanyId, async (c, t) =>
        {
            await ExecAsync(c, t, InsertUnit(w.CompanyId, "T1", "Trigger"));
            var version1 = await ScalarAsync(c, t, "select version from catalog.units where code = 'T1'");
            var seq1 = await ScalarAsync(c, t, "select change_seq from catalog.units where code = 'T1'");
            await ExecAsync(c, t, "update catalog.units set name = 'Trigger 2' where code = 'T1'");
            var version2 = await ScalarAsync(c, t, "select version from catalog.units where code = 'T1'");
            var seq2 = await ScalarAsync(c, t, "select change_seq from catalog.units where code = 'T1'");

            await ExecAsync(c, t, InsertProduct(w.CompanyId, "T-1", "Trigger"));
            var productVersion1 = await ScalarAsync(c, t, "select version from catalog.products where sku = 'T-1'");
            await ExecAsync(c, t, "update catalog.products set name = 'Trigger 2' where sku = 'T-1'");
            var productVersion2 = await ScalarAsync(c, t, "select version from catalog.products where sku = 'T-1'");
            return (version1, seq1, version2, seq2, productVersion1, productVersion2);
        });

        Assert.Equal(1L, v1);
        Assert.Equal(2L, v2);
        Assert.True(s1 > 0);
        Assert.True(s2 > s1);
        Assert.Equal(1L, pv1);
        Assert.Equal(2L, pv2);
    }

    [Fact]
    public async Task Soft_deleted_rows_free_their_unique_keys_but_live_duplicates_are_rejected()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var live = await AsAppAsync(w, w.OwnerId, w.CompanyId, async (c, t) =>
        {
            await ExecAsync(c, t, InsertUnit(w.CompanyId, "KG", "First"));
            await ExecAsync(c, t, "update catalog.units set deleted_at = now() where code = 'KG'");
            await ExecAsync(c, t, InsertUnit(w.CompanyId, "KG", "Second")); // allowed: the first is deleted
            await ExecAsync(c, t, InsertProduct(w.CompanyId, "SKU-1", "First"));
            await ExecAsync(c, t, "update catalog.products set deleted_at = now() where sku = 'SKU-1'");
            await ExecAsync(c, t, InsertProduct(w.CompanyId, "SKU-1", "Second"));
            return await ScalarAsync(c, t, VisibleRows.Replace("select ", "select ") + " - 0");
        });
        Assert.Equal(4L, live); // both deleted rows are still visible to the table, and both live ones exist

        await AssertFailsAsync(w, w.OwnerId, w.CompanyId, InsertUnit(w.CompanyId, "KG", "Dup") + "; " + InsertUnit(w.CompanyId, "KG", "Dup2"), UniqueViolation);
        await AssertFailsAsync(w, w.OwnerId, w.CompanyId, InsertProduct(w.CompanyId, "SKU-1", "Dup") + "; " + InsertProduct(w.CompanyId, "SKU-1", "Dup2"), UniqueViolation);
    }

    [Fact]
    public async Task Check_constraints_reject_bad_values()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        var c = w.CompanyId;

        await AssertFailsAsync(w, w.OwnerId, c, InsertUnit(c, "bad code", "Name"), CheckViolation);
        await AssertFailsAsync(w, w.OwnerId, c, InsertUnit(c, "KG", "   "), CheckViolation);
        await AssertFailsAsync(w, w.OwnerId, c, InsertProduct(c, "bad sku", "Name"), CheckViolation);
        await AssertFailsAsync(w, w.OwnerId, c, InsertProduct(c, "S-1", "   "), CheckViolation);
        await AssertFailsAsync(w, w.OwnerId, c, InsertProduct(c, "S-1", "Name", "sale_price=-1"), CheckViolation);
        await AssertFailsAsync(w, w.OwnerId, c, InsertProduct(c, "S-1", "Name", "cost_price=-0.0001"), CheckViolation);
        await AssertFailsAsync(w, w.OwnerId, c, InsertProduct(c, "S-1", "Name", "barcode='abc'"), CheckViolation);
    }

    [Fact]
    public async Task A_product_cannot_reference_a_unit_from_another_company()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        var theirUnit = (await w.CreateUnitAsync(w.OutsiderToken, "UB", "Unit B", company: w.OtherCompanyId)).GetProperty("id").GetGuid();

        await AssertFailsAsync(w, w.OwnerId, w.CompanyId,
            InsertProduct(w.CompanyId, "FK-1", "Cross", $"unit_id='{theirUnit}'"), ForeignKeyViolation);
    }

    [Fact]
    public async Task Suspended_memberships_and_suspended_companies_lose_access()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk");

        Task<long> Visible(Guid user) => AsAppAsync(w, user, w.CompanyId, (c, t) => ScalarAsync(c, t, VisibleRows));

        Assert.Equal(2L, await Visible(w.MemberId));
        await w.ExecAsync($"update tenancy.company_memberships set status = 'suspended' where company_id = '{w.CompanyId}' and user_id = '{w.MemberId}'");
        Assert.Equal(0L, await Visible(w.MemberId));
        Assert.Equal(2L, await Visible(w.OwnerId));

        await w.ExecAsync($"update tenancy.organization_memberships set status = 'suspended' where organization_id = '{w.OrgId}' and user_id = '{w.AdminId}'");
        Assert.Equal(0L, await Visible(w.AdminId));

        await w.ExecAsync($"update tenancy.companies set status = 'suspended' where id = '{w.CompanyId}'");
        Assert.Equal(0L, await Visible(w.OwnerId));
        await AssertFailsAsync(w, w.OwnerId, w.CompanyId, InsertUnit(w.CompanyId, "NEW", "New"), InsufficientPrivilege);
    }
}