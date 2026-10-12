using System.Text.RegularExpressions;
using FlexiCore.Migrations;

namespace FlexiCore.UnitTests;

public class MigrationScriptTests
{
    [Fact]
    public void Embedded_scripts_load_in_order_with_valid_unique_versions()
    {
        var scripts = MigrationRunner.LoadEmbeddedScripts();

        Assert.NotEmpty(scripts);
        Assert.Equal("0001_baseline", scripts[0].Version);
        Assert.All(scripts, s => Assert.Matches(new Regex(@"^\d{4}_[a-z0-9_]+$"), s.Version));
        Assert.Equal(scripts.Select(s => s.Version).Order(StringComparer.Ordinal), scripts.Select(s => s.Version));
        Assert.Equal(scripts.Count, scripts.Select(s => s.Version).Distinct().Count());
        Assert.All(scripts, s => Assert.False(string.IsNullOrWhiteSpace(s.Sql)));
    }

    [Fact]
    public void Checksum_ignores_line_endings()
    {
        Assert.Equal(
            new MigrationScript("0001_x", "select 1;\r\nselect 2;").Checksum,
            new MigrationScript("0001_x", "select 1;\nselect 2;").Checksum);
    }

    [Fact]
    public void Checksum_changes_when_content_changes()
    {
        Assert.NotEqual(
            new MigrationScript("0001_x", "select 1;").Checksum,
            new MigrationScript("0001_x", "select 2;").Checksum);
    }
}