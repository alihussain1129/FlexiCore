using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.ViewModels;

public sealed record NavItem(string Title, string Glyph, PageViewModel Page);

public sealed class MainViewModel : ObservableObject
{
    private NavItem _selected;

    public MainViewModel(IDialogService dialogs) : this(dialogs, new OwnerPaths(), TimeProvider.System)
    {
    }

    public MainViewModel(IDialogService dialogs, OwnerPaths paths, TimeProvider time)
    {
        // paths and time are constructor parameters
        // (injected so tests never touch real owner data)
        var keyService = new KeyService(paths);
        var issueService = new IssueService(paths, time);

        var keyManager = new KeyManagementService(paths, keyService, time); var activate = new ActivateViewModel(dialogs);
        var realUi = dialogs is WpfDialogService; var issue = new IssueViewModel(keyService, issueService, realUi ? (IDialogService)new AutoSaveDialogService(dialogs, AutoSaveDialogService.DefaultRoot()) : dialogs, time, paths) { RequireRequestCode = realUi, KeyManager = realUi ? keyManager : null };
        var issueNav = new NavItem("Issue License", "\uE8FB", issue);

        Items = new[]
        {
            new NavItem("Dashboard", "\uE80F", new DashboardViewModel(paths, keyService, time)),
            new NavItem("Customers", "\uE716", new CustomersViewModel(paths, time)),
            new NavItem("Licenses", "\uE8A5", new LedgerPageViewModel(paths, time, LedgerMode.Licenses)),
            new NavItem("Activate Installation", "\uE896", activate),
            issueNav,
            new NavItem("Signing Keys", "\uE72E", new KeysViewModel(paths, keyService, dialogs)),
            new NavItem("Issuance History", "\uE81C", new LedgerPageViewModel(paths, time, LedgerMode.History)),
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

        if (realUi)
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
