using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class CustomerRegistryTests
{
    [Fact]
    public void Register_creates_stable_customer_id_and_survives_registry_restart()
    {
        using var owner = new Owner();
        var registry = new CustomerRegistry(owner.Paths, owner.Time);

        var created = registry.Register(new CustomerInput("  Acme Limited  ", "Acme Holdings Limited", "ops@acme.example", "+1 555 0100"));
        var reloaded = new CustomerRegistry(owner.Paths, owner.Time);

        var found = Assert.Single(reloaded.List());
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Acme Limited", found.Name);
        Assert.Equal(CustomerStatus.Active, found.Status);
        Assert.Equal(created, reloaded.Get(created.Id));
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(owner.Paths.CustomersPath));
    }

    [Theory]
    [InlineData(" ", null, null, null)]
    [InlineData("A", null, null, null)]
    [InlineData("Valid Name", null, "not-an-email", null)]
    [InlineData("Valid Name", null, null, "bad\nphone")]
    public void Register_rejects_invalid_customer_information(string name, string? legalName, string? email, string? phone)
    {
        using var owner = new Owner();
        var registry = new CustomerRegistry(owner.Paths, owner.Time);

        Assert.Throws<LicenseToolException>(() => registry.Register(new CustomerInput(name, legalName, email, phone)));
        Assert.Empty(registry.List());
    }

    [Fact]
    public void Email_is_unique_case_insensitively_and_update_preserves_stable_identity()
    {
        using var owner = new Owner();
        var registry = new CustomerRegistry(owner.Paths, owner.Time);
        var first = registry.Register(new CustomerInput("Acme", Email: "ops@acme.example"));

        Assert.Throws<LicenseToolException>(() => registry.Register(new CustomerInput("Other Co", Email: "OPS@ACME.EXAMPLE")));
        var updated = registry.Update(first.Id, new CustomerInput("Acme Group", Email: "ops@acme.example"));

        Assert.Equal(first.Id, updated.Id);
        Assert.Equal(first.CreatedAt, updated.CreatedAt);
        Assert.Equal("Acme Group", updated.Name);
    }

    [Fact]
    public void Status_changes_are_durable_and_unknown_ids_are_rejected()
    {
        using var owner = new Owner();
        var registry = new CustomerRegistry(owner.Paths, owner.Time);
        var customer = registry.Register(new CustomerInput("Acme"));

        registry.SetStatus(customer.Id, CustomerStatus.Inactive);

        Assert.Equal(CustomerStatus.Inactive, Assert.Single(new CustomerRegistry(owner.Paths, owner.Time).List()).Status);
        Assert.Throws<LicenseToolException>(() => registry.SetStatus(Guid.NewGuid(), CustomerStatus.Active));
    }

    [Fact]
    public void Malformed_registry_is_preserved_and_never_replaced_by_a_new_record()
    {
        using var owner = new Owner();
        Directory.CreateDirectory(owner.Paths.Root);
        File.WriteAllText(owner.Paths.CustomersPath, "{broken");
        var original = File.ReadAllBytes(owner.Paths.CustomersPath);
        var registry = new CustomerRegistry(owner.Paths, owner.Time);

        Assert.Throws<LicenseToolException>(() => registry.Register(new CustomerInput("Acme")));
        Assert.Equal(original, File.ReadAllBytes(owner.Paths.CustomersPath));
    }

    [Fact]
    public async Task Customers_page_register_command_writes_the_durable_registry()
    {
        using var owner = new Owner();
        var viewModel = new CustomersViewModel(owner.Paths, owner.Time) { Name = "Registered Co", Email = "admin@registered.example" };

        viewModel.SaveCommand.Execute(null);
        await viewModel.OnShownAsync();

        var registered = Assert.Single(new CustomerRegistry(owner.Paths, owner.Time).List());
        Assert.Equal("Registered Co", registered.Name);
        Assert.Single(viewModel.Rows);
        Assert.True(viewModel.Rows[0].IsRegistered);
    }
}
