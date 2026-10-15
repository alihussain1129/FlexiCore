using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using static FlexiCore.IntegrationTests.SessionTestHelpers;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class SessionCapIsolationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task The_organization_cap_holds_when_the_database_default_is_repeatable_read()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        await UseRepeatableReadByDefaultAsync(app);
        var session = await Scenario.LoginAsync(app);

        var attempts = Enumerable.Range(0, 14).Select(i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/organizations")
            {
                Content = JsonContent.Create(new { name = $"Org {i}", slug = $"iso-org-{i:00}" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            return app.Client.SendAsync(request);
        }).ToList();
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(10, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Forbidden));
        Assert.Equal(10L, await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            "select count(*) from tenancy.organization_memberships where role = 'owner'"));
    }
}