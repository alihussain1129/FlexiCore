using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.UnitTests.LicensingTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LicenseImportApiTests(PostgresFixture postgres)
{
    private const string StatusUrl = "/api/v1/license/status";
    private const string ImportUrl = "/api/v1/license";
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class Harness : IAsyncDisposable
    {
        public required IdentityTestApp App { get; init; }
        public required string Token { get; init; }
        public required TestSigner Signer { get; init; }
        public required string Dir { get; init; }
        public string StoredPath => Path.Combine(Dir, FileLicenseStore.FileName);

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            Signer.Dispose();
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); }
            catch (IOException) { }
        }
    }

    private async Task<Harness> StartAsync(string? permitPerMinute = null, Func<TestSigner, byte[]>? preStored = null)
    {
        var signer = new TestSigner();
        var dir = Path.Combine(Path.GetTempPath(), "flexicore-license-import-" + Guid.NewGuid().ToString("N"));
        var time = new FixedTimeProvider(Now);

        if (preStored is not null)
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, FileLicenseStore.FileName), preStored(signer));
        }

        void Configure(IServiceCollection services)
        {
            services.RemoveAll<LicenseService>();
            services.AddSingleton(new LicenseService(
                new FileLicenseStore(dir), new LicenseVerifier(new[] { signer.Trusted() }, time), time));
        }

        var saved = new[] { "Deployment__Mode", "License__Directory", "License__ImportPermitPerMinute" }
            .Select(n => (Name: n, Old: Environment.GetEnvironmentVariable(n))).ToArray();
        try
        {
            Environment.SetEnvironmentVariable("Deployment__Mode", "Local");
            Environment.SetEnvironmentVariable("License__Directory", dir);
            Environment.SetEnvironmentVariable("License__ImportPermitPerMinute", permitPerMinute);

            var app = await Scenario.StartWithUserAsync(postgres, "100000", Configure);
            var token = (await Scenario.LoginAsync(app)).AccessToken;
            return new Harness { App = app, Token = token, Signer = signer, Dir = dir };
        }
        finally
        {
            foreach (var (name, old) in saved) Environment.SetEnvironmentVariable(name, old);
        }
    }

    private static string Payload(string issuedAt = "2026-01-01T00:00:00Z", string expires = "2027-01-01T00:00:00Z") =>
        TestSigner.ValidPayload
            .Replace("2026-01-01T00:00:00Z", issuedAt)
            .Replace("2027-01-01T00:00:00Z", expires);

    private static Task<HttpResponseMessage> SendAsync(Harness h, HttpMethod method, string url, byte[]? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", h.Token);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        return h.App.Client.SendAsync(request);
    }

    // ---- successful import ----

    [Fact]
    public async Task Valid_import_is_200_replaced_and_stores_the_exact_bytes()
    {
        await using var h = await StartAsync();
        var file = h.Signer.Build(Payload());

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal("local", json.GetProperty("mode").GetString());
        Assert.Equal("valid", json.GetProperty("state").GetString());
        Assert.True(json.GetProperty("replaced").GetBoolean());
        Assert.Equal("LIC-0001", json.GetProperty("license").GetProperty("licenseId").GetString());
        Assert.Equal(file, await File.ReadAllBytesAsync(h.StoredPath));

        var status = await IdentityTestApp.ReadJsonAsync(await SendAsync(h, HttpMethod.Get, StatusUrl));
        Assert.Equal("valid", status.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Importing_identical_bytes_again_is_200_with_replaced_false()
    {
        await using var h = await StartAsync();
        var file = h.Signer.Build(Payload());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Put, ImportUrl, file)).StatusCode);

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await IdentityTestApp.ReadJsonAsync(response)).GetProperty("replaced").GetBoolean());
    }

    [Fact]
    public async Task A_newer_license_replaces_the_current_one()
    {
        await using var h = await StartAsync();
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(h, HttpMethod.Put, ImportUrl, h.Signer.Build(Payload(issuedAt: "2026-02-01T00:00:00Z")))).StatusCode);
        var newer = h.Signer.Build(Payload(issuedAt: "2026-03-01T00:00:00Z"));

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, newer);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await IdentityTestApp.ReadJsonAsync(response)).GetProperty("replaced").GetBoolean());
        Assert.Equal(newer, await File.ReadAllBytesAsync(h.StoredPath));
    }

    // ---- refused imports ----

    [Fact]
    public async Task An_older_license_is_409_and_the_current_file_is_untouched()
    {
        await using var h = await StartAsync();
        var current = h.Signer.Build(Payload(issuedAt: "2026-03-01T00:00:00Z"));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Put, ImportUrl, current)).StatusCode);

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, h.Signer.Build(Payload(issuedAt: "2026-02-01T00:00:00Z")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal("urn:flexicore:problem:license-older-than-current", json.GetProperty("type").GetString());
        Assert.Equal(current, await File.ReadAllBytesAsync(h.StoredPath));
    }

    [Fact]
    public async Task A_tampered_license_is_422_invalid_signature_and_nothing_is_written_or_echoed()
    {
        await using var h = await StartAsync();
        var signedOriginal = h.Signer.Sign(Encoding.UTF8.GetBytes(Payload()));
        var tampered = TestSigner.Envelope(Encoding.UTF8.GetBytes(Payload(expires: "2099-01-01T00:00:00Z")), signedOriginal);

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, tampered);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("urn:flexicore:problem:license-rejected", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("invalid_signature", doc.RootElement.GetProperty("reason").GetString());
        Assert.DoesNotContain("Test Customer", body, StringComparison.Ordinal);
        Assert.False(File.Exists(h.StoredPath));
    }

    [Fact]
    public async Task An_expired_license_is_422_expired_and_nothing_is_written()
    {
        await using var h = await StartAsync();

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl,
            h.Signer.Build(Payload(expires: "2026-05-01T00:00:00Z")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("expired", (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("reason").GetString());
        Assert.False(File.Exists(h.StoredPath));
    }

    [Fact]
    public async Task A_malformed_file_is_400_and_nothing_is_written()
    {
        await using var h = await StartAsync();

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, "{\"not\":\"a license\"}"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("urn:flexicore:problem:license-malformed",
            (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("type").GetString());
        Assert.False(File.Exists(h.StoredPath));
    }

    // ---- status of a stored file ----

    [Fact]
    public async Task Status_with_nothing_stored_is_missing()
    {
        await using var h = await StartAsync();

        var json = await IdentityTestApp.ReadJsonAsync(await SendAsync(h, HttpMethod.Get, StatusUrl));

        Assert.Equal("missing", json.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("license").ValueKind);
    }

    [Fact]
    public async Task Status_of_a_stored_expired_license_is_expired_with_details()
    {
        await using var h = await StartAsync(preStored: s => s.Build(Payload(expires: "2026-05-01T00:00:00Z")));

        var json = await IdentityTestApp.ReadJsonAsync(await SendAsync(h, HttpMethod.Get, StatusUrl));

        Assert.Equal("expired", json.GetProperty("state").GetString());
        Assert.Equal("expired", json.GetProperty("reason").GetString());
        Assert.Equal("LIC-0001", json.GetProperty("license").GetProperty("licenseId").GetString());
    }

    [Fact]
    public async Task Status_of_a_stored_garbage_file_is_invalid_without_details()
    {
        await using var h = await StartAsync(preStored: _ => "garbage"u8.ToArray());

        var json = await IdentityTestApp.ReadJsonAsync(await SendAsync(h, HttpMethod.Get, StatusUrl));

        Assert.Equal("invalid", json.GetProperty("state").GetString());
        Assert.Equal("malformed", json.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("license").ValueKind);
    }

    // ---- rate limit ----

    [Fact]
    public async Task Import_is_rate_limited_per_user()
    {
        await using var h = await StartAsync(permitPerMinute: "2");
        var file = h.Signer.Build(Payload());

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Put, ImportUrl, file)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Put, ImportUrl, file)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsync(h, HttpMethod.Put, ImportUrl, file)).StatusCode);

        // Status reads are not limited by the import policy.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, StatusUrl)).StatusCode);
    }
}