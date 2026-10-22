using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record StatCard(string Label, string Value, string Caption);
public sealed record ActivityRow(string When, string Licensee, string LicenseId, string KeyId);

public sealed class DashboardViewModel : PageViewModel
{
    private readonly OwnerPaths _paths;
    private readonly KeyService _keys;
    private readonly TimeProvider _time;

    private IReadOnlyList<StatCard> _stats;
    private IReadOnlyList<ActivityRow> _recent = Array.Empty<ActivityRow>();
    private IReadOnlyList<string> _warnings = Array.Empty<string>();

    public DashboardViewModel(OwnerPaths paths, KeyService keys, TimeProvider time)
        : base("Dashboard", "Overview of licenses issued from this machine.", "\uE80F")
    {
        _paths = paths;
        _keys = keys;
        _time = time;
        const string dash = "\u2014";
        _stats = new[]
        {
            new StatCard("Licenses issued", dash, "Loading"),
            new StatCard("Active", dash, "Loading"),
            new StatCard("Expired", dash, "Loading"),
            new StatCard("Signing key", dash, "Loading"),
        };
    }

    public IReadOnlyList<StatCard> Stats
    {
        get => _stats;
        private set => Set(ref _stats, value);
    }

    public IReadOnlyList<ActivityRow> Recent
    {
        get => _recent;
        private set
        {
            if (Set(ref _recent, value))
            {
                OnPropertyChanged(nameof(HasRecent));
                OnPropertyChanged(nameof(HasNoRecent));
            }
        }
    }

    public IReadOnlyList<string> Warnings
    {
        get => _warnings;
        private set
        {
            if (Set(ref _warnings, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(HasNoWarnings));
            }
        }
    }

    public bool HasRecent => _recent.Count > 0;
    public bool HasNoRecent => _recent.Count == 0;
    public bool HasWarnings => _warnings.Count > 0;
    public bool HasNoWarnings => _warnings.Count == 0;

    public override async Task OnShownAsync()
    {
        try
        {
            var data = await Task.Run(() => Load());
            Stats = data.Stats;
            Recent = data.Recent;
            Warnings = data.Warnings;
            ClearStatus();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private sealed record DashboardData(
        IReadOnlyList<StatCard> Stats, IReadOnlyList<ActivityRow> Recent, IReadOnlyList<string> Warnings);

    private DashboardData Load()
    {
        var now = _time.GetUtcNow();
        var snapshot = LedgerReader.Read(_paths.LedgerPath);
        var keys = _keys.List(out var keyProblem);

        var warnings = new List<string>();
        if (keyProblem is not null) warnings.Add(keyProblem);
        if (keys.Count == 0 && keyProblem is null)
            warnings.Add("No signing key found. Create one on the Signing Keys page before issuing licenses.");
        foreach (var key in keys.Where(k => k.Problem is not null))
            warnings.Add("Key '" + key.KeyId + "': " + key.Problem);
        if (snapshot.Problem is not null) warnings.Add(snapshot.Problem);
        if (snapshot.UnreadableLines > 0)
            warnings.Add(snapshot.UnreadableLines + " ledger line(s) could not be read and are not counted.");
        if (_paths.InsideGitWorkingTree)
            warnings.Add("The owner data folder is inside a git working tree. Move it: it holds a private key and customer names.");
        if (_paths.LooksCloudSynced)
            warnings.Add("The owner data folder looks cloud-synced. A private key file should not be synced.");

        var entries = snapshot.Entries;
        var active = entries.Count(e => e.StatusAt(now) is TermStatus.Active or TermStatus.ActiveInGrace);
        var expired = entries.Count(e => e.StatusAt(now) == TermStatus.Expired);
        var pending = entries.Count(e => e.StatusAt(now) == TermStatus.NotYetValid);
        var last = entries.OrderByDescending(e => e.RecordedAt).FirstOrDefault();

        var keyValue = last?.KeyId ?? (keys.Count == 1 ? keys[0].KeyId : "\u2014");
        var keyCaption = keys.Count == 0
            ? "No key in the key store"
            : keys.Count + (keys.Count == 1 ? " key in the key store" : " keys in the key store");

        var stats = new[]
        {
            new StatCard("Licenses issued", entries.Count.ToString(),
                last is null ? "Nothing issued yet" : "Latest: " + Fmt.Time(last.RecordedAt)),
            new StatCard("Active", active.ToString(),
                pending > 0 ? "By recorded dates; " + pending + " not yet valid" : "By recorded dates"),
            new StatCard("Expired", expired.ToString(), "By recorded dates"),
            new StatCard("Signing key", keyValue, keyCaption),
        };

        var recent = entries
            .OrderByDescending(e => e.RecordedAt)
            .Take(6)
            .Select(e => new ActivityRow(Fmt.Time(e.RecordedAt), e.Licensee, e.LicenseId, e.KeyId))
            .ToList();

        return new DashboardData(stats, recent, warnings);
    }
}
