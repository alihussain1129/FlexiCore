using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlexiCore.Migrations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FlexiCore.IntegrationTests.Infrastructure;

/// <summary>A migrated database plus a running API host. Tests in the "postgres" collection run sequentially, so the process environment is safe to use.</summary>
public sealed class IdentityTestApp : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public TestDatabase Db { get; }
    public HttpClient Client { get; }

    private IdentityTestApp(TestDatabase db, WebApplicationFactory<Program> factory)
    {
        Db = db;
        _factory = factory;
        Client = factory.CreateClient();
    }

    /// <param name="registrationEnabled">"true", "false", or null to leave Registration:Enabled unset (the default).</param>
    /// <param name="rateLimitPerMinute">Per-IP request limit for the auth endpoints; high by default so ordinary tests never hit it.</param>
    /// <param name="configureServices">Optional test-only service overrides (for example a log-capturing provider).</param>
    public static async Task<IdentityTestApp> StartAsync(
        PostgresFixture postgres, string? registrationEnabled = "true", string rateLimitPerMinute = "100000",
        Action<IServiceCollection>? configureServices = null)
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", db.AppConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSecrets.SigningKeyBase64);
        Environment.SetEnvironmentVariable("Registration__Enabled", registrationEnabled);
        Environment.SetEnvironmentVariable("Identity__RateLimit__PermitPerMinute", rateLimitPerMinute);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            if (configureServices is not null) b.ConfigureTestServices(configureServices);
        });
        return new IdentityTestApp(db, factory);
    }

    public Task<HttpResponseMessage> RegisterAsync(string email, string password, string? correlationId = null) =>
        PostAsync("/api/v1/auth/register", email, password, correlationId);

    public Task<HttpResponseMessage> LoginAsync(string email, string password, string? correlationId = null) =>
        PostAsync("/api/v1/auth/login", email, password, correlationId);

    public Task<HttpResponseMessage> RefreshAsync(string? refreshToken) =>
        PostJsonAsync("/api/v1/auth/refresh", new { refreshToken });

    public Task<HttpResponseMessage> LogoutAsync(string? refreshToken) =>
        PostJsonAsync("/api/v1/auth/logout", new { refreshToken });

    public Task<HttpResponseMessage> MeAsync(string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        if (accessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return Client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public Task<HttpResponseMessage> PostJsonAsync(string url, object body, string? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (correlationId is not null)
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        return Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAsync(string url, string? email, string? password, string? correlationId) =>
        PostJsonAsync(url, new { email, password }, correlationId);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}