using System.Text;
using FlexiCore.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Logging;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LogRedactionTests(PostgresFixture postgres)
{
    private const string WrongPassword = "SENTINEL-wrong-password-9876";
    private const string MalformedBodyPassword = "SENTINEL-malformed-json-pw";
    private const string BadRefreshBody = "SENTINEL-bad-refresh-body";
    private const string GarbageBearer = "SENTINEL-garbage-bearer-value";

    [Fact]
    public async Task Credentials_never_appear_in_logs_across_success_failure_malformed_and_reuse_paths()
    {
        var capture = new CapturingLoggerProvider();
        await using var app = await Scenario.StartWithUserAsync(postgres, configureServices: services =>
        {
            services.AddSingleton<ILoggerProvider>(capture);
            services.Configure<LoggerFilterOptions>(o =>
            {
                o.MinLevel = LogLevel.Trace; // capture everything, including Debug and Trace
                o.Rules.Clear();
            });
        });

        var first = await Scenario.LoginAsync(app);
        var second = await Scenario.ReadSessionAsync(await app.RefreshAsync(first.RefreshToken));
        await app.RefreshAsync(first.RefreshToken);                                  // reuse -> family revoked
        await app.MeAsync(second.AccessToken);                                       // access token of a revoked session
        await app.LoginAsync(Scenario.Email, WrongPassword);                         // failed login
        await app.MeAsync(GarbageBearer);                                            // garbage bearer value
        await app.MeAsync(first.AccessToken[..^3] + "abc");                          // tampered signature
        await app.Client.PostAsync("/api/v1/auth/login",
            new StringContent("{\"email\":\"a@b.co\",\"password\":\"" + MalformedBodyPassword, Encoding.UTF8, "application/json")); // malformed JSON
        await app.Client.PostAsync("/api/v1/auth/refresh",
            new StringContent("{\"refreshToken\":12345,\"x\":\"" + BadRefreshBody + "\"}", Encoding.UTF8, "application/json"));     // wrong type
        var third = await Scenario.LoginAsync(app);
        await app.LogoutAsync(third.RefreshToken);

        var log = string.Join("\n", capture.Entries);
        Assert.Contains("/api/v1/auth/login", log); // proves the capture really saw the requests

        var secrets = new[]
        {
            Scenario.Password, WrongPassword, MalformedBodyPassword, BadRefreshBody, GarbageBearer,
            first.AccessToken, first.RefreshToken, second.AccessToken, second.RefreshToken,
            third.AccessToken, third.RefreshToken,
            Scenario.HashHex(first.RefreshToken), Scenario.HashHex(second.RefreshToken), Scenario.HashHex(third.RefreshToken)
        };
        for (var i = 0; i < secrets.Length; i++)
            Assert.False(log.Contains(secrets[i], StringComparison.Ordinal), $"secret #{i} was found in the logs");

        Assert.False(IdentityModelEventSource.ShowPII);
    }
}