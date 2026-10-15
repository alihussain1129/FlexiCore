using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class OrganizationMemberChangeTests(PostgresFixture postgres)
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

    private static async Task<Scenario.Session> RegisterAndAddAsync(
        IdentityTestApp app, string org, string ownerToken, string email, string role)
    {
        var session = await RegisterAsync(app, email);
        var add = await CallAsync(app, HttpMethod.Post, $"/api/v1/organizations/{org}/members", ownerToken, new { email, role });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        return session;
    }

    private static async Task<string> UserIdAsync(IdentityTestApp app, string normalizedEmail) =>
        (string)(await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            $"select id::text from identity.users where email_normalized = '{normalizedEmail}'"))!;

    private static async Task<string?> RoleAsync(IdentityTestApp app, string org, string userId) =>
        Convert.ToString(await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            $"select role from tenancy.organization_memberships where organization_id = '{org}'::uuid and user_id = '{userId}'::uuid"));

    private static async Task<long> CountAsync(IdentityTestApp app, string sql) =>
        Convert.ToInt64(await Sql.ScalarAsync(app.Db.OwnerConnectionString, sql));

    private static Task<long> AuditCountAsync(IdentityTestApp app, string action, string outcome, string reason) =>
        CountAsync(app,
            $"select count(*) from audit.events where action = '{action}' and outcome = '{outcome}' " +
            $"and coalesce(metadata->>'reason', '') = '{reason}'");

    private static Task<HttpResponseMessage> ChangeRoleAsync(IdentityTestApp app, string org, string token, string userId, string role) =>
        CallAsync(app, HttpMethod.Put, $"/api/v1/organizations/{org}/members/{userId}/role", token, new { role });

    private static Task<HttpResponseMessage> RemoveAsync(IdentityTestApp app, string org, string token, string userId) =>
        CallAsync(app, HttpMethod.Delete, $"/api/v1/organizations/{org}/members/{userId}", token);

    [Fact]
    public async Task An_owner_can_promote_a_member_and_the_change_is_audited()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "member");
        var bobId = await UserIdAsync(app, "bob@example.com");

        var change = await ChangeRoleAsync(app, org, owner.AccessToken, bobId, "admin");

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal("admin", await RoleAsync(app, org, bobId));
        Assert.Equal(1L, await AuditCountAsync(app, "tenancy.member.role_change", "success", ""));
    }

    [Fact]
    public async Task An_admin_cannot_change_roles()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var bob = await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "admin");
        await RegisterAndAddAsync(app, org, owner.AccessToken, "carol@example.com", "member");
        var carolId = await UserIdAsync(app, "carol@example.com");

        var change = await ChangeRoleAsync(app, org, bob.AccessToken, carolId, "admin");

        Assert.Equal(HttpStatusCode.Forbidden, change.StatusCode);
        Assert.Equal("member", await RoleAsync(app, org, carolId));
        Assert.Equal(1L, await AuditCountAsync(app, "tenancy.member.role_change", "denied", "not_permitted"));
    }

    [Fact]
    public async Task The_last_owner_cannot_demote_themselves()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var ownerId = await UserIdAsync(app, Scenario.Email.ToLowerInvariant());

        var change = await ChangeRoleAsync(app, org, owner.AccessToken, ownerId, "member");

        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Equal("owner", await RoleAsync(app, org, ownerId));
        Assert.Equal(1L, await AuditCountAsync(app, "tenancy.member.role_change", "denied", "last_owner"));
    }

    [Fact]
    public async Task The_last_owner_cannot_leave_but_one_of_two_owners_can()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var ownerId = await UserIdAsync(app, Scenario.Email.ToLowerInvariant());

        Assert.Equal(HttpStatusCode.Conflict, (await RemoveAsync(app, org, owner.AccessToken, ownerId)).StatusCode);
        Assert.Equal("owner", await RoleAsync(app, org, ownerId));

        var bob = await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "owner");
        var bobId = await UserIdAsync(app, "bob@example.com");

        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(app, org, bob.AccessToken, bobId)).StatusCode);
        Assert.True(string.IsNullOrEmpty(await RoleAsync(app, org, bobId)));
        Assert.Equal(1L, await CountAsync(app,
            $"select count(*) from tenancy.organization_memberships where organization_id = '{org}'::uuid and role = 'owner'"));
    }

    [Fact]
    public async Task An_admin_removes_members_but_not_admins_or_owners()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var ownerId = await UserIdAsync(app, Scenario.Email.ToLowerInvariant());
        var bob = await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "admin");
        await RegisterAndAddAsync(app, org, owner.AccessToken, "carol@example.com", "member");
        await RegisterAndAddAsync(app, org, owner.AccessToken, "dave@example.com", "admin");
        var carolId = await UserIdAsync(app, "carol@example.com");
        var daveId = await UserIdAsync(app, "dave@example.com");

        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(app, org, bob.AccessToken, carolId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RemoveAsync(app, org, bob.AccessToken, daveId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RemoveAsync(app, org, bob.AccessToken, ownerId)).StatusCode);
        Assert.Equal("admin", await RoleAsync(app, org, daveId));
        Assert.Equal("owner", await RoleAsync(app, org, ownerId));
        Assert.Equal(2L, await AuditCountAsync(app, "tenancy.member.remove", "denied", "not_permitted"));
    }

    [Fact]
    public async Task A_plain_member_can_leave_but_cannot_remove_others()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var ownerId = await UserIdAsync(app, Scenario.Email.ToLowerInvariant());
        var bob = await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "member");
        var bobId = await UserIdAsync(app, "bob@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await RemoveAsync(app, org, bob.AccessToken, ownerId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await RemoveAsync(app, org, bob.AccessToken, bobId)).StatusCode);

        var orgs = await CallAsync(app, HttpMethod.Get, "/api/v1/organizations", bob.AccessToken);
        Assert.Empty((await IdentityTestApp.ReadJsonAsync(orgs)).EnumerateArray());
    }

    [Fact]
    public async Task Removing_a_member_also_removes_their_company_memberships_in_that_organization()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "member");
        var bobId = await UserIdAsync(app, "bob@example.com");

        var company = await CallAsync(app, HttpMethod.Post, $"/api/v1/organizations/{org}/companies", owner.AccessToken,
            new { name = "Main", slug = "main-co", industryKey = "retail" });
        Assert.Equal(HttpStatusCode.Created, company.StatusCode);
        var companyId = (string)(await Sql.ScalarAsync(app.Db.OwnerConnectionString, "select id::text from tenancy.companies"))!;
        await Sql.ExecAsync(app.Db.OwnerConnectionString,
            "insert into tenancy.company_memberships (company_id, organization_id, user_id, role) " +
            $"values ('{companyId}'::uuid, '{org}'::uuid, '{bobId}'::uuid, 'member')");

        var removal = await RemoveAsync(app, org, owner.AccessToken, bobId);

        Assert.Equal(HttpStatusCode.NoContent, removal.StatusCode);
        Assert.Equal(0L, await CountAsync(app, $"select count(*) from tenancy.company_memberships where user_id = '{bobId}'::uuid"));
        Assert.Equal(1L, await CountAsync(app, "select count(*) from tenancy.company_memberships"));
        Assert.Equal(1L, await AuditCountAsync(app, "tenancy.member.remove", "success", ""));
    }

    [Fact]
    public async Task Two_owners_demoting_themselves_at_the_same_time_cannot_leave_the_organization_without_an_owner()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        var owner = await Scenario.LoginAsync(app);
        var org = await CreateOrgAsync(app, owner.AccessToken);
        var ownerId = await UserIdAsync(app, Scenario.Email.ToLowerInvariant());
        var bob = await RegisterAndAddAsync(app, org, owner.AccessToken, "bob@example.com", "owner");
        var bobId = await UserIdAsync(app, "bob@example.com");
        var ownerConnection = app.Db.OwnerConnectionString;

        await using var holder = new NpgsqlConnection(ownerConnection);
        await holder.OpenAsync();
        var lockSql = $"hashtextextended('tenancy.members:{org}', 0)";
        await using (var acquire = new NpgsqlCommand($"select pg_advisory_lock({lockSql})", holder)) await acquire.ExecuteNonQueryAsync();

        Task<HttpResponseMessage> first;
        Task<HttpResponseMessage> second;
        try
        {
            first = ChangeRoleAsync(app, org, owner.AccessToken, ownerId, "member");
            second = ChangeRoleAsync(app, org, bob.AccessToken, bobId, "member");

            var waited = false;
            for (var i = 0; i < 200 && !waited; i++)
            {
                var blocked = await CountAsync(app,
                    "select count(*) from pg_locks where locktype = 'advisory' and not granted " +
                    "and database = (select oid from pg_database where datname = current_database())");
                waited = blocked >= 2;
                if (!waited) await Task.Delay(50);
            }

            Assert.True(waited, "both requests should be waiting on the organization member lock");
        }
        finally
        {
            await using var release = new NpgsqlCommand($"select pg_advisory_unlock({lockSql})", holder);
            await release.ExecuteNonQueryAsync();
        }

        var statuses = (await Task.WhenAll(first, second)).Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();

        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, statuses);
        Assert.Equal(1L, await CountAsync(app,
            $"select count(*) from tenancy.organization_memberships where organization_id = '{org}'::uuid and role = 'owner' and status = 'active'"));
    }
}