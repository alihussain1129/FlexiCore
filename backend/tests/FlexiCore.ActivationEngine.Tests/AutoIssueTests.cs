using System.IO;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using Xunit;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class AutoIssueTests : IDisposable
{
    private const string Passphrase = "Tr0ub4dor&3-horse-staple-battery-9";
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fc-auto-" + Guid.NewGuid().ToString("N"));
    private readonly string _licenses;
    private readonly OwnerPaths _paths;
    private readonly FixedClock _clock = new(Now);

    public AutoIssueTests()
    {
        Directory.CreateDirectory(_root);
        _licenses = Path.Combine(_root, "licenses");
        _paths = new OwnerPaths(Path.Combine(_root, "home"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private async Task<IssueViewModel> NewViewModelAsync()
    {
        var keys = new KeyService(_paths);
        keys.Create("test-key-1", Passphrase, Passphrase);
        var dialogs = new AutoSaveDialogService(new NoDialogs(), _licenses);
        var vm = new IssueViewModel(keys, new IssueService(_paths, _clock), dialogs, _clock, _paths)
        {
            RequireRequestCode = true,
        };
        await vm.OnShownAsync();
        return vm;
    }

    [Fact]
    public void Auto_save_path_is_inside_a_folder_for_the_customer()
    {
        var dialogs = new AutoSaveDialogService(new NoDialogs(), _licenses);

        var path = dialogs.PickSavePath("t", "Acme-Ltd_FC-20261022-AABBCC.lic", "f");

        Assert.Equal(Path.Combine(_licenses, "Acme-Ltd", "Acme-Ltd_FC-20261022-AABBCC.lic"), path);
        Assert.True(Directory.Exists(Path.Combine(_licenses, "Acme-Ltd")));
        Assert.True(dialogs.Confirm("a", "b"));
    }

    [Fact]
    public async Task A_missing_request_code_blocks_issuing_by_default()
    {
        var vm = await NewViewModelAsync();
        vm.Licensee = "Test Co";

        await vm.IssueAsync(Passphrase);

        Assert.Equal("Error", vm.StatusKindName);
        Assert.False(vm.HasResult);
        Assert.Empty(LedgerReader.Read(_paths.LedgerPath).Entries);
        Assert.False(Directory.Exists(_licenses));
    }

    [Fact]
    public async Task With_the_override_the_license_is_issued_and_saved_without_any_dialog()
    {
        var vm = await NewViewModelAsync();
        vm.Licensee = "Test Co";
        vm.AllowUnbound = true;

        await vm.IssueAsync(Passphrase);

        Assert.Equal("Success", vm.StatusKindName);
        var folder = Path.Combine(_licenses, "Test-Co");
        Assert.Single(Directory.GetFiles(folder, "*.lic"));
        Assert.Single(LedgerReader.Read(_paths.LedgerPath).Entries);
    }

    [Fact]
    public async Task Known_customers_come_from_the_ledger()
    {
        var vm = await NewViewModelAsync();
        vm.Licensee = "Test Co";
        vm.AllowUnbound = true;
        await vm.IssueAsync(Passphrase);

        await vm.OnShownAsync();

        Assert.Contains("Test Co", vm.KnownCustomers);
    }

    [Fact]
    public async Task The_unlocked_passphrase_expires_after_30_minutes()
    {
        var vm = await NewViewModelAsync();
        vm.KeepUnlocked = true;
        vm.RememberPassphrase("abc");
        Assert.Equal("abc", vm.GetCachedPassphrase());

        _clock.Advance(TimeSpan.FromMinutes(31));

        Assert.Null(vm.GetCachedPassphrase());
    }

    [Fact]
    public async Task Turning_off_keep_unlocked_clears_the_passphrase()
    {
        var vm = await NewViewModelAsync();
        vm.KeepUnlocked = true;
        vm.RememberPassphrase("abc");

        vm.KeepUnlocked = false;

        Assert.Null(vm.GetCachedPassphrase());
    }

    private sealed class FixedClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class NoDialogs : IDialogService
    {
        public string? PickSavePath(string title, string defaultFileName, string filter) => null;
        public string? PickOpenPath(string title, string filter) => null;
        public bool Confirm(string title, string message) => false;
        public void Reveal(string filePath) { }
        public void OpenFolder(string folderPath) { }
    }
}