using System.Reflection;
using FlexiCore.Migrations;
using FlexiCore.SharedKernel;
using NetArchTest.Rules;

namespace FlexiCore.ArchitectureTests;

public class DependencyRuleTests
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
    public void SharedKernel_depends_on_no_other_FlexiCore_project() =>
        AssertNoDependency(typeof(AssemblyMarker).Assembly, "FlexiCore.Api", "FlexiCore.Migrations");

    [Fact]
    public void Api_does_not_depend_on_the_migration_runner() =>
        AssertNoDependency(typeof(FlexiCore.Api.Health.PostgresHealthCheck).Assembly, "FlexiCore.Migrations");

    [Fact]
    public void Migration_runner_does_not_depend_on_the_Api() =>
        AssertNoDependency(typeof(MigrationRunner).Assembly, "FlexiCore.Api");
}