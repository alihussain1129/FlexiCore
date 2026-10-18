using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class TenancyUsernameTests(PostgresFixture postgres)
{
    private const string Password = Scenario.Password;

    private static Task<HttpResponseMessage> SendAsync(
        IdentityTestApp app, HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return app.Client.SendAsync(request);
    }

    private static async Task<string> SignUpAsync(IdentityTestApp app, string email)
    {
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(email, Password)).StatusCode);
        return (await Scenario.LoginAsync(app, email, Password)).AccessToken;
    }

    private static async Task<Guid> InsertUsernameUserAsync(IdentityTestApp app, string username)
    {
        var hash = new PasswordService(IdentitySettings.DefaultPbkdf2Iterations).Hash(Password);
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "insert into identity.users (username, username_normalized, password_hash) values (@u, @n, @h) returning id",
            connection);
        command.Parameters.AddWithValue("u", username);
        command.Parameters.AddWithValue("n", UsernameRules.Normalize(username));
        command.Parameters.AddWithValue("h", hash);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> CreateOrgAsync(IdentityTestApp app, string token)
    {
        var response = await SendAsync(app, HttpMethod.Post, "/api/v1/organizations", token,
            new { name = "Acme", slug = "acme-trading" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> AddAsync(
        IdentityTestApp app, string token, Guid org, object body) =>
        SendAsync(app, HttpMethod.Post, $"/api/v1/organizations/{org}/members", token, body);

    [Fact]
    public async Task Owner_can_add_a_member_by_username_and_the_list_shows_it()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var owner = await SignUpAsync(app, "owner.one@example.test");
        var memberId = await InsertUsernameUserAsync(app, "Shop.Clerk");
        var org = await CreateOrgAsync(app, owner);

        var add = await AddAsync(app, owner, org, new { username = " SHOP.clerk ", role = "member" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        Assert.Equal(memberId, (await IdentityTestApp.ReadJsonAsync(add)).GetProperty("userId").GetGuid());

        var list = await SendAsync(app, HttpMethod.Get, $"/api/v1/organizations/{org}/members", owner);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var row = (await IdentityTestApp.ReadJsonAsync(list)).EnumerateArray()
            .Single(m => m.GetProperty("userId").GetGuid() == memberId);
        Assert.Equal("Shop.Clerk", row.GetProperty("username").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("email").ValueKind);
    }

    [Fact]
    public async Task Admin_can_add_a_member_by_username_but_not_an_admin()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var owner = await SignUpAsync(app, "owner.one@example.test");
        var adminToken = await SignUpAsync(app, "admin.two@example.test");
        await InsertUsernameUserAsync(app, "till.one");
        await InsertUsernameUserAsync(app, "till.two");
        var org = await CreateOrgAsync(app, owner);

        Assert.Equal(HttpStatusCode.Created,
            (await AddAsync(app, owner, org, new { email = "admin.two@example.test", role = "admin" })).StatusCode);

        Assert.Equal(HttpStatusCode.Created,
            (await AddAsync(app, adminToken, org, new { username = "till.one", role = "member" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await AddAsync(app, adminToken, org, new { username = "till.two", role = "admin" })).StatusCode);
    }

    [Fact]
    public async Task Unknown_username_is_refused_like_a_permission_failure()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var owner = await SignUpAsync(app, "owner.one@example.test");
        var org = await CreateOrgAsync(app, owner);

        var response = await AddAsync(app, owner, org, new { username = "nobody.here", role = "member" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_username_member_gets_conflict()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var owner = await SignUpAsync(app, "owner.one@example.test");
        await InsertUsernameUserAsync(app, "shop.clerk");
        var org = await CreateOrgAsync(app, owner);

        Assert.Equal(HttpStatusCode.Created,
            (await AddAsync(app, owner, org, new { username = "shop.clerk", role = "member" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await AddAsync(app, owner, org, new { username = "shop.clerk", role = "member" })).StatusCode);
    }

    [Theory]
    [InlineData("both", "a.user", "a@example.test")]
    [InlineData("malformed", "ab", null)]
    [InlineData("malformed-chars", "bad name!", null)]
    [InlineData("neither", null, null)]
    public async Task Invalid_identifiers_get_bad_request(string _, string? username, string? email)
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var owner = await SignUpAsync(app, "owner.one@example.test");
        var org = await CreateOrgAsync(app, owner);

        var response = await AddAsync(app, owner, org, new { username, email, role = "member" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}