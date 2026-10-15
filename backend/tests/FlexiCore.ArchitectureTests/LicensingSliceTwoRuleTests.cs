using System.Reflection;
using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.ArchitectureTests;

public class LicensingSliceTwoRuleTests
{
    private static string ProjectFile(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FlexiCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", project, project + ".csproj");
    }

    [Theory]
    [InlineData("FlexiCore.Identity")]
    [InlineData("FlexiCore.Tenancy")]
    [InlineData("FlexiCore.SharedKernel")]
    [InlineData("FlexiCore.Migrations")]
    public void Only_the_Api_project_references_Licensing(string project)
    {
        var text = File.ReadAllText(ProjectFile(project));
        Assert.DoesNotContain("FlexiCore.Licensing", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_project_references_Licensing()
    {
        var text = File.ReadAllText(ProjectFile("FlexiCore.Api"));
        Assert.Contains("FlexiCore.Licensing.csproj", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Licensing_assembly_has_no_AspNetCore_or_Extensions_references()
    {
        var refs = typeof(LicenseService).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
                        || n.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(refs);
    }
}