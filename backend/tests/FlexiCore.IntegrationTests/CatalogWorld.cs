using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Xunit;

namespace FlexiCore.IntegrationTests;

/// <summary>
/// A running API with two tenants. Company A (the one under test): an owner, an admin and a plain member.
/// Company B (someone else's): owned by the outsider. Used by the catalog API and RLS tests.
/// </summary>
internal sealed class CatalogWorld : IAsyncDisposable
{
    public const string OwnerEmail = "owner.one@example.test";
    public const string AdminEmail = "admin.two@example.test";
    public const string MemberEmail = "member.three@example.test";
    public const string OutsiderEmail = "outsider.four@example.test";

    public IdentityTestApp App { get; }

    public string OwnerToken { get; private set; } = "";
    public string AdminToken { get; private set; } = "";
    public string MemberToken { get; private set; } = "";
    public string OutsiderToken { get; private set; } = "";

    public Guid OwnerId { get; private set; }
    public Guid AdminId { get; private set; }
    public Guid MemberId { get; private set; }
    public Guid OutsiderId { get; private set; }

    public Guid OrgId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid OtherOrgId { get; private set; }
    public Guid OtherCompanyId { get; private set; }

    private CatalogWorld(IdentityTestApp app) { App = app; }

    public static async Task<CatalogWorld> CreateAsync(PostgresFixture postgres)
    {
        var app = await IdentityTestApp.StartAsync(postgres);
        var w = new CatalogWorld(app);

        w.OwnerToken = await SignUpAsync(app, OwnerEmail);
        w.AdminToken = await SignUpAsync(app, AdminEmail);
        w.MemberToken = await SignUpAsync(app, MemberEmail);
        w.OutsiderToken = await SignUpAsync(app, OutsiderEmail);

        w.OrgId = await CreateOrganizationAsync(app, w.OwnerToken, "Acme Trading", "acme-trading");
        w.CompanyId = await CreateCompanyAsync(app, w.OwnerToken, w.OrgId, "Acme Shop", "acme-shop");
        w.OtherOrgId = await CreateOrganizationAsync(app, w.OutsiderToken, "Other Org", "other-org");
        w.OtherCompanyId = await CreateCompanyAsync(app, w.OutsiderToken, w.OtherOrgId, "Other Co", "other-co");

        await w.ExecAsync($"""
            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            SELECT '{w.OrgId}'::uuid, id, 'admin' FROM identity.users WHERE email = '{AdminEmail}';
            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            SELECT '{w.OrgId}'::uuid, id, 'member' FROM identity.users WHERE email = '{MemberEmail}';
            INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role)
            SELECT '{w.CompanyId}'::uuid, '{w.OrgId}'::uuid, id, 'admin' FROM identity.users WHERE email = '{AdminEmail}';
            INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role)
            SELECT '{w.CompanyId}'::uuid, '{w.OrgId}'::uuid, id, 'member' FROM identity.users WHERE email = '{MemberEmail}';
            """);

        w.OwnerId = await w.UserIdAsync(OwnerEmail);
        w.AdminId = await w.UserIdAsync(AdminEmail);
        w.MemberId = await w.UserIdAsync(MemberEmail);
        w.OutsiderId = await w.UserIdAsync(OutsiderEmail);
        return w;
    }

    public ValueTask DisposeAsync() => App.DisposeAsync();

    // ---------------------------------------------------------------- urls

    public string Units(Guid? company = null) => $"/api/v1/companies/{company ?? CompanyId}/catalog/units";
    public string Unit(Guid id, Guid? company = null) => $"{Units(company)}/{id}";
    public string Products(Guid? company = null) => $"/api/v1/companies/{company ?? CompanyId}/catalog/products";
    public string Product(Guid id, Guid? company = null) => $"{Products(company)}/{id}";

    // ---------------------------------------------------------------- http

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, string? token, object? body = null, string? correlationId = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (correlationId is not null) request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        if (body is not null) request.Content = JsonContent.Create(body);
        return App.Client.SendAsync(request);
    }

    public static Task<JsonElement> ReadAsync(HttpResponseMessage response) => IdentityTestApp.ReadJsonAsync(response);

    public async Task<List<JsonElement>> ListAsync(string url, string token)
    {
        var response = await SendAsync(HttpMethod.Get, url, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).EnumerateArray().ToList();
    }

    public async Task<JsonElement> CreateUnitAsync(
        string token, string code, string name, Guid? id = null, Guid? company = null)
    {
        var response = await SendAsync(HttpMethod.Post, Units(company), token,
            new { id = id ?? Guid.NewGuid(), code, name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    public async Task<JsonElement> CreateProductAsync(
        string token, string sku, string name, Guid? unitId = null, decimal sale = 1m, decimal cost = 0m,
        string? barcode = null, Guid? id = null, Guid? company = null)
    {
        var response = await SendAsync(HttpMethod.Post, Products(company), token,
            new { id = id ?? Guid.NewGuid(), sku, barcode, name, unitId, salePrice = sale, costPrice = cost });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    public static void AssertFieldError(JsonElement problem, string field) =>
        Assert.True(
            problem.TryGetProperty("errors", out var errors) && errors.TryGetProperty(field, out _),
            $"Expected a field error for '{field}' in: {problem}");

    // ------------------------------------------------------------ database

    public Task ExecAsync(string sql) => Sql.ExecAsync(App.Db.OwnerConnectionString, sql);

    public async Task<long> CountAsync(string sql) =>
        Convert.ToInt64(await Sql.ScalarAsync(App.Db.OwnerConnectionString, sql));

    private async Task<Guid> UserIdAsync(string email) =>
        (Guid)(await Sql.ScalarAsync(App.Db.OwnerConnectionString, $"select id from identity.users where email = '{email}'"))!;

    // ------------------------------------------------------------- set-up

    private static async Task<string> SignUpAsync(IdentityTestApp app, string email)
    {
        var register = await app.RegisterAsync(email, Scenario.Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return (await Scenario.LoginAsync(app, email, Scenario.Password)).AccessToken;
    }

    private static async Task<HttpResponseMessage> PostAsync(IdentityTestApp app, string url, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await app.Client.SendAsync(request);
    }

    private static async Task<Guid> CreateOrganizationAsync(IdentityTestApp app, string token, string name, string slug)
    {
        var response = await PostAsync(app, "/api/v1/organizations", token, new { name, slug });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCompanyAsync(
        IdentityTestApp app, string token, Guid organizationId, string name, string slug)
    {
        var response = await PostAsync(app, $"/api/v1/organizations/{organizationId}/companies", token,
            new { name, slug, industryKey = "retail" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }
}