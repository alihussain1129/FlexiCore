using System.Reflection;
using FlexiCore.Identity;
using FlexiCore.SharedKernel;
using NetArchTest.Rules;

namespace FlexiCore.ArchitectureTests;

public class IdentityDependencyRuleTests
{
    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var types = Types.InAssembly(assembly);
        Assert.NotEmpty(types.GetTypes()); // guard: a rule over zero types would pass vacuously

        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful,
            "Forbidden dependency from: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Identity_does_not_depend_on_the_Api() =>
        AssertNoDependency(typeof(IdentityModule).Assembly, "FlexiCore.Api");

    [Fact]
    public void Identity_does_not_depend_on_the_migration_runner() =>
        AssertNoDependency(typeof(IdentityModule).Assembly, "FlexiCore.Migrations");

    [Fact]
    public void Identity_does_not_use_EntityFrameworkCore() =>
        AssertNoDependency(typeof(IdentityModule).Assembly, "Microsoft.EntityFrameworkCore");

    [Fact]
    public void SharedKernel_does_not_depend_on_Identity() =>
        AssertNoDependency(typeof(AssemblyMarker).Assembly, "FlexiCore.Identity");
}