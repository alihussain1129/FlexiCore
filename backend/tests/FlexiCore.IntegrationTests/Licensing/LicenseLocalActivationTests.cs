using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.UnitTests.LicensingTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class LicenseLocalActivationTests(PostgresFixture postgres)
{
    private const string StatusUrl = "/api/v1/license/status";
    private const string ImportUrl = "/api/v1/license";
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Test-controlled remote address. The in-process test host has no socket, so it is null unless set here.</summary>
    private sealed class RemoteAddress
    {
        public IPAddress? Value { get; set; } = IPAddress.Loopback;
    }

    private sealed class RemoteAddressFilter(RemoteAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address.Value;
                return nextMiddleware();
            });
            next(app);
        };
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required IdentityTestApp App { get; init; }
        public required string Token { get; init; }
        public required TestSigner Signer { get; init; }
        public required string Dir { get; init; }
        public required RemoteAddress Remote { get; init; }
        public string StoredPath => Path.Combine(Dir, FileLicenseStore.FileName);

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            Signer.Dispose();
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); }
            catch (IOException) { }
        }
    }

    private async Task<Harness> StartAsync(bool local, bool createUser = true)
    {
        var signer = new TestSigner();
        var dir = Path.Combine(Path.GetTempPath(), "flexicore-license-local-" + Guid.NewGuid().ToString("N"));
        var time = new FixedTimeProvider(Now);
        var remote = new RemoteAddress();

        void Configure(IServiceCollection services)
        {
            services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(remote));
            if (!local) return;
            services.RemoveAll<LicenseService>();
            services.AddSingleton(new LicenseService(
                new FileLicenseStore(dir), new LicenseVerifier(new[] { signer.Trusted() }, time), time));
        }

        var names = new[] { "Deployment__Mode", "License__Directory", "License__ImportPermitPerMinute" };
        var saved = names.Select(n => (Name: n, Old: Environment.GetEnvironmentVariable(n))).ToArray();
        try
        {
            Environment.SetEnvironmentVariable("Deployment__Mode", local ? "Local" : null);
            Environment.SetEnvironmentVariable("License__Directory", local ? dir : null);
            Environment.SetEnvironmentVariable("License__ImportPermitPerMinute", local ? "100" : null);

            var app = createUser
                ? await Scenario.StartWithUserAsync(postgres, "100000", Configure)
                : await IdentityTestApp.StartAsync(postgres, "true", "100000", Configure);
            var token = createUser
                ? (await Scenario.LoginAsync(app)).AccessToken
                : string.Empty;
            return new Harness { App = app, Token = token, Signer = signer, Dir = dir, Remote = remote };
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

    private static Task<HttpResponseMessage> SendAsync(
        Harness h, HttpMethod method, string url, byte[]? body = null, bool signedIn = false,
        string? host = null, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(method, url);
        if (signedIn) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", h.Token);
        if (host is not null) request.Headers.Host = host;
        foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        return h.App.Client.SendAsync(request);
    }

    // ---- Local mode: anonymous loopback is allowed ----

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public async Task Local_anonymous_loopback_can_read_status(string address)
    {
        await using var h = await StartAsync(local: true);
        h.Remote.Value = IPAddress.Parse(address);

        var response = await SendAsync(h, HttpMethod.Get, StatusUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("missing", (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("state").GetString());
    }

    [Fact]
    public async Task Local_anonymous_loopback_can_import_a_valid_license()
    {
        await using var h = await StartAsync(local: true);
        var file = h.Signer.Build(Payload());

        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(file, await File.ReadAllBytesAsync(h.StoredPath));
        var status = await IdentityTestApp.ReadJsonAsync(await SendAsync(h, HttpMethod.Get, StatusUrl));
        Assert.Equal("valid", status.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Local_first_owner_setup_is_license_gated_loopback_only_and_one_time()
    {
        await using var h = await StartAsync(local: true, createUser: false);
        const string setupUrl = "/api/v1/local/setup";
        const string email = "local.owner@example.com";
        const string password = "a-long-local-owner-password";

        var unavailable = await SendAsync(h, HttpMethod.Get, setupUrl);
        Assert.Equal(HttpStatusCode.Forbidden, unavailable.StatusCode);

        h.Remote.Value = IPAddress.Parse("203.0.113.5");
        var remote = await SendAsync(h, HttpMethod.Get, setupUrl);
        Assert.Equal(HttpStatusCode.Forbidden, remote.StatusCode);
        h.Remote.Value = IPAddress.Loopback;

        var license = h.Signer.Build(Payload());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Put, ImportUrl, license)).StatusCode);

        var status = await SendAsync(h, HttpMethod.Get, setupUrl);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.True((await IdentityTestApp.ReadJsonAsync(status)).GetProperty("setupRequired").GetBoolean());

        var publicRegistration = await SendAsync(
            h,
            HttpMethod.Post,
            "/api/v1/auth/register",
            Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { email, password })));
        Assert.Equal(HttpStatusCode.Forbidden, publicRegistration.StatusCode);

        var body = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { email, password }));
        var attempts = await Task.WhenAll(
            SendAsync(h, HttpMethod.Post, setupUrl, body),
            SendAsync(h, HttpMethod.Post, setupUrl, body));
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Conflict);

        status = await SendAsync(h, HttpMethod.Get, setupUrl);
        Assert.False((await IdentityTestApp.ReadJsonAsync(status)).GetProperty("setupRequired").GetBoolean());

        var session = await Scenario.ReadSessionAsync(await h.App.LoginAsync(email, password));
        var organizationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/organizations/")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { name = "Local Business", slug = "local-business" })
        };
        organizationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var organization = await h.App.Client.SendAsync(organizationRequest);
        Assert.Equal(HttpStatusCode.Created, organization.StatusCode);
        var organizationJson = await IdentityTestApp.ReadJsonAsync(organization);
        var companyRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/organizations/{organizationJson.GetProperty("id").GetGuid()}/companies")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                name = "Local Shop",
                slug = "local-shop",
                industryKey = "retail"
            })
        };
        companyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var company = await h.App.Client.SendAsync(companyRequest);
        Assert.Equal(HttpStatusCode.Created, company.StatusCode);

        var companiesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companies");
        companiesRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var companies = await h.App.Client.SendAsync(companiesRequest);
        Assert.Equal(HttpStatusCode.OK, companies.StatusCode);
        var listed = await IdentityTestApp.ReadJsonAsync(companies);
        Assert.Equal("Local Shop", listed[0].GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("localhost:5000")]
    [InlineData("127.0.0.1:5000")]
    [InlineData("[::1]:5000")]
    public async Task Local_anonymous_loopback_with_a_local_host_header_is_allowed(string host)
    {
        await using var h = await StartAsync(local: true);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, StatusUrl, host: host)).StatusCode);
    }

    // ---- Local mode: everything else is refused ----

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Local_anonymous_non_loopback_is_401_and_writes_nothing(string method)
    {
        await using var h = await StartAsync(local: true);
        h.Remote.Value = IPAddress.Parse("203.0.113.5");
        var body = method == "PUT" ? h.Signer.Build(Payload()) : null;

        var response = await SendAsync(h, new HttpMethod(method), method == "PUT" ? ImportUrl : StatusUrl, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(File.Exists(h.StoredPath));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Local_anonymous_with_no_remote_address_is_401_and_writes_nothing(string method)
    {
        await using var h = await StartAsync(local: true);
        h.Remote.Value = null;
        var body = method == "PUT" ? h.Signer.Build(Payload()) : null;

        var response = await SendAsync(h, new HttpMethod(method), method == "PUT" ? ImportUrl : StatusUrl, body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(File.Exists(h.StoredPath));
    }

    [Theory]
    [InlineData("Forwarded", "for=203.0.113.5")]
    [InlineData("X-Forwarded-For", "203.0.113.5")]
    [InlineData("X-Forwarded-Host", "example.test")]
    [InlineData("X-Forwarded-Proto", "https")]
    [InlineData("X-Real-IP", "203.0.113.5")]
    public async Task Local_anonymous_loopback_with_a_forwarding_header_is_401(string name, string value)
    {
        await using var h = await StartAsync(local: true);

        var status = await SendAsync(h, HttpMethod.Get, StatusUrl, headers: (name, value));
        var import = await SendAsync(h, HttpMethod.Put, ImportUrl, h.Signer.Build(Payload()), headers: (name, value));

        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, import.StatusCode);
        Assert.False(File.Exists(h.StoredPath));
    }

    [Theory]
    [InlineData("evil.example")]
    [InlineData("evil.example:5000")]
    [InlineData("localhost.evil.example")]
    public async Task Local_anonymous_loopback_with_a_foreign_host_header_is_401(string host)
    {
        await using var h = await StartAsync(local: true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(h, HttpMethod.Get, StatusUrl, host: host)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await SendAsync(h, HttpMethod.Put, ImportUrl, h.Signer.Build(Payload()), host: host)).StatusCode);
        Assert.False(File.Exists(h.StoredPath));
    }

    [Fact]
    public async Task Local_signed_in_user_from_a_non_loopback_address_still_works()
    {
        await using var h = await StartAsync(local: true);
        h.Remote.Value = IPAddress.Parse("203.0.113.5");

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, StatusUrl, signedIn: true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await SendAsync(h, HttpMethod.Put, ImportUrl, h.Signer.Build(Payload()), signedIn: true)).StatusCode);
    }

    [Fact]
    public async Task Local_anonymous_loopback_import_still_rejects_tampered_expired_and_older_licenses()
    {
        await using var h = await StartAsync(local: true);

        var signedOriginal = h.Signer.Sign(Encoding.UTF8.GetBytes(Payload()));
        var tampered = TestSigner.Envelope(Encoding.UTF8.GetBytes(Payload(expires: "2099-01-01T00:00:00Z")), signedOriginal);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(h, HttpMethod.Put, ImportUrl, tampered)).StatusCode);
        Assert.False(File.Exists(h.StoredPath));

        var expired = h.Signer.Build(Payload(expires: "2026-05-01T00:00:00Z"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await SendAsync(h, HttpMethod.Put, ImportUrl, expired)).StatusCode);
        Assert.False(File.Exists(h.StoredPath));

        var current = h.Signer.Build(Payload(issuedAt: "2026-03-01T00:00:00Z"));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Put, ImportUrl, current)).StatusCode);
        var older = h.Signer.Build(Payload(issuedAt: "2026-02-01T00:00:00Z"));
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(h, HttpMethod.Put, ImportUrl, older)).StatusCode);
        Assert.Equal(current, await File.ReadAllBytesAsync(h.StoredPath));
    }

    [Theory]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/organizations")]
    [InlineData("/api/v1/companies")]
    public async Task Local_anonymous_loopback_gets_no_access_to_other_endpoints(string url)
    {
        await using var h = await StartAsync(local: true);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(h, HttpMethod.Get, url)).StatusCode);
    }

    // ---- Hosted mode is unchanged ----

    [Fact]
    public async Task Hosted_anonymous_loopback_status_is_401_and_import_route_does_not_exist()
    {
        await using var h = await StartAsync(local: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(h, HttpMethod.Get, StatusUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(h, HttpMethod.Put, ImportUrl, "{}"u8.ToArray())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, StatusUrl, signedIn: true)).StatusCode);
    }
}