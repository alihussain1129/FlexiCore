using FlexiCore.IntegrationTests.Infrastructure;
using static FlexiCore.IntegrationTests.SessionTestHelpers;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LockoutIsolationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Concurrent_failed_logins_never_fail_with_a_server_error_when_the_database_default_is_repeatable_read()
    {
        await using var app = await Scenario.StartWithUserAsync(postgres);
        await UseRepeatableReadByDefaultAsync(app);

        var attempts = Enumerable.Range(0, 8).Select(_ => app.LoginAsync(Scenario.Email, "wrong-password-" + Guid.NewGuid())).ToList();
        var responses = await Task.WhenAll(attempts);

        Assert.True(responses.All(r => (int)r.StatusCode < 500),
            "statuses: " + string.Join(", ", responses.Select(r => (int)r.StatusCode)));
        Assert.True(Convert.ToInt64(await Sql.ScalarAsync(app.Db.OwnerConnectionString,
            "select failed_login_count from identity.users")) > 0);
    }
}