using System.Net;
using FlexiCore.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class HealthEndpointTests(PostgresFixture postgres)
{
    // Each test host receives isolated configuration; no process-wide environment mutation.
    private static TestApiFactory CreateFactory(string connectionString)
    {
        return new TestApiFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connectionString,
            ["Jwt:SigningKey"] = TestSecrets.SigningKeyBase64
        });
    }

    private const string UnreachableDb = "Host=127.0.0.1;Port=1;Database=x;Username=u;Password=p;Timeout=2";

    [Fact]
    public async Task Live_and_ready_are_healthy_when_the_database_is_reachable()
    {
        var db = await postgres.CreateDatabaseAsync();
        await using var factory = CreateFactory(db.AppConnectionString);
        using var client = factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_is_503_but_live_stays_200_when_the_database_is_unreachable()
    {
        await using var factory = CreateFactory(UnreachableDb);
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var live = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync()); // no exception text in the body
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task A_plain_correlation_id_is_echoed_and_a_hostile_one_is_replaced()
    {
        await using var factory = CreateFactory(UnreachableDb);
        using var client = factory.CreateClient();

        using var good = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        good.Headers.TryAddWithoutValidation("X-Correlation-Id", "test-123");
        using var goodResponse = await client.SendAsync(good);
        Assert.Equal("test-123", goodResponse.Headers.GetValues("X-Correlation-Id").Single());

        using var bad = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        bad.Headers.TryAddWithoutValidation("X-Correlation-Id", "bad id;<script>");
        using var badResponse = await client.SendAsync(bad);
        var replaced = badResponse.Headers.GetValues("X-Correlation-Id").Single();
        Assert.Equal(32, replaced.Length);
        Assert.DoesNotContain("<", replaced);
    }
}


