using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class LedgerExportTests
{
    [Fact]
    public async Task Export_waits_for_the_ledger_lock_without_blocking_the_caller_and_then_copies_the_whole_ledger()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-X-1"));
        var destination = o.Out("export.jsonl");
        var page = new LedgerPageViewModel(o.Paths, o.Time, LedgerMode.History, new FakeDialogs { SavePath = destination });
        var held = LedgerFileLock.Acquire(o.Paths.LedgerPath);

        try
        {
            var export = page.ExportBackupAsync();
            await Task.Delay(400);

            Assert.False(export.IsCompleted);
            Assert.False(File.Exists(destination));

            held.Dispose();
            await export.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            held.Dispose();
        }

        Assert.Equal(File.ReadAllBytes(o.Paths.LedgerPath), File.ReadAllBytes(destination));
        Assert.Equal(StatusKind.Success.ToString(), page.StatusKindName);
    }

    [Fact]
    public async Task A_failed_export_releases_the_ledger_lock()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-X-1"));
        var existing = o.Out("already-there.jsonl");
        File.WriteAllText(existing, "keep me");
        var page = new LedgerPageViewModel(o.Paths, o.Time, LedgerMode.History, new FakeDialogs { SavePath = existing });

        await page.ExportBackupAsync();

        Assert.Equal(StatusKind.Error.ToString(), page.StatusKindName);
        Assert.Equal("keep me", File.ReadAllText(existing));
        using var ledgerLock = LedgerFileLock.Acquire(o.Paths.LedgerPath, TimeSpan.FromSeconds(2));
        ledgerLock.EnsureFor(o.Paths.LedgerPath);
    }
}
