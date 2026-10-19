using System.Reflection;
using FlexiCore.Catalog;
using FlexiCore.SharedKernel;
using FlexiCore.Tenancy;
using NetArchTest.Rules;
using Xunit;

namespace FlexiCore.ArchitectureTests;

public class CatalogArchitectureRuleTests
{
    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var types = Types.InAssembly(assembly);
        Assert.NotEmpty(types.GetTypes()); // guard: a rule over zero types would pass vacuously

        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful,
            "Forbidden dependency from: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    private static void AssertNamespaceDoesNotUse(string ns, string forbidden)
    {
        var types = Types.InAssembly(typeof(CatalogModule).Assembly).That().ResideInNamespace(ns);
        Assert.NotEmpty(types.GetTypes()); // guard

        var result = types.ShouldNot().HaveDependencyOn(forbidden).GetResult();
        Assert.True(result.IsSuccessful,
            "Forbidden dependency from: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Catalog_does_not_depend_on_the_Api() =>
        AssertNoDependency(typeof(CatalogModule).Assembly, "FlexiCore.Api");

    [Fact]
    public void Catalog_does_not_depend_on_the_migration_runner() =>
        AssertNoDependency(typeof(CatalogModule).Assembly, "FlexiCore.Migrations");

    [Fact]
    public void Catalog_does_not_depend_on_Identity() =>
        AssertNoDependency(typeof(CatalogModule).Assembly, "FlexiCore.Identity");

    [Fact]
    public void Catalog_does_not_depend_on_Devices() =>
        AssertNoDependency(typeof(CatalogModule).Assembly, "FlexiCore.Devices");

    [Fact]
    public void Catalog_does_not_use_EntityFrameworkCore() =>
        AssertNoDependency(typeof(CatalogModule).Assembly, "Microsoft.EntityFrameworkCore");

    [Fact]
    public void Catalog_data_does_not_depend_on_endpoints() =>
        AssertNamespaceDoesNotUse("FlexiCore.Catalog.Data", "FlexiCore.Catalog.Endpoints");

    [Fact]
    public void Catalog_endpoints_do_not_use_Npgsql_directly() =>
        AssertNamespaceDoesNotUse("FlexiCore.Catalog.Endpoints", "Npgsql");

    [Fact]
    public void Tenancy_does_not_depend_on_Catalog() =>
        AssertNoDependency(typeof(TenancyModule).Assembly, "FlexiCore.Catalog");

    [Fact]
    public void SharedKernel_does_not_depend_on_Catalog() =>
        AssertNoDependency(typeof(AssemblyMarker).Assembly, "FlexiCore.Catalog");
}