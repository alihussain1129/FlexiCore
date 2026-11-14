using System.Collections.ObjectModel;
using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record KeyRow(
    string KeyId, string Fingerprint, string FingerprintShort, string Created, string Status, string PrivateKeyPath,
    bool IsNonExportable);

public sealed class KeysViewModel : PageViewModel
{
    private readonly OwnerPaths _paths;
    private readonly KeyService _keys;
    private readonly IDialogService _dialogs;
    private KeyRow? _selected;
    private string _newKeyId = "";

    public KeysViewModel(
        OwnerPaths paths,
        KeyService keys,
        IDialogService dialogs,
        KeyManagementService? keyManager = null)
        : base("Signing Keys", "Create keys, check fingerprints and export public keys.", "\uE72E")
    {
        _paths = paths;
        _keys = keys;
        _dialogs = dialogs;
        KeyManager = keyManager;

        RefreshCommand = new AsyncRelayCommand(async () =>
        {
            await ReloadAsync();
            ClearStatus();
        });
        ExportSelectedCommand = new AsyncRelayCommand(
            () => ExportAsync(new[] { _selected!.KeyId }, _selected.KeyId + ".public.json"),
            () => _selected is not null);
        ExportAllCommand = new AsyncRelayCommand(
            () => ExportAsync(Rows.Select(r => r.KeyId).ToList(), "flexicore-public-keys.json"),
            () => Rows.Count > 0);
        OpenFolderCommand = new RelayCommand(() =>
        {
            if (Directory.Exists(KeyFolder)) _dialogs.OpenFolder(KeyFolder);
            else SetStatus(StatusKind.Info, "The key folder does not exist yet. It is created together with the first key.");
        });
    }

    public ObservableCollection<KeyRow> Rows { get; } = new();
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ExportSelectedCommand { get; }
    public AsyncRelayCommand ExportAllCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public string KeyFolder => _paths.KeysDirectory;
    public bool SupportsPlatformKeys => _keys.SupportsPlatformKeys;
    public bool HasNoRows => Rows.Count == 0;

    public string NewKeyId { get => _newKeyId; set => Set(ref _newKeyId, value); }

    public KeyManagementService? KeyManager { get; set; }
    private string _autoId = "";

    private async Task ApplySuggestionAsync()
    {
        var km = KeyManager;
        if (km is null) return;
        try
        {
            var next = await Task.Run(() => km.NextKeyId());
            if (NewKeyId.Length == 0 || NewKeyId == _autoId) NewKeyId = next;
            _autoId = next;
        }
        catch (Exception)
        {
            // The suggestion is a convenience. Typing an id always works.
        }
    }

    public KeyRow? SelectedRow
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value)) OnPropertyChanged(nameof(SelectedFingerprint));
        }
    }

    public string SelectedFingerprint =>
        _selected is null ? "Select a key to see its full fingerprint." : _selected.Fingerprint;

    public override async Task OnShownAsync()
    {
        try { await ReloadAsync(); }
        catch (Exception ex) { SetStatus(StatusKind.Error, Describe(ex)); }
    }

    private async Task ReloadAsync()
    {
        var data = await Task.Run(() =>
        {
            var keys = _keys.List(out var problem);
            return (keys, problem);
        });

        Rows.Clear();
        foreach (var k in data.keys)
        {
            Rows.Add(new KeyRow(
                k.KeyId,
                k.Fingerprint ?? "unavailable",
                k.Fingerprint is null ? "unavailable" : Fmt.Short(k.Fingerprint, 32),
                Fmt.Time(k.CreatedUtc),
                k.Problem ?? "Ready",
                k.PrivateKeyPath,
                k.IsNonExportable));
        }
        SelectedRow = null;
        OnPropertyChanged(nameof(HasNoRows)); await ApplySuggestionAsync();
        if (data.problem is not null) SetStatus(StatusKind.Error, data.problem);
    }

    /// <summary>Returns true when the key was created (the view then clears both passphrase boxes).</summary>
    public async Task<bool> CreateAsync(string passphrase, string confirmation)
    {
        ClearStatus();
        var id = NewKeyId.Trim();
        if (id.Length == 0)
        {
            SetStatus(StatusKind.Error, "Enter a key id, for example fc-2026-a.");
            return false;
        }

        IsBusy = true;
        try
        {
            var km = KeyManager;
            var isTest = id.StartsWith("dev-", StringComparison.Ordinal) || id.StartsWith("test", StringComparison.Ordinal);
            if (km is not null) await Task.Run(() => km.EnsureCanCreate(id));
            var created = await Task.Run(() => _keys.Create(id, passphrase, confirmation));
            string? logProblem = null;
            if (km is not null)
            {
                try { km.RecordManualKey(id, isTest); }
                catch (Exception ex) { logProblem = " The key was created, but the key history could not be updated: " + ex.Message; }
            }
            NewKeyId = "";
            await ReloadAsync();
            SetStatus(StatusKind.Success,
                "Key '" + id + "' created. Fingerprint: " + created.Fingerprint
                + ". Now make two encrypted backups of " + id + ".p8 on separate media and store the passphrase safely. "
                + "Without the file and the passphrase, no license can be issued with this key."
                + (isTest || km is null ? "" : " It will not sign licenses until its public key is in the ERP key file and the ERP is rebuilt.")
                + (logProblem ?? ""));
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> CreatePlatformKeyAsync()
    {
        ClearStatus();
        var id = NewKeyId.Trim();
        if (id.Length == 0)
        {
            SetStatus(StatusKind.Error, "Enter a key id before creating the device-protected key.");
            return false;
        }

        IsBusy = true;
        try
        {
            var manager = KeyManager;
            var isTest = id.StartsWith("dev-", StringComparison.Ordinal) || id.StartsWith("test", StringComparison.Ordinal);
            if (manager is not null) await Task.Run(() => manager.EnsureCanCreate(id));
            var created = await Task.Run(() => _keys.CreatePlatformKey(id));
            string? logProblem = null;
            if (manager is not null)
            {
                try { manager.RecordManualKey(id, isTest); }
                catch (Exception ex) { logProblem = " Key history could not be updated: " + ex.Message; }
            }
            NewKeyId = "";
            await ReloadAsync();
            SetStatus(StatusKind.Success,
                "Non-exportable platform key '" + id + "' created. Android Keystore hardware backing was not verified. "
                + "This private key cannot be backed up or moved; losing or uninstalling this device permanently removes it."
                + (isTest || manager is null ? "" : " It cannot sign production licenses until its public key is in the ERP key file.")
                + (logProblem ?? ""));
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ImportEncryptedKeyAsync(string path, string passphrase)
    {
        ClearStatus();
        IsBusy = true;
        try
        {
            var created = await Task.Run(() => _keys.ImportEncryptedKey(path, passphrase));
            await ReloadAsync();
            SetStatus(StatusKind.Success,
                "Encrypted signing key '" + Path.GetFileNameWithoutExtension(created.PrivateKeyPath)
                + "' imported and validated. Its existing PBES2-encrypted PKCS#8 bytes were preserved.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportEncryptedPrivateKeyAsync()
    {
        if (_selected is null)
        {
            SetStatus(StatusKind.Info, "Select an exportable encrypted key first.");
            return;
        }
        if (_selected.IsNonExportable)
        {
            SetStatus(StatusKind.Error, "This platform key is non-exportable and cannot be backed up as a private-key file.");
            return;
        }

        var path = await _dialogs.PickSavePathAsync(
            "Back up encrypted signing key", _selected.KeyId + ".p8",
            "Encrypted signing key (*.p8)|*.p8|All files (*.*)|*.*");
        if (path is null)
        {
            SetStatus(StatusKind.Info, "Encrypted private-key backup cancelled.");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() => _keys.ExportEncryptedPrivateKey(_selected.KeyId, path));
            await _dialogs.CompleteSaveAsync(path);
            SetStatus(StatusKind.Success,
                "Encrypted private-key backup saved. The .p8 file remains passphrase-protected; store the passphrase separately.");
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync(IReadOnlyList<string> ids, string defaultName)
    {
        var path = await _dialogs.PickSavePathAsync(
            "Export public key set", defaultName, "Public key set (*.json)|*.json|All files (*.*)|*.*");
        if (path is null)
        {
            SetStatus(StatusKind.Info, "Export cancelled.");
            return;
        }

        ClearStatus();
        IsBusy = true;
        try
        {
            var exported = await Task.Run(() => _keys.ExportKeySet(ids, path));
            await _dialogs.CompleteSaveAsync(path);
            var list = string.Join(", ", exported.Select(e => e.KeyId + " (" + Fmt.Short(e.Fingerprint, 16) + ")"));
            SetStatus(StatusKind.Success,
                "Exported " + exported.Count + " public key(s) to " + path + ": " + list
                + ". The file holds public keys only and was checked by the production key loader.");
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
