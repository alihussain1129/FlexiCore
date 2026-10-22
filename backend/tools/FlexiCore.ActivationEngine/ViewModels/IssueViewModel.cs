using System.Collections.ObjectModel;
using System.Globalization;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed class IssueViewModel : PageViewModel
{
    private readonly KeyService _keys;
    private readonly IssueService _issue;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;

    private string? _selectedKeyId;
    private string _licensee = "";
    private string _licenseId;
    private DateTime? _notBeforeDate;
    private DateTime? _expiresDate;
    private bool _perpetual;
    private string _graceDaysText = "0";
    private string _maxInstallationsText = "1";
    private string _requestText = "";
    private bool _writeActivationCode = true;
    private IReadOnlyList<DetailRow> _result = Array.Empty<DetailRow>();
    private string? _lastLicensePath;

    public IssueViewModel(KeyService keys, IssueService issue, IDialogService dialogs, TimeProvider time, OwnerPaths? paths = null)
        : base("Issue License", "Sign a license, verify it, save it and record it in the ledger.", "\uE8FB")
    {
        _keys = keys;
        _issue = issue;
        _dialogs = dialogs;
        _time = time; _ownerPaths = paths;

        var now = time.GetUtcNow();
        var today = now.UtcDateTime.Date;
        _licenseId = LicenseIdGenerator.Next(now);
        _notBeforeDate = today;
        _expiresDate = today.AddYears(1);

        GenerateIdCommand = new RelayCommand(() => LicenseId = LicenseIdGenerator.Next(_time.GetUtcNow()));
        OpenFolderCommand = new RelayCommand(
            () =>
            {
                if (_lastLicensePath is null) return;
                var folder = Path.GetDirectoryName(_lastLicensePath);
                if (folder is not null) _dialogs.OpenFolder(folder);
            },
            () => _lastLicensePath is not null);
        NewCustomerCommand = new RelayCommand(() =>
        {
            Licensee = "";
            RequestText = "";
            LastActivationText = "";
            _lastLicensePath = null;
            Result = Array.Empty<DetailRow>();
            NotBeforeDate = _time.GetUtcNow().UtcDateTime.Date;
            ClearStatus();
        });
        RevealCommand = new RelayCommand(
            () =>
            {
                if (_lastLicensePath is not null) _dialogs.Reveal(_lastLicensePath);
            },
            () => _lastLicensePath is not null);
    }

    public ObservableCollection<string> KeyIds { get; } = new();
    public RelayCommand GenerateIdCommand { get; }
    public RelayCommand RevealCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand NewCustomerCommand { get; }
    public bool HasNoKeys => KeyIds.Count == 0;

    public string? SelectedKeyId { get => _selectedKeyId; set => Set(ref _selectedKeyId, value); }
    public string Licensee { get => _licensee; set => Set(ref _licensee, value); }
    public string LicenseId { get => _licenseId; set => Set(ref _licenseId, value); }
    public string GraceDaysText { get => _graceDaysText; set => Set(ref _graceDaysText, value); }
    public string MaxInstallationsText { get => _maxInstallationsText; set => Set(ref _maxInstallationsText, value); }
    public bool WriteActivationCode { get => _writeActivationCode; set => Set(ref _writeActivationCode, value); }

    public DateTime? NotBeforeDate
    {
        get => _notBeforeDate;
        set
        {
            if (Set(ref _notBeforeDate, value)) { OnPropertyChanged(nameof(NotBeforePreview)); ApplyTerm(); }
        }
    }

    public DateTime? ExpiresDate
    {
        get => _expiresDate;
        set
        {
            if (Set(ref _expiresDate, value)) OnPropertyChanged(nameof(ExpiryPreview));
        }
    }

    public bool IsPerpetual
    {
        get => _perpetual;
        set
        {
            if (!Set(ref _perpetual, value)) return;
            OnPropertyChanged(nameof(ExpiryPreview));
            OnPropertyChanged(nameof(IsNotPerpetual));
        }
    }

    public bool IsNotPerpetual => !_perpetual;

    // ---- Term selector (Phase 1). Start policy: the term begins at issuance. ----
    private TermKind _selectedTerm = TermKind.OneYear;
    public IReadOnlyList<TermChoice> TermChoices => TermPlanner.Choices;
    public bool IsCustomTerm => _selectedTerm == TermKind.Custom;

    public TermKind SelectedTerm
    {
        get => _selectedTerm;
        set
        {
            if (!Set(ref _selectedTerm, value)) return;
            ApplyTerm();
            OnPropertyChanged(nameof(IsCustomTerm));
            OnPropertyChanged(nameof(TermSummary));
        }
    }

    public string TermSummary =>
        "Term: " + TermPlanner.Label(_selectedTerm)
        + ". Start policy: the term begins at issuance. Starting at first activation is not supported by the current signed license format.";

    private void ApplyTerm()
    {
        var from = (_notBeforeDate ?? _time.GetUtcNow().UtcDateTime.Date).Date;
        IsPerpetual = _selectedTerm == TermKind.Lifetime;
        if (TermPlanner.ExpiryDate(_selectedTerm, from) is { } date) ExpiresDate = date;
    }

    public string RequestText
    {
        get => _requestText;
        set
        {
            if (Set(ref _requestText, value)) OnPropertyChanged(nameof(BindingSummary));
        }
    }

    public string NotBeforePreview
    {
        get
        {
            var today = _time.GetUtcNow().UtcDateTime.Date;
            var d = (_notBeforeDate ?? today).Date;
            if (d < today) return "That date is in the past. Choose today or later.";
            return d == today
                ? "Valid immediately, from the moment of issuing."
                : "Valid from " + d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 00:00:00 UTC.";
        }
    }

    public string ExpiryPreview
    {
        get
        {
            if (_perpetual) return "Never expires.";
            if (_expiresDate is not { } e) return "Choose an expiry date.";
            return "Expires " + e.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " 23:59:59 UTC.";
        }
    }

    public string BindingSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_requestText))
                return "Not bound: the license carries no installation id. Paste the customer's request code to bind it, unless an unbound license is what you intend.";
            try
            {
                var p = RequestService.Parse(_requestText);
                return "Bound to installation " + Fmt.Short(p.InstallationId, 16) + ". The id is written into the signed license.";
            }
            catch (LicenseToolException)
            {
                return "This is not a valid request code.";
            }
        }
    }

    public IReadOnlyList<DetailRow> Result
    {
        get => _result;
        private set
        {
            if (Set(ref _result, value)) OnPropertyChanged(nameof(HasResult));
        }
    }

    public bool HasResult => _result.Count > 0;

    // ---- Simple-mode helpers: known customers, request-code rule, optional in-memory unlock ----
    private readonly OwnerPaths? _ownerPaths;
    private bool _allowUnbound;
    private bool _keepUnlocked;
    private string? _cachedPassphrase;
    private DateTimeOffset _cacheUntil;

    public ObservableCollection<string> KnownCustomers { get; } = new();

    /// <summary>When true, a license without a customer request code is refused (the ERP refuses unbound licenses by default).</summary>
    public bool RequireRequestCode { get; set; }

    public KeyManagementService? KeyManager { get; set; }

    private string _keyNotice = "";

    public string KeyNotice
    {
        get => _keyNotice;
        private set
        {
            if (Set(ref _keyNotice, value)) { OnPropertyChanged(nameof(HasKeyNotice)); OnPropertyChanged(nameof(NoKeyTitle)); OnPropertyChanged(nameof(NoKeyHelp)); }
        }
    }

    public bool HasKeyNotice => _keyNotice.Length > 0; public string NoKeyTitle => HasKeyNotice ? "No signing key can be used right now." : "No signing key exists yet, so a license cannot be issued."; public string NoKeyHelp => HasKeyNotice ? _keyNotice : "Create a key on the Signing Keys page first.";

    private void ApplyKeyPolicy(KeyOverview? overview)
    {
        if (overview is null)
        {
            KeyNotice = "";
            return;
        }
        if (overview.Recommended is { } pick) SelectedKeyId = pick.KeyId;
        KeyNotice = overview.Notice;
    }

    public bool AllowUnbound { get => _allowUnbound; set => Set(ref _allowUnbound, value); }
    public bool HasMultipleKeys => KeyIds.Count > 1;
    public bool HasSingleKey => KeyIds.Count == 1;

    public bool KeepUnlocked
    {
        get => _keepUnlocked;
        set
        {
            if (Set(ref _keepUnlocked, value) && !value) LockKey();
        }
    }

    public bool IsUnlocked => _cachedPassphrase is not null && _time.GetUtcNow() < _cacheUntil;

    public string UnlockHint =>
        IsUnlocked
            ? "Key unlocked until " + _cacheUntil.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture)
              + " UTC. You can leave the passphrase empty."
            : "";

    public string? GetCachedPassphrase()
    {
        if (_cachedPassphrase is null) return null;
        if (_time.GetUtcNow() >= _cacheUntil)
        {
            LockKey();
            return null;
        }
        return _cachedPassphrase;
    }

    public void RememberPassphrase(string passphrase)
    {
        if (!_keepUnlocked || string.IsNullOrEmpty(passphrase)) return;
        _cachedPassphrase = passphrase;
        _cacheUntil = _time.GetUtcNow().AddMinutes(30);
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(UnlockHint));
    }

    public void LockKey()
    {
        _cachedPassphrase = null;
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(UnlockHint));
    }

    private void LoadKnownCustomers()
    {
        if (_ownerPaths is null) return;
        try
        {
            var names = LedgerReader.Read(_ownerPaths.LedgerPath).Entries
                .Select(e => e.Licensee)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            KnownCustomers.Clear();
            foreach (var name in names) KnownCustomers.Add(name);
        }
        catch (Exception)
        {
            // The list is only a convenience. Typing a name always works.
        }
    }

    private string _lastActivationText = "";
    public string LastActivationText { get => _lastActivationText; private set => Set(ref _lastActivationText, value); }

    public void SetInstallation(string installationId)
    {
        RequestText = InstallationIds.ToRequestCode(installationId);
        SetStatus(StatusKind.Info, "Request imported. Fill in the customer and license terms, then issue.");
    }

    public override async Task OnShownAsync()
    {
        try
        {
            var today = _time.GetUtcNow().UtcDateTime.Date;
            if (_notBeforeDate is { } start && start < today) NotBeforeDate = today;
            var list = await Task.Run(() => _keys.List(out _));
            var previous = _selectedKeyId; var manager = KeyManager; var overview = manager is null ? null : await Task.Run(() => manager.Load());
            KeyIds.Clear();
            foreach (var key in list) { if (overview is null || overview.IsSelectable(key.KeyId)) KeyIds.Add(key.KeyId); }
            SelectedKeyId = previous is not null && KeyIds.Contains(previous) ? previous : KeyIds.FirstOrDefault(); ApplyKeyPolicy(overview);
            OnPropertyChanged(nameof(HasNoKeys)); OnPropertyChanged(nameof(HasMultipleKeys)); OnPropertyChanged(nameof(HasSingleKey)); LoadKnownCustomers();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
        }
    }

    /// <summary>Returns true when the passphrase was used for a signing attempt (the view then clears the box).</summary>
    public async Task<bool> IssueAsync(string passphrase)
    {
        ClearStatus();
        Result = Array.Empty<DetailRow>();

        if (string.IsNullOrWhiteSpace(SelectedKeyId))
        {
            SetStatus(StatusKind.Error, "Select a signing key. If the list is empty, create one on the Signing Keys page.");
            return false;
        }
        if (string.IsNullOrEmpty(passphrase))
        {
            SetStatus(StatusKind.Error, "Enter the passphrase of the signing key.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(Licensee))
        {
            SetStatus(StatusKind.Error, "Enter the customer or organization name.");
            return false;
        }
        if (RequireRequestCode && !AllowUnbound && string.IsNullOrWhiteSpace(RequestText))
        {
            SetStatus(StatusKind.Error,
                "Paste the customer's request code first. The customer opens Activate in the ERP, presses Copy and sends it to you. "
                + "Without it the ERP refuses the license. (Advanced options has a testing override.)");
            return false;
        }
        if (string.IsNullOrWhiteSpace(LicenseId))
        {
            SetStatus(StatusKind.Error, "Enter a license id, or press Generate.");
            return false;
        }

        var now = _time.GetUtcNow();
        var today = now.UtcDateTime.Date;
        var fromDate = (NotBeforeDate ?? today).Date;
        if (fromDate < today)
        {
            SetStatus(StatusKind.Error, "The valid-from date is in the past. Choose today or a later date.");
            return false;
        }
        var notBefore = fromDate == today ? LicenseIssuer.Truncate(now) : UtcMidnight(fromDate);

        DateTimeOffset? expires = null;
        if (!IsPerpetual)
        {
            if (ExpiresDate is not { } expiryDate)
            {
                SetStatus(StatusKind.Error, "Choose an expiry date, or tick Perpetual.");
                return false;
            }
            var endOfDay = UtcMidnight(expiryDate.Date).AddDays(1).AddSeconds(-1);
            if (endOfDay <= notBefore)
            {
                SetStatus(StatusKind.Error, "The expiry must be later than the valid-from time.");
                return false;
            }
            expires = endOfDay;
        }

        if (!TryInt(GraceDaysText, out var grace))
        {
            SetStatus(StatusKind.Error, "Grace days must be a whole number.");
            return false;
        }
        if (!TryInt(MaxInstallationsText, out var installs))
        {
            SetStatus(StatusKind.Error, "Maximum installations must be a whole number.");
            return false;
        }

        string? installationId = null;
        if (!string.IsNullOrWhiteSpace(RequestText))
        {
            try { installationId = RequestService.Parse(RequestText).InstallationId; }
            catch (LicenseToolException ex)
            {
                SetStatus(StatusKind.Error, ex.Message);
                return false;
            }
        }

        var draft = new IssueInput(
            SelectedKeyId, passphrase, LicenseId, Licensee, notBefore, expires, grace, installs,
            installationId, "", WriteActivationCode);

        try
        {
            IssueService.BuildRequest(draft, now);
        }
        catch (LicenseToolException ex)
        {
            SetStatus(StatusKind.Error, ex.Message);
            return false;
        }

        var path = _dialogs.PickSavePath(
            "Save license file", FileStem(Licensee, LicenseId) + ".lic", "FlexiCore license (*.lic)|*.lic|All files (*.*)|*.*");
        if (path is null)
        {
            SetStatus(StatusKind.Info, "Cancelled. Nothing was issued.");
            return false;
        }

        var summary =
            "Issue license " + LicenseId.Trim() + " to '" + Licensee.Trim() + "'?\n\n"
            + "Valid from: " + Fmt.Time(notBefore) + "\n"
            + "Expires: " + Fmt.Expiry(expires) + "\n"
            + "Term: " + TermPlanner.Label(SelectedTerm) + " (begins at issuance)\n" + "Grace days: " + grace + "\n"
            + "Installations: " + installs + "\n"
            + "Bound to an installation: " + (installationId is null ? "No" : "Yes") + "\n"
            + "Signing key: " + SelectedKeyId + "\n"
            + "Save to: " + path + "\n\n"
            + "A signed license cannot be revoked remotely if the customer works offline.";
        if (!_dialogs.Confirm("Issue license", summary))
        {
            SetStatus(StatusKind.Info, "Cancelled. Nothing was issued.");
            return false;
        }

        IsBusy = true;
        try
        {
            var input = draft with { OutputPath = path };
            var outcome = await Task.Run(() => _issue.Issue(input));

            var rows = new List<DetailRow>
            {
                new DetailRow("License ID", outcome.LicenseId),
                new DetailRow("Customer", outcome.Licensee),
                new DetailRow("Signing key", outcome.KeyId),
                new DetailRow("Valid from", Fmt.Time(outcome.NotBefore)),
                new DetailRow("Expires", Fmt.Expiry(outcome.ExpiresAt)),
                new DetailRow("Bound to an installation", outcome.Bound ? "Yes" : "No"),
                new DetailRow("License file", outcome.LicensePath),
            };
            if (outcome.ActivationCodePath is not null)
                rows.Add(new DetailRow("Activation code file", outcome.ActivationCodePath));
            rows.Add(new DetailRow("File SHA-256", outcome.Sha256));
            rows.Add(new DetailRow("Verified with", outcome.VerifiedWith));
            rows.Add(new DetailRow("Ledger", "Recorded"));
            foreach (var warning in outcome.Warnings) rows.Add(new DetailRow("Warning", warning));

            _lastLicensePath = outcome.LicensePath;
            try { LastActivationText = ActivationCode.Encode(File.ReadAllBytes(outcome.LicensePath)); }
            catch (Exception) { LastActivationText = ""; }
            Result = rows;
            SetStatus(StatusKind.Success,
                "License " + outcome.LicenseId + " was signed, verified, saved and recorded in the ledger.");

            // A request code and license id must not be reused by accident for the next customer.
            LicenseId = LicenseIdGenerator.Next(_time.GetUtcNow());
            RequestText = "";
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, Describe(ex));
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FileStem(string licensee, string licenseId)
    {
        var bad = Path.GetInvalidFileNameChars();
        var cleaned = new string(licensee.Trim().Select(c => bad.Contains(c) || char.IsWhiteSpace(c) ? '-' : c).ToArray());
        if (cleaned.Length > 40) cleaned = cleaned.Substring(0, 40);
        cleaned = cleaned.Trim('-', '.');
        return (cleaned.Length == 0 ? "license" : cleaned) + "_" + licenseId.Trim();
    }

    private static DateTimeOffset UtcMidnight(DateTime d) =>
        new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero);

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
