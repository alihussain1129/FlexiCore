using FlexiCore.Accounting;
using NetArchTest.Rules;

namespace FlexiCore.ArchitectureTests;

public class AccountingLayeringRuleTests
{
    private static void AssertNamespaceDoesNotUse(string ns, string forbidden)
    {
        var types = Types.InAssembly(typeof(AccountingModule).Assembly).That().ResideInNamespace(ns);
        Assert.NotEmpty(types.GetTypes()); // guard: a rule over zero types would pass vacuously

        var result = types.ShouldNot().HaveDependencyOn(forbidden).GetResult();
        Assert.True(result.IsSuccessful,
            "Forbidden dependency from: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Accounting_data_does_not_depend_on_endpoints() =>
        AssertNamespaceDoesNotUse("FlexiCore.Accounting.Data", "FlexiCore.Accounting.Endpoints");

    [Fact]
    public void Accounting_endpoints_do_not_use_Npgsql_directly() =>
        AssertNamespaceDoesNotUse("FlexiCore.Accounting.Endpoints", "Npgsql");
}
