using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenancyApiTests(PostgresFixture postgres)
{
    private const string Password = Scenario.Password;

    private const string OwnerEmail = "owner.one@example.test";
    private const string AdminEmail = "admin.two@example.test";
    private const string MemberEmail = "member.three@example.test";
    private const string OutsiderEmail = "outsider.four@example.test";

    private Task<IdentityTestApp> StartAsync() => IdentityTestApp.StartAsync(postgres);

    private static async Task<string> SignUpAsync(IdentityTestApp app, string email)
    {
        var register = await app.RegisterAsync(email, Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return (await Scenario.LoginAsync(app, email, Password)).AccessToken;
    }

    private static Task<HttpResponseMessage> SendAsync(
        IdentityTestApp app, HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return app.Client.SendAsync(request);
    }

    private static async Task<JsonElement> CreateOrganizationAsync(
        IdentityTestApp app, string token, string name, string slug)
    {
        var response = await SendAsync(app, HttpMethod.Post, "/api/v1/organizations", token, new { name, slug });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await IdentityTestApp.ReadJsonAsync(response);
    }

    private static Task<HttpResponseMessage> CreateCompanyAsync(
        IdentityTestApp app, string token, Guid organizationId, string name, string slug, string industryKey = "retail") =>
        SendAsync(app, HttpMethod.Post, $"/api/v1/organizations/{organizationId}/companies", token,
            new { name, slug, industryKey });

    private static async Task<List<JsonElement>> GetListAsync(IdentityTestApp app, string url, string token)
    {
        var response = await SendAsync(app, HttpMethod.Get, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).EnumerateArray().ToList();
    }

    private static async Task ExecAsync(IdentityTestApp app, string sql)
    {
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(IdentityTestApp app, string sql)
    {
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Every_endpoint_requires_a_live_session()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var session = await Scenario.LoginAsync(app);
        var someId = Guid.NewGuid();
        var calls = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Post, "/api/v1/organizations", new { name = "Acme", slug = "acme" }),
            (HttpMethod.Get, "/api/v1/organizations", null),
            (HttpMethod.Post, $"/api/v1/organizations/{someId}/companies",
                new { name = "Shop", slug = "shop", industryKey = "retail" }),
            (HttpMethod.Get, "/api/v1/companies", null),
            (HttpMethod.Get, $"/api/v1/companies/{someId}", null)
        };

        foreach (var (method, url, body) in calls)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, method, url, null, body)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, method, url, "not-a-token", body)).StatusCode);
        }

        // The same token works until the session is ended.
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(app, HttpMethod.Get, "/api/v1/organizations", session.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await app.LogoutAsync(session.RefreshToken)).StatusCode);

        foreach (var (method, url, body) in calls)
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await SendAsync(app, method, url, session.AccessToken, body)).StatusCode);
        }
    }

    [Fact]
    public async Task The_creator_becomes_owner_and_can_list_and_fetch()
    {
        await using var app = await StartAsync();
        var token = await SignUpAsync(app, OwnerEmail);

        var org = await CreateOrganizationAsync(app, token, "Acme Trading", "acme-trading");
        Assert.Equal("Acme Trading", org.GetProperty("name").GetString());
        Assert.Equal("acme-trading", org.GetProperty("slug").GetString());
        Assert.Equal("owner", org.GetProperty("role").GetString());
        var orgId = org.GetProperty("id").GetGuid();

        var orgs = await GetListAsync(app, "/api/v1/organizations", token);
        var listedOrg = Assert.Single(orgs);
        Assert.Equal(orgId, listedOrg.GetProperty("id").GetGuid());
        Assert.Equal("owner", listedOrg.GetProperty("role").GetString());

        var page = await GetListAsync(app, "/api/v1/organizations?limit=1&offset=0", token);
        Assert.Single(page);
        var invalidPage = await SendAsync(
            app, HttpMethod.Get, "/api/v1/organizations?limit=101&offset=0", token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);

        var created = await CreateCompanyAsync(app, token, orgId, "Acme Pharmacy", "acme-pharmacy", "pharmacy");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var company = await IdentityTestApp.ReadJsonAsync(created);
        var companyId = company.GetProperty("id").GetGuid();
        Assert.Equal(orgId, company.GetProperty("organizationId").GetGuid());
        Assert.Equal("pharmacy", company.GetProperty("industryKey").GetString());
        Assert.Equal("owner", company.GetProperty("role").GetString());

        var companies = await GetListAsync(app, "/api/v1/companies", token);
        Assert.Equal(companyId, Assert.Single(companies).GetProperty("id").GetGuid());

        var fetched = await SendAsync(app, HttpMethod.Get, $"/api/v1/companies/{companyId}", token);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var fetchedJson = await IdentityTestApp.ReadJsonAsync(fetched);
        Assert.Equal("Acme Pharmacy", fetchedJson.GetProperty("name").GetString());
        Assert.Equal("acme-pharmacy", fetchedJson.GetProperty("slug").GetString());
        Assert.Equal(orgId, fetchedJson.GetProperty("organizationId").GetGuid());
    }

    [Fact]
    public async Task Other_users_cannot_see_or_use_someone_elses_tenant()
    {
        await using var app = await StartAsync();
        var tokenA = await SignUpAsync(app, OwnerEmail);
        var tokenB = await SignUpAsync(app, OutsiderEmail);

        var org = await CreateOrganizationAsync(app, tokenA, "Acme Trading", "acme-trading");
        var orgId = org.GetProperty("id").GetGuid();
        var created = await CreateCompanyAsync(app, tokenA, orgId, "Acme Pharmacy", "acme-pharmacy");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var companyId = (await IdentityTestApp.ReadJsonAsync(created)).GetProperty("id").GetGuid();

        Assert.Empty(await GetListAsync(app, "/api/v1/organizations", tokenB));
        Assert.Empty(await GetListAsync(app, "/api/v1/companies", tokenB));

        var theirs = await SendAsync(app, HttpMethod.Get, $"/api/v1/companies/{companyId}", tokenB);
        var random = await SendAsync(app, HttpMethod.Get, $"/api/v1/companies/{Guid.NewGuid()}", tokenB);
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, random.StatusCode);

        var intoTheirs = await CreateCompanyAsync(app, tokenB, orgId, "Intruder", "intruder");
        var intoRandom = await CreateCompanyAsync(app, tokenB, Guid.NewGuid(), "Intruder", "intruder");
        Assert.Equal(HttpStatusCode.Forbidden, intoTheirs.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, intoRandom.StatusCode);

        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM tenancy.companies"));
        Assert.Single(await GetListAsync(app, "/api/v1/organizations", tokenA));
        Assert.Single(await GetListAsync(app, "/api/v1/companies", tokenA));
    }

    [Fact]
    public async Task Only_organization_owners_and_admins_can_create_companies()
    {
        await using var app = await StartAsync();
        var ownerToken = await SignUpAsync(app, OwnerEmail);
        var adminToken = await SignUpAsync(app, AdminEmail);
        var memberToken = await SignUpAsync(app, MemberEmail);

        var org = await CreateOrganizationAsync(app, ownerToken, "Acme Trading", "acme-trading");
        var orgId = org.GetProperty("id").GetGuid();

        await ExecAsync(app, $"""
            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            SELECT '{orgId}'::uuid, id, 'admin' FROM identity.users WHERE email = '{AdminEmail}';
            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            SELECT '{orgId}'::uuid, id, 'member' FROM identity.users WHERE email = '{MemberEmail}';
            """);

        Assert.Equal(HttpStatusCode.Created,
            (await CreateCompanyAsync(app, ownerToken, orgId, "Owner Shop", "owner-shop")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await CreateCompanyAsync(app, adminToken, orgId, "Admin Shop", "admin-shop")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await CreateCompanyAsync(app, memberToken, orgId, "Member Shop", "member-shop")).StatusCode);

        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM tenancy.companies WHERE slug = 'member-shop'"));
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM tenancy.companies"));
    }

    [Fact]
    public async Task Duplicate_slugs_conflict_only_within_their_scope()
    {
        await using var app = await StartAsync();
        var tokenA = await SignUpAsync(app, OwnerEmail);
        var tokenB = await SignUpAsync(app, OutsiderEmail);

        var org = await CreateOrganizationAsync(app, tokenA, "Acme Trading", "acme-trading");
        var orgId = org.GetProperty("id").GetGuid();

        // Organization slugs are globally unique.
        var duplicateOrg = await SendAsync(app, HttpMethod.Post, "/api/v1/organizations", tokenB,
            new { name = "Other", slug = "acme-trading" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateOrg.StatusCode);

        // Company slugs are unique within one organization only.
        Assert.Equal(HttpStatusCode.Created,
            (await CreateCompanyAsync(app, tokenA, orgId, "Main Shop", "main-shop")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await CreateCompanyAsync(app, tokenA, orgId, "Main Shop Again", "main-shop")).StatusCode);

        var otherOrg = await CreateOrganizationAsync(app, tokenA, "Other Org", "other-org");
        Assert.Equal(HttpStatusCode.Created,
            (await CreateCompanyAsync(app, tokenA, otherOrg.GetProperty("id").GetGuid(), "Main Shop", "main-shop")).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_validation_problems()
    {
        await using var app = await StartAsync();
        var token = await SignUpAsync(app, OwnerEmail);
        var org = await CreateOrganizationAsync(app, token, "Acme Trading", "acme-trading");
        var orgId = org.GetProperty("id").GetGuid();

        object[] badOrganizations =
        [
            new { name = "", slug = "valid-slug" },
            new { name = "   ", slug = "valid-slug" },
            new { name = new string('x', 161), slug = "valid-slug" },
            new { name = "Bad\u0000Name", slug = "valid-slug" },
            new { name = (string?)null, slug = "valid-slug" },
            new { name = "Ok", slug = "ab" },
            new { name = "Ok", slug = new string('a', 81) },
            new { name = "Ok", slug = "Upper-Case" },
            new { name = "Ok", slug = "-leading" },
            new { name = "Ok", slug = "trailing-" },
            new { name = "Ok", slug = "has space" },
            new { name = "Ok", slug = (string?)null }
        ];

        foreach (var body in badOrganizations)
        {
            var response = await SendAsync(app, HttpMethod.Post, "/api/v1/organizations", token, body);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 400 for {JsonSerializer.Serialize(body)} but got {(int)response.StatusCode}.");
        }

        var sample = await SendAsync(app, HttpMethod.Post, "/api/v1/organizations", token,
            new { name = "Ok", slug = "ab" });
        Assert.Equal("application/problem+json", sample.Content.Headers.ContentType?.MediaType);
        var sampleJson = await IdentityTestApp.ReadJsonAsync(sample);
        Assert.True(sampleJson.GetProperty("errors").TryGetProperty("slug", out _));

        object[] badCompanies =
        [
            new { name = "", slug = "shop-one", industryKey = "retail" },
            new { name = "Ok", slug = "s", industryKey = "retail" },
            new { name = "Ok", slug = "shop-one", industryKey = "Retail" },
            new { name = "Ok", slug = "shop-one", industryKey = "1retail" },
            new { name = "Ok", slug = "shop-one", industryKey = "r" },
            new { name = "Ok", slug = "shop-one", industryKey = new string('r', 65) },
            new { name = "Ok", slug = "shop-one", industryKey = "has-dash" },
            new { name = "Ok", slug = "shop-one", industryKey = (string?)null }
        ];

        foreach (var body in badCompanies)
        {
            var response = await SendAsync(app, HttpMethod.Post,
                $"/api/v1/organizations/{orgId}/companies", token, body);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 400 for {JsonSerializer.Serialize(body)} but got {(int)response.StatusCode}.");
        }

        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM tenancy.organizations"));
        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM tenancy.companies"));
    }

    [Fact]
    public async Task Length_boundaries_are_enforced_exactly()
    {
        await using var app = await StartAsync();
        var token = await SignUpAsync(app, OwnerEmail);

        // Valid extremes: slug of 3 and 80 characters, name of 160 characters.
        await CreateOrganizationAsync(app, token, "Short Slug", "abc");
        await CreateOrganizationAsync(app, token, new string('n', 160), new string('a', 80));
        var org = await CreateOrganizationAsync(app, token, "Boundary Org", "boundary-org");
        var orgId = org.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created,
            (await CreateCompanyAsync(app, token, orgId, new string('n', 160), "abc", "ab")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await CreateCompanyAsync(app, token, orgId, "Long Slug", new string('b', 80), new string('r', 64))).StatusCode);
    }

    [Fact]
    public async Task Lists_are_capped_at_one_hundred_entries()
    {
        await using var app = await StartAsync();
        var token = await SignUpAsync(app, OwnerEmail);

        await ExecAsync(app, $"""
            WITH new_orgs AS (
                INSERT INTO tenancy.organizations (name, slug)
                SELECT 'Org ' || g, 'org-' || lpad(g::text, 4, '0') FROM generate_series(1, 105) g
                RETURNING id)
            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            SELECT n.id, u.id, 'member' FROM new_orgs n CROSS JOIN identity.users u WHERE u.email = '{OwnerEmail}';
            """);

        Assert.Equal(105, await CountAsync(app, "SELECT count(*) FROM tenancy.organization_memberships"));
        Assert.Equal(100, (await GetListAsync(app, "/api/v1/organizations", token)).Count);
    }

    [Fact]
    public async Task Alternating_users_on_one_pool_never_see_each_others_data()
    {
        await using var app = await StartAsync();
        var tokenA = await SignUpAsync(app, OwnerEmail);
        var tokenB = await SignUpAsync(app, OutsiderEmail);
        await CreateOrganizationAsync(app, tokenA, "Alpha Org", "alpha-org");
        await CreateOrganizationAsync(app, tokenB, "Beta Org", "beta-org");

        var checks = Enumerable.Range(0, 24).Select(async i =>
        {
            var (token, expectedSlug) = i % 2 == 0 ? (tokenA, "alpha-org") : (tokenB, "beta-org");
            var list = await GetListAsync(app, "/api/v1/organizations", token);
            Assert.Equal(expectedSlug, Assert.Single(list).GetProperty("slug").GetString());
        });

        await Task.WhenAll(checks);
    }
}