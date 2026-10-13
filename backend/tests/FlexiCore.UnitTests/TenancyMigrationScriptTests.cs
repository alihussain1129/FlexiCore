using FlexiCore.Migrations;

namespace FlexiCore.UnitTests;

public sealed class TenancyMigrationScriptTests
{
    [Fact]
    public void Tenancy_migration_follows_identity_and_audit_migrations()
    {
        var versions = MigrationRunner.LoadEmbeddedScripts()
            .Select(s => s.Version)
            .ToList();

        Assert.Contains("0001_baseline", versions);
        Assert.Contains("0002_identity", versions);
        Assert.Contains("0003_audit", versions);
        Assert.Contains("0004_tenancy", versions);

        Assert.True(versions.IndexOf("0003_audit") <
                    versions.IndexOf("0004_tenancy"));
    }

    [Fact]
    public void Tenancy_migration_defines_company_isolation()
    {
        var migration = MigrationRunner.LoadEmbeddedScripts()
            .Single(s => s.Version == "0004_tenancy")
            .Sql;

        Assert.Contains("CREATE SCHEMA tenancy", migration);
        Assert.Contains("CREATE TABLE tenancy.organizations", migration);
        Assert.Contains("CREATE TABLE tenancy.companies", migration);
        Assert.Contains("CREATE TABLE tenancy.branches", migration);
        Assert.Contains("ENABLE ROW LEVEL SECURITY", migration);
        Assert.Contains("branches_company_isolation", migration);
        Assert.Contains("app.user_id", migration);
        Assert.Contains("app.company_id", migration);
        Assert.Contains("GRANT INSERT, UPDATE, DELETE", migration);
    }
}
