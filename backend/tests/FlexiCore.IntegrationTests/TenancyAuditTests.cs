using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenancyAuditTests(PostgresFixture postgres)
{
    private const string Password = Scenario.Password;

    private const string OwnerEmail = "owner.one@example.test";
    private const string OutsiderEmail = "outsider.four@example.test";

    private static async Task<string> SignUpAsync(IdentityTestApp app, string email)
    {
        var register = await app.RegisterAsync(email, Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return (await Scenario.LoginAsync(app, email, Password)).AccessToken;
    }

    private static Task<HttpResponseMessage> PostAsync(
        IdentityTestApp app, string url, string token, object body, string? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (correlationId is not null) request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        return app.Client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> CreateOrgAsync(
        IdentityTestApp app, string token, string slug, string? correlationId = null) =>
        PostAsync(app, "/api/v1/organizations", token, new { name = $"Org {slug}", slug }, correlationId);

    private static Task<HttpResponseMessage> CreateCompanyAsync(
        IdentityTestApp app, string token, Guid organizationId, string slug, string? correlationId = null) =>
        PostAsync(app, $"/api/v1/organizations/{organizationId}/companies", token,
            new { name = $"Company {slug}", slug, industryKey = "retail" }, correlationId);

    private static async Task<List<string?[]>> RowsAsync(IdentityTestApp app, string sql)
    {
        var rows = new List<string?[]>();
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new string?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++) values[i] = reader.IsDBNull(i) ? null : reader.GetString(i);
            rows.Add(values);
        }

        return rows;
    }

    private static async Task<long> CountAsync(IdentityTestApp app, string sql)
    {
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> UserIdAsync(IdentityTestApp app, string email) =>
        Assert.Single(await RowsAsync(app, $"SELECT id::text FROM identity.users WHERE email = '{email}'"))[0]!;

    [Fact]
    public async Task Successful_creations_write_one_audit_row_each_with_the_correlation_id()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var token = await SignUpAsync(app, OwnerEmail);
        var userId = await UserIdAsync(app, OwnerEmail);

        var orgResponse = await CreateOrgAsync(app, token, "acme-trading", "c2-org-create");
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);
        var orgId = (await IdentityTestApp.ReadJsonAsync(orgResponse)).GetProperty("id").GetGuid();

        var companyResponse = await CreateCompanyAsync(app, token, orgId, "acme-pharmacy", "c2-company-create");
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var companyId = (await IdentityTestApp.ReadJsonAsync(companyResponse)).GetProperty("id").GetGuid();

        var orgRow = Assert.Single(await RowsAsync(app,
            "SELECT actor_user_id::text, outcome, target_type, target_id, correlation_id, metadata->>'slug' " +
            "FROM audit.events WHERE action = 'tenancy.organization.create'"));
        Assert.Equal(new string?[] { userId, "success", "organization", orgId.ToString(), "c2-org-create", "acme-trading" }, orgRow);

        var companyRow = Assert.Single(await RowsAsync(app,
            "SELECT actor_user_id::text, outcome, target_type, target_id, correlation_id, " +
            "metadata->>'company_id', metadata->>'slug' " +
            "FROM audit.events WHERE action = 'tenancy.company.create'"));
        Assert.Equal(
            new string?[] { userId, "success", "organization", orgId.ToString(), "c2-company-create", companyId.ToString(), "acme-pharmacy" },
            companyRow);

        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM audit.events WHERE action LIKE 'tenancy.%'"));
    }

    [Fact]
    public async Task Refused_attempts_write_audit_rows_and_create_nothing()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var tokenA = await SignUpAsync(app, OwnerEmail);
        var tokenB = await SignUpAsync(app, OutsiderEmail);
        var aId = await UserIdAsync(app, OwnerEmail);
        var bId = await UserIdAsync(app, OutsiderEmail);

        var orgResponse = await CreateOrgAsync(app, tokenA, "acme-trading");
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);
        var orgId = (await IdentityTestApp.ReadJsonAsync(orgResponse)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, (await CreateCompanyAsync(app, tokenA, orgId, "main-shop")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CreateCompanyAsync(app, tokenA, orgId, "main-shop", "c2-dup")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateCompanyAsync(app, tokenB, orgId, "intruder", "c2-denied")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CreateOrgAsync(app, tokenB, "acme-trading")).StatusCode);

        var duplicate = Assert.Single(await RowsAsync(app,
            $"SELECT outcome, target_id, correlation_id, metadata->>'slug', metadata->>'reason' FROM audit.events " +
            $"WHERE action = 'tenancy.company.create' AND actor_user_id = '{aId}'::uuid AND outcome = 'failure'"));
        Assert.Equal(new string?[] { "failure", orgId.ToString(), "c2-dup", "main-shop", "duplicate_slug" }, duplicate);

        var denied = Assert.Single(await RowsAsync(app,
            $"SELECT outcome, target_id, correlation_id, metadata->>'slug', metadata->>'reason' FROM audit.events " +
            $"WHERE action = 'tenancy.company.create' AND actor_user_id = '{bId}'::uuid"));
        Assert.Equal(new string?[] { "denied", orgId.ToString(), "c2-denied", "intruder", "not_permitted" }, denied);

        var duplicateOrg = Assert.Single(await RowsAsync(app,
            $"SELECT outcome, target_id, metadata->>'reason' FROM audit.events " +
            $"WHERE action = 'tenancy.organization.create' AND actor_user_id = '{bId}'::uuid"));
        Assert.Equal(new string?[] { "failure", null, "duplicate_slug" }, duplicateOrg);

        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM tenancy.companies"));
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM tenancy.organizations"));
    }

    [Fact]
    public async Task The_eleventh_organization_is_refused_and_audited()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var tokenA = await SignUpAsync(app, OwnerEmail);
        var tokenB = await SignUpAsync(app, OutsiderEmail);
        var aId = await UserIdAsync(app, OwnerEmail);

        for (var i = 1; i <= 10; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, tokenA, $"cap-org-{i:00}")).StatusCode);
        }

        var eleventh = await CreateOrgAsync(app, tokenA, "cap-org-11");
        Assert.Equal(HttpStatusCode.Forbidden, eleventh.StatusCode);
        var problem = await IdentityTestApp.ReadJsonAsync(eleventh);
        Assert.Equal("urn:flexicore:problem:quota-exceeded", problem.GetProperty("type").GetString());

        Assert.Equal(1, await CountAsync(app,
            $"SELECT count(*) FROM audit.events WHERE action = 'tenancy.organization.create' " +
            $"AND outcome = 'denied' AND metadata->>'reason' = 'limit_reached' AND actor_user_id = '{aId}'::uuid"));
        Assert.Equal(10, await CountAsync(app,
            $"SELECT count(*) FROM tenancy.organization_memberships WHERE role = 'owner' AND user_id = '{aId}'::uuid"));

        // The cap is per user: someone else is unaffected.
        Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, tokenB, "other-users-org")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_creations_by_one_user_never_exceed_the_cap()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var token = await SignUpAsync(app, OwnerEmail);
        var userId = await UserIdAsync(app, OwnerEmail);

        var attempts = Enumerable.Range(0, 20).Select(i => CreateOrgAsync(app, token, $"race-org-{i:00}"));
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Forbidden));
        Assert.Equal(10, await CountAsync(app,
            $"SELECT count(*) FROM tenancy.organization_memberships WHERE role = 'owner' AND user_id = '{userId}'::uuid"));
        Assert.Equal(10, await CountAsync(app,
            "SELECT count(*) FROM audit.events WHERE action = 'tenancy.organization.create' " +
            "AND outcome = 'denied' AND metadata->>'reason' = 'limit_reached'"));
    }

    [Fact]
    public async Task Audit_rows_never_contain_secrets_or_names()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var token = await SignUpAsync(app, OwnerEmail);

        var created = await PostAsync(app, "/api/v1/organizations", token,
            new { name = "Acme Trading", slug = "acme-trading" }, "c2-secret-check");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var duplicate = await PostAsync(app, "/api/v1/organizations", token,
            new { name = "Acme Trading", slug = "acme-trading" }, "c2-secret-check-2");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var rows = await RowsAsync(app,
            "SELECT action, outcome, coalesce(target_type, ''), coalesce(target_id, ''), " +
            "coalesce(correlation_id, ''), metadata::text FROM audit.events WHERE action LIKE 'tenancy.%'");
        Assert.Equal(2, rows.Count);

        foreach (var value in rows.SelectMany(r => r))
        {
            Assert.DoesNotContain(Password, value);
            Assert.DoesNotContain(token, value);
            Assert.DoesNotContain("Acme Trading", value);
        }
    }
}