using System.Reflection;
using FlexiCore.SharedKernel;
using FlexiCore.Tenancy;
using NetArchTest.Rules;

namespace FlexiCore.ArchitectureTests;

public class TenancyDependencyRuleTests
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
    public void Tenancy_does_not_depend_on_the_Api() =>
        AssertNoDependency(typeof(TenancyModule).Assembly, "FlexiCore.Api");

    [Fact]
    public void Tenancy_does_not_depend_on_the_migration_runner() =>
        AssertNoDependency(typeof(TenancyModule).Assembly, "FlexiCore.Migrations");

    [Fact]
    public void Tenancy_does_not_depend_on_Identity() =>
        AssertNoDependency(typeof(TenancyModule).Assembly, "FlexiCore.Identity");

    [Fact]
    public void Tenancy_does_not_use_EntityFrameworkCore() =>
        AssertNoDependency(typeof(TenancyModule).Assembly, "Microsoft.EntityFrameworkCore");

    [Fact]
    public void SharedKernel_does_not_depend_on_Tenancy() =>
        AssertNoDependency(typeof(AssemblyMarker).Assembly, "FlexiCore.Tenancy");
}