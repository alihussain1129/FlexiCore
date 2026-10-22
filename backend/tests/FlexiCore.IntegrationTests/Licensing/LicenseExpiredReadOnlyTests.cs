using System.Net;
using System.Text;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.UnitTests.LicensingTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.IntegrationTests;

/// <summary>Local mode: an expired license is read-only; every other bad state still blocks everything.</summary>
[Collection("postgres")]
public sealed class LicenseExpiredReadOnlyTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Expired = "2026-05-01T00:00:00Z";
    private const string Future = "2027-01-01T00:00:00Z";

    private sealed class Harness : IAsyncDisposable
    {
        public required IdentityTestApp App { get; init; }
        public required string Token { get; init; }
        public required TestSigner Signer { get; init; }
        public required string Dir { get; init; }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            Signer.Dispose();
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); }
            catch (IOException) { }
        }
    }

    private async Task<Harness> StartAsync(Func<TestSigner, byte[]>? preStored)
    {
        var signer = new TestSigner();
        var dir = Path.Combine(Path.GetTempPath(), "flexicore-license-ro-" + Guid.NewGuid().ToString("N"));
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

        var saved = new[] { "Deployment__Mode", "License__Directory" }
            .Select(n => (Name: n, Old: Environment.GetEnvironmentVariable(n))).ToArray();
        try
        {
            Environment.SetEnvironmentVariable("Deployment__Mode", "Local");
            Environment.SetEnvironmentVariable("License__Directory", dir);

            var app = await Scenario.StartWithUserAsync(postgres, "100000", Configure);
            var token = (await Scenario.LoginAsync(app)).AccessToken;
            return new Harness { App = app, Token = token, Signer = signer, Dir = dir };
        }
        finally
        {
            foreach (var (name, old) in saved) Environment.SetEnvironmentVariable(name, old);
        }
    }

    private static string Payload(string expires) =>
        TestSigner.ValidPayload.Replace("2027-01-01T00:00:00Z", expires);

    private static Task<HttpResponseMessage> SendAsync(Harness h, HttpMethod method, string url, string? json = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", h.Token);
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return h.App.Client.SendAsync(request);
    }

    [Fact]
    public async Task Expired_license_still_allows_reads()
    {
        await using var h = await StartAsync(s => s.Build(Payload(Expired)));

        var response = await SendAsync(h, HttpMethod.Get, "/api/v1/companies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("expired-read-only", Assert.Single(response.Headers.GetValues("X-License-State")));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Expired_license_blocks_every_change(string method)
    {
        await using var h = await StartAsync(s => s.Build(Payload(Expired)));

        var response = await SendAsync(h, new HttpMethod(method), "/api/v1/organizations/", "{}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal("urn:flexicore:problem:license-required", json.GetProperty("type").GetString());
        Assert.Equal("expired", json.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Missing_license_still_blocks_reads()
    {
        await using var h = await StartAsync(preStored: null);

        var response = await SendAsync(h, HttpMethod.Get, "/api/v1/companies");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.Equal("missing", json.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Valid_license_does_not_block_changes()
    {
        await using var h = await StartAsync(s => s.Build(Payload(Future)));
        var slug = "org-" + Guid.NewGuid().ToString("N")[..8];

        var response = await SendAsync(h, HttpMethod.Post, "/api/v1/organizations/",
            "{\"name\":\"Read Only Test\",\"slug\":\"" + slug + "\"}");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}