using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationApp;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Window? window = null;
            var dialogs = new AvaloniaDialogService(() => window);
            window = new Window
            {
            Icon = new Avalonia.Controls.WindowIcon(Avalonia.Platform.AssetLoader.Open(new System.Uri("avares://FlexiCore.ActivationApp/Assets/logo-512.png"))),
                Title = "FlexiCore Activation Engine",
                Width = 1360,
                Height = 900,
                MinWidth = 900,
                MinHeight = 650,
                Content = new ActivationShell(
                    new MainViewModel(
                        dialogs, new OwnerPaths(), TimeProvider.System,
                        new ActivationEngineOptions(IncludeKeyManagement: true)),
                    dialogs)
            };
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
