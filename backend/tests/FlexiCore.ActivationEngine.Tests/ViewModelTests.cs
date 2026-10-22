using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using FlexiCore.Licensing;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class DashboardAndCustomersTests
{
    [Fact]
    public async Task Dashboard_with_no_data_shows_zero_counts_empty_state_and_a_missing_key_warning()
    {
        using var o = new Owner();
        var vm = new DashboardViewModel(o.Paths, o.Keys, o.Time);

        await vm.OnShownAsync();

        Assert.Equal("0", vm.Stats[0].Value);
        Assert.True(vm.HasNoRecent);
        Assert.Contains(vm.Warnings, w => w.Contains("No signing key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dashboard_counts_come_from_the_persisted_ledger()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-D-1", output: o.Out("1.lic")));
        o.Issue.Issue(o.Input(key, "FC-D-2", output: o.Out("2.lic")));
        var vm = new DashboardViewModel(o.Paths, o.Keys, o.Time);

        await vm.OnShownAsync();

        Assert.Equal("2", vm.Stats[0].Value);
        Assert.Equal("2", vm.Stats[1].Value);
        Assert.Equal("0", vm.Stats[2].Value);
        Assert.Equal(2, vm.Recent.Count);
        Assert.True(vm.HasRecent);
        Assert.DoesNotContain(vm.Warnings, w => w.Contains("No signing key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Customers_are_grouped_by_name_ignoring_case_and_can_be_searched()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-C-1", "Acme Ltd", output: o.Out("1.lic")));
        o.Issue.Issue(o.Input(key, "FC-C-2", "acme ltd", output: o.Out("2.lic")));
        o.Issue.Issue(o.Input(key, "FC-C-3", "Beta Co", output: o.Out("3.lic")));
        var vm = new CustomersViewModel(o.Paths, o.Time);

        await vm.OnShownAsync();

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(2, vm.Rows.Single(r => r.Name.StartsWith("acme", StringComparison.OrdinalIgnoreCase)).Licenses);
        vm.SearchText = "beta";
        Assert.Single(vm.Rows);
        Assert.False(vm.HasNoRows);
    }

    [Fact]
    public async Task Customers_page_is_empty_without_a_ledger()
    {
        using var o = new Owner();
        var vm = new CustomersViewModel(o.Paths, o.Time);
        await vm.OnShownAsync();
        Assert.True(vm.HasNoRows);
        Assert.Empty(vm.Rows);
    }

    [Fact]
    public async Task History_page_lists_newest_first()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-H-1", output: o.Out("1.lic")));
        var vm = new LedgerPageViewModel(o.Paths, o.Time, LedgerMode.History);

        await vm.OnShownAsync();

        Assert.Equal("Issuance History", vm.Title);
        Assert.Equal("FC-H-1", Assert.Single(vm.Rows).LicenseId);
        Assert.Equal("Active", vm.Rows[0].Status);
    }
}

public sealed class IssueViewModelTests
{
    private static async Task<(Owner Owner, IssueViewModel Vm, FakeDialogs Dialogs)> ReadyAsync(string? savePath = null)
    {
        var o = new Owner();
        o.CreateKey();
        var dialogs = new FakeDialogs { SavePath = savePath ?? o.Out("vm.lic"), ConfirmResult = true };
        var vm = new IssueViewModel(o.Keys, o.Issue, dialogs, o.Time);
        await vm.OnShownAsync();
        vm.Licensee = "VM Customer";
        return (o, vm, dialogs);
    }

    [Fact]
    public async Task Issue_page_issues_a_verifiable_license_and_records_it()
    {
        var (o, vm, _) = await ReadyAsync();
        using (o)
        {
            Assert.Equal("test-key-1", vm.SelectedKeyId);

            var used = await vm.IssueAsync(Owner.Pass);

            Assert.True(used);
            Assert.NotEqual("Error", vm.StatusKindName);
            Assert.True(File.Exists(o.Out("vm.lic")));
            Assert.Equal(LicenseStatus.Valid, o.Verify("test-key-1", o.Out("vm.lic")).Status);
            Assert.Single(LedgerReader.Read(o.Paths.LedgerPath).Entries);
        }
    }

    [Fact]
    public async Task Cancelling_the_save_dialog_or_the_confirmation_issues_nothing()
    {
        var (o, vm, dialogs) = await ReadyAsync();
        using (o)
        {
            dialogs.SavePath = null;
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.False(File.Exists(o.Paths.LedgerPath));

            dialogs.SavePath = o.Out("vm.lic");
            dialogs.ConfirmResult = false;
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.False(File.Exists(o.Out("vm.lic")));
            Assert.False(File.Exists(o.Paths.LedgerPath));
        }
    }

    [Fact]
    public async Task Missing_passphrase_licensee_or_key_is_reported_as_an_error()
    {
        var (o, vm, dialogs) = await ReadyAsync();
        using (o)
        {
            Assert.False(await vm.IssueAsync(""));
            Assert.Equal("Error", vm.StatusKindName);

            vm.Licensee = "  ";
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.Equal("Error", vm.StatusKindName);

            vm.Licensee = "VM Customer";
            vm.SelectedKeyId = null;
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.Equal("Error", vm.StatusKindName);

            Assert.Equal(0, dialogs.ConfirmCalls);
            Assert.False(File.Exists(o.Paths.LedgerPath));
        }
    }

    [Fact]
    public async Task Past_start_date_bad_numbers_and_bad_request_code_are_rejected_before_any_dialog()
    {
        var (o, vm, dialogs) = await ReadyAsync();
        using (o)
        {
            vm.NotBeforeDate = Owner.Now.UtcDateTime.Date.AddDays(-1);
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.Equal("Error", vm.StatusKindName);
            vm.NotBeforeDate = Owner.Now.UtcDateTime.Date;

            vm.GraceDaysText = "abc";
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.Equal("Error", vm.StatusKindName);
            vm.GraceDaysText = "0";

            vm.RequestText = "FCR1-nope";
            Assert.False(await vm.IssueAsync(Owner.Pass));
            Assert.Equal("Error", vm.StatusKindName);

            Assert.Equal(0, dialogs.ConfirmCalls);
            Assert.False(File.Exists(o.Paths.LedgerPath));
        }
    }

    [Fact]
    public async Task Issue_page_binds_a_valid_request_code_into_the_license()
    {
        var (o, vm, _) = await ReadyAsync();
        using (o)
        {
            var id = InstallationIds.FromMachineSource("unit-test-machine");
            vm.SetInstallation(id);

            Assert.True(await vm.IssueAsync(Owner.Pass));

            Assert.Equal(id, o.Verify("test-key-1", o.Out("vm.lic")).Payload!.InstallationId);
        }
    }
}

public sealed class ActivateViewModelTests
{
    private static string Id() => InstallationIds.FromMachineSource("unit-test-machine");
    private static string Code() => InstallationIds.ToRequestCode(Id());

    [Fact]
    public void Valid_code_is_verified_and_can_proceed()
    {
        var vm = new ActivateViewModel(new FakeDialogs());
        string? proceeded = null;
        vm.ProceedRequested += id => proceeded = id;

        vm.RequestText = Code();
        vm.VerifyCommand.Execute(null);

        Assert.True(vm.HasParsed);
        Assert.Equal("Success", vm.StatusKindName);
        Assert.Equal(Id(), vm.ParsedInstallationId);
        Assert.True(vm.ProceedCommand.CanExecute(null));
        vm.ProceedCommand.Execute(null);
        Assert.Equal(Id(), proceeded);
    }

    [Fact]
    public void Invalid_code_shows_an_error_and_cannot_proceed()
    {
        var vm = new ActivateViewModel(new FakeDialogs());

        vm.RequestText = "garbage";
        vm.VerifyCommand.Execute(null);

        Assert.False(vm.HasParsed);
        Assert.Equal("Error", vm.StatusKindName);
        Assert.False(vm.ProceedCommand.CanExecute(null));
    }

    [Fact]
    public void Editing_the_text_after_a_good_check_clears_the_result()
    {
        var vm = new ActivateViewModel(new FakeDialogs());
        vm.RequestText = Code();
        vm.VerifyCommand.Execute(null);
        Assert.True(vm.HasParsed);

        vm.RequestText = Code() + " ";

        Assert.False(vm.HasParsed);
    }

    [Fact]
    public void Opening_a_request_file_parses_it()
    {
        using var dir = new TempDir();
        var path = dir.Combine("request.txt");
        File.WriteAllText(path, Code());
        var vm = new ActivateViewModel(new FakeDialogs { OpenPath = path });

        vm.OpenFileCommand.Execute(null);

        Assert.True(vm.HasParsed);
        Assert.Equal(Id(), vm.ParsedInstallationId);
    }
}

public sealed class NavigationTests
{
    [Fact]
    public void Every_section_is_present_and_selecting_it_shows_its_own_page()
    {
        using var o = new Owner();
        var vm = new MainViewModel(new FakeDialogs(), o.Paths, o.Time);

        Assert.Equal(
            new[] { "Dashboard", "Customers", "Licenses", "Activate Installation", "Issue License",
                    "Signing Keys", "Issuance History", "Settings", "Help" },
            vm.Items.Select(i => i.Title).ToArray());
        Assert.Equal(vm.Items.Count, vm.Items.Select(i => i.Page).Distinct().Count());

        foreach (var item in vm.Items)
        {
            vm.Selected = item;
            Assert.Same(item.Page, vm.CurrentPage);
        }
    }

    [Fact]
    public void Quick_action_commands_navigate_to_the_intended_page()
    {
        using var o = new Owner();
        var vm = new MainViewModel(new FakeDialogs(), o.Paths, o.Time);

        vm.GoIssueCommand.Execute(null);
        Assert.IsType<IssueViewModel>(vm.CurrentPage);
        vm.GoKeysCommand.Execute(null);
        Assert.IsType<KeysViewModel>(vm.CurrentPage);
        vm.GoActivateCommand.Execute(null);
        Assert.IsType<ActivateViewModel>(vm.CurrentPage);
    }

    [Fact]
    public void Settings_and_help_describe_the_owner_folders_without_secrets()
    {
        using var o = new Owner();
        var settings = new SettingsViewModel(o.Paths);
        var help = new HelpViewModel(o.Paths);

        Assert.Contains(settings.Details, d => d.Label == "Owner data folder" && d.Value == o.Paths.Root);
        Assert.Contains(help.Sections, s => s.Body.Contains(o.Paths.Root, StringComparison.Ordinal));
        Assert.True(help.Sections.Count >= 6);
        Assert.DoesNotContain(settings.Details, d => d.Value.Contains("BEGIN", StringComparison.Ordinal));
    }
}
