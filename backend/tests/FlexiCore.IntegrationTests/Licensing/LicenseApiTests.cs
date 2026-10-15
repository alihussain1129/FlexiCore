using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LicenseApiTests(PostgresFixture postgres)
{
    private const string StatusUrl = "/api/v1/license/status";
    private const string ImportUrl = "/api/v1/license";

    /// <summary>Sets environment variables for the duration of host startup and restores the old values afterwards.</summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly (string Name, string? Old)[] _saved;

        public EnvScope(params (string Name, string? Value)[] vars)
        {
            _saved = vars.Select(v => (v.Name, Environment.GetEnvironmentVariable(v.Name))).ToArray();
            foreach (var (name, value) in vars) Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, old) in _saved) Environment.SetEnvironmentVariable(name, old);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "flexicore-license-api-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
            catch (IOException) { }
        }
    }

    private async Task<(IdentityTestApp App, string Token)> StartAsync(string? mode, string? directory)
    {
        // The host is built inside StartWithUserAsync, so the variables only need to exist until it returns.
        using (new EnvScope(("Deployment__Mode", mode), ("License__Directory", directory)))
        {
            var app = await Scenario.StartWithUserAsync(postgres);
            var token = (await Scenario.LoginAsync(app)).AccessToken;
            return (app, token);
        }
    }

    private static Task<HttpResponseMessage> SendAsync(
        IdentityTestApp app, HttpMethod method, string url, string? token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return app.Client.SendAsync(request);
    }

    private static ByteArrayContent Body(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response) =>
        (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("type").GetString();

    // ---- Hosted mode ----

    [Theory]
    [InlineData(null)]
    [InlineData("Hosted")]
    [InlineData("hosted")]
    public async Task Hosted_status_is_notApplicable_and_not_cached(string? mode)
    {
        var (app, token) = await StartAsync(mode, null);
        await using var scope = app;

        var response = await SendAsync(app, HttpMethod.Get, StatusUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal("hosted", json.GetProperty("mode").GetString());
        Assert.False(json.GetProperty("licenseRequired").GetBoolean());
        Assert.Equal("notApplicable", json.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("license").ValueKind);
    }

    [Fact]
    public async Task Hosted_status_requires_a_live_session()
    {
        var (app, token) = await StartAsync(null, null);
        await using var scope = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, HttpMethod.Get, StatusUrl, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, HttpMethod.Get, StatusUrl, "not-a-token")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(app, HttpMethod.Get, StatusUrl, token)).StatusCode);
    }

    [Fact]
    public async Task Hosted_has_no_import_route()
    {
        var (app, token) = await StartAsync(null, null);
        await using var scope = app;

        var response = await SendAsync(app, HttpMethod.Put, ImportUrl, token, Body("{}"u8.ToArray(), "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- Local mode, startup ----

    [Fact]
    public async Task Invalid_mode_stops_startup()
    {
        using var dir = new TempDirectory();
        await Assert.ThrowsAnyAsync<Exception>(() => StartAsync("Cloud", dir.Path));
    }

    [Fact]
    public async Task Local_without_a_directory_stops_startup()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => StartAsync("Local", null));
    }

    [Fact]
    public async Task Local_with_a_relative_directory_stops_startup()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => StartAsync("Local", "licenses"));
    }

    // ---- Local mode, production wiring ----

    [Fact]
    public async Task Local_with_the_production_wiring_and_no_embedded_key_reports_unavailable()
    {
        // Holds while the build embeds no production key. When one is embedded, this test must change:
        // the status becomes "missing" for an empty directory.
        using var dir = new TempDirectory();
        var (app, token) = await StartAsync("Local", dir.Path);
        await using var scope = app;

        var response = await SendAsync(app, HttpMethod.Get, StatusUrl, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal("local", json.GetProperty("mode").GetString());
        Assert.True(json.GetProperty("licenseRequired").GetBoolean());
        Assert.Equal("unavailable", json.GetProperty("state").GetString());
        Assert.Equal("no_trusted_keys", json.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("license").ValueKind);
    }

    [Fact]
    public async Task Local_endpoints_require_a_live_session()
    {
        using var dir = new TempDirectory();
        var (app, _) = await StartAsync("Local", dir.Path);
        await using var scope = app;

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(app, HttpMethod.Get, StatusUrl, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(app, HttpMethod.Put, ImportUrl, null, Body("{}"u8.ToArray(), "application/json"))).StatusCode);
    }

    // ---- Local mode, import request handling (rejected before any verification) ----

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/octet-stream")]
    public async Task Import_with_a_wrong_content_type_is_415_and_writes_nothing(string contentType)
    {
        using var dir = new TempDirectory();
        var (app, token) = await StartAsync("Local", dir.Path);
        await using var scope = app;

        var response = await SendAsync(app, HttpMethod.Put, ImportUrl, token, Body("{}"u8.ToArray(), contentType));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("urn:flexicore:problem:license-unsupported-media-type", await ProblemTypeAsync(response));
        Assert.False(Directory.Exists(dir.Path));
    }

    [Fact]
    public async Task Import_over_the_size_limit_is_413_and_writes_nothing()
    {
        using var dir = new TempDirectory();
        var (app, token) = await StartAsync("Local", dir.Path);
        await using var scope = app;

        var oversized = Encoding.UTF8.GetBytes(new string(' ', LicenseLimits.MaxFileBytes + 1));
        var response = await SendAsync(app, HttpMethod.Put, ImportUrl, token, Body(oversized, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("urn:flexicore:problem:license-too-large", await ProblemTypeAsync(response));
        Assert.False(Directory.Exists(dir.Path));
    }
}