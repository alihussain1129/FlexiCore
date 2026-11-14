using System.Text;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class LedgerSnapshotCaptureTests
{
    private static string[] IssueTwo(Owner o)
    {
        var key = o.CreateKey();
        foreach (var id in new[] { "FC-S-1", "FC-S-2" })
            o.Issue.Issue(o.Input(key, id));
        return File.ReadAllLines(o.Paths.LedgerPath);
    }

    [Fact]
    public void A_capture_is_unaffected_by_later_changes_to_the_file()
    {
        using var o = new Owner();
        var good = IssueTwo(o);
        var path = o.Out("capture.jsonl");
        File.WriteAllLines(path, good);
        var original = File.ReadAllBytes(path);

        var capture = LedgerReader.CaptureBackup(path);
        File.WriteAllText(path, "overwritten after the capture");

        Assert.Null(capture.Problem);
        Assert.Equal(original, capture.Bytes);
        Assert.Equal(good, capture.Lines.Select(l => l.Raw).ToArray());
        Assert.Equal(new[] { "FC-S-1", "FC-S-2" }, capture.Entries.Select(e => e.LicenseId).ToArray());
    }

    [Fact]
    public void Each_raw_line_is_paired_with_its_own_record_and_blank_lines_are_skipped()
    {
        using var o = new Owner();
        var good = IssueTwo(o);

        var capture = LedgerReader.Parse(Encoding.UTF8.GetBytes(good[0] + "\n\n   \n" + good[1] + "\r\n"));

        Assert.Equal(2, capture.Lines.Count);
        Assert.Equal(good[0], capture.Lines[0].Raw);
        Assert.Equal("FC-S-1", capture.Lines[0].Entry!.LicenseId);
        Assert.Equal(good[1], capture.Lines[1].Raw);
        Assert.Equal("FC-S-2", capture.Lines[1].Entry!.LicenseId);
    }

    [Fact]
    public void A_BOM_is_ignored_but_invalid_UTF8_is_refused()
    {
        using var o = new Owner();
        var good = IssueTwo(o);

        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(good[0])).ToArray();
        var ok = LedgerReader.Parse(withBom);
        Assert.Null(ok.Problem);
        Assert.Single(ok.Entries);

        var bad = LedgerReader.Parse(Encoding.UTF8.GetBytes(good[0] + "\n").Concat(new byte[] { 0xC3, 0x28 }).ToArray());
        Assert.Contains("UTF-8", bad.Problem);
        Assert.Empty(bad.Lines);
    }

    [Fact]
    public void A_missing_backup_is_a_problem_but_a_missing_live_ledger_is_empty()
    {
        using var o = new Owner();

        Assert.NotNull(LedgerReader.CaptureBackup(o.Out("does-not-exist.jsonl")).Problem);

        using var ledgerLock = LedgerFileLock.Acquire(o.Paths.LedgerPath);
        var live = LedgerReader.CaptureLive(o.Paths.LedgerPath, ledgerLock);
        Assert.Null(live.Problem);
        Assert.Empty(live.Lines);
    }

    [Fact]
    public void A_lock_for_a_different_ledger_is_rejected()
    {
        using var o = new Owner();
        using var ledgerLock = LedgerFileLock.Acquire(o.Paths.LedgerPath);

        Assert.Throws<InvalidOperationException>(() =>
            LedgerReader.CaptureLive(o.Out("other-ledger.jsonl"), ledgerLock));
    }
}
