using System.IO;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;
using Xunit;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class GuidedIssueFlowTests : IDisposable
{
    private const string Passphrase = "Tr0ub4dor&3-horse-staple-battery-9";
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "fc-guided-" + Guid.NewGuid().ToString("N"));
    private readonly string _outDir;
    private readonly OwnerPaths _paths;
    private readonly FixedClock _clock = new(Now);
    private readonly ScriptedDialogs _dialogs;

    public GuidedIssueFlowTests()
    {
        _outDir = Path.Combine(_root, "out");
        Directory.CreateDirectory(_outDir);
        _paths = new OwnerPaths(Path.Combine(_root, "home"));
        _dialogs = new ScriptedDialogs(Path.Combine(_outDir, "first.lic"));
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
        var vm = new IssueViewModel(keys, new IssueService(_paths, _clock), _dialogs, _clock);
        await vm.OnShownAsync();
        return vm;
    }

    [Fact]
    public async Task Three_month_license_is_issued_verified_and_copyable()
    {
        var vm = await NewViewModelAsync();
        vm.Licensee = "Test Co";
        vm.SelectedTerm = TermKind.ThreeMonths;

        await vm.IssueAsync(Passphrase);

        Assert.Equal("Success", vm.StatusKindName);
        Assert.True(vm.HasResult);
        Assert.Contains(vm.Result, r => r.Label == "Expires" && r.Value == "2027-01-22 23:59 UTC");
        Assert.StartsWith("FCL1-", vm.LastActivationText, StringComparison.Ordinal);

        // The copied text must decode to the exact file on disk and verify against the public key.
        Assert.True(ActivationCode.TryDecode(System.Text.Encoding.ASCII.GetBytes(vm.LastActivationText), out var decoded));
        var file = File.ReadAllBytes(_dialogs.LastSavePath!);
        Assert.Equal(file, decoded);
        var trusted = PublicKeyExport.Read(Path.Combine(_paths.KeysDirectory, "test-key-1.public.json"));
        var check = new LicenseVerifier(trusted, _clock).Verify(decoded);
        Assert.Equal(LicenseStatus.Valid, check.Status);
        Assert.Equal("Test Co", check.Payload!.Licensee);
        Assert.Single(LedgerReader.Read(_paths.LedgerPath).Entries);
    }

    [Fact]
    public async Task Reusing_a_license_id_is_refused_and_nothing_is_overwritten()
    {
        var vm = await NewViewModelAsync();
        vm.Licensee = "First Co";
        await vm.IssueAsync(Passphrase);
        var firstId = vm.Result.First(r => r.Label == "License ID").Value;
        var firstPath = _dialogs.LastSavePath!;
        var firstBytes = File.ReadAllBytes(firstPath);

        var secondPath = Path.Combine(_outDir, "second.lic");
        _dialogs.NextSavePath = secondPath;
        vm.Licensee = "Second Co";
        vm.LicenseId = firstId;
        await vm.IssueAsync(Passphrase);

        Assert.Equal("Error", vm.StatusKindName);
        Assert.False(File.Exists(secondPath));
        Assert.Equal(firstBytes, File.ReadAllBytes(firstPath));
        Assert.Single(LedgerReader.Read(_paths.LedgerPath).Entries);
    }

    [Fact]
    public async Task An_existing_file_is_never_overwritten()
    {
        var vm = await NewViewModelAsync();
        var target = Path.Combine(_outDir, "existing.lic");
        File.WriteAllText(target, "do not touch");
        _dialogs.NextSavePath = target;
        vm.Licensee = "Test Co";

        await vm.IssueAsync(Passphrase);

        Assert.Equal("Error", vm.StatusKindName);
        Assert.Equal("do not touch", File.ReadAllText(target));
        Assert.Empty(LedgerReader.Read(_paths.LedgerPath).Entries);
    }

    [Fact]
    public async Task New_customer_clears_the_form_and_the_result()
    {
        var vm = await NewViewModelAsync();
        vm.Licensee = "Test Co";
        await vm.IssueAsync(Passphrase);
        Assert.True(vm.HasResult);

        vm.NewCustomerCommand.Execute(null);

        Assert.Equal("", vm.Licensee);
        Assert.False(vm.HasResult);
        Assert.Equal("", vm.LastActivationText);
        Assert.Equal(Now.UtcDateTime.Date, vm.NotBeforeDate!.Value);
    }

    [Fact]
    public async Task A_stale_start_date_moves_to_today_when_the_page_is_shown()
    {
        var vm = await NewViewModelAsync();
        _clock.Advance(TimeSpan.FromDays(2));

        await vm.OnShownAsync();

        Assert.Equal(_clock.GetUtcNow().UtcDateTime.Date, vm.NotBeforeDate!.Value);
    }

    private sealed class FixedClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public void Advance(TimeSpan by) => _now += by;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ScriptedDialogs(string firstSavePath) : IDialogService
    {
        public string? NextSavePath { get; set; } = firstSavePath;
        public string? LastSavePath { get; private set; }

        public string? PickSavePath(string title, string defaultFileName, string filter)
        {
            LastSavePath = NextSavePath;
            return NextSavePath;
        }

        public string? PickOpenPath(string title, string filter) => null;
        public bool Confirm(string title, string message) => true;
        public void Reveal(string filePath) { }
        public void OpenFolder(string folderPath) { }
    }
}