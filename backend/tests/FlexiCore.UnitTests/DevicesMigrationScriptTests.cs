using FlexiCore.Migrations;

namespace FlexiCore.UnitTests;

public sealed class DevicesMigrationScriptTests
{
    private static string DevicesSql() => MigrationRunner.LoadEmbeddedScripts()
        .Single(s => s.Version == "0011_devices").Sql.ToLowerInvariant();

    [Fact]
    public void Devices_migration_follows_the_username_migrations()
    {
        var versions = MigrationRunner.LoadEmbeddedScripts().Select(s => s.Version).ToList();

        Assert.True(versions.IndexOf("0010_tenancy_member_username") < versions.IndexOf("0011_devices"));
    }

    [Fact]
    public void Devices_migration_stores_hashes_only_and_never_deletes_devices()
    {
        var sql = DevicesSql();

        Assert.Contains("create table devices.devices", sql);
        Assert.Contains("create table devices.pairing_codes", sql);
        Assert.Contains("secret_hash", sql);
        Assert.Contains("code_hash", sql);
        Assert.DoesNotContain("grant delete", sql);
        Assert.DoesNotContain("secret text", sql);
        Assert.DoesNotContain("code text", sql);
    }
}