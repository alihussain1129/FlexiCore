using System.Reflection;
using FlexiCore.Licensing;
using Xunit;

namespace FlexiCore.ArchitectureTests;

public class LicensingDependencyRuleTests
{
    private static string BackendFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FlexiCore.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [Fact]
    public void Licensing_assembly_references_no_other_FlexiCore_assembly()
    {
        var refs = typeof(LicenseVerifier).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n.StartsWith("FlexiCore", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(refs);
    }

    [Fact]
    public void Licensing_project_file_has_no_project_references()
    {
        var text = File.ReadAllText(BackendFile("src", "FlexiCore.Licensing", "FlexiCore.Licensing.csproj"));
        Assert.DoesNotContain("<ProjectReference", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_project_file_does_not_reference_Licensing()
    {
        var text = File.ReadAllText(BackendFile("src", "FlexiCore.Identity", "FlexiCore.Identity.csproj"));
        Assert.DoesNotContain("FlexiCore.Licensing", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_assembly_does_not_reference_Licensing()
    {
        var identity = Assembly.Load("FlexiCore.Identity");
        var refs = identity.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain("FlexiCore.Licensing", refs);
    }
}