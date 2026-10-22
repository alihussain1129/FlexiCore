using System.Collections.ObjectModel;
using System.Globalization;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record ManagedManagedKeyRow(
    string KeyId, string State, string Created, string Licenses, string LastUsed,
    string NotExpired, string InErp, string RemoveFromErp, string Advice);

public sealed record KeyEventRow(string When, string KeyId, string Action, string Detail);

public sealed class KeyManagementViewModel : PageViewModel
{
    private readonly KeyManagementService _service;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private KeyOverview? _overview;
    private ManagedManagedKeyRow? _selectedRow;
    private string _suggestedKeyId = "";
    private string _erpFileText = "";

    public KeyManagementViewModel(KeyManagementService service, IDialogService dialogs, TimeProvider time)
        : base("Key Management", "Which key signs your licenses, what each key has signed, and when to rotate.", "\uE9D9")
    {
        _service = service;
        _dialogs = dialogs;
        _time = time;

        RefreshCommand = new RelayCommand(() => _ = ReloadAsync(true));
        RetireCommand = new RelayCommand(Retire);
        ReactivateCommand = new RelayCommand(Reactivate);
        MarkInErpCommand = new RelayCommand(MarkInErp);
        ChooseErpFileCommand = new RelayCommand(ChooseErpFile);
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand RetireCommand { get; }
    public RelayCommand ReactivateCommand { get; }
    public RelayCommand MarkInErpCommand { get; }
    public RelayCommand ChooseErpFileCommand { get; }

    public ObservableCollection<ManagedManagedKeyRow> Rows { get; } = new();
    public ObservableCollection<KeyEventRow> Events { get; } = new();
    public bool HasNoRows => Rows.Count == 0;

    public ManagedManagedKeyRow? SelectedRow { get => _selectedRow; set => Set(ref _selectedRow, value); }
    public string SuggestedKeyId { get => _suggestedKeyId; private set => Set(ref _suggestedKeyId, value); }
    public string ErpFileText { get => _erpFileText; private set => Set(ref _erpFileText, value); }

    public override Task OnShownAsync() => ReloadAsync(false);

    private async Task ReloadAsync(bool announce)
    {
        try
        {
            var overview = await Task.Run(() => _service.Load());
            var events = await Task.Run(() => _service.ReadEvents(30));
            var suggested = await Task.Run(() => _service.NextKeyId());
            Apply(overview, events, suggested);
            if (announce)
                SetStatus(StatusKind.Info,
                    overview.ErpKeyFile is null
                        ? "The ERP key file was not found. Choose it so keys can be checked."
                        : overview.ErpFileProblem ?? "Keys were checked against the ERP key file.");
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    private void Apply(KeyOverview overview, IReadOnlyList<KeyEvent> events, string suggested)
    {
        _overview = overview;
        var selectedId = _selectedRow?.KeyId;
        var now = _time.GetUtcNow();

        Rows.Clear();
        foreach (var key in overview.Keys.OrderByDescending(k => k.CreatedUtc)) Rows.Add(ToRow(key, now));
        SelectedRow = selectedId is null ? null : Rows.FirstOrDefault(r => r.KeyId == selectedId);

        Events.Clear();
        foreach (var e in events) Events.Add(new KeyEventRow(Fmt.Time(e.When), e.KeyId, e.Action, e.Detail));

        SuggestedKeyId = suggested;
        ErpFileText = overview.ErpKeyFile is null
            ? "Not found. Choose the ERP key file."
            : overview.ErpFileProblem is not null
                ? overview.ErpKeyFile + " (" + overview.ErpFileProblem + ")"
                : overview.ErpKeyFile;
        OnPropertyChanged(nameof(HasNoRows));
        if (overview.Problem is not null) SetStatus(StatusKind.Error, overview.Problem);
    }

    private static ManagedManagedKeyRow ToRow(ManagedKey k, DateTimeOffset now) => new(
        k.KeyId,
        k.State,
        Fmt.Time(k.CreatedUtc),
        k.Licenses.ToString(CultureInfo.InvariantCulture),
        k.LastUsed is { } used ? Fmt.Time(used) : "Never",
        k.Outstanding.ToString(CultureInfo.InvariantCulture),
        k.ErpText,
        k.Retired ? (k.SafeToRemoveFromErp ? "Yes" : "Not yet") : "No, still active",
        KeyManagementService.RotationAdvice(k, now) ?? "");

    private void Retire()
    {
        if (SelectedRow is not { } row) { SetStatus(StatusKind.Info, "Select a key in the table first."); return; }
        var message = "Retire key " + row.KeyId + "?\n\n"
            + "The Engine will no longer sign new licenses with it. Licenses it already signed keep working, "
            + "because the ERP still trusts its public key.";
        if (!_dialogs.Confirm("Retire key", message)) return;
        Run(() => _service.Retire(row.KeyId), "Key " + row.KeyId + " is retired.");
    }

    private void Reactivate()
    {
        if (SelectedRow is not { } row) { SetStatus(StatusKind.Info, "Select a key in the table first."); return; }
        Run(() => _service.Reactivate(row.KeyId), "Key " + row.KeyId + " is active again.");
    }

    private void MarkInErp()
    {
        if (SelectedRow is not { } row) { SetStatus(StatusKind.Info, "Select a key in the table first."); return; }
        var message = "Confirm that the public key of " + row.KeyId + " is built into the ERP that your customers run?\n\n"
            + "If it is not, licenses signed with it will be refused.";
        if (!_dialogs.Confirm("Built into the ERP", message)) return;
        Run(() => _service.MarkBuiltIntoErp(row.KeyId), "Key " + row.KeyId + " is marked as built into the ERP.");
    }

    private void ChooseErpFile()
    {
        var path = _dialogs.PickOpenPath(
            "Choose the ERP key file (production-public-keys.json)", "Key file (*.json)|*.json|All files (*.*)|*.*");
        if (path is null) return;
        Run(() => _service.SetErpKeyFile(path), "The ERP key file was set. Keys are checked against it now.");
    }

    private void Run(Action action, string success)
    {
        ClearStatus();
        try { action(); }
        catch (Exception ex) when (ex is LicenseToolException or IOException or UnauthorizedAccessException)
        {
            SetStatus(StatusKind.Error, Describe(ex));
            return;
        }
        _ = ReloadAsync(false);
        SetStatus(StatusKind.Success, success);
    }

    public string? GetSelectedPublicKeySnippet()
    {
        if (_selectedRow is null || _overview is null) return null;
        var key = _overview.Keys.FirstOrDefault(k => k.KeyId == _selectedRow.KeyId);
        return key?.PublicKeyBase64 is null ? null : KeyManagementService.PublicKeySnippet(key.KeyId, key.PublicKeyBase64);
    }

    /// <summary>Returns true when the passphrases were used (the view then clears the boxes).</summary>
    public async Task<bool> CreateKeyAsync(string passphrase, string confirmation)
    {
        ClearStatus();
        IsBusy = true;
        try
        {
            var id = await Task.Run(() => _service.CreateRotatedKey(passphrase, confirmation));
            SetStatus(StatusKind.Success,
                "Key " + id + " was created. It is not used for signing yet. Add its public key to the ERP key file "
                + "(use Copy public key entry), rebuild the ERP, then press Check ERP key file.");
            await ReloadAsync(false);
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }
        return true;
    }
}