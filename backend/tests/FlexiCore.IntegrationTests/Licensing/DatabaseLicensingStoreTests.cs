using System.Text;
using System.Text.Json;
using FlexiCore.Api.Licensing;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.Migrations;
using FlexiCore.Tenancy;
using FlexiCore.UnitTests.LicensingTests;
using Npgsql;

namespace FlexiCore.IntegrationTests.Licensing;

[Collection("postgres")]
public sealed class DatabaseLicensingStoreTests(PostgresFixture postgres)
{
    private static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000091");
    private static readonly Guid NonMemberId = Guid.Parse("10000000-0000-0000-0000-000000000092");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000091");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    // TestSigner.ValidPayload is dated 2026-01-01..2027-01-01. This test verifies with the real clock,
    // so the payload dates must move with Now or the test breaks once 2027-01-01 passes.
    private static string LivePayload => TestSigner.ValidPayload
        .Replace("2026-01-01T00:00:00Z", Stamp(Now.AddDays(-1)), StringComparison.Ordinal)
        .Replace("2027-01-01T00:00:00Z", Stamp(Now.AddDays(365)), StringComparison.Ordinal);

    [Fact]
    public async Task Lifecycle_persists_atomically_enforces_replay_limits_revocation_and_tenant_isolation()
    {
        using var signer = new TestSigner();
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();
        await SeedAsync(db.OwnerConnectionString);

        await using var source = NpgsqlDataSource.Create(db.AppConnectionString);
        var connections = new TenantConnectionFactory(source);
        var store = new LicensingStore(connections, new FixedTimeProvider(Now));
        var context = TenantContext.For(UserId, correlationId: "licensing-integration-test");
        var clock = new FixedTimeProvider(Now);
        var verifier = new LicenseVerifier([signer.Trusted()], clock);
        var rawLicense = signer.Build(LivePayload);
        var license = verifier.Verify(rawLicense);
        Assert.NotNull(license.Payload);

        Assert.Equal(LicensingWriteOutcome.Created, await store.RegisterLicenseAsync(context, OrganizationId, UserId,
            Guid.NewGuid(), null, rawLicense, TestSigner.KeyId, license.Payload!, default));
        Assert.Equal(LicensingWriteOutcome.Unchanged, await store.RegisterLicenseAsync(context, OrganizationId, UserId,
            Guid.NewGuid(), null, rawLicense, TestSigner.KeyId, license.Payload!, default));

        var revocationVerifier = new SignedRevocationListVerifier([signer.Trusted()]);
        var emptySnapshot = Snapshot(signer, [], Stamp(Now.AddMinutes(-1)), Stamp(Now.AddDays(6)));
        var verifiedSnapshot = revocationVerifier.Verify(emptySnapshot);
        Assert.True(verifiedSnapshot.IsValid);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportRevocationSnapshotAsync(
            context, OrganizationId, UserId, emptySnapshot, verifiedSnapshot.List!, TestSigner.KeyId, default));

        // A signed revocation may arrive before the corresponding license artifact. Keep it
        // pending and reject a later registration instead of dropping the signed revocation.
        const string revokedBeforeRegistrationId = "LIC-BLOCKED";
        var revokedBeforeRegistrationJson = LivePayload.Replace(
            "LIC-0001", revokedBeforeRegistrationId, StringComparison.Ordinal);
        var revokedBeforeRegistrationBytes = signer.Build(revokedBeforeRegistrationJson);
        var revokedBeforeRegistrationPayload = verifier.Verify(revokedBeforeRegistrationBytes).Payload!;
        var pendingSnapshot = Snapshot(signer, [(revokedBeforeRegistrationId, Stamp(Now), "pre-registration revoke")],
            Stamp(Now), Stamp(Now.AddDays(6)));
        var verifiedPending = revocationVerifier.Verify(pendingSnapshot);
        Assert.True(verifiedPending.IsValid);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportRevocationSnapshotAsync(
            context, OrganizationId, UserId, pendingSnapshot, verifiedPending.List!, TestSigner.KeyId, default));
        Assert.Equal(LicensingWriteOutcome.Forbidden, await store.RegisterLicenseAsync(context, OrganizationId,
            UserId, Guid.NewGuid(), null, revokedBeforeRegistrationBytes, TestSigner.KeyId,
            revokedBeforeRegistrationPayload, default));

        var installation = new string('a', 64);
        var request = Guid.NewGuid();
        Assert.Equal(LicensingWriteOutcome.Created, await store.RecordActivationAsync(context, OrganizationId,
            UserId, request, "LIC-0001", installation, "activated", null, default));
        Assert.Equal(LicensingWriteOutcome.Unchanged, await store.RecordActivationAsync(context, OrganizationId,
            UserId, request, "LIC-0001", installation, "activated", null, default));
        Assert.Equal(LicensingWriteOutcome.Invalid, await store.RecordActivationAsync(context, OrganizationId,
            UserId, Guid.NewGuid(), "LIC-0001", new string('b', 64), "activated", null, default));

        var entitlement = Entitlement(signer, revision: 1);
        var entitlementVerifier = new SignedModuleEntitlementVerifier([signer.Trusted()], clock);
        var verifiedEntitlement = entitlementVerifier.Verify(entitlement);
        Assert.True(verifiedEntitlement.IsValid);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportEntitlementAsync(context, OrganizationId,
            UserId, entitlement, verifiedEntitlement, default));
        Assert.Equal(LicensingWriteOutcome.Unchanged, await store.ImportEntitlementAsync(context, OrganizationId,
            UserId, entitlement, verifiedEntitlement, default));
        Assert.True((await store.CheckAccessAsync(context, OrganizationId, "core", "companies.create", default)).Allowed);

        var staleRevision = Entitlement(signer, revision: 1, feature: "companies.delete");
        Assert.Equal(LicensingWriteOutcome.Conflict, await store.ImportEntitlementAsync(context, OrganizationId,
            UserId, staleRevision, entitlementVerifier.Verify(staleRevision), default));

        var unauthorized = await store.RegisterLicenseAsync(TenantContext.For(NonMemberId), OrganizationId,
            NonMemberId, Guid.NewGuid(), null, rawLicense, TestSigner.KeyId, license.Payload!, default);
        Assert.Equal(LicensingWriteOutcome.Forbidden, unauthorized);

        var revokedSnapshot = Snapshot(signer, [
            ("LIC-0001", Stamp(Now), "security incident"),
            (revokedBeforeRegistrationId, Stamp(Now), "pre-registration revoke")
        ], Stamp(Now.AddMinutes(2)), Stamp(Now.AddDays(7)));
        var verifiedRevoked = revocationVerifier.Verify(revokedSnapshot);
        Assert.True(verifiedRevoked.IsValid);
        Assert.Equal(LicensingWriteOutcome.Created, await store.ImportRevocationSnapshotAsync(
            context, OrganizationId, UserId, revokedSnapshot, verifiedRevoked.List!, TestSigner.KeyId, default));
        Assert.False((await store.CheckAccessAsync(context, OrganizationId, "core", "companies.create", default)).Allowed);
        Assert.Equal(LicensingWriteOutcome.Forbidden, await store.RecordActivationAsync(context, OrganizationId,
            UserId, Guid.NewGuid(), "LIC-0001", new string('c', 64), "activated", null, default));
    }

    private static async Task SeedAsync(string connectionString)
    {
        const string sql = """
            INSERT INTO identity.users (id,email,email_normalized,password_hash) VALUES
              ('10000000-0000-0000-0000-000000000091','owner@example.test','OWNER@EXAMPLE.TEST','test'),
              ('10000000-0000-0000-0000-000000000092','outsider@example.test','OUTSIDER@EXAMPLE.TEST','test');
            INSERT INTO tenancy.organizations(id,name,slug)
              VALUES ('20000000-0000-0000-0000-000000000091','Test Customer','test-customer');
            INSERT INTO tenancy.organization_memberships(organization_id,user_id,role)
              VALUES ('20000000-0000-0000-0000-000000000091','10000000-0000-0000-0000-000000000091','owner');
            """;
        await Sql.ExecAsync(connectionString, sql);
    }

    private static byte[] Snapshot(TestSigner signer, (string Id, string At, string Reason)[] revoked,
        string issued, string next)
    {
        var id = Guid.NewGuid().ToString("N");
        var entries = string.Join(',', revoked.Select(x => "{\"licenseId\":\"" + x.Id + "\",\"revokedAt\":\"" + x.At
            + "\",\"reason\":\"" + x.Reason + "\"}"));
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"listId\":\"" + id + "\",\"issuedAt\":\"" + issued
            + "\",\"nextUpdate\":\"" + next + "\",\"revokedLicenses\":[" + entries + "]}");
        return TestSigner.Envelope(payload, signer.Sign(payload));
    }

    private static byte[] Entitlement(TestSigner signer, int revision, string feature = "companies.create")
    {
        var from = Stamp(Now.AddMinutes(-1));
        var until = Stamp(Now.AddDays(30));
        var payload = """
            {"schemaVersion":1,"licenseId":"LIC-0001","organizationId":"20000000-0000-0000-0000-000000000091",
             "customerId":"test-customer","revision":__REVISION__,"issuedAt":"__FROM__",
             "validFrom":"__FROM__","validUntil":"__UNTIL__",
             "modules":[{"moduleKey":"core","featureKey":"__FEATURE__","enabled":true,
               "maxUsers":10,"maxDevices":5,"maxCompanies":7,"maxBranches":10}]}
            """.Replace("__REVISION__", revision.ToString(), StringComparison.Ordinal)
            .Replace("__FROM__", from, StringComparison.Ordinal)
            .Replace("__UNTIL__", until, StringComparison.Ordinal)
            .Replace("__FEATURE__", feature, StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(payload);
        return TestSigner.Envelope(bytes, signer.Sign(bytes));
    }

    private static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
}
