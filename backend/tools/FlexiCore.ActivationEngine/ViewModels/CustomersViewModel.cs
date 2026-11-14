using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record CustomerRow(
    Guid? Id, string Name, string LegalName, string Email, string Phone, string Status,
    bool IsRegistered, int Licenses, int ActiveNow, string LastIssued, string LatestExpiry);

/// <summary>Editable customer master records joined with historical licensee names from the immutable issue ledger.</summary>
public sealed class CustomersViewModel : PageViewModel
{
    private readonly OwnerPaths _paths;
    private readonly TimeProvider _time;
    private readonly CustomerRegistry _registry;
    private List<CustomerRow> _all = new();
    private IReadOnlyList<CustomerRow> _rows = Array.Empty<CustomerRow>();
    private string _search = "";
    private string _summary = "";
    private CustomerRow? _selectedRow;
    private string _name = "";
    private string _legalName = "";
    private string _email = "";
    private string _phone = "";

    public CustomersViewModel(OwnerPaths paths, TimeProvider time)
        : base("Customers", "Registered customer records and license history from the local issue ledger.", "\uE716")
    {
        _paths = paths;
        _time = time;
        _registry = new CustomerRegistry(paths, time);
        SaveCommand = new RelayCommand(Save);
        NewCommand = new RelayCommand(New);
        SetActiveCommand = new RelayCommand(() => SetStatus(CustomerStatus.Active), () => SelectedRow?.IsRegistered == true);
        SetInactiveCommand = new RelayCommand(() => SetStatus(CustomerStatus.Inactive), () => SelectedRow?.IsRegistered == true);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand NewCommand { get; }
    public RelayCommand SetActiveCommand { get; }
    public RelayCommand SetInactiveCommand { get; }

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
        set { if (Set(ref _search, value)) ApplyFilter(); }
    }

    public CustomerRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!Set(ref _selectedRow, value)) return;
            Name = value?.Name ?? "";
            LegalName = value?.LegalName ?? "";
            Email = value?.Email ?? "";
            Phone = value?.Phone ?? "";
            CommandRequery.Invalidate();
        }
    }

    public string Name { get => _name; set => Set(ref _name, value); }
    public string LegalName { get => _legalName; set => Set(ref _legalName, value); }
    public string Email { get => _email; set => Set(ref _email, value); }
    public string Phone { get => _phone; set => Set(ref _phone, value); }

    public override async Task OnShownAsync()
    {
        try
        {
            var ledgerTask = Task.Run(() => LedgerReader.Read(_paths.LedgerPath));
            var customersTask = Task.Run(_registry.List);
            await Task.WhenAll(ledgerTask, customersTask);
            var snapshot = await ledgerTask;
            var customers = await customersTask;
            var now = _time.GetUtcNow();
            var licenseGroups = snapshot.Entries
                .GroupBy(e => e.Licensee.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
            var customerNames = customers.GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

            var rows = customers.Select(customer =>
            {
                // Historical licenses have no customer ID. Attach by exact name only when that name is unique.
                var history = customerNames[customer.Name].Length == 1 && licenseGroups.TryGetValue(customer.Name, out var found)
                    ? found : Array.Empty<LedgerEntry>();
                return ToRow(customer, history, now);
            }).ToList();

            foreach (var (name, entries) in licenseGroups)
            {
                if (customerNames.ContainsKey(name)) continue;
                rows.Add(ToLegacyRow(name, entries, now));
            }

            _all = rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
            ApplyFilter();
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

    private void Save()
    {
        try
        {
            var input = new CustomerInput(Name, LegalName, Email, Phone);
            var customer = SelectedRow?.Id is { } id ? _registry.Update(id, input) : _registry.Register(input);
            _ = ReloadAndSelectAsync(customer.Id, "Customer saved.");
        }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private void SetStatus(CustomerStatus status)
    {
        if (SelectedRow?.Id is not { } id) return;
        try
        {
            _registry.SetStatus(id, status);
            _ = ReloadAndSelectAsync(id, "Customer status set to " + status + ".");
        }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private async Task ReloadAndSelectAsync(Guid id, string message)
    {
        await OnShownAsync();
        SelectedRow = _all.SingleOrDefault(r => r.Id == id);
        SetStatus(StatusKind.Success, message);
    }

    private void New()
    {
        SelectedRow = null;
        Name = LegalName = Email = Phone = "";
        ClearStatus();
    }

    private void ApplyFilter()
    {
        var q = _search.Trim();
        Rows = q.Length == 0 ? _all : _all.Where(r => r.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        Summary = _rows.Count + " of " + _all.Count + " customers";
    }

    private static CustomerRow ToRow(CustomerRecord customer, IReadOnlyCollection<LedgerEntry> history, DateTimeOffset now)
    {
        var anyPerpetual = history.Any(e => e.ExpiresAt is null);
        var expiry = history.Count == 0 ? "—" : anyPerpetual ? "Never" : Fmt.Time(history.Max(e => e.ExpiresAt!.Value));
        return new CustomerRow(customer.Id, customer.Name, customer.LegalName ?? "", customer.Email ?? "", customer.Phone ?? "",
            customer.Status.ToString(), true, history.Count,
            history.Count(e => e.StatusAt(now) is TermStatus.Active or TermStatus.ActiveInGrace),
            history.Count == 0 ? "—" : Fmt.Time(history.Max(e => e.RecordedAt)), expiry);
    }

    private static CustomerRow ToLegacyRow(string name, IReadOnlyCollection<LedgerEntry> history, DateTimeOffset now)
    {
        var anyPerpetual = history.Any(e => e.ExpiresAt is null);
        var expiry = anyPerpetual ? "Never" : Fmt.Time(history.Max(e => e.ExpiresAt!.Value));
        return new CustomerRow(null, name, "", "", "", "Legacy", false, history.Count,
            history.Count(e => e.StatusAt(now) is TermStatus.Active or TermStatus.ActiveInGrace),
            Fmt.Time(history.Max(e => e.RecordedAt)), expiry);
    }
}
