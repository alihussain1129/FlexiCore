using System.Reflection;
using FlexiCore.Devices;
using FlexiCore.Identity;
using FlexiCore.Licensing;
using FlexiCore.SharedKernel;
using FlexiCore.Tenancy;
using NetArchTest.Rules;

namespace FlexiCore.ArchitectureTests;

public class DevicesDependencyRuleTests
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
    public void Devices_does_not_depend_on_the_Api_or_the_migration_runner() =>
        AssertNoDependency(typeof(DevicesModule).Assembly, "FlexiCore.Api", "FlexiCore.Migrations");

    [Fact]
    public void Devices_does_not_depend_on_Identity_Tenancy_or_Licensing() =>
        AssertNoDependency(typeof(DevicesModule).Assembly, "FlexiCore.Identity", "FlexiCore.Tenancy", "FlexiCore.Licensing");

    [Fact]
    public void Devices_does_not_use_EntityFrameworkCore() =>
        AssertNoDependency(typeof(DevicesModule).Assembly, "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Other_modules_do_not_depend_on_Devices()
    {
        AssertNoDependency(typeof(IdentityModule).Assembly, "FlexiCore.Devices");
        AssertNoDependency(typeof(TenancyModule).Assembly, "FlexiCore.Devices");
        AssertNoDependency(typeof(LicenseService).Assembly, "FlexiCore.Devices");
        AssertNoDependency(typeof(AssemblyMarker).Assembly, "FlexiCore.Devices");
    }
}