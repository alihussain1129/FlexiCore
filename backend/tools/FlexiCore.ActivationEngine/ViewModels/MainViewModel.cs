using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record NavItem(string Title, string Glyph, PageViewModel Page);
public sealed record ActivationEngineOptions(
    bool RequireRequestCode = false,
    bool UseAutoSave = false,
    bool IncludeKeyManagement = false,
    string? AutoSaveRoot = null,
    IPlatformSigningKeyStore? PlatformSigningKeyStore = null);

public sealed class MainViewModel : ObservableObject
{
    private NavItem _selected;

    public MainViewModel(IDialogService dialogs) : this(dialogs, new OwnerPaths(), TimeProvider.System)
    {
    }

    public MainViewModel(
        IDialogService dialogs,
        OwnerPaths paths,
        TimeProvider time,
        ActivationEngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(time);
        options ??= new ActivationEngineOptions();

        var keyService = new KeyService(paths, options.PlatformSigningKeyStore);
        var keyManager = new KeyManagementService(paths, keyService, time);
        var issueService = new IssueService(paths, time, keyService, keyManager);
        var activate = new ActivateViewModel(dialogs);
        var issueDialogs = options.UseAutoSave
            ? new AutoSaveDialogService(
                dialogs,
                options.AutoSaveRoot ?? AutoSaveDialogService.DefaultRoot())
            : dialogs;
        var issue = new IssueViewModel(keyService, issueService, issueDialogs, time, paths)
        {
            RequireRequestCode = options.RequireRequestCode,
            KeyManager = options.IncludeKeyManagement ? keyManager : null,
        };
        var issueNav = new NavItem("Issue License", "\uE8FB", issue);

        Items = new[]
        {
            new NavItem("Dashboard", "\uE80F", new DashboardViewModel(paths, keyService, time)),
            new NavItem("Customers", "\uE716", new CustomersViewModel(paths, time)),
            new NavItem("Licenses", "\uE8A5", new LedgerPageViewModel(paths, time, LedgerMode.Licenses, dialogs)),
            new NavItem("Activate Installation", "\uE896", activate),
            issueNav,
            new NavItem(
                "Signing Keys",
                "\uE72E",
                new KeysViewModel(
                    paths,
                    keyService,
                    dialogs,
                    options.IncludeKeyManagement ? keyManager : null)),
            new NavItem("Issuance History", "\uE81C", new LedgerPageViewModel(paths, time, LedgerMode.History, dialogs)),
            new NavItem("Settings", "\uE713", new SettingsViewModel(paths)),
            new NavItem("Help", "\uE897", new HelpViewModel(paths)),
        };

        activate.ProceedRequested += id =>
        {
            issue.SetInstallation(id);
            Selected = issueNav;
        };

        GoActivateCommand = new RelayCommand(() => Selected = Items.First(i => i.Title == "Activate Installation"));
        GoIssueCommand = new RelayCommand(() => Selected = issueNav);
        GoKeysCommand = new RelayCommand(() => Selected = Items.First(i => i.Title == "Signing Keys"));

        if (options.IncludeKeyManagement)
        {
            var navList = Items.ToList();
            var at = navList.FindIndex(i => i.Title == "Signing Keys");
            navList.Insert(at < 0 ? navList.Count : at + 1,
                new NavItem("Key Management", "\uE9D9", new KeyManagementViewModel(keyManager, dialogs, time)));
            Items = navList;
        }

        _selected = Items[0];
        _ = ShowAsync(_selected);
    }

    public IReadOnlyList<NavItem> Items { get; }

    public RelayCommand GoActivateCommand { get; }
    public RelayCommand GoIssueCommand { get; }
    public RelayCommand GoKeysCommand { get; }

    public NavItem? Selected
    {
        get => _selected;
        set
        {
            if (value is null) return;
            if (Set(ref _selected, value))
            {
                OnPropertyChanged(nameof(CurrentPage));
                _ = ShowAsync(value);
            }
        }
    }

    public PageViewModel CurrentPage => _selected.Page;

    private static async Task ShowAsync(NavItem item)
    {
        try { await item.Page.OnShownAsync(); }
        catch (Exception) { /* each page reports its own errors */ }
    }
}
