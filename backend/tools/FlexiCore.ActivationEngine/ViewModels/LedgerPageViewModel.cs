using FlexiCore.ActivationEngine.Services;

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
    private readonly LedgerMode _mode;
    private List<LedgerRow> _all = new();
    private IReadOnlyList<LedgerRow> _rows = Array.Empty<LedgerRow>();
    private string _search = "";
    private string _summary = "";

    public LedgerPageViewModel(OwnerPaths paths, TimeProvider time, LedgerMode mode)
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
