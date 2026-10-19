using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlexiCore.Devices.Data;
using FlexiCore.Devices.Security;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.UnitTests.LicensingTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class DevicePairingTests(PostgresFixture postgres)
{
    private const string PairUrl = "/api/v1/devices/pair";
    private const string MeUrl = "/api/v1/devices/me";
    private const string DevicesUrl = "/api/v1/devices";
    private const string CodesUrl = "/api/v1/devices/pairing-codes";
    private const string InvalidCodeType = "urn:flexicore:problem:invalid-pairing-code";
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
        public required TestSigner Signer { get; init; }
        public required string Dir { get; init; }
        public required NpgsqlDataSource DataSource { get; init; }
        public required CapturingLoggerProvider Logs { get; init; }

        public DeviceStore Store => new(DataSource, new DeviceAuditWriter(DataSource));

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            await DataSource.DisposeAsync();
            Signer.Dispose();
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); }
            catch (IOException) { }
        }
    }

    private sealed record Paired(Guid Id, bool IsOwner, string Credential)
    {
        public string Secret => Credential[(Credential.IndexOf('.') + 1)..];
    }

    private async Task<Harness> StartAsync(bool local = true, bool licensed = true, bool withUser = false)
    {
        var signer = new TestSigner();
        var dir = Path.Combine(Path.GetTempPath(), "flexicore-devices-" + Guid.NewGuid().ToString("N"));
        var time = new FixedTimeProvider(Now);
        var remote = new RemoteAddress();
        var logs = new CapturingLoggerProvider();

        void Configure(IServiceCollection services)
        {
            services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(remote));
            services.AddLogging(b => b.AddProvider(logs));
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

            var app = withUser
                ? await Scenario.StartWithUserAsync(postgres, "100000", Configure)
                : await IdentityTestApp.StartAsync(postgres, "true", "100000", Configure);

            var harness = new Harness
            {
                App = app,
                Signer = signer,
                Dir = dir,
                DataSource = NpgsqlDataSource.Create(app.Db.AppConnectionString),
                Logs = logs,
            };

            if (local && licensed) await ImportLicenseAsync(harness);
            return harness;
        }
        finally
        {
            foreach (var (name, old) in saved) Environment.SetEnvironmentVariable(name, old);
        }
    }

    private static async Task ImportLicenseAsync(Harness h)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/license")
        {
            Content = new ByteArrayContent(h.Signer.Build(TestSigner.ValidPayload)),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        var response = await h.App.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> NewCodeAsync(Harness h, bool owner)
    {
        var result = await h.Store.CreatePairingCodeAsync(
            owner, null, "administrator", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10), null, CancellationToken.None);
        Assert.Equal(CodeOutcome.Created, result.Outcome);
        return PairingCode.Format(result.Code!);
    }

    private static Task<HttpResponseMessage> PairAsync(Harness h, string? code, string? name = "Test device") =>
        h.App.PostJsonAsync(PairUrl, new { code, deviceName = name });

    private static async Task<Paired> PairOkAsync(Harness h, string code, string name = "Test device")
    {
        var response = await PairAsync(h, code, name);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        return new Paired(
            json.GetProperty("deviceId").GetGuid(),
            json.GetProperty("isOwner").GetBoolean(),
            json.GetProperty("credential").GetString()!);
    }

    private static Task<HttpResponseMessage> SendAsync(Harness h, HttpMethod method, string url, string? credential = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (credential is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Device", credential);
        return h.App.Client.SendAsync(request);
    }

    private static async Task<string?> ProblemTypeAsync(HttpResponseMessage response) =>
        (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("type").GetString();

    private static async Task<long> AuditCountAsync(Harness h, string where) =>
        Convert.ToInt64(await Sql.ScalarAsync(
            h.App.Db.OwnerConnectionString, "select count(*) from audit.events where " + where));

    // ---- Pairing ----

    [Fact]
    public async Task Pairing_needs_a_valid_license()
    {
        await using var h = await StartAsync(licensed: false);
        var code = await NewCodeAsync(h, owner: true);

        var response = await PairAsync(h, code);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_valid_owner_code_pairs_a_device_that_can_then_authenticate()
    {
        await using var h = await StartAsync();
        var code = await NewCodeAsync(h, owner: true);

        var response = await PairAsync(h, code, "Front desk PC");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        Assert.True(json.GetProperty("isOwner").GetBoolean());
        var deviceId = json.GetProperty("deviceId").GetGuid();
        var credential = json.GetProperty("credential").GetString()!;
        Assert.StartsWith(deviceId.ToString("N") + ".", credential);

        var me = await SendAsync(h, HttpMethod.Get, MeUrl, credential);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meJson = await IdentityTestApp.ReadJsonAsync(me);
        Assert.Equal(deviceId, meJson.GetProperty("deviceId").GetGuid());
        Assert.Equal("Front desk PC", meJson.GetProperty("name").GetString());
        Assert.True(meJson.GetProperty("isOwner").GetBoolean());
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        await using var h = await StartAsync();
        var code = await NewCodeAsync(h, owner: true);
        await PairOkAsync(h, code);

        var second = await PairAsync(h, code, "Second device");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(InvalidCodeType, await ProblemTypeAsync(second));
    }

    [Fact]
    public async Task Malformed_unknown_empty_and_expired_codes_all_get_the_same_answer()
    {
        await using var h = await StartAsync();
        var expired = await NewCodeAsync(h, owner: false);
        await Sql.ExecAsync(
            h.App.Db.OwnerConnectionString,
            "update devices.pairing_codes set created_at = now() - interval '2 hours', expires_at = now() - interval '1 hour'");

        var attempts = new string?[] { null, "", "not a code", PairingCode.Format(PairingCode.Generate()), expired };
        foreach (var attempt in attempts)
        {
            var response = await PairAsync(h, attempt);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(InvalidCodeType, await ProblemTypeAsync(response));
        }
    }

    [Fact]
    public async Task A_bad_device_name_is_rejected_and_does_not_use_up_the_code()
    {
        await using var h = await StartAsync();
        var code = await NewCodeAsync(h, owner: true);

        foreach (var name in new string?[] { null, "   ", new string('x', 81) })
        {
            var response = await PairAsync(h, code, name);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("urn:flexicore:problem:invalid-device-name", await ProblemTypeAsync(response));
        }

        var paired = await PairOkAsync(h, code, "Valid name");
        Assert.True(paired.IsOwner);
    }

    [Fact]
    public async Task Pairing_attempts_are_rate_limited_per_address()
    {
        await using var h = await StartAsync();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 15; i++)
            statuses.Add((await PairAsync(h, PairingCode.Format(PairingCode.Generate()))).StatusCode);

        Assert.All(statuses.Take(10), s => Assert.Equal(HttpStatusCode.BadRequest, s));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    // ---- Authentication ----

    [Fact]
    public async Task Missing_wrong_unknown_and_malformed_credentials_are_all_unauthorized()
    {
        await using var h = await StartAsync();
        var device = await PairOkAsync(h, await NewCodeAsync(h, owner: true));

        var wrongSecret = DeviceSecret.FormatCredential(device.Id, new string('A', DeviceSecret.SecretTextLength));
        var unknownDevice = DeviceSecret.FormatCredential(Guid.NewGuid(), device.Secret);

        foreach (var credential in new string?[] { null, wrongSecret, unknownDevice, "garbage" })
        {
            var response = await SendAsync(h, HttpMethod.Get, MeUrl, credential);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var good = await SendAsync(h, HttpMethod.Get, MeUrl, device.Credential);
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    [Fact]
    public async Task Bearer_tokens_still_work_and_are_not_devices()
    {
        await using var h = await StartAsync(withUser: true);
        var session = await Scenario.LoginAsync(h.App);

        var me = await h.App.MeAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, MeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var asDevice = await h.App.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, asDevice.StatusCode);
    }

    // ---- Owner and ordinary devices ----

    [Fact]
    public async Task An_owner_creates_ordinary_codes_and_ordinary_devices_cannot_use_owner_routes()
    {
        await using var h = await StartAsync();
        var owner = await PairOkAsync(h, await NewCodeAsync(h, owner: true), "Owner PC");

        var codeResponse = await SendAsync(h, HttpMethod.Post, CodesUrl, owner.Credential);
        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        var code = (await IdentityTestApp.ReadJsonAsync(codeResponse)).GetProperty("code").GetString()!;

        var ordinary = await PairOkAsync(h, code, "Tablet");
        Assert.False(ordinary.IsOwner);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, MeUrl, ordinary.Credential)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(h, HttpMethod.Get, DevicesUrl, ordinary.Credential)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(h, HttpMethod.Post, CodesUrl, ordinary.Credential)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{owner.Id}/revoke", ordinary.Credential)).StatusCode);
    }

    [Fact]
    public async Task An_owner_can_list_devices_and_the_list_holds_no_secrets()
    {
        await using var h = await StartAsync();
        var owner = await PairOkAsync(h, await NewCodeAsync(h, owner: true), "Owner PC");
        var ordinary = await PairOkAsync(h, await NewCodeAsync(h, owner: false), "Tablet");

        var response = await SendAsync(h, HttpMethod.Get, DevicesUrl, owner.Credential);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(owner.Secret, body);
        Assert.DoesNotContain(ordinary.Secret, body);
        var list = System.Text.Json.JsonDocument.Parse(body).RootElement;
        Assert.Equal(2, list.GetArrayLength());
        var names = list.EnumerateArray().Select(d => d.GetProperty("name").GetString()).ToArray();
        Assert.Contains("Owner PC", names);
        Assert.Contains("Tablet", names);
    }

    // ---- Revocation ----

    [Fact]
    public async Task Revoking_a_device_takes_effect_immediately()
    {
        await using var h = await StartAsync();
        var owner = await PairOkAsync(h, await NewCodeAsync(h, owner: true), "Owner PC");
        var ordinary = await PairOkAsync(h, await NewCodeAsync(h, owner: false), "Tablet");
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, MeUrl, ordinary.Credential)).StatusCode);

        var revoke = await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{ordinary.Id}/revoke", owner.Credential);

        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(h, HttpMethod.Get, MeUrl, ordinary.Credential)).StatusCode);

        var again = await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{ordinary.Id}/revoke", owner.Credential);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("urn:flexicore:problem:device-already-revoked", await ProblemTypeAsync(again));

        var unknown = await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{Guid.NewGuid()}/revoke", owner.Credential);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task The_last_active_owner_device_cannot_be_revoked()
    {
        await using var h = await StartAsync();
        var first = await PairOkAsync(h, await NewCodeAsync(h, owner: true), "Owner one");
        var second = await PairOkAsync(h, await NewCodeAsync(h, owner: true), "Owner two");

        var revokeSecond = await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{second.Id}/revoke", first.Credential);
        Assert.Equal(HttpStatusCode.NoContent, revokeSecond.StatusCode);

        var revokeLast = await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{first.Id}/revoke", first.Credential);
        Assert.Equal(HttpStatusCode.Conflict, revokeLast.StatusCode);
        Assert.Equal("urn:flexicore:problem:last-owner-device", await ProblemTypeAsync(revokeLast));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(h, HttpMethod.Get, MeUrl, first.Credential)).StatusCode);
    }

    // ---- Secrets and audit ----

    [Fact]
    public async Task Secrets_and_codes_are_stored_only_as_hashes_and_never_logged_or_audited()
    {
        await using var h = await StartAsync();
        var code = await NewCodeAsync(h, owner: true);
        PairingCode.TryNormalize(code, out var normalizedCode);
        var device = await PairOkAsync(h, code);
        await SendAsync(h, HttpMethod.Get, MeUrl, device.Credential);

        var hashLength = Convert.ToInt32(await Sql.ScalarAsync(
            h.App.Db.OwnerConnectionString, "select octet_length(secret_hash) from devices.devices"));
        Assert.Equal(32, hashLength);

        Assert.Equal(0, await AuditCountAsync(h, $"position('{device.Secret}' in metadata::text) > 0"));
        Assert.Equal(0, await AuditCountAsync(h, $"position('{normalizedCode}' in metadata::text) > 0"));
        Assert.DoesNotContain(h.Logs.Entries, e => e.Contains(device.Secret, StringComparison.Ordinal));
        Assert.DoesNotContain(h.Logs.Entries, e => e.Contains(normalizedCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Pairing_code_creation_pairing_denial_and_revocation_are_audited()
    {
        await using var h = await StartAsync();
        var owner = await PairOkAsync(h, await NewCodeAsync(h, owner: true), "Owner PC");
        await PairAsync(h, PairingCode.Format(PairingCode.Generate()));

        var codeResponse = await SendAsync(h, HttpMethod.Post, CodesUrl, owner.Credential);
        var code = (await IdentityTestApp.ReadJsonAsync(codeResponse)).GetProperty("code").GetString()!;
        var ordinary = await PairOkAsync(h, code, "Tablet");
        await SendAsync(h, HttpMethod.Post, $"{DevicesUrl}/{ordinary.Id}/revoke", owner.Credential);

        Assert.Equal(1, await AuditCountAsync(h, $"action = 'device.pair' and outcome = 'success' and target_id = '{owner.Id}'"));
        Assert.Equal(1, await AuditCountAsync(h, $"action = 'device.pair' and outcome = 'success' and target_id = '{ordinary.Id}'"));
        Assert.Equal(1, await AuditCountAsync(h, "action = 'device.pair' and outcome = 'denied'"));
        Assert.Equal(2, await AuditCountAsync(h, "action = 'device.pairing_code.create' and outcome = 'success'"));
        Assert.Equal(1, await AuditCountAsync(h, $"action = 'device.revoke' and outcome = 'success' and target_id = '{ordinary.Id}'"));
    }

    // ---- Hosted mode ----

    [Fact]
    public async Task Hosted_mode_has_no_device_routes()
    {
        await using var h = await StartAsync(local: false);

        var pair = await PairAsync(h, PairingCode.Format(PairingCode.Generate()));
        var me = await SendAsync(h, HttpMethod.Get, MeUrl);

        Assert.Equal(HttpStatusCode.NotFound, pair.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, me.StatusCode);
    }
}