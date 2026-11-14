using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public enum LedgerMode { Licenses, History }

public sealed record LedgerRow(
    string LicenseId,
    string Licensee,
    string KeyId,
    string Status,
    string NotBefore,
    string Expires,
    string Grace,
    string Installations,
    string Recorded,
    string FileSha256);

/// <summary>Shows the existing issue ledger. Used for both the Licenses and the Issuance History pages.</summary>
public sealed class LedgerPageViewModel : PageViewModel
{
    private readonly OwnerPaths _paths;
    private readonly TimeProvider _time;
    private readonly IDialogService? _dialogs;
    private readonly LedgerMode _mode;
    private List<LedgerRow> _all = new();
    private IReadOnlyList<LedgerRow> _rows = Array.Empty<LedgerRow>();
    private string _search = "";
    private string _summary = "";

    public LedgerPageViewModel(OwnerPaths paths, TimeProvider time, LedgerMode mode, IDialogService? dialogs = null)
        : base(
            mode == LedgerMode.Licenses ? "Licenses" : "Issuance History",
            mode == LedgerMode.Licenses
                ? "Licenses recorded in the issue ledger on this machine."
                : "Audit trail of every license issued, newest first.",
            mode == LedgerMode.Licenses ? "\uE8A5" : "\uE81C")
    {
        _paths = paths;
        _time = time;
        _mode = mode;
        _dialogs = dialogs;
    }

    public string Note =>
        "Status is computed from the dates in the ledger. It is not a live check: licenses work offline, "
        + "so a recorded license keeps working on the customer's machine until its dates end. "
        + "The ledger cannot revoke a license remotely.";

    public IReadOnlyList<LedgerRow> Rows
    {
        get => _rows;
        private set
        {
            if (Set(ref _rows, value)) OnPropertyChanged(nameof(HasNoRows));
        }
    }

    public bool HasNoRows => _all.Count == 0;
    public string Summary { get => _summary; private set => Set(ref _summary, value); }

    public string SearchText
    {
        get => _search;
        set
        {
            if (Set(ref _search, value)) ApplyFilter();
        }
    }

    public async Task ExportBackupAsync()
    {
        if (_dialogs is null)
        {
            SetStatus(StatusKind.Error, "Ledger export is unavailable in this owner workflow.");
            return;
        }
        if (!File.Exists(_paths.LedgerPath))
        {
            SetStatus(StatusKind.Info, "There is no issue ledger to export yet.");
            return;
        }

        try
        {
            var destination = await _dialogs.PickSavePathAsync(
                "Export issue ledger",
                "flexicore-issue-ledger.jsonl",
                "Issue ledger backup (*.jsonl)|*.jsonl|All files (*.*)|*.*");

            if (destination is null)
            {
                SetStatus(StatusKind.Info, "Issue ledger export cancelled.");
                return;
            }

            // Waiting for the ledger lock can take a while: never do it on the UI thread.
            var ledgerPath = _paths.LedgerPath;
            await Task.Run(() =>
            {
                using (LedgerFileLock.Acquire(ledgerPath))
                    SafeFile.CopyNew(ledgerPath, destination);
            });

            await _dialogs.CompleteSaveAsync(destination);
            SetStatus(StatusKind.Success, "Issue ledger backup exported.");
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    public async Task RestoreBackupAsync()
    {
        if (_dialogs is null)
        {
            SetStatus(StatusKind.Error, "Ledger restore is unavailable in this owner workflow.");
            return;
        }

        try
        {
            var path = await _dialogs.PickOpenPathAsync(
                "Choose an issue ledger backup",
                "Issue ledger backup (*.jsonl)|*.jsonl|All files (*.*)|*.*");
            if (path is null)
            {
                SetStatus(StatusKind.Info, "Issue ledger restore cancelled.");
                return;
            }

            var service = new LedgerRestoreService(_paths, _time);
            var preview = await Task.Run(() => service.Preview(path));
            if (preview.Added == 0)
            {
                SetStatus(StatusKind.Info,
                    "Nothing to restore. All " + preview.AlreadyPresent + " record(s) in that backup are already in the ledger.");
                return;
            }

            var message = "Add " + preview.Added + " missing record(s) from this backup to the issue ledger? "
                + preview.AlreadyPresent + " record(s) are already there. Existing records are never changed or removed, "
                + "and a copy of the current ledger is saved first.";
            if (!await _dialogs.ConfirmAsync("Restore issue ledger", message))
            {
                SetStatus(StatusKind.Info, "Issue ledger restore cancelled.");
                return;
            }

            var result = await Task.Run(() => service.Restore(path));
            await OnShownAsync();
            SetStatus(StatusKind.Success,
                "Restored " + result.Added + " record(s). "
                + (result.SafetyCopyPath is null
                    ? "There was no previous ledger to copy."
                    : "The previous ledger was copied to " + result.SafetyCopyPath + "."));
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    public override async Task OnShownAsync()
    {
        try
        {
            var snapshot = await Task.Run(() => LedgerReader.Read(_paths.LedgerPath));
            var now = _time.GetUtcNow();

            var ordered = _mode == LedgerMode.History
                ? snapshot.Entries.OrderByDescending(e => e.RecordedAt)
                : snapshot.Entries.OrderBy(e => e.Licensee, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.NotBefore);

            _all = ordered.Select(e => new LedgerRow(
                e.LicenseId,
                e.Licensee,
                e.KeyId,
                Fmt.Status(e.StatusAt(now)),
                Fmt.Time(e.NotBefore),
                Fmt.Expiry(e.ExpiresAt),
                e.GraceDays + " d",
                e.MaxInstallations.ToString(),
                Fmt.Time(e.RecordedAt),
                Fmt.Short(e.FileSha256, 16))).ToList();

            ApplyFilter();
            OnPropertyChanged(nameof(HasNoRows));

            if (snapshot.Problem is not null) SetStatus(StatusKind.Error, snapshot.Problem);
            else if (snapshot.UnreadableLines > 0)
                SetStatus(StatusKind.Info, snapshot.UnreadableLines + " ledger line(s) could not be read and are not shown.");
            else ClearStatus();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private void ApplyFilter()
    {
        var q = _search.Trim();
        Rows = q.Length == 0
            ? _all
            : _all.Where(r => r.LicenseId.Contains(q, StringComparison.OrdinalIgnoreCase)
                              || r.Licensee.Contains(q, StringComparison.OrdinalIgnoreCase)
                              || r.KeyId.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Summary = _rows.Count + " of " + _all.Count + " records";
    }
}
