using FlexiCore.Migrations;

namespace FlexiCore.UnitTests;

public sealed class TenancyAuditMigrationScriptTests
{
    [Fact]
    public void Audit_and_limits_migration_follows_the_onboarding_migration()
    {
        var versions = MigrationRunner.LoadEmbeddedScripts().Select(s => s.Version).ToList();

        Assert.Contains("0005_tenancy_roles_onboarding", versions);
        Assert.Contains("0006_tenancy_audit_and_limits", versions);
        Assert.True(versions.IndexOf("0005_tenancy_roles_onboarding") < versions.IndexOf("0006_tenancy_audit_and_limits"));
    }

    [Fact]
    public void Audit_and_limits_migration_defines_the_cap_the_lock_and_the_audit_rows()
    {
        var sql = MigrationRunner.LoadEmbeddedScripts()
            .Single(s => s.Version == "0006_tenancy_audit_and_limits")
            .Sql;

        Assert.Contains("53400", sql);
        Assert.Contains("pg_advisory_xact_lock", sql);
        Assert.Contains("tenancy.organization.create", sql);
        Assert.Contains("tenancy.company.create", sql);
        Assert.Contains("audit.events", sql);
        Assert.Contains("app.correlation_id", sql);
        Assert.DoesNotContain("DROP FUNCTION", sql, StringComparison.OrdinalIgnoreCase);
    }
}