using System.Linq;
using FlexiCore.Api.Licensing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FlexiCore.IntegrationTests;

// Pure configuration tests: no database or web host is started.
public class DeploymentSettingsTests
{
    private static readonly string AbsoluteDir = Path.Combine(Path.GetTempPath(), "flexicore-license");

    private static IConfiguration Cfg(string? mode = null, string? directory = null)
    {
        var items = new Dictionary<string, string?>();
        if (mode is not null) items["Deployment:Mode"] = mode;
        if (directory is not null) items["License:Directory"] = directory;
        return new ConfigurationBuilder().AddInMemoryCollection(items).Build();
    }

    [Fact]
    public void Absent_IsHosted_WithNoDirectory()
    {
        var s = DeploymentSettings.From(Cfg());
        Assert.Equal(DeploymentMode.Hosted, s.Mode);
        Assert.Null(s.LicenseDirectory);
    }

    [Fact]
    public void Empty_IsHosted()
    {
        Assert.Equal(DeploymentMode.Hosted, DeploymentSettings.From(Cfg(mode: "")).Mode);
    }

    [Theory]
    [InlineData("Hosted")]
    [InlineData("hosted")]
    [InlineData("HOSTED")]
    public void Hosted_IsAccepted_AnyCase_AndIgnoresTheDirectory(string mode)
    {
        var s = DeploymentSettings.From(Cfg(mode, AbsoluteDir));
        Assert.Equal(DeploymentMode.Hosted, s.Mode);
        Assert.Null(s.LicenseDirectory);
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    public void Local_WithAbsoluteDirectory_IsAccepted(string mode)
    {
        var s = DeploymentSettings.From(Cfg(mode, AbsoluteDir));
        Assert.Equal(DeploymentMode.Local, s.Mode);
        Assert.Equal(AbsoluteDir, s.LicenseDirectory);
    }

    [Fact]
    public void Local_WithoutDirectory_Fails()
    {
        Assert.Throws<InvalidOperationException>(() => DeploymentSettings.From(Cfg("Local")));
        Assert.Throws<InvalidOperationException>(() => DeploymentSettings.From(Cfg("Local", "   ")));
    }

    [Fact]
    public void Local_WithRelativeDirectory_Fails()
    {
        Assert.Throws<InvalidOperationException>(() => DeploymentSettings.From(Cfg("Local", "licenses")));
    }

    [Theory]
    [InlineData("Cloud")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData(" ")]
    [InlineData("Local ")]
    [InlineData("Hosted,Local")]
    public void InvalidExplicitMode_Fails(string mode)
    {
        Assert.Throws<InvalidOperationException>(() => DeploymentSettings.From(Cfg(mode, AbsoluteDir)));
    }
}