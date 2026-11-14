using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FlexiCore.Api.Licensing;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.Tenancy;
using FlexiCore.UnitTests.LicensingTests;
using Npgsql;

namespace FlexiCore.IntegrationTests.Licensing;

[Collection("postgres")]
public sealed class EntitlementApiEnforcementTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Direct_company_operations_obey_signed_feature_and_revocation_state()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres, useProductionEntitlements: true);
        using var signer = new TestSigner();
        await RegisterOwnerAsync(app);
        var session = await Scenario.ReadSessionAsync(await app.LoginAsync("licensed-owner@example.test", Scenario.Password));
        var organizationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/organizations")
        { Content = JsonContent.Create(new { name = "Licensed Customer", slug = "licensed-customer" }) };
        organizationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var organization = await app.Client.SendAsync(organizationRequest);
        Assert.Equal(HttpStatusCode.Created, organization.StatusCode);
        var orgId = (await IdentityTestApp.ReadJsonAsync(organization)).GetProperty("id").GetGuid();
        var userId = await UserIdAsync(app.Db.OwnerConnectionString);

        var first = await CreateCompanyAsync(app, session.AccessToken, orgId, "Before License", "before-license");
        Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);

        var now = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.AddMinutes(-2), TimeSpan.Zero);
        var clock = new FixedTimeProvider(now.AddMinutes(2));
        var licenseBytes = SignLicense(signer, now, "Licensed Customer");
        var licenseVerifier = new LicenseVerifier([signer.Trusted()], clock);
        var verifiedLicense = licenseVerifier.Verify(licenseBytes);
        Assert.NotNull(verifiedLicense.Payload);

        await using var dataSource = NpgsqlDataSource.Create(app.Db.AppConnectionString);
        var store = new LicensingStore(new TenantConnectionFactory(dataSource), clock);
        var context = TenantContext.For(userId);
        Assert.Equal(LicensingWriteOutcome.Created, await store.RegisterLicenseAsync(context, orgId, userId,
            Guid.NewGuid(), verifiedLicense.Payload!.InstallationId, licenseBytes, TestSigner.KeyId, verifiedLicense.Payload, default));

        var emptySnapshot = SignSnapshot(signer, now, []);
        var revVerifier = new SignedRevocationListVerifier([signer.Trusted()]);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportRevocationSnapshotAsync(context, orgId, userId,
            emptySnapshot, revVerifier.Verify(emptySnapshot).List!, TestSigner.KeyId, default));

        var entitlementBytes = SignEntitlement(signer, orgId, "licensed-customer", now, true);
        var entitlementVerifier = new SignedModuleEntitlementVerifier([signer.Trusted()], clock);
        var entitlement = entitlementVerifier.Verify(entitlementBytes);
        Assert.True(entitlement.IsValid);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportEntitlementAsync(context, orgId, userId,
            entitlementBytes, entitlement, default));

        var allowed = await CreateCompanyAsync(app, session.AccessToken, orgId, "Licensed Company", "licensed-company");
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

        var revoked = SignSnapshot(signer, now.AddMinutes(1), [("LIC-API-001", now.AddMinutes(1), "security incident")]);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportRevocationSnapshotAsync(context, orgId, userId,
            revoked, revVerifier.Verify(revoked).List!, TestSigner.KeyId, default));
        var denied = await CreateCompanyAsync(app, session.AccessToken, orgId, "After Revocation", "after-revocation");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static async Task<IdentityTestApp> RegisterOwnerAsync(IdentityTestApp app)
    {
        var result = await app.RegisterAsync("licensed-owner@example.test", Scenario.Password);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return app;
    }

    private static async Task<HttpResponseMessage> CreateCompanyAsync(IdentityTestApp app, string token,
        Guid organizationId, string name, string slug)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/companies")
        { Content = JsonContent.Create(new { name, slug, industryKey = "retail" }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await app.Client.SendAsync(request);
    }

    private static async Task<Guid> UserIdAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM identity.users WHERE email_normalized=@normalized", connection);
        command.Parameters.AddWithValue("normalized", global::FlexiCore.Identity.Security.EmailRules.Normalize("licensed-owner@example.test"));
        var scalar = await command.ExecuteScalarAsync();
        Assert.NotNull(scalar);
        return (Guid)scalar!;
    }

    private static byte[] SignLicense(TestSigner signer, DateTimeOffset issued, string licensee)
    {
        var issuedText = Stamp(issued);
        var expiresText = Stamp(issued.AddDays(365));
        var payload = $$"""
            {"schemaVersion":1,"licenseId":"LIC-API-001","product":"FlexiCore","licensee":"{{licensee}}",
             "issuedAt":"{{issuedText}}","notBefore":"{{issuedText}}","expiresAt":"{{expiresText}}",
             "graceDays":0,"maxInstallations":1}
            """;
        return signer.Build(payload);
    }

    private static byte[] SignSnapshot(TestSigner signer, DateTimeOffset issued,
        (string Id, DateTimeOffset At, string Reason)[] entries)
    {
        var listId = Guid.NewGuid().ToString("N");
        var items = string.Join(',', entries.Select(x => "{\"licenseId\":\"" + x.Id + "\",\"revokedAt\":\""
            + Stamp(x.At) + "\",\"reason\":\"" + x.Reason + "\"}"));
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"listId\":\"" + listId + "\",\"issuedAt\":\""
            + Stamp(issued) + "\",\"nextUpdate\":\"" + Stamp(issued.AddDays(7)) + "\",\"revokedLicenses\":[" + items + "]}");
        return TestSigner.Envelope(payload, signer.Sign(payload));
    }

    private static byte[] SignEntitlement(TestSigner signer, Guid organizationId, string slug,
        DateTimeOffset issued, bool enabled)
    {
        var at = Stamp(issued);
        var until = Stamp(issued.AddDays(365));
        var payload = $$"""
            {"schemaVersion":1,"licenseId":"LIC-API-001","organizationId":"{{organizationId:D}}",
             "customerId":"{{slug}}","revision":1,"issuedAt":"{{at}}","validFrom":"{{at}}",
             "validUntil":"{{until}}","modules":[{"moduleKey":"core",
             "featureKey":"companies.create","enabled":{{enabled.ToString().ToLowerInvariant()}},
             "maxUsers":10,"maxDevices":2,"maxCompanies":3,"maxBranches":5}]}
            """;
        var bytes = Encoding.UTF8.GetBytes(payload);
        return TestSigner.Envelope(bytes, signer.Sign(bytes));
    }

    private static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
}
