using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Xunit;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class CatalogApiTests(PostgresFixture postgres)
{
    private const string AuditCount = "select count(*) from audit.events where action like 'catalog.%'";

    [Fact]
    public async Task Owners_and_admins_can_create_list_read_update_and_delete_units_and_products()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var unit = await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        var unitId = unit.GetProperty("id").GetGuid();
        Assert.Equal(1, unit.GetProperty("version").GetInt32());
        Assert.Equal(w.CompanyId, unit.GetProperty("companyId").GetGuid());
        Assert.Equal(JsonValueKind.Null, unit.GetProperty("deletedAt").ValueKind);
        var unitSeq = unit.GetProperty("changeSeq").GetInt64();
        Assert.True(unitSeq > 0);

        var product = await w.CreateProductAsync(
            w.AdminToken, "MILK-1", "Milk 1L", unitId, 1.25m, 0.9m, "5901234123457");
        var productId = product.GetProperty("id").GetGuid();
        Assert.Equal(unitId, product.GetProperty("unitId").GetGuid());
        Assert.Equal(1.25m, product.GetProperty("salePrice").GetDecimal());
        Assert.Equal(0.9m, product.GetProperty("costPrice").GetDecimal());
        Assert.Equal("5901234123457", product.GetProperty("barcode").GetString());
        Assert.True(product.GetProperty("changeSeq").GetInt64() > unitSeq);

        Assert.Equal(unitId, Assert.Single(await w.ListAsync(w.Units(), w.AdminToken)).GetProperty("id").GetGuid());
        Assert.Equal(productId, Assert.Single(await w.ListAsync(w.Products(), w.OwnerToken)).GetProperty("id").GetGuid());

        var fetchedUnit = await w.SendAsync(HttpMethod.Get, w.Unit(unitId), w.OwnerToken);
        Assert.Equal(HttpStatusCode.OK, fetchedUnit.StatusCode);
        Assert.Equal("Kilogram", (await CatalogWorld.ReadAsync(fetchedUnit)).GetProperty("name").GetString());
        var fetchedProduct = await w.SendAsync(HttpMethod.Get, w.Product(productId), w.AdminToken);
        Assert.Equal(HttpStatusCode.OK, fetchedProduct.StatusCode);
        Assert.Equal("MILK-1", (await CatalogWorld.ReadAsync(fetchedProduct)).GetProperty("sku").GetString());

        var updatedUnit = await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.AdminToken,
            new { version = 1, code = "KGS", name = "Kilograms" });
        Assert.Equal(HttpStatusCode.OK, updatedUnit.StatusCode);
        var u2 = await CatalogWorld.ReadAsync(updatedUnit);
        Assert.Equal(2, u2.GetProperty("version").GetInt32());
        Assert.Equal("KGS", u2.GetProperty("code").GetString());
        Assert.True(u2.GetProperty("changeSeq").GetInt64() > unitSeq);

        var updatedProduct = await w.SendAsync(HttpMethod.Put, w.Product(productId), w.OwnerToken,
            new { version = 1, sku = "MILK-1", barcode = (string?)null, name = "Milk 2L", unitId = (Guid?)null, salePrice = 2.5m, costPrice = 1m });
        Assert.Equal(HttpStatusCode.OK, updatedProduct.StatusCode);
        var p2 = await CatalogWorld.ReadAsync(updatedProduct);
        Assert.Equal(2, p2.GetProperty("version").GetInt32());
        Assert.Equal("Milk 2L", p2.GetProperty("name").GetString());
        Assert.Equal(2.5m, p2.GetProperty("salePrice").GetDecimal());
        Assert.Equal(JsonValueKind.Null, p2.GetProperty("unitId").ValueKind);
        Assert.Equal(JsonValueKind.Null, p2.GetProperty("barcode").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(productId), w.AdminToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Get, w.Product(productId), w.OwnerToken)).StatusCode);
        Assert.Empty(await w.ListAsync(w.Products(), w.OwnerToken));

        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Get, w.Unit(unitId), w.OwnerToken)).StatusCode);
        Assert.Empty(await w.ListAsync(w.Units(), w.OwnerToken));

        // Soft delete: the rows are still there.
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where deleted_at is not null"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products where deleted_at is not null"));
    }

    [Fact]
    public async Task Concurrent_product_create_and_unit_delete_never_leave_a_live_product_using_a_deleted_unit()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        // Repeat the competing API operations on separate requests/connections. The
        // only valid outcomes are: product wins and deletion conflicts, or deletion
        // wins and product validation rejects the now-deleted unit.
        for (var i = 0; i < 20; i++)
        {
            var unitId = (await w.CreateUnitAsync(w.OwnerToken, $"R{i}", $"Race unit {i}")).GetProperty("id").GetGuid();
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var createTask = Task.Run(async () =>
            {
                await start.Task;
                return await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken,
                    new { id = Guid.NewGuid(), sku = $"RACE-{i}", name = "Race product", unitId, salePrice = 1m, costPrice = 0m });
            });
            var deleteTask = Task.Run(async () =>
            {
                await start.Task;
                return await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken);
            });

            start.TrySetResult();
            using var create = await createTask;
            using var delete = await deleteTask;

            if (create.StatusCode == HttpStatusCode.Created)
            {
                Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
            }
            else
            {
                Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
                Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            }

            Assert.Equal(0L, await w.CountAsync($"""
                select count(*)
                from catalog.products p
                join catalog.units u on u.id = p.unit_id and u.company_id = p.company_id
                where p.company_id = '{w.CompanyId}'::uuid
                  and p.deleted_at is null
                  and u.deleted_at is not null
                """));
        }
    }

    [Fact]
    public async Task Codes_and_names_are_trimmed_and_prices_keep_four_decimals()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var unit = await w.CreateUnitAsync(w.OwnerToken, "  KG  ", "  Kilogram  ");
        Assert.Equal("KG", unit.GetProperty("code").GetString());
        Assert.Equal("Kilogram", unit.GetProperty("name").GetString());

        var product = await w.CreateProductAsync(w.OwnerToken, " SKU-1 ", "  Milk  ", sale: 12.3456m, cost: 0.0001m, barcode: "  5901234123457 ");
        Assert.Equal("SKU-1", product.GetProperty("sku").GetString());
        Assert.Equal("Milk", product.GetProperty("name").GetString());
        Assert.Equal("5901234123457", product.GetProperty("barcode").GetString());
        Assert.Equal(12.3456m, product.GetProperty("salePrice").GetDecimal());
        Assert.Equal(0.0001m, product.GetProperty("costPrice").GetDecimal());
    }

    [Fact]
    public async Task A_plain_member_can_read_but_not_write()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        var unitId = (await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram")).GetProperty("id").GetGuid();
        var productId = (await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk")).GetProperty("id").GetGuid();

        Assert.Single(await w.ListAsync(w.Units(), w.MemberToken));
        Assert.Single(await w.ListAsync(w.Products(), w.MemberToken));
        Assert.Equal(HttpStatusCode.OK, (await w.SendAsync(HttpMethod.Get, w.Unit(unitId), w.MemberToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.SendAsync(HttpMethod.Get, w.Product(productId), w.MemberToken)).StatusCode);

        var writes = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Post, w.Units(), new { id = Guid.NewGuid(), code = "N1", name = "New" }),
            (HttpMethod.Put, w.Unit(unitId), new { version = 1, code = "KG", name = "Hacked" }),
            (HttpMethod.Delete, w.Unit(unitId), null),
            (HttpMethod.Post, w.Products(), new { id = Guid.NewGuid(), sku = "N-1", name = "New" }),
            (HttpMethod.Put, w.Product(productId), new { version = 1, sku = "SKU-1", name = "Hacked" }),
            (HttpMethod.Delete, w.Product(productId), null)
        };
        foreach (var (method, url, body) in writes)
            Assert.Equal(HttpStatusCode.Forbidden, (await w.SendAsync(method, url, w.MemberToken, body)).StatusCode);

        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where version = 1 and name = 'Kilogram' and deleted_at is null"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products where version = 1 and name = 'Milk' and deleted_at is null"));
        Assert.Equal(2L, await w.CountAsync(AuditCount)); // only the owner's two creates
    }

    [Fact]
    public async Task Unauthenticated_callers_get_401_and_outsiders_get_404_with_no_data_leaked()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        var unitId = (await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram")).GetProperty("id").GetGuid();
        var productId = (await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk")).GetProperty("id").GetGuid();

        var calls = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Get, w.Units(), null),
            (HttpMethod.Post, w.Units(), new { id = Guid.NewGuid(), code = "N1", name = "New" }),
            (HttpMethod.Get, w.Unit(unitId), null),
            (HttpMethod.Put, w.Unit(unitId), new { version = 1, code = "KG", name = "Kilogram" }),
            (HttpMethod.Delete, w.Unit(unitId), null),
            (HttpMethod.Get, w.Products(), null),
            (HttpMethod.Post, w.Products(), new { id = Guid.NewGuid(), sku = "N-1", name = "New" }),
            (HttpMethod.Get, w.Product(productId), null),
            (HttpMethod.Put, w.Product(productId), new { version = 1, sku = "SKU-1", name = "Milk" }),
            (HttpMethod.Delete, w.Product(productId), null)
        };

        foreach (var (method, url, body) in calls)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await w.SendAsync(method, url, null, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await w.SendAsync(method, url, "not-a-token", body)).StatusCode);

            // A device credential is not a user token: catalog endpoints accept users only.
            var device = new HttpRequestMessage(method, url);
            device.Headers.Authorization = new AuthenticationHeaderValue("Device", "abc");
            Assert.Equal(HttpStatusCode.Unauthorized, (await w.App.Client.SendAsync(device)).StatusCode);
        }

        // A user outside the company, and a company that does not exist, look exactly the same: 404.
        var randomCompany = Guid.NewGuid();
        foreach (var (method, url, body) in calls)
        {
            var outsider = await w.SendAsync(method, url, w.OutsiderToken, body);
            Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
            var text = await outsider.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Kilogram", text);
            Assert.DoesNotContain("Milk", text);

            var randomUrl = url.Replace(w.CompanyId.ToString(), randomCompany.ToString());
            var random = await w.SendAsync(method, randomUrl, w.OwnerToken, body);
            Assert.Equal(HttpStatusCode.NotFound, random.StatusCode);
        }

        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products"));
        Assert.Equal(2L, await w.CountAsync(AuditCount));
    }

    [Fact]
    public async Task A_token_stops_working_after_logout()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        var session = await Scenario.LoginAsync(w.App, CatalogWorld.OwnerEmail, Scenario.Password);

        Assert.Equal(HttpStatusCode.OK, (await w.SendAsync(HttpMethod.Get, w.Units(), session.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await w.App.LogoutAsync(session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await w.SendAsync(HttpMethod.Get, w.Units(), session.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task Creating_twice_with_the_same_id_and_content_returns_the_same_record()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var unitId = Guid.NewGuid();
        var unitBody = new { id = unitId, code = "KG", name = "Kilogram" };
        var first = await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken, unitBody);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var a = await CatalogWorld.ReadAsync(first);
        var second = await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken, unitBody);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var b = await CatalogWorld.ReadAsync(second);
        Assert.Equal(a.GetProperty("version").GetInt32(), b.GetProperty("version").GetInt32());
        Assert.Equal(a.GetProperty("changeSeq").GetInt64(), b.GetProperty("changeSeq").GetInt64());
        Assert.Equal(a.GetProperty("updatedAt").GetString(), b.GetProperty("updatedAt").GetString());
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from audit.events where action = 'catalog.unit.create'"));

        var productId = Guid.NewGuid();
        var productBody = new { id = productId, sku = "SKU-1", barcode = "5901234123457", name = "Milk", unitId = (Guid?)unitId, salePrice = 1.5m, costPrice = 1m };
        Assert.Equal(HttpStatusCode.Created, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken, productBody)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken, productBody)).StatusCode);
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from audit.events where action = 'catalog.product.create'"));

        // Same id, different content: conflict, and nothing changes.
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken,
            new { id = unitId, code = "KG", name = "Another name" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken,
            new { id = unitId, code = "KGX", name = "Kilogram" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken,
            new { id = productId, sku = "SKU-1", barcode = "5901234123457", name = "Milk", unitId = (Guid?)unitId, salePrice = 9m, costPrice = 1m })).StatusCode);
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where version = 1 and name = 'Kilogram'"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products where version = 1 and sale_price = 1.5"));
    }

    [Fact]
    public async Task An_id_that_exists_in_another_company_returns_409_without_revealing_that_row()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var otherUnit = await w.CreateUnitAsync(w.OutsiderToken, "OTHERSECRET1", "Hidden Name", company: w.OtherCompanyId);
        var otherUnitId = otherUnit.GetProperty("id").GetGuid();
        var otherProduct = await w.CreateProductAsync(w.OutsiderToken, "HIDDEN-SKU", "Hidden Product", company: w.OtherCompanyId);
        var otherProductId = otherProduct.GetProperty("id").GetGuid();

        var attempts = new (string Url, object Body)[]
        {
            (w.Units(), new { id = otherUnitId, code = "ZZ", name = "Mine" }),
            (w.Units(), new { id = otherUnitId, code = "OTHERSECRET1", name = "Hidden Name" }), // identical to their row
            (w.Products(), new { id = otherProductId, sku = "MINE-1", name = "Mine" }),
            (w.Products(), new { id = otherProductId, sku = "HIDDEN-SKU", name = "Hidden Product" }) // identical to their row
        };

        foreach (var (url, body) in attempts)
        {
            var response = await w.SendAsync(HttpMethod.Post, url, w.OwnerToken, body);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("OTHERSECRET1", text);
            Assert.DoesNotContain("Hidden", text);
            Assert.DoesNotContain(w.OtherCompanyId.ToString(), text);
        }

        Assert.Empty(await w.ListAsync(w.Units(), w.OwnerToken));
        Assert.Empty(await w.ListAsync(w.Products(), w.OwnerToken));
        // The other company's rows are untouched.
        Assert.Single(await w.ListAsync(w.Units(w.OtherCompanyId), w.OutsiderToken));
    }

    [Fact]
    public async Task A_stale_version_returns_409_with_the_current_record_and_a_valid_update_increments_the_version()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var unitId = (await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram")).GetProperty("id").GetGuid();
        var v2 = await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.OwnerToken, new { version = 1, code = "KG", name = "Kilogram 2" });
        Assert.Equal(HttpStatusCode.OK, v2.StatusCode);
        Assert.Equal(2, (await CatalogWorld.ReadAsync(v2)).GetProperty("version").GetInt32());

        var stale = await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.AdminToken, new { version = 1, code = "KG", name = "Stale write" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var current = (await CatalogWorld.ReadAsync(stale)).GetProperty("current");
        Assert.Equal(2, current.GetProperty("version").GetInt32());
        Assert.Equal("Kilogram 2", current.GetProperty("name").GetString());
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where name = 'Kilogram 2' and version = 2"));

        var v3 = await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.AdminToken, new { version = 2, code = "KG", name = "Kilogram 3" });
        Assert.Equal(HttpStatusCode.OK, v3.StatusCode);
        Assert.Equal(3, (await CatalogWorld.ReadAsync(v3)).GetProperty("version").GetInt32());

        var productId = (await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk")).GetProperty("id").GetGuid();
        var pv2 = await w.SendAsync(HttpMethod.Put, w.Product(productId), w.OwnerToken,
            new { version = 1, sku = "SKU-1", name = "Milk 2", salePrice = 2m, costPrice = 1m });
        Assert.Equal(HttpStatusCode.OK, pv2.StatusCode);
        var pStale = await w.SendAsync(HttpMethod.Put, w.Product(productId), w.OwnerToken,
            new { version = 1, sku = "SKU-1", name = "Stale", salePrice = 3m, costPrice = 1m });
        Assert.Equal(HttpStatusCode.Conflict, pStale.StatusCode);
        var pCurrent = (await CatalogWorld.ReadAsync(pStale)).GetProperty("current");
        Assert.Equal(2, pCurrent.GetProperty("version").GetInt32());
        Assert.Equal("Milk 2", pCurrent.GetProperty("name").GetString());

        // The version is required, and updating something missing or deleted is a 404.
        var noVersion = await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.OwnerToken, new { code = "KG", name = "No version" });
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        CatalogWorld.AssertFieldError(await CatalogWorld.ReadAsync(noVersion), "version");

        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Put, w.Unit(Guid.NewGuid()), w.OwnerToken,
            new { version = 1, code = "KG", name = "Nothing" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(productId), w.OwnerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Put, w.Product(productId), w.OwnerToken,
            new { version = 3, sku = "SKU-1", name = "Deleted", salePrice = 1m, costPrice = 1m })).StatusCode);
    }

    [Fact]
    public async Task A_deleted_record_leaves_the_lists_and_cannot_clash_with_a_new_sku_or_code()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var unitId = (await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram")).GetProperty("id").GetGuid();
        var firstId = Guid.NewGuid();
        var firstBody = new { id = firstId, sku = "SKU-1", barcode = (string?)null, name = "First", unitId = (Guid?)unitId, salePrice = 1m, costPrice = 0m };
        Assert.Equal(HttpStatusCode.Created, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken, firstBody)).StatusCode);

        // A unit used by a live product cannot be deleted.
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken)).StatusCode);
        Assert.Single(await w.ListAsync(w.Units(), w.OwnerToken));

        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(firstId), w.OwnerToken)).StatusCode);
        Assert.Empty(await w.ListAsync(w.Products(), w.OwnerToken));
        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Get, w.Product(firstId), w.OwnerToken)).StatusCode);

        // The SKU is free again.
        var secondId = (await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Second")).GetProperty("id").GetGuid();
        var listed = Assert.Single(await w.ListAsync(w.Products(), w.OwnerToken));
        Assert.Equal(secondId, listed.GetProperty("id").GetGuid());

        // Deleting again is harmless; replaying the old create is a conflict, not a resurrection.
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(firstId), w.OwnerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken, firstBody)).StatusCode);
        Assert.Single(await w.ListAsync(w.Products(), w.OwnerToken));

        // With no live product using it, the unit can go, and its code is free again.
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(secondId), w.OwnerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken)).StatusCode);
        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram again");
        Assert.Single(await w.ListAsync(w.Units(), w.OwnerToken));
    }

    [Fact]
    public async Task A_duplicate_sku_or_unit_code_in_one_company_returns_409()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        var dupUnit = await w.SendAsync(HttpMethod.Post, w.Units(), w.AdminToken, new { id = Guid.NewGuid(), code = "KG", name = "Other" });
        Assert.Equal(HttpStatusCode.Conflict, dupUnit.StatusCode);

        var litre = (await w.CreateUnitAsync(w.OwnerToken, "L", "Litre")).GetProperty("id").GetGuid();
        var renameToTaken = await w.SendAsync(HttpMethod.Put, w.Unit(litre), w.OwnerToken, new { version = 1, code = "KG", name = "Litre" });
        Assert.Equal(HttpStatusCode.Conflict, renameToTaken.StatusCode);

        await w.CreateProductAsync(w.OwnerToken, "SKU-1", "First");
        var dupProduct = await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken, new { id = Guid.NewGuid(), sku = "SKU-1", name = "Second" });
        Assert.Equal(HttpStatusCode.Conflict, dupProduct.StatusCode);

        var second = (await w.CreateProductAsync(w.OwnerToken, "SKU-2", "Second")).GetProperty("id").GetGuid();
        var toTaken = await w.SendAsync(HttpMethod.Put, w.Product(second), w.OwnerToken,
            new { version = 1, sku = "SKU-1", name = "Second", salePrice = 1m, costPrice = 0m });
        Assert.Equal(HttpStatusCode.Conflict, toTaken.StatusCode);

        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.units where code = 'KG'"));
        Assert.Equal(1L, await w.CountAsync("select count(*) from catalog.products where sku = 'SKU-2' and version = 1"));

        // The same SKU in a different company is fine.
        await w.CreateProductAsync(w.OutsiderToken, "SKU-1", "Theirs", company: w.OtherCompanyId);
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_field_level_errors_and_writes_nothing()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);
        var token = w.OwnerToken;

        async Task<JsonElement> BadAsync(string url, object body)
        {
            var response = await w.SendAsync(HttpMethod.Post, url, token, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            return await CatalogWorld.ReadAsync(response);
        }

        CatalogWorld.AssertFieldError(await BadAsync(w.Units(), new { id = Guid.NewGuid(), code = "KG" }), "name");
        CatalogWorld.AssertFieldError(await BadAsync(w.Units(), new { id = Guid.NewGuid(), name = "Kilogram" }), "code");
        CatalogWorld.AssertFieldError(await BadAsync(w.Units(), new { id = Guid.NewGuid(), code = "bad code!", name = "Kilogram" }), "code");
        CatalogWorld.AssertFieldError(await BadAsync(w.Units(), new { code = "KG", name = "Kilogram" }), "id");
        CatalogWorld.AssertFieldError(await BadAsync(w.Units(), new { id = Guid.Empty, code = "KG", name = "Kilogram" }), "id");

        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-1", salePrice = 1m }), "name");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-1", name = "Milk", salePrice = -1m }), "salePrice");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-1", name = "Milk", costPrice = -0.01m }), "costPrice");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-1", name = "Milk", barcode = "12" }), "barcode");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "bad sku", name = "Milk" }), "sku");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), name = "Milk" }), "sku");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-1", name = "Milk", salePrice = 1.00001m }), "salePrice");
        CatalogWorld.AssertFieldError(await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-1", name = "Milk", costPrice = 100000000000000m }), "costPrice");

        var many = await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "", name = "", barcode = "x", salePrice = -1m, costPrice = -1m });
        foreach (var field in new[] { "sku", "name", "barcode", "salePrice", "costPrice" })
            CatalogWorld.AssertFieldError(many, field);

        // A unit that does not exist, belongs to another company, or is deleted: the same 400, no leak.
        var theirUnit = (await w.CreateUnitAsync(w.OutsiderToken, "UB", "Unit B", company: w.OtherCompanyId)).GetProperty("id").GetGuid();
        var deletedUnit = (await w.CreateUnitAsync(token, "OLD", "Old")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Unit(deletedUnit), token)).StatusCode);
        foreach (var unitId in new[] { Guid.NewGuid(), theirUnit, deletedUnit })
            CatalogWorld.AssertFieldError(
                await BadAsync(w.Products(), new { id = Guid.NewGuid(), sku = "S-2", name = "Milk", unitId }), "unitId");

        // Malformed JSON is a 400, not a 500.
        var broken = new HttpRequestMessage(HttpMethod.Post, w.Units())
        {
            Content = new StringContent("{ not json", Encoding.UTF8, "application/json")
        };
        broken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.App.Client.SendAsync(broken)).StatusCode);

        Assert.Equal(0L, await w.CountAsync("select count(*) from catalog.products"));
        Assert.Equal(0L, await w.CountAsync("select count(*) from catalog.units where company_id = '" + w.CompanyId + "' and deleted_at is null"));
    }

    [Fact]
    public async Task Every_successful_write_writes_one_audit_row_with_the_correlation_id_and_a_refused_write_writes_none()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        async Task<long> ForAsync(string action, string correlation) => await w.CountAsync(
            $"select count(*) from audit.events where action = '{action}' and correlation_id = '{correlation}' " +
            $"and tenant_id = '{w.CompanyId}' and actor_user_id = '{w.OwnerId}' and outcome = 'success'");

        // a. create, plus a harmless replay
        var unitId = Guid.NewGuid();
        var unitBody = new { id = unitId, code = "KG", name = "Kilogram" };
        Assert.Equal(HttpStatusCode.Created,
            (await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken, unitBody, "corr-catalog-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken, unitBody, "corr-catalog-replay")).StatusCode);
        Assert.Equal(1L, await ForAsync("catalog.unit.create", "corr-catalog-1"));
        Assert.Equal(1L, await w.CountAsync(AuditCount));

        // b. a product
        var productId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Created, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken,
            new { id = productId, sku = "SKU-1", name = "Milk", salePrice = 1m, costPrice = 0m }, "corr-catalog-2")).StatusCode);
        Assert.Equal(1L, await ForAsync("catalog.product.create", "corr-catalog-2"));
        Assert.Equal(2L, await w.CountAsync(AuditCount));

        // c. refused, conflicting, invalid and missing: none of these writes an audit row
        Assert.Equal(HttpStatusCode.Forbidden, (await w.SendAsync(HttpMethod.Post, w.Units(), w.MemberToken,
            new { id = Guid.NewGuid(), code = "N1", name = "New" }, "corr-catalog-refused")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Post, w.Units(), w.OutsiderToken,
            new { id = Guid.NewGuid(), code = "N2", name = "New" }, "corr-catalog-refused")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.SendAsync(HttpMethod.Post, w.Products(), w.OwnerToken,
            new { id = Guid.NewGuid(), sku = "S-9", name = "Bad", salePrice = -1m }, "corr-catalog-refused")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Post, w.Units(), w.OwnerToken,
            new { id = Guid.NewGuid(), code = "KG", name = "Dup" }, "corr-catalog-refused")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.OwnerToken,
            new { version = 99, code = "KG", name = "Stale" }, "corr-catalog-refused")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.SendAsync(HttpMethod.Put, w.Unit(Guid.NewGuid()), w.OwnerToken,
            new { version = 1, code = "KG", name = "Missing" }, "corr-catalog-refused")).StatusCode);
        Assert.Equal(2L, await w.CountAsync(AuditCount));
        Assert.Equal(0L, await w.CountAsync("select count(*) from audit.events where correlation_id = 'corr-catalog-refused'"));

        // d. updates
        Assert.Equal(HttpStatusCode.OK, (await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.AdminToken,
            new { version = 1, code = "KG", name = "Kilogram 2" }, "corr-catalog-3")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.SendAsync(HttpMethod.Put, w.Product(productId), w.OwnerToken,
            new { version = 1, sku = "SKU-1", name = "Milk 2", salePrice = 2m, costPrice = 0m }, "corr-catalog-4")).StatusCode);
        Assert.Equal(4L, await w.CountAsync(AuditCount));
        Assert.Equal(1L, await w.CountAsync(
            $"select count(*) from audit.events where action = 'catalog.unit.update' and correlation_id = 'corr-catalog-3' and actor_user_id = '{w.AdminId}'"));
        Assert.Equal(1L, await ForAsync("catalog.product.update", "corr-catalog-4"));

        // e. deletes, then deleting again (nothing to write, nothing to audit)
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(productId), w.OwnerToken, null, "corr-catalog-5")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken, null, "corr-catalog-6")).StatusCode);
        Assert.Equal(1L, await ForAsync("catalog.product.delete", "corr-catalog-5"));
        Assert.Equal(1L, await ForAsync("catalog.unit.delete", "corr-catalog-6"));
        Assert.Equal(6L, await w.CountAsync(AuditCount));
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Product(productId), w.OwnerToken, null, "corr-catalog-again")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken, null, "corr-catalog-again")).StatusCode);
        Assert.Equal(6L, await w.CountAsync(AuditCount));

        // The version is recorded in the audit metadata.
        Assert.Equal(1L, await w.CountAsync(
            "select count(*) from audit.events where action = 'catalog.unit.update' and metadata->>'version' = '2'"));
    }

    [Fact]
    public async Task Listing_is_paged_searchable_by_name_sku_or_barcode_and_capped()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        foreach (var letter in "ABCDE")
            await w.CreateProductAsync(w.OwnerToken, $"W-{letter}", $"Widget {letter}");
        await w.CreateProductAsync(w.OwnerToken, "G-1", "Gadget", barcode: "5901234123457");

        string NameAt(List<JsonElement> list, int i) => list[i].GetProperty("name").GetString()!;

        var all = await w.ListAsync(w.Products(), w.OwnerToken);
        Assert.Equal(6, all.Count);
        Assert.Equal("Gadget", NameAt(all, 0)); // ordered by name
        Assert.Equal("Widget E", NameAt(all, 5));

        var page1 = await w.ListAsync(w.Products() + "?limit=2&offset=0", w.MemberToken);
        Assert.Equal(new[] { "Gadget", "Widget A" }, new[] { NameAt(page1, 0), NameAt(page1, 1) });
        var page3 = await w.ListAsync(w.Products() + "?limit=2&offset=4", w.MemberToken);
        Assert.Equal(new[] { "Widget D", "Widget E" }, new[] { NameAt(page3, 0), NameAt(page3, 1) });
        Assert.Empty(await w.ListAsync(w.Products() + "?limit=2&offset=6", w.OwnerToken));

        // The cap: out-of-range paging is rejected.
        foreach (var bad in new[] { "limit=101", "limit=0", "limit=-5", "offset=-1", "limit=abc" })
            Assert.Equal(HttpStatusCode.BadRequest,
                (await w.SendAsync(HttpMethod.Get, w.Products() + "?" + bad, w.OwnerToken)).StatusCode);
        Assert.Equal(6, (await w.ListAsync(w.Products() + "?limit=100", w.OwnerToken)).Count);

        // Search by name (any case), SKU and barcode; LIKE wildcards are plain text.
        Assert.Equal(5, (await w.ListAsync(w.Products() + "?q=widget", w.OwnerToken)).Count);
        Assert.Single(await w.ListAsync(w.Products() + "?q=GADGET", w.OwnerToken));
        Assert.Single(await w.ListAsync(w.Products() + "?q=G-1", w.OwnerToken));
        Assert.Single(await w.ListAsync(w.Products() + "?q=5901234", w.OwnerToken));
        Assert.Empty(await w.ListAsync(w.Products() + "?q=nomatch", w.OwnerToken));
        Assert.Empty(await w.ListAsync(w.Products() + "?q=%25", w.OwnerToken));
        Assert.Empty(await w.ListAsync(w.Products() + "?q=_", w.OwnerToken));
        Assert.Equal(2, (await w.ListAsync(w.Products() + "?q=widget&limit=2", w.OwnerToken)).Count);

        await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        await w.CreateUnitAsync(w.OwnerToken, "LTR", "Litre");
        Assert.Single(await w.ListAsync(w.Units() + "?q=litre", w.OwnerToken));
        Assert.Single(await w.ListAsync(w.Units() + "?q=KG", w.OwnerToken));
        Assert.Equal(2, (await w.ListAsync(w.Units(), w.MemberToken)).Count);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.SendAsync(HttpMethod.Get, w.Units() + "?limit=101", w.OwnerToken)).StatusCode);
    }

    [Fact]
    public async Task Change_sequence_numbers_only_go_up_across_writes()
    {
        await using var w = await CatalogWorld.CreateAsync(postgres);

        var unit = await w.CreateUnitAsync(w.OwnerToken, "KG", "Kilogram");
        var unitId = unit.GetProperty("id").GetGuid();
        var seq1 = unit.GetProperty("changeSeq").GetInt64();
        var product = await w.CreateProductAsync(w.OwnerToken, "SKU-1", "Milk");
        var seq2 = product.GetProperty("changeSeq").GetInt64();
        var updated = await CatalogWorld.ReadAsync(await w.SendAsync(HttpMethod.Put, w.Unit(unitId), w.OwnerToken,
            new { version = 1, code = "KG", name = "Kilogram 2" }));
        var seq3 = updated.GetProperty("changeSeq").GetInt64();
        var deletedUnitSeq = await w.CountAsync("select 0"); // placeholder so the delete below is exercised
        Assert.Equal(HttpStatusCode.NoContent, (await w.SendAsync(HttpMethod.Delete, w.Unit(unitId), w.OwnerToken)).StatusCode);
        var seq4 = await w.CountAsync($"select change_seq from catalog.units where id = '{unitId}'");

        Assert.True(seq1 < seq2);
        Assert.True(seq2 < seq3);
        Assert.True(seq3 < seq4);
        Assert.Equal(0L, deletedUnitSeq);
    }
}
