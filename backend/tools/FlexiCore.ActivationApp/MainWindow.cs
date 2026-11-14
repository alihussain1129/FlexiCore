using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;
using Button = Avalonia.Controls.Button;
using CheckBox = Avalonia.Controls.CheckBox;
using Orientation = Avalonia.Layout.Orientation;
using Path = Avalonia.Controls.Shapes.Path;

namespace FlexiCore.ActivationApp;

internal interface IActivationDialogs : IDialogService
{
    Task ShowErrorAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
}

internal sealed class ActivationShell : UserControl
{
    private readonly MainViewModel _model;
    private readonly IActivationDialogs _dialogs;
    private StackPanel _navigation = new() { Spacing = 5 };
    private readonly StackPanel _page = new() { Spacing = 14 };
    private readonly TextBlock _pageTitle = new() { FontSize = 28, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _pageSubtitle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border _status = new() { Padding = new Thickness(12), CornerRadius = new CornerRadius(8), IsVisible = false };
    private readonly ScrollViewer _contentScroll;
    private readonly StackPanel _contentBody;
    private readonly Grid _root;
    private readonly Grid _main;
    private Border _sidebar = null!;
    private Border _mobileHeader = null!;
    private Border _mobileMorePanel = null!;
    private Border _mobileBottomNavigation = null!;
    private Border _contentRoot = null!;
    private bool _compact;
    private bool _showMore;

    public ActivationShell(MainViewModel model, IActivationDialogs dialogs)
    {
        FontFamily = FlexiTokens.Type.Font;
        Resources["ContentControlThemeFontFamily"] = FlexiTokens.Type.Font;
        _model = model;
        _dialogs = dialogs;

        _root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("270,*"),
            RowDefinitions = new RowDefinitions("*"),
            Background = ActivationDesignSystem.Background
        };
        _mobileHeader = BuildMobileHeader();
        _mobileMorePanel = BuildMobileMorePanel();
        _mobileBottomNavigation = BuildMobileBottomNavigation();
        _sidebar = BuildSidebar();
        _root.Children.Add(_sidebar);
        _root.Children.Add(_mobileHeader);
        _root.Children.Add(_mobileMorePanel);
        _root.Children.Add(_mobileBottomNavigation);
        _main = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Margin = new Thickness(ActivationDesignSystem.Space16, ActivationDesignSystem.Space12)
        };
        _main.Children.Add(_pageTitle);
        Grid.SetRow(_pageSubtitle, 1);
        _main.Children.Add(_pageSubtitle);
        _contentBody = new StackPanel { Spacing = ActivationDesignSystem.Space8, Margin = new Thickness(0, ActivationDesignSystem.Space12, 0, 16) };
        _contentBody.Children.Add(_status);
        _contentBody.Children.Add(_page);
        _contentScroll = new ScrollViewer { Content = _contentBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(_contentScroll, 2);
        _main.Children.Add(_contentScroll);
        _root.Children.Add(_main);
        _contentRoot = new Border { Background = ActivationDesignSystem.Background, Child = _root };
        Content = _contentRoot;
        SizeChanged += (_, e) => ApplyResponsiveLayout(e.NewSize.Width);
        Loaded += async (_, _) =>
        {
            await _model.CurrentPage.OnShownAsync();
            ShowCurrentPage();
            if (Environment.GetEnvironmentVariable("FLEXI_SHOT") is { Length: > 0 } shotDirectory)
                _ = RunScreenshotModeAsync(shotDirectory);
        };
        _model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.Selected) or nameof(MainViewModel.CurrentPage))
                ShowCurrentPage();
        };
        ApplyResponsiveLayout(Bounds.Width);
        ShowCurrentPage();
    }

    private Border BuildSidebar()
    {
        var panel = new StackPanel { Spacing = ActivationDesignSystem.Space8, Margin = new Thickness(ActivationDesignSystem.Space8), VerticalAlignment = VerticalAlignment.Stretch };
        panel.Children.Add(FlexiUi.BrandBlock());
        _navigation = new StackPanel { Spacing = 5 };
        var navScroll = new ScrollViewer { Content = _navigation, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(navScroll);
        panel.Children.Add(ThemeButton());
        var box = new Border
        {
            Background = ActivationDesignSystem.SurfaceElevated,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium),
            Padding = new Thickness(12),
            Child = new TextBlock
            {
                Text = "Owner tools\nPrivate keys stay on this machine.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = ActivationDesignSystem.SecondaryText
            }
        };
        panel.Children.Add(box);
        var sidebar = new Border { Background = ActivationDesignSystem.Surface, BorderBrush = ActivationDesignSystem.Border, BorderThickness = new Thickness(0, 0, 1, 0), Child = panel };
        RebuildNavigation();
        return sidebar;
    }

    private void ApplyResponsiveLayout(double width)
    {
        var compact = width < ActivationDesignSystem.CompactBreakpoint;
        var layoutChanged = compact != _compact;
        _compact = compact;
        _root.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "270,*");
        _root.RowDefinitions = new RowDefinitions(compact ? "Auto,*,Auto,Auto" : "*");
        _sidebar.IsVisible = !compact;
        _mobileHeader.IsVisible = compact;
        _mobileMorePanel.IsVisible = compact && _showMore;
        _mobileBottomNavigation.IsVisible = compact;
        _main.Margin = compact
            ? new Thickness(ActivationDesignSystem.Space4, ActivationDesignSystem.Space4)
            : new Thickness(ActivationDesignSystem.Space16, ActivationDesignSystem.Space12);
        _contentBody.Margin = new Thickness(0, ActivationDesignSystem.Space12, 0, compact ? 24 : 16);
        _contentScroll.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
        _pageTitle.FontSize = compact ? 24 : 28;
        _pageSubtitle.FontSize = compact ? 13 : 14;
        Grid.SetRow(_main, compact ? 1 : 0);
        Grid.SetColumn(_main, compact ? 0 : 1);
        Grid.SetRow(_mobileHeader, 0);
        Grid.SetColumn(_mobileHeader, 0);
        Grid.SetRow(_mobileMorePanel, 1);
        Grid.SetColumn(_mobileMorePanel, 0);
        Grid.SetRow(_mobileBottomNavigation, 3);
        Grid.SetColumn(_mobileBottomNavigation, 0);
        if (layoutChanged) ShowCurrentPage();
    }

    private void RebuildNavigation()
    {
        _navigation.Children.Clear();
        foreach (var item in _model.Items)
        {
            _navigation.Children.Add(NavigationButton(item, false));
        }
        RebuildMobileNavigation();
    }

    private void RebuildMobileNavigation()
    {
        var primaryTitles = new[] { "Dashboard", "Customers", "Activate Installation" };
        var primaryItems = _model.Items
            .Where(item => primaryTitles.Contains(item.Title))
            .ToList();
        var mobilePrimary = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", primaryItems.Count + 1))),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < primaryItems.Count; index++)
        {
            var button = MobileNavigationButton(primaryItems[index]);
            Grid.SetColumn(button, index);
            mobilePrimary.Children.Add(button);
        }

        var moreSelected = _showMore || primaryItems.All(item => !ReferenceEquals(item, _model.Selected));
        var more = new Button
        {
            Content = MobileNavigationContent("More", "M12 8a2 2 0 1 0 0-4 2 2 0 0 0 0 4zm0 6a2 2 0 1 0 0-4 2 2 0 0 0 0 4zm0 6a2 2 0 1 0 0-4 2 2 0 0 0 0 4z", moreSelected),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 60,
            Padding = new Thickness(4, 6),
            Background = moreSelected ? ActivationDesignSystem.PrimaryContainer : Brushes.Transparent,
            Foreground = moreSelected ? ActivationDesignSystem.PrimaryStrong : ActivationDesignSystem.SecondaryText,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusSmall)
        };
        more.Click += (_, _) =>
        {
            _showMore = !_showMore;
            _mobileMorePanel.IsVisible = _compact && _showMore;
            RebuildMobileNavigation();
        };
        Grid.SetColumn(more, primaryItems.Count);
        mobilePrimary.Children.Add(more);
        _mobileBottomNavigation.Child = mobilePrimary;
        _mobileMorePanel.Child = MoreNavigationItems();
    }

    private Control MoreNavigationItems()
    {
        var primaryTitles = new[] { "Dashboard", "Customers", "Activate Installation" };
        var items = _model.Items.Where(item => !primaryTitles.Contains(item.Title));
        var grid = new UniformGrid { Columns = 3 };
        foreach (var item in items) grid.Children.Add(NavigationButton(item, true));
        var handle = new Border
        {
            Width = 40,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = ActivationDesignSystem.Border,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(handle);
        stack.Children.Add(FlexiUi.Text("More", 16, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText, false));
        stack.Children.Add(grid);
        return new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1, 1, 1, 0),
            CornerRadius = new CornerRadius(24, 24, 0, 0),
            Padding = new Thickness(16, 12, 16, 16),
            VerticalAlignment = VerticalAlignment.Bottom,
            BoxShadow = ActivationDesignSystem.ShadowLarge,
            Child = stack
        };
    }

    private Button NavigationButton(NavItem item, bool compact)
    {
        var selected = ReferenceEquals(item, _model.Selected);
        var foreground = selected
            ? ActivationDesignSystem.PrimaryStrong
            : (compact ? ActivationDesignSystem.PrimaryText : ActivationDesignSystem.SecondaryText);
        var icon = FlexiUi.Icon(FlexiUi.IconFor(item.Title), foreground, compact ? 22 : 20);
        Control content;
        if (compact)
        {
            var stack = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(icon);
            stack.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 96
            });
            content = stack;
        }
        else
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(icon);
            row.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 14,
                FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            });
            content = row;
        }
        var button = new Button
        {
            Content = content,
            HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = compact ? new Thickness(6, 12) : new Thickness(14, 0),
            MinHeight = compact ? 84 : 46,
            Margin = compact ? new Thickness(4) : new Thickness(0, 0, 0, 4),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium),
            Background = selected ? ActivationDesignSystem.PrimaryContainer : Brushes.Transparent,
            Foreground = foreground
        };
        button.Click += async (_, _) => await SelectNavigationAsync(item);
        return button;
    }

    private Button MobileNavigationButton(NavItem item)
    {
        var selected = ReferenceEquals(item, _model.Selected);
        var title = item.Title switch
        {
            "Activate Installation" => "Activate",
            _ => item.Title
        };
        var button = new Button
        {
            Content = MobileNavigationContent(title, item.Title switch
            {
                "Dashboard" => "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z",
                "Customers" => "M16 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM8 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm0 2c-2.3 0-7 1.2-7 3.5V20h14v-3.5C15 14.2 10.3 13 8 13zm8 0c-.3 0-.6 0-1 .1 1.2.8 2 1.9 2 3.4V20h7v-3.5c0-2.3-4.7-3.5-8-3.5z",
                _ => "M12 2 3 13h7l-1 9 12-13h-7z"
            }, selected),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 60,
            Padding = new Thickness(4, 6),
            Background = selected ? ActivationDesignSystem.PrimaryContainer : Brushes.Transparent,
            Foreground = selected ? ActivationDesignSystem.PrimaryStrong : ActivationDesignSystem.SecondaryText,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusSmall)
        };
        button.Click += async (_, _) => await SelectNavigationAsync(item);
        return button;
    }

    private static Control MobileNavigationContent(string title, string iconData, bool selected)
    {
        var foreground = selected ? ActivationDesignSystem.PrimaryStrong : ActivationDesignSystem.SecondaryText;
        return new StackPanel
        {
            Spacing = 3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Path
                {
                    Data = Geometry.Parse(iconData),
                    Fill = foreground,
                    Width = 22,
                    Height = 22,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = title, FontSize = 11, FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
    }

    private async Task SelectNavigationAsync(NavItem item)
    {
        _showMore = false;
        _mobileMorePanel.IsVisible = false;
        _model.Selected = item;
        await item.Page.OnShownAsync();
        ShowCurrentPage();
    }

    private Border BuildMobileHeader()
    {
        var header = new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(ActivationDesignSystem.Space4, 6)
        };
        header.Child = MobileHeaderContent();
        return header;
    }

    private Control MobileHeaderContent()
    {
        var brandMark = FlexiLogo.Mark(36);
        var title = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "FlexiCore", FontWeight = FontWeight.Bold, Foreground = ActivationDesignSystem.PrimaryText },
                new TextBlock { Text = "Activation Engine", FontSize = 11, Foreground = ActivationDesignSystem.SecondaryText }
            }
        };
        var toggle = ThemeIconButton();
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("40,*,Auto"),
            Children = { brandMark, title, toggle }
        };
        Grid.SetColumn(title, 1);
        Grid.SetColumn(toggle, 2);
        return header;
    }

    private Border BuildMobileMorePanel()
    {
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#66000000")),
            IsVisible = false,
            ZIndex = 20,
            Child = MoreNavigationItems()
        };
        panel.PointerPressed += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, panel)) return;
            _showMore = false;
            panel.IsVisible = false;
            RebuildMobileNavigation();
        };
        return panel;
    }

    private Border BuildMobileBottomNavigation()
    {
        var panel = new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(4),
            Child = new StackPanel { Orientation = Orientation.Horizontal }
        };
        return panel;
    }

    private Button ThemeIconButton()
    {
        var button = new Button
        {
            Content = FlexiUi.Icon(ActivationDesignSystem.IsDark ? FlexiUi.IconSun : FlexiUi.IconMoon, ActivationDesignSystem.PrimaryText, 20),
            Width = 44,
            Height = 44,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = ActivationDesignSystem.ActionSurface,
            CornerRadius = new CornerRadius(22)
        };
        button.Click += (_, _) => ToggleTheme();
        return button;
    }

    private Button ThemeButton()
    {
        var button = new Button
        {
            Content = ActivationDesignSystem.IsDark ? "Light theme" : "Dark theme",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 48,
            Background = ActivationDesignSystem.ActionSurface,
            Foreground = ActivationDesignSystem.PrimaryText,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusSmall)
        };
        button.Click += (_, _) => ToggleTheme();
        return button;
    }

    private void ToggleTheme()
    {
        ActivationDesignSystem.ToggleTheme();
        var variant = ActivationDesignSystem.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        if (Avalonia.Application.Current is { } application)
            application.RequestedThemeVariant = variant;
        var oldSidebar = _sidebar;
        _sidebar = BuildSidebar();
        _sidebar.IsVisible = !_compact;
        ReplaceRootChild(oldSidebar, _sidebar);
        _mobileHeader.Background = ActivationDesignSystem.Surface;
        _mobileHeader.BorderBrush = ActivationDesignSystem.Border;
        _mobileHeader.Child = MobileHeaderContent();
        _mobileMorePanel.Child = MoreNavigationItems();
        _mobileBottomNavigation.Background = ActivationDesignSystem.Surface;
        _mobileBottomNavigation.BorderBrush = ActivationDesignSystem.Border;
        _contentRoot.Background = ActivationDesignSystem.Background;
        _root.Background = ActivationDesignSystem.Background;
        ShowCurrentPage();
    }

    private async Task RunScreenshotModeAsync(string directory)
    {
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            var sizes = new[] { (Name: "desktop", Width: 1280, Height: 820), (Name: "phone", Width: 400, Height: 860) };
            foreach (var dark in new[] { false, true })
            {
                if (ActivationDesignSystem.IsDark != dark) ToggleTheme();
                foreach (var size in sizes)
                {
                    if (TopLevel.GetTopLevel(this) is Window window) { window.Width = size.Width; window.Height = size.Height; }
                    await Task.Delay(900);
                    var number = 0;
                    foreach (var item in _model.Items.ToList())
                    {
                        number++;
                        await SelectNavigationAsync(item);
                        await Task.Delay(500);
                        var baseName = $"{size.Name}-{(dark ? "dark" : "light")}-{number:00}-{item.Title.Replace(' ', '-')}";
                        await SaveShotAsync(System.IO.Path.Combine(directory, baseName + ".png"));
                        if (number == 1 && size.Name == "phone")
                        {
                            _showMore = true;
                            _mobileMorePanel.IsVisible = _compact;
                            RebuildMobileNavigation();
                            await Task.Delay(400);
                            await SaveShotAsync(System.IO.Path.Combine(directory, baseName + "-more.png"));
                            _showMore = false;
                            _mobileMorePanel.IsVisible = false;
                            RebuildMobileNavigation();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "shot-error.txt"), ex.ToString());
        }
        Environment.Exit(0);
    }

    private async Task SaveShotAsync(string file)
    {
        await Task.Yield();
        var width = Math.Max(1, (int)Math.Round(Bounds.Width));
        var height = Math.Max(1, (int)Math.Round(Bounds.Height));
        using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(this);
        bitmap.Save(file);
    }

    private void ReplaceRootChild(Control oldChild, Control newChild)
    {
        var index = _root.Children.IndexOf(oldChild);
        _root.Children.Remove(oldChild);
        _root.Children.Insert(index, newChild);
    }

    private void ShowCurrentPage()
    {
        RebuildNavigation();
        var vm = _model.CurrentPage;
        _page.MaxWidth = vm is DashboardViewModel or CustomersViewModel or LedgerPageViewModel ? 1240 : 860;
        _pageTitle.Foreground = ActivationDesignSystem.PrimaryText;
        _pageTitle.Text = vm.Title;
        _pageSubtitle.Foreground = ActivationDesignSystem.SecondaryText;
        _pageSubtitle.Text = vm.Subtitle;
        _page.Children.Clear();
        _status.IsVisible = vm.HasStatus;
        _status.Background = vm.StatusKindName switch
        {
            "Error" => ActivationDesignSystem.StatusErrorSurface,
            "Success" => ActivationDesignSystem.StatusSuccessSurface,
            _ => ActivationDesignSystem.StatusInfoSurface
        };
        var statusForeground = vm.StatusKindName switch
        {
            "Error" => ActivationDesignSystem.StatusErrorText,
            "Success" => ActivationDesignSystem.StatusSuccessText,
            _ => ActivationDesignSystem.PrimaryText
        };
        _status.Child = new TextBlock { Text = vm.StatusText, Foreground = statusForeground, TextWrapping = TextWrapping.Wrap };

        switch (vm)
        {
            case DashboardViewModel dashboard: ShowDashboard(dashboard); break;
            case CustomersViewModel customers: ShowCustomers(customers); break;
            case LedgerPageViewModel ledger: ShowLedger(ledger); break;
            case ActivateViewModel activate: ShowActivate(activate); break;
            case IssueViewModel issue: ShowIssue(issue); break;
            case KeysViewModel keys: ShowKeys(keys); break;
            case KeyManagementViewModel manager: ShowKeyManagement(manager); break;
            case SettingsViewModel settings: ShowSettings(settings); break;
            case HelpViewModel help: ShowHelp(help); break;
        }
        if (vm.HasStatus)
        {
            _status.IsVisible = true;
            _status.Child = new TextBlock { Text = vm.StatusText, Foreground = statusForeground, TextWrapping = TextWrapping.Wrap };
        }
    }

    private void ShowDashboard(DashboardViewModel vm)
    {
        Control actions = _compact
            ? new StackPanel
            {
                Spacing = 10,
                Margin = new Thickness(0, 10, 0, 0),
                Children =
                {
                    HeroAction("Review activation request", () => _model.GoActivateCommand.Execute(null)),
                    HeroActionSecondary("Issue a license", () => _model.GoIssueCommand.Execute(null))
                }
            }
            : new WrapPanel
            {
                Margin = new Thickness(0, 10, 0, 0),
                Children =
                {
                    HeroAction("Review activation request", () => _model.GoActivateCommand.Execute(null)),
                    HeroActionSecondary("Issue a license", () => _model.GoIssueCommand.Execute(null))
                }
            };
        _page.Children.Add(FlexiUi.Hero(_compact, actions));

        var columns = _compact || Bounds.Width < 1100 ? 2 : 4;
        var stats = new Grid
        {
            Margin = new Thickness(-6, 0, -6, 0),
            ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", columns)))
        };
        for (var index = 0; index < vm.Stats.Count; index++)
        {
            var item = vm.Stats[index];
            var row = index / columns;
            while (stats.RowDefinitions.Count <= row)
                stats.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var card = FlexiUi.StatCard(item.Label, item.Value, item.Caption, FlexiUi.IconForStat(item.Label), FlexiUi.AccentForStat(item.Label), _compact);
            Grid.SetRow(card, row);
            Grid.SetColumn(card, index % columns);
            stats.Children.Add(card);
        }
        _page.Children.Add(stats);

        var monthly = vm.Monthly.Select(p => (p.Label, p.Count)).ToList();
        var bars = FlexiUi.BarChartCard("Licenses issued", "Last 6 months", monthly, _compact);
        var donut = FlexiUi.DonutCard("License status", vm.ActiveCount, vm.ExpiredCount, vm.PendingCount, _compact);
        var charts = new Grid { Margin = new Thickness(-6, 0, -6, 0) };
        if (_compact || Bounds.Width < 900)
        {
            charts.RowDefinitions = new RowDefinitions("Auto,Auto");
            Grid.SetRow(donut, 1);
        }
        else
        {
            charts.ColumnDefinitions = new ColumnDefinitions("3*,2*");
            Grid.SetColumn(donut, 1);
        }
        charts.Children.Add(bars);
        charts.Children.Add(donut);
        _page.Children.Add(charts);

        SectionTitle(_page, "Recent issuance");
        if (!vm.HasRecent) _page.Children.Add(Empty("No licenses have been issued on this machine."));
        else
        {
            var list = new StackPanel();
            var rows = vm.Recent.ToList();
            for (var i = 0; i < rows.Count; i++)
                list.Children.Add(FlexiUi.ActivityRow(rows[i].Licensee, $"{rows[i].LicenseId}  ·  Signing key {rows[i].KeyId}", rows[i].When, i, i == rows.Count - 1, _compact));
            _page.Children.Add(FlexiUi.ListCard(list));
        }

        SectionTitle(_page, "Owner-data checks");
        if (!vm.HasWarnings) _page.Children.Add(Row("No owner-data warnings were reported."));
        else foreach (var warning in vm.Warnings) _page.Children.Add(Warning(warning));
    }

    private Button HeroAction(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(18, 11),
            Margin = _compact ? new Thickness(0) : new Thickness(0, 0, 10, 10),
            MinHeight = 48,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = _compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = Brushes.White,
            Foreground = Brush.Parse(ActivationDesignSystem.Primary900Hex),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusSmall)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private Button HeroActionSecondary(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(18, 11),
            Margin = _compact ? new Thickness(0) : new Thickness(0, 0, 10, 10),
            MinHeight = 48,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = _compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Colors.White, 0.14),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Colors.White, 0.35),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusSmall)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void ShowCustomers(CustomersViewModel vm)
    {
        var search = CreateTextInput(vm.SearchText, "Search organizations");
        var summary = Caption(vm.Summary);
        var results = new StackPanel { Spacing = 4 };
        _page.Children.Add(search);
        _page.Children.Add(summary);
        _page.Children.Add(results);
        void FillResults()
        {
            results.Children.Clear();
            summary.Text = vm.Summary;
            if (vm.Rows.Count == 0)
                results.Children.Add(Empty(vm.HasNoRows ? "Customers appear here after licenses are issued." : "No customers match this search."));
            var rowIndex = 0;
            foreach (var r in vm.Rows)
            {
                results.Children.Add(FlexiPages.AvatarCard(r.Name,
                    new[]
                    {
                        $"{r.Licenses} licenses  ·  {r.ActiveNow} active now",
                        $"Last issued {r.LastIssued}  ·  latest expiry {r.LatestExpiry}"
                    },
                    rowIndex++, null, _compact));
            }
        }
        search.TextChanged += (_, _) => { vm.SearchText = search.Text ?? ""; FillResults(); };
        FillResults();
        AddRefresh(vm);
    }

    private void ShowLedger(LedgerPageViewModel vm)
    {
        var search = CreateTextInput(vm.SearchText, "Search by license, customer, or signing key");
        var summary = Caption(vm.Summary);
        var results = new StackPanel { Spacing = 4 };
        _page.Children.Add(search);
        _page.Children.Add(summary);
        _page.Children.Add(results);
        void FillResults()
        {
            results.Children.Clear();
            summary.Text = vm.Summary;
            if (vm.Rows.Count == 0)
                results.Children.Add(Empty(vm.HasNoRows ? "The issue ledger has no records yet." : "No records match this search."));
            var rowIndex = 0;
            foreach (var r in vm.Rows)
            {
                results.Children.Add(FlexiPages.AvatarCard(r.Licensee,
                    new[]
                    {
                        $"{r.LicenseId}",
                        $"Key {r.KeyId}  ·  Valid {r.NotBefore} — {r.Expires}",
                        $"Grace {r.Grace}  ·  Installations {r.Installations}",
                        $"Recorded {r.Recorded}",
                        $"SHA-256 {r.FileSha256}"
                    },
                    rowIndex++, FlexiPages.StatusChip(r.Status), _compact));
            }
        }
        search.TextChanged += (_, _) => { vm.SearchText = search.Text ?? ""; FillResults(); };
        FillResults();
        _page.Children.Add(Warning(vm.Note));
        _page.Children.Add(ButtonRow(
            Button("Export ledger backup", async () =>
            {
                await vm.ExportBackupAsync();
                ShowCurrentPage();
            }),
            Button("Restore ledger backup", async () =>
            {
                await vm.RestoreBackupAsync();
                ShowCurrentPage();
            })));
        AddRefresh(vm);
    }

    private void ShowActivate(ActivateViewModel vm)
    {
        _page.Children.Add(SectionCard("Request code", "Paste a request code from the customer's ERP, or import a request file.",
            MultilineInput(vm.RequestText, "Paste the complete request code here", text => vm.RequestText = text)));
        var actions = ButtonRow(
            ButtonSync("Verify code", () => { vm.VerifyCommand.Execute(null); ShowCurrentPage(); }),
            Button("Open request file…", async () => await ImportRequestAsync(vm)),
            ButtonSync("Use this computer (test)", () => { vm.UseThisComputerCommand.Execute(null); ShowCurrentPage(); }),
            ButtonSync("Clear", () => { vm.ClearCommand.Execute(null); ShowCurrentPage(); }));
        _page.Children.Add(actions);
        if (vm.HasParsed)
            _page.Children.Add(SectionCard("Verified request", $"Installation ID: {vm.ParsedInstallationId}\nRequest code: {vm.ParsedRequestCode}",
                ButtonRow(Button("Continue to issue", async () =>
                {
                    vm.ProceedCommand.Execute(null);
                    await _model.CurrentPage.OnShownAsync();
                    ShowCurrentPage();
                }))));
        _page.Children.Add(Warning(vm.Caution));
    }

    private async Task ImportRequestAsync(ActivateViewModel vm)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this)
                ?? throw new InvalidOperationException("The application storage provider is unavailable.");
            var selected = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open customer request file",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Request files") { Patterns = new[] { "*.txt", "*.req" } } }
            });
            if (selected.Count == 0) return;
            await using var stream = await selected[0].OpenReadAsync();
            vm.RequestText = await ReadLimitedTextAsync(stream, 256 * 1024);
            vm.VerifyCommand.Execute(null);
            ShowCurrentPage();
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("Could not read request file", ex.Message); }
    }

    private static async Task<string> ReadLimitedTextAsync(Stream stream, int maximumBytes)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk);
            if (read == 0) break;
            if (buffer.Length + read > maximumBytes)
                throw new InvalidDataException("The request file is larger than 256 KB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read));
        }
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    private void ShowIssue(IssueViewModel vm)
    {
        if (vm.HasNoKeys) _page.Children.Add(Warning(vm.NoKeyTitle + " " + vm.NoKeyHelp));
        else _page.Children.Add(ComboField("Signing key", vm.KeyIds, vm.SelectedKeyId, value =>
        {
            vm.SelectedKeyId = value;
            ShowCurrentPage();
        }));
        _page.Children.Add(Field("Customer / organization", vm.Licensee, value => vm.Licensee = value, "Choose or enter a customer"));
        if (vm.KnownCustomers.Count > 0)
            _page.Children.Add(ComboField("Known customer", vm.KnownCustomers, null, value => vm.Licensee = value));
        _page.Children.Add(Field("License ID", vm.LicenseId, value => vm.LicenseId = value, "License identifier",
            ButtonSync("Generate", () => { vm.GenerateIdCommand.Execute(null); ShowCurrentPage(); })));
        _page.Children.Add(ComboField("Term", vm.TermChoices, vm.TermChoices.FirstOrDefault(c => c.Kind == vm.SelectedTerm),
            choice => vm.SelectedTerm = choice.Kind, choice => choice.Label));
        _page.Children.Add(new TextBlock { Text = vm.TermSummary, TextWrapping = TextWrapping.Wrap, Foreground = ActivationDesignSystem.SecondaryText });
        _page.Children.Add(Field("Valid from", vm.NotBeforeDate?.ToString("yyyy-MM-dd") ?? "", value =>
        {
            if (DateTime.TryParse(value, out var date)) vm.NotBeforeDate = date.Date;
        }, "YYYY-MM-DD"));
        if (vm.IsNotPerpetual)
            _page.Children.Add(Field("Expires", vm.ExpiresDate?.ToString("yyyy-MM-dd") ?? "", value =>
            {
                if (DateTime.TryParse(value, out var date)) vm.ExpiresDate = date.Date;
            }, "YYYY-MM-DD"));
        else _page.Children.Add(Caption(vm.ExpiryPreview));
        _page.Children.Add(Field("Grace days", vm.GraceDaysText, value => vm.GraceDaysText = value, "0"));
        _page.Children.Add(Field("Maximum installations", vm.MaxInstallationsText, value => vm.MaxInstallationsText = value, "1"));
        _page.Children.Add(SectionCard("Installation binding", vm.BindingSummary,
            MultilineInput(vm.RequestText, "Optional request code", text => vm.RequestText = text)));
        if (vm.RequireRequestCode)
            _page.Children.Add(CheckField("Allow an unbound license (advanced/testing only)", vm.AllowUnbound, value => vm.AllowUnbound = value));
        _page.Children.Add(CheckField("Write an activation-code file", vm.WriteActivationCode, value => vm.WriteActivationCode = value));
        _page.Children.Add(CheckField("Keep key unlocked for 30 minutes", vm.KeepUnlocked, value => vm.KeepUnlocked = value));
        if (vm.KeyManager is not null && vm.HasKeyNotice) _page.Children.Add(Warning(vm.KeyNotice));
        var passphrase = vm.RequiresPassphrase
            ? SecretInput("Signing key passphrase")
            : null;
        if (passphrase is null)
            _page.Children.Add(Warning("This key is non-exportable and is accessed through the platform keystore. Its hardware backing has not been verified."));
        if (!string.IsNullOrEmpty(vm.UnlockHint)) _page.Children.Add(Caption(vm.UnlockHint));
        if (passphrase is not null) _page.Children.Add(passphrase);
        _page.Children.Add(ButtonRow(Button("Issue license", async () =>
        {
            await RunSafe(async () =>
            {
                await vm.IssueAsync(passphrase?.Text ?? "");
                if (passphrase is not null) passphrase.Text = "";
                ShowCurrentPage();
            });
        }), ButtonSync("New customer", () => { vm.NewCustomerCommand.Execute(null); ShowCurrentPage(); })));
        foreach (var result in vm.Result) _page.Children.Add(Row($"{result.Label}: {result.Value}"));
        if (!string.IsNullOrWhiteSpace(vm.LastActivationText))
        {
            var codeBox = MultilineReadOnly(vm.LastActivationText);
            _page.Children.Add(SectionCard("Activation code", "Share this code with the customer using a secure channel.", codeBox,
                ButtonRow(Button("Copy activation code", async () => await CopyAsync(vm.LastActivationText)))));
        }
    }

    private void ShowKeys(KeysViewModel vm)
    {
        var first = SecretInput("Passphrase");
        var second = SecretInput("Confirm passphrase");
        _page.Children.Add(SectionCard("Create a signing key",
            "The private key is encrypted on disk. Keep two encrypted backups and store the passphrase separately.",
            Field("Key ID", vm.NewKeyId, value => vm.NewKeyId = value, "e.g. fc-2026-a",
                ButtonSync("Suggest ID", () =>
                {
                    vm.NewKeyId = vm.KeyManager?.NextKeyId() ?? vm.NewKeyId;
                    ShowCurrentPage();
                })),
            first,
            second,
            ButtonRow(Button("Create key", async () =>
            {
                await RunSafe(async () =>
                {
                    if (await vm.CreateAsync(first.Text ?? "", second.Text ?? ""))
                    {
                        first.Text = "";
                        second.Text = "";
                    }
                    ShowCurrentPage();
                });
            }), Button("Refresh", async () => { await vm.OnShownAsync(); ShowCurrentPage(); }))));
        if (vm.SupportsPlatformKeys)
            _page.Children.Add(SectionCard("Create an Android Keystore key",
                "The P-256 private key is generated on this device and is non-exportable. Hardware backing is not asserted. "
                + "This key cannot be moved or backed up; losing or uninstalling this device permanently removes it.",
                ButtonRow(Button("Create non-exportable key", async () =>
                {
                    if (!await _dialogs.ConfirmAsync(
                            "Create device-bound key",
                            "This private key cannot be exported or restored on another device. Continue?"))
                        return;
                    await vm.CreatePlatformKeyAsync();
                    ShowCurrentPage();
                }))));

        var importPassphrase = SecretInput("Encrypted .p8 passphrase");
        _page.Children.Add(SectionCard("Import encrypted signing key",
            "Imports the existing PBES2-encrypted PKCS#8 .p8 format. The file is validated with its passphrase; unencrypted keys are refused.",
            importPassphrase,
            ButtonRow(Button("Choose and import .p8…", async () =>
            {
                var path = await _dialogs.PickOpenPathAsync(
                    "Import encrypted signing key", "Encrypted signing key (*.p8)|*.p8|All files (*.*)|*.*");
                if (path is null) return;
                if (await vm.ImportEncryptedKeyAsync(path, importPassphrase.Text ?? ""))
                    importPassphrase.Text = "";
                ShowCurrentPage();
            }))));

        _page.Children.Add(FlexiSections.Header("Local signing keys",
            "Select a key to copy its fingerprint or export its public key.", FlexiUi.IconKey));
        if (vm.Rows.Count == 0) _page.Children.Add(Empty("No signing keys are stored in the owner key directory."));
        var keyIndex = 0;
        foreach (var row in vm.Rows)
        {
            var picked = row;
            _page.Children.Add(FlexiSections.SelectableCard($"{row.KeyId}",
                new[] { $"Created {row.Created}  ·  Fingerprint {row.FingerprintShort}" },
                keyIndex++, FlexiSections.KeyChip($"{row.Status}"), _compact,
                object.Equals(vm.SelectedRow, row),
                () => { vm.SelectedRow = picked; ShowCurrentPage(); },
                FlexiUi.IconKey));
        }
        _page.Children.Add(SectionCard("Selected key fingerprint", null,
            FlexiSections.MonoValue(vm.SelectedFingerprint),
            ButtonRow(Button("Copy fingerprint", async () => await CopyAsync(vm.SelectedRow?.Fingerprint ?? "")))));
        var keyActions = new List<Button>
        {
            Button("Export selected public key", async () => { await ExecuteCommandAsync(vm.ExportSelectedCommand); ShowCurrentPage(); }),
            Button("Export all public keys", async () => { await ExecuteCommandAsync(vm.ExportAllCommand); ShowCurrentPage(); }),
            ButtonSync("Open key folder", () => vm.OpenFolderCommand.Execute(null))
        };
        if (vm.SelectedRow is { IsNonExportable: false })
            keyActions.Insert(0, Button("Back up encrypted private key", async () =>
            {
                await vm.ExportEncryptedPrivateKeyAsync();
                ShowCurrentPage();
            }));
        _page.Children.Add(ButtonRow(keyActions.ToArray()));
        _page.Children.Add(Caption("Key directory: " + vm.KeyFolder));
    }

    private void ShowKeyManagement(KeyManagementViewModel vm)
    {
        _page.Children.Add(FlexiSections.Info("ERP public-key file: " + vm.ErpFileText));
        _page.Children.Add(ButtonRow(
            Button("Choose ERP key file…", async () => { await ExecuteCommandAsync(vm.ChooseErpFileCommand); ShowCurrentPage(); }),
            Button("Check / refresh", async () => { await vm.OnShownAsync(); ShowCurrentPage(); })));
        _page.Children.Add(FlexiSections.Header("Key lifecycle",
            "Select a key, then retire it, reactivate it or mark it as built into the ERP.", FlexiUi.IconKey));
        if (vm.Rows.Count == 0) _page.Children.Add(Empty("No managed key records are available."));
        var keyIndex = 0;
        foreach (var row in vm.Rows)
        {
            var picked = row;
            _page.Children.Add(FlexiSections.SelectableCard($"{row.KeyId}",
                new[]
                {
                    $"Created {row.Created}  ·  Licenses {row.Licenses}  ·  Outstanding {row.NotExpired}",
                    $"Last used {row.LastUsed}  ·  In ERP: {row.InErp}  ·  Remove from ERP: {row.RemoveFromErp}",
                    $"{row.Advice}"
                },
                keyIndex++, FlexiSections.KeyChip($"{row.State}"), _compact,
                object.Equals(vm.SelectedRow, row),
                () => { vm.SelectedRow = picked; ShowCurrentPage(); },
                FlexiUi.IconKey));
        }
        _page.Children.Add(ButtonRow(
            Button("Retire selected key", async () => { await ExecuteCommandAsync(vm.RetireCommand); await vm.OnShownAsync(); ShowCurrentPage(); }),
            Button("Reactivate selected key", async () => { vm.ReactivateCommand.Execute(null); await vm.OnShownAsync(); ShowCurrentPage(); }),
            Button("Mark built into ERP", async () => { await ExecuteCommandAsync(vm.MarkInErpCommand); await vm.OnShownAsync(); ShowCurrentPage(); })));
        if (vm.SelectedRow is not null && vm.GetSelectedPublicKeySnippet() is { } snippet)
            _page.Children.Add(ButtonRow(Button("Copy public key entry", async () => await CopyAsync(snippet))));
        var password = SecretInput("New key passphrase");
        var confirm = SecretInput("Confirm passphrase");
        _page.Children.Add(SectionCard("Rotate signing key",
            "A new key is not selected for signing until its public key is added to the ERP build.",
            password, confirm, ButtonRow(Button("Create next key", async () =>
            {
                await RunSafe(async () =>
                {
                    await vm.CreateKeyAsync(password.Text ?? "", confirm.Text ?? "");
                    password.Text = "";
                    confirm.Text = "";
                    ShowCurrentPage();
                });
            }))));
        SectionTitle(_page, "Recent key-management events");
        foreach (var item in vm.Events)
            _page.Children.Add(FlexiSections.EventRow($"{item.When}", $"{item.KeyId}", $"{item.Action}", $"{item.Detail}"));
        if (vm.Events.Count == 0) _page.Children.Add(Empty("No key-management events have been recorded."));
    }

    private void ShowSettings(SettingsViewModel vm)
    {
        _page.Children.Add(FlexiSections.Header("Owner data locations",
            "These paths are used by the shared activation services. Keep the owner-data folder private and outside cloud-synced folders.",
            FlexiUi.IconGear));
        _page.Children.Add(FlexiSections.DetailsCard(vm.Details.Select(d => ($"{d.Label}", $"{d.Value}"))));
        _page.Children.Add(Warning("Back up the encrypted private key files and the issue ledger. The passphrase is not stored by this application."));
    }

    private void ShowHelp(HelpViewModel vm)
    {
        var number = 1;
        foreach (var section in vm.Sections)
            _page.Children.Add(FlexiSections.HelpSection(number++, $"{section.Heading}", $"{section.Body}"));
        _page.Children.Add(FlexiSections.StepsCard("Safe operating sequence", new[]
        {
            "Confirm the customer and commercial approval outside this tool.",
            "Verify the customer's request code.",
            "Check the selected signing key and expiry.",
            "Review the confirmation before signing.",
            "Keep the ledger and encrypted key backups protected."
        }));
    }

    private void AddRefresh(PageViewModel vm) =>
        _page.Children.Add(ButtonRow(Button("Refresh from owner data", async () =>
        {
            await vm.OnShownAsync();
            ShowCurrentPage();
        })));

    private Control SectionCard(string title, string? body = null, params Control[] children)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = ActivationDesignSystem.PrimaryText, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(body))
            stack.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Foreground = ActivationDesignSystem.SecondaryText });
        foreach (var child in children) stack.Children.Add(child);
        return Card(stack);
    }

    private static Control Card(Control content, double? width = null) =>
        new Border
        {
            Width = width ?? double.NaN,
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 12),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = content
        };

    private static Control Row(string text) => Card(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
    private static Control Warning(string text) =>
        FlexiPages.Callout(text, ActivationDesignSystem.StatusWarningSurface, ActivationDesignSystem.Warning, FlexiPages.IconWarning);

    private static Control Empty(string text) => FlexiPages.EmptyState(text);

    private static void SectionTitle(Panel parent, string title) => parent.Children.Add(SectionTitleControl(title));
    private static Control SectionTitleControl(string title) =>
        new TextBlock { Text = title, FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = ActivationDesignSystem.PrimaryText, Margin = new Thickness(0, 10, 0, 0) };
    private static TextBlock Caption(string text) =>
        new() { Text = text, FontSize = 12, FontWeight = FontWeight.Medium, Foreground = ActivationDesignSystem.SecondaryText, TextWrapping = TextWrapping.Wrap };

    private static TextBox CreateTextInput(string text, string watermark) =>
        new()
        {
            Text = text,
            PlaceholderText = watermark,
            Padding = new Thickness(12),
            MinHeight = 48,
            Background = ActivationDesignSystem.Surface,
            Foreground = ActivationDesignSystem.PrimaryText,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium)
        };
    private static TextBox MultilineInput(string text, string watermark, Action<string> changed)
    {
        var input = new TextBox
        {
            Text = text, PlaceholderText = watermark, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 130, Padding = new Thickness(12), VerticalContentAlignment = VerticalAlignment.Top,
            Background = ActivationDesignSystem.Surface, Foreground = ActivationDesignSystem.PrimaryText,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium)
        };
        input.TextChanged += (_, _) => changed(input.Text ?? "");
        return input;
    }
    private static TextBox MultilineReadOnly(string text) =>
        new()
        {
            Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 90, Padding = new Thickness(12), Background = ActivationDesignSystem.Surface,
            Foreground = ActivationDesignSystem.PrimaryText, BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium)
        };
    private static TextBox SecretInput(string watermark) =>
        new()
        {
            PlaceholderText = watermark,
            Padding = new Thickness(12),
            MinHeight = 48,
            PasswordChar = '●',
            Background = ActivationDesignSystem.Surface,
            Foreground = ActivationDesignSystem.PrimaryText,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium)
        };

    private static Control Field(string title, string text, Action<string> changed, string watermark, Control? trailing = null)
    {
        var row = new StackPanel { Spacing = 6 };
        row.Children.Add(Caption(title));
        var input = CreateTextInput(text, watermark);
        input.TextChanged += (_, _) => changed(input.Text ?? "");
        row.Children.Add(input);
        if (trailing is not null) row.Children.Add(trailing);
        return row;
    }

    private static Control ComboField<T>(string title, IEnumerable<T> items, T? selected, Action<T> changed, Func<T, string>? display = null)
    {
        var combo = new ComboBox
        {
            ItemsSource = items.ToArray(),
            SelectedItem = selected,
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ActivationDesignSystem.Surface,
            Foreground = ActivationDesignSystem.PrimaryText,
            BorderBrush = ActivationDesignSystem.Border,
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium)
        };
        if (display is not null)
            combo.ItemTemplate = new FuncDataTemplate<T>((item, _) => new TextBlock { Text = item is null ? "" : display(item) });
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is T item) changed(item); };
        return new StackPanel { Spacing = 6, Children = { Caption(title), combo } };
    }

    private static Control CheckField(string text, bool initial, Action<bool> changed)
    {
        var check = new CheckBox { Content = text, IsChecked = initial };
        check.IsCheckedChanged += (_, _) => changed(check.IsChecked == true);
        return check;
    }

    private static Control ButtonRow(params Button[] buttons)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var button in buttons) panel.Children.Add(button);
        return panel;
    }

    private static Button Button(string label, Func<Task> action, bool primary = false)
    {
        var emphasize = primary || label is "Issue license" or "Create key" or "Verify code" or "Continue to issue" or "Create next key";
        var button = new Button
        {
            Content = label,
            Padding = new Thickness(18, 11),
            Margin = new Thickness(0, 0, 10, 10),
            MinHeight = 48,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = emphasize ? ActivationDesignSystem.Primary : ActivationDesignSystem.ActionSurface,
            Foreground = emphasize ? ActivationDesignSystem.OnPrimary : ActivationDesignSystem.PrimaryText,
            BorderBrush = emphasize ? Brushes.Transparent : ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusMedium)
        };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static Button ButtonSync(string label, Action action) =>
        Button(label, () => { action(); return Task.CompletedTask; });

    private async Task ExecuteCommandAsync(AsyncRelayCommand command)
    {
        if (!command.CanExecute(null)) return;
        command.Execute(null);
        while (!command.CanExecute(null)) await Task.Delay(50);
    }

    private async Task CopyAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard
                ?? throw new InvalidOperationException("The system clipboard is not available.");
            await clipboard.SetTextAsync(text);
            await _dialogs.ShowMessageAsync("Copied", "Text copied to the clipboard.");
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("Clipboard unavailable", ex.Message); }
    }

    private async Task RunSafe(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("Activation Engine error", ex.Message); }
    }

}
