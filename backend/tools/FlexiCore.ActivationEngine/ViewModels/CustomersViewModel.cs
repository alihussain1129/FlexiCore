using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record CustomerRow(string Name, int Licenses, int ActiveNow, string LastIssued, string LatestExpiry);

/// <summary>Customers are derived from the issue ledger. There is no second customer database.</summary>
public sealed class CustomersViewModel : PageViewModel
{
    private readonly OwnerPaths _paths;
    private readonly TimeProvider _time;
    private List<CustomerRow> _all = new();
    private IReadOnlyList<CustomerRow> _rows = Array.Empty<CustomerRow>();
    private string _search = "";
    private string _summary = "";

    public CustomersViewModel(OwnerPaths paths, TimeProvider time)
        : base("Customers", "Organizations you have issued licenses to, from the issue ledger.", "\uE716")
    {
        _paths = paths;
        _time = time;
    }

    public IReadOnlyList<CustomerRow> Rows
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

            _all = snapshot.Entries
                .GroupBy(e => e.Licensee.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var anyPerpetual = g.Any(e => e.ExpiresAt is null);
                    var latestExpiry = anyPerpetual ? "Never" : Fmt.Time(g.Max(e => e.ExpiresAt!.Value));
                    var activeNow = g.Count(e => e.StatusAt(now) is TermStatus.Active or TermStatus.ActiveInGrace);
                    return new CustomerRow(
                        g.First().Licensee.Trim(), g.Count(), activeNow, Fmt.Time(g.Max(e => e.RecordedAt)), latestExpiry);
                })
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyFilter();
            OnPropertyChanged(nameof(HasNoRows));

            if (snapshot.Problem is not null) SetStatus(StatusKind.Error, snapshot.Problem);
            else if (snapshot.UnreadableLines > 0)
                SetStatus(StatusKind.Info, snapshot.UnreadableLines + " ledger line(s) could not be read and are not counted.");
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
            : _all.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Summary = _rows.Count + " of " + _all.Count + " customers";
    }
}
