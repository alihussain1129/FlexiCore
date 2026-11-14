using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationApp;

public sealed class App : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var root = Path.Combine(
                Android.App.Application.Context.FilesDir?.AbsolutePath
                    ?? throw new InvalidOperationException("Android app-private storage is unavailable."),
                "FlexiCoreActivationEngine");
            var paths = new OwnerPaths(root);
            AndroidKeyStoreSignerStore? platformKeys = null;
            AndroidDialogService? dialogs = null;
            ActivationShell? shell = null;
            platformKeys = new AndroidKeyStoreSignerStore(paths);
            dialogs = new AndroidDialogService(
                () => shell is null ? null : TopLevel.GetTopLevel(shell),
                paths);
            var model = new MainViewModel(
                dialogs,
                paths,
                TimeProvider.System,
                new ActivationEngineOptions(
                    IncludeKeyManagement: true,
                    PlatformSigningKeyStore: platformKeys));
            shell = new ActivationShell(model, dialogs);
            singleView.MainView = shell;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
