using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlexiCore.IntegrationTests.Infrastructure;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class OrganizationMemberTests(PostgresFixture postgres)
{
    private const string OtherPassword = "Zx9-Quartz-lantern-77";

    private static async Task<HttpResponseMessage> CallAsync(
        IdentityTestApp app, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await app.Client.SendAsync(request);
    }

    private static async Task<string> CreateOrgAsync(IdentityTestApp app, string token)
    {
        var response = await CallAsync(app, HttpMethod.Post, "/api/v1/organizations", token, new { name = "Acme", slug = "acme-co" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("id").GetString()!;
    }

    private static async Task<Scenario.Session> RegisterAsync(IdentityTestApp app, string email)
    {
        Assert.Equal(HttpStatusCode.Created, (await app.RegisterAsync(email, OtherPassword)).StatusCode);
        return await Scenario.LoginAsync(app, email, OtherPassword);
    }

    private static Task<HttpResponseMessage> AddAsync(IdentityTestApp app, string org, string token, string email, string role) =>
        CallAsync(app, HttpMethod.Post, $"/api/v1/organizations/{org}/members", token, new { email, role });

    private static async Task<long> AuditCountAsync(IdentityTestApp app, string outcome, string reason) =>
        Convert.ToInt64(await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            $"select count(*) from audit.events where action = 'tenancy.member.add' and outcome = '{outcome}' " +
            $"and coalesce(metadata->>'reason', '') = '{reason}'"));

    [Fact]
    public async Task An_owner_adds_an_existing_user_by_a_differently_cased_email_and_the_success_is_audited()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var bob = await RegisterAsync(app, "Bob@Example.com");

        var add = await AddAsync(app, org, owner.AccessToken, "  BOB@example.COM  ", "member");

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var list = await CallAsync(app, HttpMethod.Get, $"/api/v1/organizations/{org}/members", owner.AccessToken);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var members = (await IdentityTestApp.ReadJsonAsync(list)).EnumerateArray().ToList();
        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => string.Equals(m.GetProperty("email").GetString(), "bob@example.com", StringComparison.OrdinalIgnoreCase)
                                      && m.GetProperty("role").GetString() == "member");

        var orgs = await CallAsync(app, HttpMethod.Get, "/api/v1/organizations", bob.AccessToken);
        Assert.Single((await IdentityTestApp.ReadJsonAsync(orgs)).EnumerateArray());
        Assert.Equal(1L, await AuditCountAsync(app, "success", ""));
    }

    [Fact]
    public async Task Adding_someone_who_is_already_a_member_returns_409_and_audits_the_failure()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        await RegisterAsync(app, "bob@example.com");

        Assert.Equal(HttpStatusCode.Created, (await AddAsync(app, org, owner.AccessToken, "bob@example.com", "member")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await AddAsync(app, org, owner.AccessToken, "bob@example.com", "member")).StatusCode);
        Assert.Equal(1L, await AuditCountAsync(app, "failure", "already_member"));
    }

    [Fact]
    public async Task An_admin_may_add_members_but_not_admins_or_owners()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var bob = await RegisterAsync(app, "bob@example.com");
        await RegisterAsync(app, "carol@example.com");
        await RegisterAsync(app, "dave@example.com");
        Assert.Equal(HttpStatusCode.Created, (await AddAsync(app, org, owner.AccessToken, "bob@example.com", "admin")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await AddAsync(app, org, bob.AccessToken, "carol@example.com", "member")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AddAsync(app, org, bob.AccessToken, "dave@example.com", "admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AddAsync(app, org, bob.AccessToken, "dave@example.com", "owner")).StatusCode);
        Assert.Equal(2L, await AuditCountAsync(app, "denied", "not_permitted"));
    }

    [Fact]
    public async Task A_plain_member_and_an_outsider_can_neither_list_nor_add()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var bob = await RegisterAsync(app, "bob@example.com");
        var mallory = await RegisterAsync(app, "mallory@example.com");
        await RegisterAsync(app, "dave@example.com");
        Assert.Equal(HttpStatusCode.Created, (await AddAsync(app, org, owner.AccessToken, "bob@example.com", "member")).StatusCode);

        foreach (var token in new[] { bob.AccessToken, mallory.AccessToken })
        {
            var list = await CallAsync(app, HttpMethod.Get, $"/api/v1/organizations/{org}/members", token);
            Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await AddAsync(app, org, token, "dave@example.com", "member")).StatusCode);
        }
    }

    [Fact]
    public async Task An_unknown_email_gets_the_same_403_as_a_permission_failure()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);

        var unknown = await AddAsync(app, org, owner.AccessToken, "nobody@example.com", "member");

        Assert.Equal(HttpStatusCode.Forbidden, unknown.StatusCode);
        Assert.Equal(1L, await AuditCountAsync(app, "denied", "not_permitted"));
    }
}