using System.Text;
using FlexiCore.Api.Licensing;
using FlexiCore.IntegrationTests.Infrastructure;
using FlexiCore.Licensing;
using FlexiCore.Migrations;
using FlexiCore.Tenancy;
using FlexiCore.UnitTests.LicensingTests;
using Npgsql;

namespace FlexiCore.IntegrationTests.Licensing;

/// <summary>A migrated disposable database, a test signer and stores over it. A second store simulates an application restart.</summary>
internal sealed class RevocationTestEnv : IDisposable
{
    private readonly List<NpgsqlDataSource> _sources = new();

    public RevocationTestEnv(TestDatabase db)
    {
        Db = db;
        Store = NewStore();
    }

    public TestDatabase Db { get; }
    public TestSigner Signer { get; } = new();
    public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
    public LicensingStore Store { get; }

    public LicensingStore NewStore()
    {
        var source = NpgsqlDataSource.Create(Db.AppConnectionString);
        _sources.Add(source);
        return new LicensingStore(new TenantConnectionFactory(source), new FixedTimeProvider(Now));
    }

    public void Dispose()
    {
        Signer.Dispose();
        foreach (var source in _sources) source.Dispose();
    }
}

[Collection("postgres")]
public sealed class PendingRevocationTests(PostgresFixture postgres)
{
    private static readonly Guid OwnerA = Guid.Parse("10000000-0000-0000-0000-0000000000a1");
    private static readonly Guid OwnerB = Guid.Parse("10000000-0000-0000-0000-0000000000b1");
    private static readonly Guid OrgA = Guid.Parse("20000000-0000-0000-0000-0000000000a1");
    private static readonly Guid OrgB = Guid.Parse("20000000-0000-0000-0000-0000000000b1");

    [Fact]
    public async Task Revocation_received_before_its_license_is_persisted_and_blocks_the_later_registration()
    {
        using var env = await StartAsync();
        var snapshot = Snapshot(env, env.Now.AddMinutes(-2), ("LIC-PEND-1", env.Now.AddMinutes(-2), "incident before delivery"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, snapshot));

        Assert.Equal(1L, await CountAsync(env, "pending_revocations", ForOrg(OrgA, "LIC-PEND-1")));
        Assert.Equal(0L, await CountAsync(env, "revocations", ForOrg(OrgA, "LIC-PEND-1")));

        var license = License(env, "LIC-PEND-1", "Customer A", env.Now.AddMinutes(-1));
        Assert.Equal(LicensingWriteOutcome.Forbidden, await RegisterAsync(env, env.Store, OrgA, OwnerA, license));
        Assert.Equal(0L, await CountAsync(env, "licenses", "license_id='LIC-PEND-1'"));
    }

    [Fact]
    public async Task Pending_revocation_survives_a_restart_and_reordered_or_duplicate_delivery_cannot_bypass_it()
    {
        using var env = await StartAsync();
        var revokedAt = env.Now.AddMinutes(-2);
        var first = Snapshot(env, env.Now.AddMinutes(-2), ("LIC-PEND-2", revokedAt, "incident"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, first));

        // A new data source and store stand in for an application restart; the state lives in PostgreSQL.
        var restarted = env.NewStore();
        var blocked = License(env, "LIC-PEND-2", "Customer A", env.Now.AddMinutes(-1));
        Assert.Equal(LicensingWriteOutcome.Forbidden, await RegisterAsync(env, restarted, OrgA, OwnerA, blocked));

        // Duplicate delivery of the same snapshot changes nothing.
        Assert.Equal(LicensingWriteOutcome.Unchanged, await ImportAsync(env, restarted, OrgA, OwnerA, first));
        Assert.Equal(1L, await CountAsync(env, "pending_revocations", ForOrg(OrgA, "LIC-PEND-2")));

        // A reordered (older) snapshot without the revocation is refused.
        var older = Snapshot(env, env.Now.AddMinutes(-10));
        Assert.Equal(LicensingWriteOutcome.Conflict, await ImportAsync(env, restarted, OrgA, OwnerA, older));

        // A newer snapshot that drops the pending entry is refused too.
        var dropping = Snapshot(env, env.Now.AddMinutes(-1));
        Assert.Equal(LicensingWriteOutcome.Conflict, await ImportAsync(env, restarted, OrgA, OwnerA, dropping));

        // A newer cumulative snapshot that keeps it is accepted and adds no duplicate row.
        var cumulative = Snapshot(env, env.Now.AddMinutes(-1),
            ("LIC-PEND-2", revokedAt, "incident"), ("LIC-PEND-3", env.Now.AddMinutes(-1), "second incident"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, restarted, OrgA, OwnerA, cumulative));
        Assert.Equal(2L, await CountAsync(env, "pending_revocations", "organization_id='" + OrgA + "'"));
        Assert.Equal(LicensingWriteOutcome.Forbidden, await RegisterAsync(env, env.Store, OrgA, OwnerA, blocked));
    }

    [Fact]
    public async Task Pending_revocations_are_scoped_to_their_organization()
    {
        using var env = await StartAsync();
        var snapshot = Snapshot(env, env.Now.AddMinutes(-2), ("LIC-ISO", env.Now.AddMinutes(-2), "incident"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, snapshot));

        var otherCustomer = License(env, "LIC-ISO", "Customer B", env.Now.AddMinutes(-1));
        Assert.Equal(LicensingWriteOutcome.Created, await RegisterAsync(env, env.Store, OrgB, OwnerB, otherCustomer));
        Assert.Equal(0L, await CountAsync(env, "pending_revocations", "organization_id='" + OrgB + "'"));
        Assert.Equal(1L, await CountAsync(env, "pending_revocations", "organization_id='" + OrgA + "'"));
    }

    [Fact]
    public async Task Revocation_received_after_the_license_is_recorded_as_a_revocation_and_blocks_reinstallation()
    {
        using var env = await StartAsync();
        var license = License(env, "LIC-FIRST", "Customer A", env.Now.AddMinutes(-5));
        Assert.Equal(LicensingWriteOutcome.Created, await RegisterAsync(env, env.Store, OrgA, OwnerA, license));

        var snapshot = Snapshot(env, env.Now.AddMinutes(-1), ("LIC-FIRST", env.Now.AddMinutes(-1), "incident"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, snapshot));
        Assert.Equal(1L, await CountAsync(env, "revocations", ForOrg(OrgA, "LIC-FIRST")));
        Assert.Equal(0L, await CountAsync(env, "pending_revocations", ForOrg(OrgA, "LIC-FIRST")));

        Assert.Equal(LicensingWriteOutcome.Forbidden, await env.Store.RecordActivationAsync(
            TenantContext.For(OwnerA), OrgA, OwnerA, Guid.NewGuid(), "LIC-FIRST", new string('c', 64), "activated", null, default));
    }

    [Fact]
    public async Task Renewal_cannot_register_a_successor_that_a_signed_snapshot_already_revoked()
    {
        using var env = await StartAsync();
        var predecessor = License(env, "LIC-OLD", "Customer A", env.Now.AddMinutes(-20));
        Assert.Equal(LicensingWriteOutcome.Created, await RegisterAsync(env, env.Store, OrgA, OwnerA, predecessor));

        // Renewal requires a fresh snapshot, so import an empty one first, then the one that revokes the successor id.
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, Snapshot(env, env.Now.AddMinutes(-3))));
        var revoked = Snapshot(env, env.Now.AddMinutes(-2), ("LIC-NEW", env.Now.AddMinutes(-2), "revoked before renewal"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, revoked));
        Assert.Equal(1L, await CountAsync(env, "pending_revocations", ForOrg(OrgA, "LIC-NEW")));

        var successor = License(env, "LIC-NEW", "Customer A", env.Now.AddMinutes(-10));
        Assert.Equal(LicensingWriteOutcome.Forbidden, await RenewAsync(env, OrgA, OwnerA, successor, "LIC-OLD"));
        Assert.Equal(0L, await CountAsync(env, "licenses", "license_id='LIC-NEW'"));
        Assert.Equal(0L, await CountAsync(env, "renewals", "organization_id='" + OrgA + "'"));
    }

    [Fact]
    public async Task Renewal_to_an_unrevoked_successor_still_succeeds()
    {
        using var env = await StartAsync();
        var predecessor = License(env, "LIC-OLD", "Customer A", env.Now.AddMinutes(-20));
        Assert.Equal(LicensingWriteOutcome.Created, await RegisterAsync(env, env.Store, OrgA, OwnerA, predecessor));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, Snapshot(env, env.Now.AddMinutes(-3))));
        var unrelated = Snapshot(env, env.Now.AddMinutes(-2), ("LIC-UNRELATED", env.Now.AddMinutes(-2), "other incident"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.Store, OrgA, OwnerA, unrelated));

        var successor = License(env, "LIC-NEW", "Customer A", env.Now.AddMinutes(-10));
        Assert.Equal(LicensingWriteOutcome.Created, await RenewAsync(env, OrgA, OwnerA, successor, "LIC-OLD"));
        Assert.Equal(1L, await CountAsync(env, "licenses", "license_id='LIC-NEW'"));
        Assert.Equal(1L, await CountAsync(env, "renewals", "organization_id='" + OrgA + "'"));
    }

    [Fact]
    public async Task Failure_inside_the_store_rolls_back_the_snapshot_and_leaves_the_store_usable()
    {
        using var env = await StartAsync();

        // Passes every in-memory check but violates the 1..1000 character CHECK on the reason column,
        // so PostgreSQL rejects it after the snapshot row has already been inserted in the same transaction.
        var badList = new SignedRevocationList(
            Guid.NewGuid().ToString("N"), env.Now.AddMinutes(-2), env.Now.AddDays(7),
            [new RevokedLicense("LIC-ROLLBACK-1", env.Now.AddMinutes(-2), new string('x', 1001))]);

        LicensingWriteOutcome? outcome = null;
        Exception? error = null;
        try
        {
            outcome = await env.Store.ImportRevocationSnapshotAsync(
                TenantContext.For(OwnerA), OrgA, OwnerA, Encoding.UTF8.GetBytes("rollback-test"), badList, TestSigner.KeyId, default);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        Assert.True(error is not null || outcome != LicensingWriteOutcome.Created,
            "The failing import must not report success.");
        Assert.Equal(0L, await CountAsync(env, "revocation_snapshots", "organization_id='" + OrgA + "'"));
        Assert.Equal(0L, await CountAsync(env, "pending_revocations", "organization_id='" + OrgA + "'"));
        Assert.Equal(0L, await CountAsync(env, "revocations", "organization_id='" + OrgA + "'"));

        // The failed transaction must not poison later imports (no leftover lock or newer issued_at).
        var valid = Snapshot(env, env.Now.AddMinutes(-1), ("LIC-ROLLBACK-OK", env.Now.AddMinutes(-1), "valid"));
        Assert.Equal(LicensingWriteOutcome.Created, await ImportAsync(env, env.NewStore(), OrgA, OwnerA, valid));
        Assert.Equal(1L, await CountAsync(env, "pending_revocations", ForOrg(OrgA, "LIC-ROLLBACK-OK")));
    }

    private async Task<RevocationTestEnv> StartAsync()
    {
        var db = await postgres.CreateDatabaseAsync();
        await new MigrationRunner(db.OwnerConnectionString).RunAsync();
        await Sql.ExecAsync(db.OwnerConnectionString, $"""
            INSERT INTO identity.users (id,email,email_normalized,password_hash) VALUES
              ('{OwnerA}','owner-a@example.test','owner-a@example.test','test'),
              ('{OwnerB}','owner-b@example.test','owner-b@example.test','test');
            INSERT INTO tenancy.organizations(id,name,slug) VALUES
              ('{OrgA}','Customer A','customer-a'),
              ('{OrgB}','Customer B','customer-b');
            INSERT INTO tenancy.organization_memberships(organization_id,user_id,role) VALUES
              ('{OrgA}','{OwnerA}','owner'),
              ('{OrgB}','{OwnerB}','owner');
            """);
        return new RevocationTestEnv(db);
    }

    private static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static string ForOrg(Guid organization, string licenseId) =>
        "organization_id='" + organization + "' AND license_id='" + licenseId + "'";

    private static async Task<long> CountAsync(RevocationTestEnv env, string table, string where) =>
        Convert.ToInt64(await Sql.ScalarAsync(env.Db.OwnerConnectionString,
            "SELECT count(*) FROM licensing." + table + " WHERE " + where));

    private static byte[] License(RevocationTestEnv env, string licenseId, string licensee, DateTimeOffset issued)
    {
        var json = $$"""
            {"schemaVersion":1,"licenseId":"{{licenseId}}","product":"FlexiCore","licensee":"{{licensee}}",
             "issuedAt":"{{Stamp(issued)}}","notBefore":"{{Stamp(issued)}}","expiresAt":"{{Stamp(issued.AddDays(365))}}",
             "graceDays":0,"maxInstallations":1}
            """;
        return env.Signer.Build(json);
    }

    private static byte[] Snapshot(RevocationTestEnv env, DateTimeOffset issued,
        params (string Id, DateTimeOffset At, string Reason)[] revoked)
    {
        var items = string.Join(',', revoked.Select(r =>
            "{\"licenseId\":\"" + r.Id + "\",\"revokedAt\":\"" + Stamp(r.At) + "\",\"reason\":\"" + r.Reason + "\"}"));
        var payload = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"listId\":\"" + Guid.NewGuid().ToString("N")
            + "\",\"issuedAt\":\"" + Stamp(issued) + "\",\"nextUpdate\":\"" + Stamp(issued.AddDays(7))
            + "\",\"revokedLicenses\":[" + items + "]}");
        return TestSigner.Envelope(payload, env.Signer.Sign(payload));
    }

    private static async Task<LicensingWriteOutcome> ImportAsync(
        RevocationTestEnv env, LicensingStore store, Guid org, Guid actor, byte[] snapshot)
    {
        var verified = new SignedRevocationListVerifier([env.Signer.Trusted()]).Verify(snapshot);
        Assert.True(verified.IsValid);
        var list = verified.List ?? throw new InvalidOperationException("The verified snapshot has no list.");
        return await store.ImportRevocationSnapshotAsync(
            TenantContext.For(actor), org, actor, snapshot, list, TestSigner.KeyId, default);
    }

    private static async Task<LicensingWriteOutcome> RegisterAsync(
        RevocationTestEnv env, LicensingStore store, Guid org, Guid actor, byte[] signed)
    {
        var result = new LicenseVerifier([env.Signer.Trusted()], new FixedTimeProvider(env.Now)).Verify(signed);
        var payload = result.Payload ?? throw new InvalidOperationException("The test license did not verify.");
        return await store.RegisterLicenseAsync(
            TenantContext.For(actor), org, actor, Guid.NewGuid(), null, signed, TestSigner.KeyId, payload, default);
    }

    private static async Task<LicensingWriteOutcome> RenewAsync(
        RevocationTestEnv env, Guid org, Guid actor, byte[] signed, string predecessorId)
    {
        var result = new LicenseVerifier([env.Signer.Trusted()], new FixedTimeProvider(env.Now)).Verify(signed);
        var payload = result.Payload ?? throw new InvalidOperationException("The test license did not verify.");
        return await env.Store.RecordRenewalAsync(
            TenantContext.For(actor), org, actor, Guid.NewGuid(), "annual renewal", signed, TestSigner.KeyId,
            payload, predecessorId, default);
    }
}
