using FlexiCore.Migrations;

namespace FlexiCore.UnitTests;

public class IdentityMigrationScriptTests
{
    [Fact]
    public void Identity_and_audit_migrations_follow_the_baseline_in_order()
    {
        var versions = MigrationRunner.LoadEmbeddedScripts().Select(s => s.Version).ToList();

        Assert.Contains("0002_identity", versions);
        Assert.Contains("0003_audit", versions);
        Assert.True(versions.IndexOf("0001_baseline") < versions.IndexOf("0002_identity"));
        Assert.True(versions.IndexOf("0002_identity") < versions.IndexOf("0003_audit"));
    }
}