using System.Text;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class LedgerRestoreIntegrityTests
{
    private static LedgerRestoreService Svc(Owner o) => new(o.Paths, o.Time);

    private static string[] IssueThree(Owner o)
    {
        var key = o.CreateKey();
        foreach (var id in new[] { "FC-R-1", "FC-R-2", "FC-R-3" })
            o.Issue.Issue(o.Input(key, id));
        return File.ReadAllLines(o.Paths.LedgerPath);
    }

    private static string WithCustomer(string line, string name)
    {
        var changed = line.Replace("Test Customer", name);
        Assert.NotEqual(line, changed);
        return changed;
    }

    private static string WriteBackup(Owner o, params string[] lines)
    {
        var path = o.Out("backup.jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string[] SafetyCopies(Owner o) =>
        Directory.GetFiles(o.Paths.Root, "issue-ledger.jsonl.before-restore-*");

    private static void AssertRefusedUnchanged(Owner o, string backup, params string[] fragments)
    {
        var before = File.ReadAllBytes(o.Paths.LedgerPath);

        var restore = Assert.Throws<LicenseToolException>(() => Svc(o).Restore(backup));
        var preview = Assert.Throws<LicenseToolException>(() => Svc(o).Preview(backup));

        foreach (var fragment in fragments)
        {
            Assert.Contains(fragment, restore.Message);
            Assert.Contains(fragment, preview.Message);
        }
        Assert.Equal(before, File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Empty(SafetyCopies(o));
    }

    [Fact]
    public void Two_identical_copies_of_an_id_in_the_ledger_are_refused()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        File.WriteAllLines(o.Paths.LedgerPath, new[] { good[0], good[0] });

        AssertRefusedUnchanged(o, WriteBackup(o, good[0]), "FC-R-1", "more than once in the current ledger");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_matching_copy_cannot_hide_a_conflicting_copy_in_the_ledger(bool conflictingFirst)
    {
        using var o = new Owner();
        var good = IssueThree(o);
        var bad = WithCustomer(good[0], "Someone Else");
        File.WriteAllLines(o.Paths.LedgerPath, conflictingFirst ? new[] { bad, good[0] } : new[] { good[0], bad });

        AssertRefusedUnchanged(o, WriteBackup(o, good[0]), "FC-R-1", "more than once in the current ledger");
    }

    [Fact]
    public void Two_conflicting_copies_of_an_id_in_the_ledger_are_refused()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        File.WriteAllLines(o.Paths.LedgerPath, new[]
        {
            WithCustomer(good[0], "Customer A"),
            WithCustomer(good[0], "Customer B"),
        });

        AssertRefusedUnchanged(o, WriteBackup(o, good[0]), "FC-R-1", "more than once in the current ledger");
    }

    [Fact]
    public void Duplicate_ids_inside_the_backup_are_refused_even_when_the_details_differ()
    {
        using var o = new Owner();
        var good = IssueThree(o);

        AssertRefusedUnchanged(
            o, WriteBackup(o, good[0], WithCustomer(good[0], "Someone Else")), "FC-R-1", "more than once");
    }

    [Fact]
    public void A_backup_id_already_in_the_ledger_with_different_details_is_refused_by_name()
    {
        using var o = new Owner();
        var good = IssueThree(o);

        AssertRefusedUnchanged(
            o, WriteBackup(o, good[0], WithCustomer(good[1], "Someone Else"), good[2]), "FC-R-2", "different details");
    }

    [Fact]
    public void A_backup_of_genuinely_new_ids_is_appended_after_the_untouched_ledger()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        File.WriteAllLines(o.Paths.LedgerPath, new[] { good[0] });
        var before = File.ReadAllBytes(o.Paths.LedgerPath);
        var backup = WriteBackup(o, good[1], good[2]);

        var result = Svc(o).Restore(backup);

        var nl = Environment.NewLine;
        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.AlreadyPresent);
        Assert.Equal(3, result.TotalAfter);
        Assert.Equal(
            before.Concat(Encoding.UTF8.GetBytes(good[1] + nl + good[2] + nl)).ToArray(),
            File.ReadAllBytes(o.Paths.LedgerPath));
        Assert.Equal(before, File.ReadAllBytes(Assert.Single(SafetyCopies(o))));
    }

    [Fact]
    public void A_duplicate_the_backup_does_not_mention_is_left_alone()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        File.WriteAllLines(o.Paths.LedgerPath, new[] { good[2], good[2] });
        var before = File.ReadAllBytes(o.Paths.LedgerPath);

        var result = Svc(o).Restore(WriteBackup(o, good[0]));

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.AlreadyPresent);
        Assert.Equal(3, result.TotalAfter);
        Assert.Equal(
            before.Concat(Encoding.UTF8.GetBytes(good[0] + Environment.NewLine)).ToArray(),
            File.ReadAllBytes(o.Paths.LedgerPath));
    }

    [Fact]
    public void Backup_lines_are_written_exactly_as_stored_not_reserialized()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        var spaced = good[0].Replace("\"licenseId\":", "\"licenseId\" :");
        Assert.NotEqual(good[0], spaced);
        File.Delete(o.Paths.LedgerPath);

        Svc(o).Restore(WriteBackup(o, spaced));

        Assert.Equal(spaced + Environment.NewLine, File.ReadAllText(o.Paths.LedgerPath));
    }

    [Fact]
    public void A_backup_that_is_not_valid_utf8_is_refused()
    {
        using var o = new Owner();
        var good = IssueThree(o);
        var backup = o.Out("backup.jsonl");
        File.WriteAllBytes(
            backup,
            Encoding.UTF8.GetBytes(good[0] + Environment.NewLine).Concat(new byte[] { 0xC3, 0x28, 0x0A }).ToArray());

        AssertRefusedUnchanged(o, backup, "UTF-8");
    }

    [Fact]
    public void A_backup_over_the_size_limit_is_refused_without_being_parsed()
    {
        using var o = new Owner();
        IssueThree(o);
        var backup = o.Out("huge.jsonl");
        using (var stream = new FileStream(backup, FileMode.CreateNew))
            stream.SetLength(50L * 1024 * 1024 + 1);

        AssertRefusedUnchanged(o, backup, "50 MB");
    }
}
