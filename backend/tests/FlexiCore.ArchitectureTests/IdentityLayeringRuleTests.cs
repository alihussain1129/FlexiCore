using FlexiCore.Identity;
using NetArchTest.Rules;

namespace FlexiCore.ArchitectureTests;

public class IdentityLayeringRuleTests
{
    private static void AssertNamespaceHasNoDependency(string identityNamespace, params string[] forbidden)
    {
        var types = Types.InAssembly(typeof(IdentityModule).Assembly).That().ResideInNamespace(identityNamespace);
        Assert.NotEmpty(types.GetTypes()); // guard: a rule over zero types would pass vacuously

        var result = types.ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful,
            "Forbidden dependency from: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Data_does_not_depend_on_Endpoints() =>
        AssertNamespaceHasNoDependency("FlexiCore.Identity.Data", "FlexiCore.Identity.Endpoints");

    [Fact]
    public void Security_does_not_depend_on_Data_or_Endpoints() =>
        AssertNamespaceHasNoDependency("FlexiCore.Identity.Security", "FlexiCore.Identity.Data", "FlexiCore.Identity.Endpoints");

    [Fact]
    public void Identity_has_no_logging_dependency_so_credentials_cannot_be_logged_from_it() =>
        AssertNamespaceHasNoDependency("FlexiCore.Identity", "Microsoft.Extensions.Logging");

    [Fact]
    public void Identity_does_not_use_HttpLogging() =>
        AssertNamespaceHasNoDependency("FlexiCore.Identity", "Microsoft.AspNetCore.HttpLogging");

    [Fact]
    public void The_Api_does_not_use_HttpLogging_which_could_record_headers_and_bodies()
    {
        var types = Types.InAssembly(typeof(Program).Assembly);
        Assert.NotEmpty(types.GetTypes());

        var result = types.ShouldNot().HaveDependencyOn("Microsoft.AspNetCore.HttpLogging").GetResult();
        Assert.True(result.IsSuccessful, "Forbidden dependency from: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}