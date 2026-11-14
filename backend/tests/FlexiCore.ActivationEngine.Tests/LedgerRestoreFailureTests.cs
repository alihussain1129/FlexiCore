using System.Text;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

/// <summary>
/// Restore failure paths. No permission tricks: failures come from the internal BeforeReplaceForTests hook,
/// a directory at the ledger path, a vanished temp file, and a ledger held open with FileShare.None.
/// Limits: the hook runs after the temp file is written, so a failure inside the write itself is not injected,
/// and cancellation is simulated by throwing OperationCanceledException from the hook.
/// </summary>
public sealed class LedgerRestoreFailureTests
{
    private static LedgerRestoreService Svc(Owner o) => new(o.Paths, o.Time);

    private static string[] IssueThree(Owner o)
    {
        var key = o.CreateKey();
        foreach (var id in new[] { "FC-X-1", "FC-X-2", "FC-X-3" })
            o.Issue.Issue(o.Input(key, id));
        return File.ReadAllLines(o.Paths.LedgerPath);
    }

    private static string WriteBackup(Owner o, params string[] lines)
    {
        var path = o.Out("backup.jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string[] TempFiles(Owner o) => Directory.GetFiles(o.Paths.Root, "*.tmp");

    private static string[] SafetyCopies(Owner o) =>
        Directory.GetFiles(o.Paths.Root, "issue-ledger.jsonl.before-restore-*");

    /// <summary>The ledger holds the first record; the backup adds the other two, so a restore has real work to do.</summary>
    private static (byte[] Before, string Backup) Arrange(Owner o)
    {
        var good = IssueThree(o);
        File.WriteAllLines(o.Paths.LedgerPath, new[] { good[0] });
        return (File.ReadAllBytes(o.Paths.LedgerPath), WriteBackup(o, good[1], good[2]));
    }

    [Fact]
    public void A_simulated_disk_full_error_before_the_replace_keeps_the_ledger_and_removes_the_temp_file()
    {
        using var o = new Owner();
        var (before, backup) = Arrange(o);
        var svc = Svc(o);
        svc.BeforeReplaceForTests = _ => throw new IOException("There is not enough space on the disk.");

        var ex = Assert.Throws<LicenseToolException>(() => svc.Restore(backup));

        Assert.Contains("The existing ledger was not changed", ex.Message);
        Assert.DoesNotContain("not enough space", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
        Assert.Equal(before, File.ReadAllBytes(Assert.Single(SafetyCopies(o))));
    }

    [Fact]
    public void An_unexpected_exception_type_keeps_the_ledger_removes_the_temp_file_and_is_not_replaced()
    {
        using var o = new Owner();
        var (before, backup) = Arrange(o);
        var svc = Svc(o);
        svc.BeforeReplaceForTests = _ => throw new InvalidOperationException("boom");

        var ex = Assert.Throws<InvalidOperationException>(() => svc.Restore(backup));

        Assert.Equal("boom", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
    }

    [Fact]
    public void Simulated_cancellation_keeps_the_ledger_and_removes_the_temp_file()
    {
        using var o = new Owner();
        var (before, backup) = Arrange(o);
        var svc = Svc(o);
        svc.BeforeReplaceForTests = _ => throw new OperationCanceledException();

        Assert.Throws<OperationCanceledException>(() => svc.Restore(backup));

        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
    }

    [Fact]
    public void A_failed_atomic_replace_because_the_temp_file_vanished_leaves_the_ledger_untouched()
    {
        using var o = new Owner();
        var (before, backup) = Arrange(o);
        var svc = Svc(o);
        svc.BeforeReplaceForTests = temp => File.Delete(temp);

        Assert.Throws<LicenseToolException>(() => svc.Restore(backup));

        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
    }

    [Fact]
    public void A_directory_where_the_ledger_should_be_fails_the_replace_and_removes_the_temp_file()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        var backup = WriteBackup(o, good[0]);
        File.Delete(o.Paths.LedgerPath);
        Directory.CreateDirectory(o.Paths.LedgerPath);

        Assert.Throws<LicenseToolException>(() => Svc(o).Restore(backup));

        Assert.True(Directory.Exists(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
    }

    [Fact]
    public void A_cleanup_failure_does_not_hide_the_original_error()
    {
        using var o = new Owner();
        var (before, backup) = Arrange(o);
        var svc = Svc(o);
        svc.BeforeReplaceForTests = temp =>
        {
            // Make the temp path undeletable with File.Delete: a non-empty directory now sits at that path.
            File.Delete(temp);
            Directory.CreateDirectory(temp);
            File.WriteAllText(Path.Combine(temp, "x"), "x");
            throw new InvalidOperationException("original failure");
        };

        var ex = Assert.Throws<InvalidOperationException>(() => svc.Restore(backup));

        Assert.Equal("original failure", ex.Message);
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
    }

    [Fact]
    public void A_ledger_that_cannot_be_opened_refuses_the_restore_and_changes_nothing()
    {
        using var o = new Owner();
        var (before, backup) = Arrange(o);

        using (new FileStream(o.Paths.LedgerPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = Assert.Throws<LicenseToolException>(() => Svc(o).Restore(backup));
            Assert.Contains("nothing was restored", ex.Message);
        }

        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
        Assert.Empty(SafetyCopies(o));
    }

    [Fact]
    public void A_live_ledger_with_invalid_utf8_refuses_the_restore_without_leaking_its_contents()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        var backup = WriteBackup(o, good[0]);
        var damaged = Encoding.UTF8.GetBytes("{\"licensee\":\"SECRET-CUSTOMER-NAME\"}\n")
            .Concat(new byte[] { 0xC3, 0x28, 0x0A }).ToArray();
        File.WriteAllBytes(o.Paths.LedgerPath, damaged);

        var ex = Assert.Throws<LicenseToolException>(() => Svc(o).Restore(backup));

        Assert.Contains("UTF-8", ex.Message);
        Assert.DoesNotContain("SECRET-CUSTOMER-NAME", ex.Message);
        Assert.Equal(damaged, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(TempFiles(o));
        Assert.Empty(SafetyCopies(o));
    }
}
