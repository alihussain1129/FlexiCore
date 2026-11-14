using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;
using FlexiCore.UnitTests.LicensingTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlexiCore.IntegrationTests;

/// <summary>
/// End-to-end licensing chain with test keys only: key creation, trust file, issuance (Activation Engine services),
/// production verifier, API import, offline activation, host restart, forgery rejection.
/// Real production private keys are never used; the real production public key file is only hashed, never written.
/// </summary>
[Collection("postgres")]
public sealed class LicenseEndToEndTests(PostgresFixture postgres)
{
    private const string StatusUrl = "/api/v1/license/status";
    private const string ImportUrl = "/api/v1/license";
    private const string KeyId = "e2e-key-1";
    private const string Pass = "correct horse battery staple";
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class Workspace : IDisposable
    {
        public Workspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "flexicore-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            LicenseDir = Path.Combine(Root, "api-license-store");
            OutDir = Path.Combine(Root, "out");
            Directory.CreateDirectory(OutDir);
            var trustDir = Path.Combine(Root, "trust");
            Directory.CreateDirectory(trustDir);
            TrustFile = Path.Combine(trustDir, "production-public-keys.json");

            Paths = new OwnerPaths(Path.Combine(Root, "owner"));
            Keys = new KeyService(Paths);
            var time = new FixedTimeProvider(Now);
            // The discovery callback returns null so the real ERP key file is never found or used.
            KeyManager = new KeyManagementService(Paths, Keys, time, () => null);
            Issue = new IssueService(Paths, time, Keys, KeyManager);
        }

        public string Root { get; }
        public string LicenseDir { get; }
        public string OutDir { get; }
        public string TrustFile { get; }
        public OwnerPaths Paths { get; }
        public KeyService Keys { get; }
        public KeyManagementService KeyManager { get; }
        public IssueService Issue { get; }
        public string StoredPath => Path.Combine(LicenseDir, FileLicenseStore.FileName);

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class Host(IdentityTestApp app, string token) : IAsyncDisposable
    {
        public IdentityTestApp App { get; } = app;
        public string Token { get; } = token;
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    private Task<Host> StartHostAsync(Workspace w) => StartHostAsync(w, new FixedTimeProvider(Now), monotonic: false);

    private async Task<Host> StartHostAsync(Workspace w, FixedTimeProvider baseTime, bool monotonic)
    {
        TimeProvider time = monotonic ? new MonotonicClock(baseTime, w.LicenseDir) : baseTime;

        void Configure(IServiceCollection services)
        {
            services.RemoveAll<LicenseService>();
            // Rebuilt from the isolated trust file on every start, using the production key-set parser.
            var trusted = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(w.TrustFile));
            services.AddSingleton(new LicenseService(
                new FileLicenseStore(w.LicenseDir),
                new LicenseVerifier(trusted, time),
                time,
                new MachineInstallationIdentity(),
                requireBinding: true));
        }

        var names = new[] { "Deployment__Mode", "License__Directory", "License__ImportPermitPerMinute", "License__LocalActivationAllowedRemoteAddresses" };
        var saved = names.Select(n => (Name: n, Old: Environment.GetEnvironmentVariable(n))).ToArray();
        try
        {
            Environment.SetEnvironmentVariable("Deployment__Mode", "Local");
            Environment.SetEnvironmentVariable("License__Directory", w.LicenseDir);
            Environment.SetEnvironmentVariable("License__ImportPermitPerMinute", "100");
            Environment.SetEnvironmentVariable("License__LocalActivationAllowedRemoteAddresses", "172.18.0.1");

            var app = await Scenario.StartWithUserAsync(postgres, "100000", Configure);
            var token = (await Scenario.LoginAsync(app)).AccessToken;
            return new Host(app, token);
        }
        finally
        {
            foreach (var (name, old) in saved) Environment.SetEnvironmentVariable(name, old);
        }
    }

    private static Task<HttpResponseMessage> SendAsync(Host h, HttpMethod method, string url, byte[]? body = null)
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

    private static async Task<string?> StateAsync(Host h)
    {
        var response = await SendAsync(h, HttpMethod.Get, StatusUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("state").GetString();
    }

    private static (string[] Ids, string? FileHash) ProductionSnapshot()
    {
        var ids = ProductionKeyLoader.Load().Select(k => k.KeyId).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        var file = KeyManagementService.DiscoverErpKeyFile();
        var hash = file is null ? null : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        return (ids, hash);
    }

    private static byte[] Tamper(byte[] genuine)
    {
        using var doc = JsonDocument.Parse(genuine);
        var root = doc.RootElement;
        var payload = Base64Url.DecodeFromChars(root.GetProperty("payload").GetString()!);
        var signature = Base64Url.DecodeFromChars(root.GetProperty("signature").GetString()!);
        var node = JsonNode.Parse(payload)!.AsObject();
        node["maxInstallations"] = 999;
        return TestSigner.Envelope(Encoding.UTF8.GetBytes(node.ToJsonString()), signature, 1, KeyId);
    }

    private static async Task AssertRejectedAndStoreUnchangedAsync(
        Host h, Workspace w, byte[] bad, byte[]? expectedStored, string what)
    {
        var response = await SendAsync(h, HttpMethod.Put, ImportUrl, bad);
        Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, what + " was not rejected with 422: " + response.StatusCode);
        if (expectedStored is null) Assert.False(File.Exists(w.StoredPath), what + " must not create a stored license");
        else Assert.Equal(expectedStored, await File.ReadAllBytesAsync(w.StoredPath));
    }

    [Fact]
    public async Task Issue_verify_import_activate_restart_and_reject_forgeries()
    {
        var productionBefore = ProductionSnapshot();
        using var w = new Workspace();

        // 1-2. Isolated ES256 key from the real KeyService; public and private halves match.
        var created = w.Keys.Create(KeyId, Pass, Pass);
        using (var signer = w.Keys.OpenSigner(KeyId, Pass))
        {
            var exported = Assert.Single(PublicKeyExport.Read(created.PublicKeyPath));
            Assert.Equal(KeyId, exported.KeyId);
            Assert.Equal(KeyId, signer.KeyId);
            Assert.True(exported.SubjectPublicKeyInfo.AsSpan().SequenceEqual(signer.SubjectPublicKeyInfo));
            Assert.Equal(created.Fingerprint, KeyStore.Fingerprint(signer.SubjectPublicKeyInfo));
        }

        // 3. Isolated trust file holding that public key, registered as the ERP key file.
        var set = w.Keys.ExportKeySet(new[] { KeyId }, w.TrustFile);
        Assert.Equal(created.Fingerprint, Assert.Single(set).Fingerprint);
        w.KeyManager.SetErpKeyFile(w.TrustFile);
        Assert.True(w.KeyManager.Load().IsSelectable(KeyId));

        // 4. Issue through the real Activation Engine service, bound to this machine.
        var machineId = new MachineInstallationIdentity().InstallationId;
        Assert.False(string.IsNullOrEmpty(machineId), "This machine has no readable installation id, so binding cannot be tested here.");
        var start = LicenseIssuer.Truncate(Now);
        var outcome = w.Issue.Issue(new IssueInput(
            KeyId, Pass, "FC-E2E-0001", "End To End Customer",
            start, start.AddYears(1), 0, 1, machineId, Path.Combine(w.OutDir, "e2e.lic"), true));
        Assert.True(outcome.Bound);
        Assert.Equal(KeyId, outcome.KeyId);
        var licenseBytes = await File.ReadAllBytesAsync(outcome.LicensePath);
        Assert.Equal("FC-E2E-0001", Assert.Single(LedgerReader.Read(w.Paths.LedgerPath).Entries).LicenseId);

        // 5. Production verifier, built by the production key-set parser from the isolated trust file.
        var trusted = ProductionKeyLoader.ParseKeySet(File.ReadAllBytes(w.TrustFile));
        Assert.Equal(KeyId, Assert.Single(trusted).KeyId);
        var verified = new LicenseVerifier(trusted, new FixedTimeProvider(Now)).Verify(licenseBytes);
        Assert.Equal(LicenseStatus.Valid, verified.Status);
        Assert.Equal("FC-E2E-0001", verified.Payload!.LicenseId);
        Assert.Equal(machineId, verified.Payload.InstallationId);

        // The embedded production keys must NOT trust a test-key license.
        var embedded = new LicenseVerifier(ProductionKeyLoader.Load(), new FixedTimeProvider(Now)).Verify(licenseBytes);
        Assert.False(embedded.IsAccepted);

        // Forgery inputs. Each must fail the verifier directly and through the API.
        var tampered = Tamper(licenseBytes);
        using var rogue = new TestSigner();
        var wrongKey = rogue.Build(TestSigner.ValidPayload, keyId: KeyId);
        var unknownKey = rogue.Build(TestSigner.ValidPayload, keyId: "rogue-key-1");
        var direct = new LicenseVerifier(trusted, new FixedTimeProvider(Now));
        Assert.Equal(LicenseStatus.InvalidSignature, direct.Verify(tampered).Status);
        Assert.Equal(LicenseStatus.InvalidSignature, direct.Verify(wrongKey).Status);
        Assert.Equal(LicenseStatus.UnknownKeyId, direct.Verify(unknownKey).Status);

        // A genuine license bound to a different computer.
        var otherMachine = w.Issue.Issue(new IssueInput(
            KeyId, Pass, "FC-E2E-0002", "Other Machine",
            start, start.AddYears(1), 0, 1, InstallationIds.FromMachineSource("some-other-machine"),
            Path.Combine(w.OutDir, "other.lic"), false));
        var otherMachineBytes = await File.ReadAllBytesAsync(otherMachine.LicensePath);

        // 6-7. First host: nothing stored, forgeries rejected, then genuine import activates offline.
        await using (var first = await StartHostAsync(w))
        {
            Assert.Equal("missing", await StateAsync(first));
            await AssertRejectedAndStoreUnchangedAsync(first, w, tampered, null, "tampered license");
            await AssertRejectedAndStoreUnchangedAsync(first, w, wrongKey, null, "wrong-key license");
            await AssertRejectedAndStoreUnchangedAsync(first, w, unknownKey, null, "unknown-key license");

            var import = await SendAsync(first, HttpMethod.Put, ImportUrl, licenseBytes);
            Assert.Equal(HttpStatusCode.OK, import.StatusCode);
            Assert.Equal(licenseBytes, await File.ReadAllBytesAsync(w.StoredPath));
            Assert.Equal("valid", await StateAsync(first));

            var wrongMachine = await SendAsync(first, HttpMethod.Put, ImportUrl, otherMachineBytes);
            Assert.NotEqual(HttpStatusCode.OK, wrongMachine.StatusCode);
            Assert.Equal(licenseBytes, await File.ReadAllBytesAsync(w.StoredPath));
        }

        // 8-9. Restart: a brand new host over the same license directory, no import this time.
        await using (var second = await StartHostAsync(w))
        {
            Assert.Equal("valid", await StateAsync(second));
            Assert.Equal(licenseBytes, await File.ReadAllBytesAsync(w.StoredPath));

            // 10. Forgeries are still rejected after the restart and the activation is untouched.
            await AssertRejectedAndStoreUnchangedAsync(second, w, tampered, licenseBytes, "tampered license after restart");
            await AssertRejectedAndStoreUnchangedAsync(second, w, wrongKey, licenseBytes, "wrong-key license after restart");
            await AssertRejectedAndStoreUnchangedAsync(second, w, unknownKey, licenseBytes, "unknown-key license after restart");
            Assert.Equal("valid", await StateAsync(second));
        }

        // The embedded production trust list and the real production key file are untouched.
        var productionAfter = ProductionSnapshot();
        Assert.Equal(productionBefore.Ids, productionAfter.Ids);
        Assert.Equal(productionBefore.FileHash, productionAfter.FileHash);
        Assert.DoesNotContain(KeyId, productionAfter.Ids);
    }

    [Fact]
    public void Issuance_is_refused_until_the_public_key_is_in_the_trust_file()
    {
        using var w = new Workspace();
        w.Keys.Create(KeyId, Pass, Pass);
        var start = LicenseIssuer.Truncate(Now);
        var output = Path.Combine(w.OutDir, "refused.lic");
        IssueInput Input() => new(KeyId, Pass, "FC-E2E-REFUSED", "Nobody", start, start.AddYears(1), 0, 1, null, output, false);

        // No trust file registered at all.
        Assert.Throws<LicenseToolException>(() => w.Issue.Issue(Input()));

        // A trust file that holds a different key.
        w.Keys.Create("e2e-other-1", Pass, Pass);
        var otherTrust = Path.Combine(w.Root, "trust", "other-keys.json");
        w.Keys.ExportKeySet(new[] { "e2e-other-1" }, otherTrust);
        w.KeyManager.SetErpKeyFile(otherTrust);
        Assert.False(w.KeyManager.Load().IsSelectable(KeyId));
        Assert.Throws<LicenseToolException>(() => w.Issue.Issue(Input()));

        Assert.False(File.Exists(output));
        Assert.False(File.Exists(w.Paths.LedgerPath));

        // Registering the correct trust file makes the same request succeed.
        w.Keys.ExportKeySet(new[] { KeyId }, w.TrustFile);
        w.KeyManager.SetErpKeyFile(w.TrustFile);
        Assert.True(w.KeyManager.Load().IsSelectable(KeyId));
        Assert.True(File.Exists(w.Issue.Issue(Input()).LicensePath));
    }

    private static void Provision(Workspace w)
    {
        w.Keys.Create(KeyId, Pass, Pass);
        w.Keys.ExportKeySet(new[] { KeyId }, w.TrustFile);
        w.KeyManager.SetErpKeyFile(w.TrustFile);
    }

    private static IssueOutcome IssueLicense(Workspace w, string licenseId, string? installationId, bool code = false)
    {
        var start = LicenseIssuer.Truncate(Now);
        return w.Issue.Issue(new IssueInput(
            KeyId, Pass, licenseId, "E2E " + licenseId, start, start.AddYears(1), 0, 1, installationId,
            Path.Combine(w.OutDir, licenseId + ".lic"), code));
    }

    [Fact]
    public async Task Activation_code_activates_offline_survives_restart_and_a_corrupted_code_is_refused()
    {
        using var w = new Workspace();
        Provision(w);
        var machineId = new MachineInstallationIdentity().InstallationId;
        Assert.False(string.IsNullOrEmpty(machineId));
        var outcome = IssueLicense(w, "FC-E2E-CODE", machineId, code: true);
        var licenseBytes = await File.ReadAllBytesAsync(outcome.LicensePath);
        var codeBytes = await File.ReadAllBytesAsync(outcome.ActivationCodePath!);

        // Corrupt one character in the middle of the code.
        var chars = Encoding.ASCII.GetString(codeBytes).ToCharArray();
        var at = chars.Length / 2;
        while (chars[at] == '-' || char.IsWhiteSpace(chars[at])) at++;
        chars[at] = chars[at] == 'A' ? 'B' : 'A';
        var corrupted = Encoding.ASCII.GetBytes(new string(chars));

        await using (var first = await StartHostAsync(w))
        {
            var bad = await SendAsync(first, HttpMethod.Put, ImportUrl, corrupted);
            Assert.True(
                bad.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
                "A corrupted activation code was not rejected: " + bad.StatusCode);
            Assert.False(File.Exists(w.StoredPath));
            Assert.Equal("missing", await StateAsync(first));

            var good = await SendAsync(first, HttpMethod.Put, ImportUrl, codeBytes);
            Assert.Equal(HttpStatusCode.OK, good.StatusCode);
            Assert.Equal(licenseBytes, await File.ReadAllBytesAsync(w.StoredPath));
            Assert.Equal("valid", await StateAsync(first));
        }

        await using var second = await StartHostAsync(w);
        Assert.Equal("valid", await StateAsync(second));
    }

    [Fact]
    public async Task Binding_is_enforced_a_license_for_another_computer_or_an_unbound_one_is_refused_with_exact_reasons()
    {
        using var w = new Workspace();
        Provision(w);
        var otherComputer = IssueLicense(w, "FC-E2E-OTHER", InstallationIds.FromMachineSource("some-other-machine"));
        var unbound = IssueLicense(w, "FC-E2E-UNBOUND", null);

        await using var h = await StartHostAsync(w);
        foreach (var (path, reason) in new[]
                 {
                     (otherComputer.LicensePath, "wrong_installation"),
                     (unbound.LicensePath, "not_bound"),
                 })
        {
            var response = await SendAsync(h, HttpMethod.Put, ImportUrl, await File.ReadAllBytesAsync(path));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(reason, (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("reason").GetString());
            Assert.False(File.Exists(w.StoredPath));
        }
        Assert.Equal("missing", await StateAsync(h));
    }

    [Fact]
    public async Task Clock_rollback_does_not_revive_an_expired_license_and_the_license_is_kept()
    {
        using var w = new Workspace();
        Provision(w);
        var machineId = new MachineInstallationIdentity().InstallationId;
        Assert.False(string.IsNullOrEmpty(machineId));
        var licenseBytes = await File.ReadAllBytesAsync(IssueLicense(w, "FC-E2E-CLOCK", machineId).LicensePath);
        var clock = new FixedTimeProvider(Now);

        await using (var first = await StartHostAsync(w, clock, monotonic: true))
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(first, HttpMethod.Put, ImportUrl, licenseBytes)).StatusCode);
            Assert.Equal("valid", await StateAsync(first));
            clock.Now = new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero); // after the one-year term
            Assert.Equal("expired", await StateAsync(first));
        }
        Assert.True(File.Exists(Path.Combine(w.LicenseDir, MonotonicClock.FileName)));

        // Restart with the clock set back to when the license was still valid.
        await using var second = await StartHostAsync(w, new FixedTimeProvider(Now), monotonic: true);
        Assert.Equal("expired", await StateAsync(second));
        Assert.Equal(licenseBytes, await File.ReadAllBytesAsync(w.StoredPath));
    }

    private static async Task<(HttpStatusCode Status, string? State)> BusinessRouteAsync(Host h)
    {
        var response = await SendAsync(h, HttpMethod.Get, "/api/v1/companies");
        string? state = null;
        if (response.StatusCode == HttpStatusCode.Forbidden)
            state = (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("state").GetString();
        return (response.StatusCode, state);
    }

    [Fact]
    public async Task Enforcement_blocks_business_routes_without_a_valid_license_and_after_expiry_even_with_a_rolled_back_clock()
    {
        using var w = new Workspace();
        Provision(w);
        var machineId = new MachineInstallationIdentity().InstallationId;
        Assert.False(string.IsNullOrEmpty(machineId));
        var licenseBytes = await File.ReadAllBytesAsync(IssueLicense(w, "FC-E2E-ENFORCE", machineId).LicensePath);
        var clock = new FixedTimeProvider(Now);

        await using (var first = await StartHostAsync(w, clock, monotonic: true))
        {
            Assert.Equal((HttpStatusCode.Forbidden, "missing"), await BusinessRouteAsync(first));
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(first, HttpMethod.Get, StatusUrl)).StatusCode);

            Assert.Equal(HttpStatusCode.OK, (await SendAsync(first, HttpMethod.Put, ImportUrl, licenseBytes)).StatusCode);
            Assert.Equal((HttpStatusCode.OK, (string?)null), await BusinessRouteAsync(first));

            clock.Now = new DateTimeOffset(2027, 7, 1, 0, 0, 0, TimeSpan.Zero);
            Assert.Equal((HttpStatusCode.Forbidden, "expired"), await BusinessRouteAsync(first));
        }

        await using var second = await StartHostAsync(w, new FixedTimeProvider(Now), monotonic: true);
        Assert.Equal((HttpStatusCode.Forbidden, "expired"), await BusinessRouteAsync(second));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(second, HttpMethod.Get, StatusUrl)).StatusCode);
        Assert.Equal(licenseBytes, await File.ReadAllBytesAsync(w.StoredPath));
    }
}
