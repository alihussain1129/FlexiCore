using System.Text;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class LedgerRestoreServiceTests
{
    private static LedgerRestoreService Svc(Owner o) => new(o.Paths, o.Time);

    private static string IssueThree(Owner o)
    {
        var key = o.CreateKey();
        foreach (var id in new[] { "FC-R-1", "FC-R-2", "FC-R-3" })
            o.Issue.Issue(o.Input(key, id));
        return key;
    }

    private static (string Backup, byte[] Original) Backup(Owner o)
    {
        var backup = o.Out("backup.jsonl");
        File.Copy(o.Paths.LedgerPath, backup);
        return (backup, File.ReadAllBytes(backup));
    }

    private static string[] SafetyCopies(Owner o) => Directory.GetFiles(o.Paths.Root, "issue-ledger.jsonl.before-restore-*");

    [Fact]
    public void Restore_into_a_missing_ledger_recreates_it_byte_for_byte()
    {
        using var o = new Owner();
        IssueThree(o);
        var (backup, original) = Backup(o);
        File.Delete(o.Paths.LedgerPath);

        var result = Svc(o).Restore(backup);

        Assert.Equal(3, result.Added);
        Assert.Equal(0, result.AlreadyPresent);
        Assert.Null(result.SafetyCopyPath);
        Assert.Equal(original, File.ReadAllBytes(o.Paths.LedgerPath));
    }

    [Fact]
    public void Merge_appends_only_missing_records_keeps_existing_bytes_and_saves_a_safety_copy()
    {
        using var o = new Owner();
        IssueThree(o);
        var (backup, original) = Backup(o);
        var lines = File.ReadAllLines(o.Paths.LedgerPath);
        var truncated = Encoding.UTF8.GetBytes(lines[0] + Environment.NewLine);
        File.WriteAllBytes(o.Paths.LedgerPath, truncated);

        var result = Svc(o).Restore(backup);

        Assert.Equal(2, result.Added);
        Assert.Equal(1, result.AlreadyPresent);
        Assert.Equal(3, result.TotalAfter);
        Assert.Equal(original, File.ReadAllBytes(o.Paths.LedgerPath));
        var copy = Assert.Single(Directory.GetFiles(o.Paths.Root, "issue-ledger.jsonl.before-restore-*"));
        Assert.Equal(truncated, File.ReadAllBytes(copy));
        Assert.Equal(copy, result.SafetyCopyPath);
    }

    [Fact]
    public void Restoring_the_same_backup_twice_changes_nothing_the_second_time()
    {
        using var o = new Owner();
        IssueThree(o);
        var (backup, original) = Backup(o);
        File.Delete(o.Paths.LedgerPath);
        Svc(o).Restore(backup);

        var again = Svc(o).Restore(backup);

        Assert.Equal(0, again.Added);
        Assert.Equal(3, again.AlreadyPresent);
        Assert.Null(again.SafetyCopyPath);
        Assert.Equal(original, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(SafetyCopies(o));
    }

    [Fact]
    public void A_license_id_with_different_details_aborts_and_changes_nothing()
    {
        using var o = new Owner();
        IssueThree(o);
        var backup = o.Out("backup.jsonl");
        File.WriteAllText(backup, File.ReadAllText(o.Paths.LedgerPath).Replace("Test Customer", "Someone Else"));
        var before = File.ReadAllBytes(o.Paths.LedgerPath);

        Assert.Throws<LicenseToolException>(() => Svc(o).Restore(backup));

        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(SafetyCopies(o));
    }

    [Theory]
    [InlineData("unreadable")]
    [InlineData("empty")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public void An_unusable_backup_is_refused_and_changes_nothing(string kind)
    {
        using var o = new Owner();
        IssueThree(o);
        var good = File.ReadAllLines(o.Paths.LedgerPath);
        var backup = o.Out("backup.jsonl");
        switch (kind)
        {
            case "unreadable": File.WriteAllLines(backup, new[] { good[0], "not json" }); break;
            case "empty": File.WriteAllText(backup, ""); break;
            case "duplicate": File.WriteAllLines(backup, new[] { good[0], good[0] }); break;
        }
        var before = File.ReadAllBytes(o.Paths.LedgerPath);

        Assert.Throws<LicenseToolException>(() => Svc(o).Restore(backup));

        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(SafetyCopies(o));
    }

    [Fact]
    public void The_live_ledger_and_relative_paths_are_refused_as_backups()
    {
        using var o = new Owner();
        IssueThree(o);

        Assert.Throws<LicenseToolException>(() => Svc(o).Restore(o.Paths.LedgerPath));
        Assert.Throws<LicenseToolException>(() => Svc(o).Restore("relative.jsonl"));
    }

    [Fact]
    public void A_current_ledger_whose_last_line_has_no_newline_is_not_glued_to_the_restored_lines()
    {
        using var o = new Owner();
        IssueThree(o);
        var (backup, _) = Backup(o);
        var lines = File.ReadAllLines(o.Paths.LedgerPath);
        File.WriteAllText(o.Paths.LedgerPath, lines[0]);

        var result = Svc(o).Restore(backup);

        Assert.Equal(2, result.Added);
        var snapshot = LedgerReader.Read(o.Paths.LedgerPath);
        Assert.Equal(0, snapshot.UnreadableLines);
        Assert.Equal(3, snapshot.Entries.Count);
    }

    [Fact]
    public void Preview_reports_the_plan_and_writes_nothing()
    {
        using var o = new Owner();
        IssueThree(o);
        var (backup, _) = Backup(o);
        var lines = File.ReadAllLines(o.Paths.LedgerPath);
        File.WriteAllBytes(o.Paths.LedgerPath, Encoding.UTF8.GetBytes(lines[0] + Environment.NewLine));
        var before = File.ReadAllBytes(o.Paths.LedgerPath);

        var preview = Svc(o).Preview(backup);

        Assert.Equal(2, preview.Added);
        Assert.Equal(1, preview.AlreadyPresent);
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(SafetyCopies(o));
    }

    [Fact]
    public void After_a_restore_a_duplicate_license_id_is_refused_again()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        o.Issue.Issue(o.Input(key, "FC-R-1"));
        var (backup, _) = Backup(o);
        File.Delete(o.Paths.LedgerPath);
        Svc(o).Restore(backup);
        var again = o.Out("again.lic");

        Assert.Throws<LicenseToolException>(() => o.Issue.Issue(o.Input(key, "FC-R-1", output: again)));

        Assert.False(File.Exists(again));
    }
    [Fact]
    public async Task Concurrent_issuance_with_the_same_license_id_records_only_one_license()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var firstOutput = o.Out("concurrent-first.lic");
        var secondOutput = o.Out("concurrent-second.lic");

        static bool TryIssue(IssueService service, IssueInput input)
        {
            try
            {
                service.Issue(input);
                return true;
            }
            catch (LicenseToolException)
            {
                return false;
            }
        }

        var first = Task.Run(() => TryIssue(
            o.Issue, o.Input(key, "FC-CONCURRENT-1", output: firstOutput)));
        var second = Task.Run(() => TryIssue(
            o.Issue, o.Input(key, "FC-CONCURRENT-1", output: secondOutput)));

        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Single(LedgerReader.Read(o.Paths.LedgerPath).Entries);
        Assert.Equal(1, new[] { File.Exists(firstOutput), File.Exists(secondOutput) }
            .Count(exists => exists));
    }


    [Fact]
    public void A_second_lock_owner_cannot_enter_until_the_first_releases()
    {
        using var o = new Owner();
        var first = LedgerFileLock.Acquire(o.Paths.LedgerPath);

        try
        {
            Assert.Throws<LicenseToolException>(() =>
                LedgerFileLock.Acquire(o.Paths.LedgerPath, TimeSpan.FromMilliseconds(150)));
        }
        finally
        {
            first.Dispose();
        }

        using var second = LedgerFileLock.Acquire(
            o.Paths.LedgerPath, TimeSpan.FromSeconds(2));

        second.EnsureFor(o.Paths.LedgerPath);
    }

    [Fact]
    public async Task Reader_waits_for_lock_even_when_the_ledger_does_not_exist_yet()
    {
        using var o = new Owner();
        Assert.False(File.Exists(o.Paths.LedgerPath));

        var heldLock = LedgerFileLock.Acquire(o.Paths.LedgerPath);
        using var started = new ManualResetEventSlim();

        try
        {
            var readTask = Task.Run(() =>
            {
                started.Set();
                return LedgerReader.Read(o.Paths.LedgerPath);
            });

            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            var readPending = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromMilliseconds(200)));
            Assert.NotSame(readTask, readPending);

            heldLock.Dispose();

            var snapshot = await readTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(snapshot.Entries);
            Assert.Equal(0, snapshot.UnreadableLines);
            Assert.Null(snapshot.Problem);
        }
        finally
        {
            heldLock.Dispose();
        }
    }

    [Fact]
    public async Task Issuance_waits_until_an_existing_ledger_lock_is_released()
    {
        using var o = new Owner();
        var key = o.CreateKey();
        var output = o.Out("lock-wait-issue.lic");
        var heldLock = LedgerFileLock.Acquire(o.Paths.LedgerPath);
        using var started = new ManualResetEventSlim();

        try
        {
            var issueTask = Task.Run(() =>
            {
                started.Set();
                o.Issue.Issue(o.Input(key, "FC-LOCK-WAIT-ISSUE", output: output));
            });

            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            var issuePending = await Task.WhenAny(issueTask, Task.Delay(TimeSpan.FromMilliseconds(200)));
            Assert.NotSame(issueTask, issuePending);
            Assert.False(File.Exists(output));

            heldLock.Dispose();

            await issueTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(File.Exists(output));
            var snapshot = LedgerReader.Read(o.Paths.LedgerPath);
            Assert.Single(snapshot.Entries);
            Assert.Equal("FC-LOCK-WAIT-ISSUE", snapshot.Entries[0].LicenseId);
        }
        finally
        {
            heldLock.Dispose();
        }
    }

    [Fact]
    public async Task Restore_waits_until_an_existing_ledger_lock_is_released()
    {
        using var o = new Owner();
        IssueThree(o);
        var (backup, original) = Backup(o);
        File.Delete(o.Paths.LedgerPath);

        var heldLock = LedgerFileLock.Acquire(o.Paths.LedgerPath);
        using var started = new ManualResetEventSlim();

        try
        {
            var restoreTask = Task.Run(() =>
            {
                started.Set();
                return Svc(o).Restore(backup);
            });

            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            var restorePending = await Task.WhenAny(restoreTask, Task.Delay(TimeSpan.FromMilliseconds(200)));
            Assert.NotSame(restoreTask, restorePending);
            Assert.False(File.Exists(o.Paths.LedgerPath));

            heldLock.Dispose();

            var result = await restoreTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(3, result.Added);
            Assert.Equal(0, result.AlreadyPresent);
            Assert.Equal(original, File.ReadAllBytes(o.Paths.LedgerPath));
        }
        finally
        {
            heldLock.Dispose();
        }
    }

}

public sealed class LedgerRestorePageTests
{
    private static LedgerPageViewModel Page(Owner o, FakeDialogs dialogs) =>
        new(o.Paths, o.Time, LedgerMode.History, dialogs);

    private static (string Backup, byte[] Original) IssueAndBackup(Owner o)
    {
        var key = o.CreateKey();
        foreach (var id in new[] { "FC-P-1", "FC-P-2" }) o.Issue.Issue(o.Input(key, id));
        var backup = o.Out("backup.jsonl");
        File.Copy(o.Paths.LedgerPath, backup);
        return (backup, File.ReadAllBytes(backup));
    }

    [Fact]
    public async Task Restore_after_confirmation_brings_back_a_deleted_ledger()
    {
        using var o = new Owner();
        var (backup, original) = IssueAndBackup(o);
        File.Delete(o.Paths.LedgerPath);
        var dialogs = new FakeDialogs { OpenPath = backup, ConfirmResult = true };
        var page = Page(o, dialogs);

        await page.RestoreBackupAsync();

        Assert.Equal(original, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Equal(StatusKind.Success.ToString(), page.StatusKindName);
    }

    [Fact]
    public async Task Declining_the_confirmation_changes_nothing()
    {
        using var o = new Owner();
        var (backup, _) = IssueAndBackup(o);
        File.Delete(o.Paths.LedgerPath);
        var dialogs = new FakeDialogs { OpenPath = backup, ConfirmResult = false };
        var page = Page(o, dialogs);

        await page.RestoreBackupAsync();

        Assert.False(File.Exists(o.Paths.LedgerPath));
        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Equal(StatusKind.Info.ToString(), page.StatusKindName);
    }

    [Fact]
    public async Task A_corrupt_backup_reports_an_error_asks_nothing_and_changes_nothing()
    {
        using var o = new Owner();
        IssueAndBackup(o);
        var before = File.ReadAllBytes(o.Paths.LedgerPath);
        var bad = o.Out("bad.jsonl");
        File.WriteAllText(bad, "not json");
        var dialogs = new FakeDialogs { OpenPath = bad, ConfirmResult = true };
        var page = Page(o, dialogs);

        await page.RestoreBackupAsync();

        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Equal(0, dialogs.ConfirmCalls);
        Assert.Equal(StatusKind.Error.ToString(), page.StatusKindName);
    }
}
