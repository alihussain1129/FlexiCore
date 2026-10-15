using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenancyRateLimitTests(PostgresFixture postgres)
{
    private const string Password = Scenario.Password;
    private const string UserA = "owner.one@example.test";
    private const string UserB = "outsider.four@example.test";

    private static Task<IdentityTestApp> StartAsync(PostgresFixture postgres, string limit) =>
        IdentityTestApp.StartAsync(postgres, tenancyCreateLimitPerMinute: limit);

    private static async Task<string> SignUpAsync(IdentityTestApp app, string email)
    {
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(email, Password)).StatusCode);
        return (await Scenario.LoginAsync(app, email, Password)).AccessToken;
    }

    private static Task<HttpResponseMessage> SendAsync(
        IdentityTestApp app, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return app.Client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> CreateOrgAsync(IdentityTestApp app, string token, string slug) =>
        SendAsync(app, HttpMethod.Post, "/api/v1/organizations", token, new { name = $"Org {slug}", slug });

    private static Task<HttpResponseMessage> CreateCompanyAsync(
        IdentityTestApp app, string token, Guid organizationId, string slug) =>
        SendAsync(app, HttpMethod.Post, $"/api/v1/organizations/{organizationId}/companies", token,
            new { name = $"Company {slug}", slug, industryKey = "retail" });

    private static async Task<long> CountAsync(IdentityTestApp app, string sql)
    {
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Creations_over_the_limit_get_429_with_Retry_After()
    {
        await using var app = await StartAsync(postgres, "3");
        var token = await SignUpAsync(app, UserA);

        for (var i = 1; i <= 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, token, $"rl-org-{i}")).StatusCode);

        var limited = await CreateOrgAsync(app, token, "rl-org-4");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task The_limit_is_per_user_not_per_IP()
    {
        await using var app = await StartAsync(postgres, "3");
        var tokenA = await SignUpAsync(app, UserA);
        var tokenB = await SignUpAsync(app, UserB);

        for (var i = 1; i <= 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, tokenA, $"a-org-{i}")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await CreateOrgAsync(app, tokenA, "a-org-4")).StatusCode);

        // Same client IP, different user: unaffected.
        Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, tokenB, "b-org-1")).StatusCode);
    }

    [Fact]
    public async Task A_rejected_request_creates_nothing_and_writes_no_audit_row()
    {
        await using var app = await StartAsync(postgres, "3");
        var token = await SignUpAsync(app, UserA);

        for (var i = 1; i <= 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, token, $"rl-org-{i}")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await CreateOrgAsync(app, token, "rl-org-4")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await CreateOrgAsync(app, token, "rl-org-5")).StatusCode);

        Assert.Equal(3, await CountAsync(app, "SELECT count(*) FROM tenancy.organizations"));
        Assert.Equal(3, await CountAsync(app, "SELECT count(*) FROM audit.events WHERE action LIKE 'tenancy.%'"));
        Assert.Equal(0, await CountAsync(app,
            "SELECT count(*) FROM audit.events WHERE action LIKE 'tenancy.%' AND outcome <> 'success'"));
    }

    [Fact]
    public async Task Organization_and_company_creation_share_one_window()
    {
        await using var app = await StartAsync(postgres, "3");
        var token = await SignUpAsync(app, UserA);

        var orgResponse = await CreateOrgAsync(app, token, "shared-org");
        Assert.Equal(HttpStatusCode.Created, orgResponse.StatusCode);
        var orgId = (await IdentityTestApp.ReadJsonAsync(orgResponse)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, (await CreateCompanyAsync(app, token, orgId, "shop-one")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateCompanyAsync(app, token, orgId, "shop-two")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await CreateCompanyAsync(app, token, orgId, "shop-three")).StatusCode);
    }

    [Fact]
    public async Task Reads_are_never_rate_limited()
    {
        await using var app = await StartAsync(postgres, "2");
        var token = await SignUpAsync(app, UserA);

        Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, token, "read-org-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateOrgAsync(app, token, "read-org-2")).StatusCode);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(app, HttpMethod.Get, "/api/v1/organizations", token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(app, HttpMethod.Get, "/api/v1/companies", token)).StatusCode);
        }
    }
}