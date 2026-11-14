using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationApp;

internal sealed class AvaloniaDialogService(Func<Window?> owner) : IActivationDialogs
{
    public string? PickSavePath(string title, string defaultFileName, string filter) => null;
    public string? PickOpenPath(string title, string filter) => null;
    public bool Confirm(string title, string message) => false;

    public async Task<string?> PickSavePathAsync(string title, string defaultFileName, string filter)
    {
        try
        {
            var parent = RequireOwner();
            if (!parent.StorageProvider.CanSave)
            {
                await ShowErrorAsync("Save is unavailable", "The current platform does not provide a save picker.");
                return null;
            }

            var file = await parent.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = defaultFileName,
                FileTypeChoices = FileTypes(filter)
            });
            return await GetDesktopPath(file, "The selected destination is not a local file path.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open save picker", ex.Message);
            return null;
        }
    }

    public async Task<string?> PickOpenPathAsync(string title, string filter)
    {
        try
        {
            var parent = RequireOwner();
            if (!parent.StorageProvider.CanOpen)
            {
                await ShowErrorAsync("Open is unavailable", "The current platform does not provide a file picker.");
                return null;
            }

            var files = await parent.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = FileTypes(filter)
            });
            return files.Count == 0 ? null : await GetDesktopPath(files[0], "This workflow needs a local file path.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Could not open file picker", ex.Message);
            return null;
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = CreateDialog(title, message, "Continue?", "Cancel");
        var ownerWindow = RequireOwner();
        return await dialog.ShowDialog<bool>(ownerWindow);
    }

    public void Reveal(string filePath)
    {
        try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); }
        catch (Exception ex) { _ = ShowErrorAsync("Could not open file", ex.Message); }
    }

    public void OpenFolder(string folderPath)
    {
        try { Process.Start(new ProcessStartInfo(folderPath) { UseShellExecute = true }); }
        catch (Exception ex) { _ = ShowErrorAsync("Could not open folder", ex.Message); }
    }

    public Task ShowErrorAsync(string title, string message) => ShowMessageAsync(title, message);

    public Task ShowMessageAsync(string title, string message) =>
        CreateDialog(title, message, "Close").ShowDialog(RequireOwner());

    private Window RequireOwner() =>
        owner() ?? throw new InvalidOperationException("The application window is not available.");

    private static Window CreateDialog(string title, string message, string primary, string? secondary = null)
    {
        var result = new Window
        {
            Icon = new Avalonia.Controls.WindowIcon(Avalonia.Platform.AssetLoader.Open(new System.Uri("avares://FlexiCore.ActivationApp/Assets/logo-512.png"))),
            Title = title,
            Width = 480,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var layout = new StackPanel { Spacing = 16 };
        layout.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8
        };
        var primaryButton = new Button { Content = primary, MinWidth = 92, IsDefault = true };
        if (secondary is null)
            primaryButton.Click += (_, _) => result.Close();
        else
            primaryButton.Click += (_, _) => result.Close(true);
        buttons.Children.Add(primaryButton);

        if (secondary is not null)
        {
            var cancel = new Button { Content = secondary, MinWidth = 92, IsCancel = true };
            cancel.Click += (_, _) => result.Close(false);
            buttons.Children.Add(cancel);
        }

        layout.Children.Add(buttons);
        result.Content = new Border { Padding = new Avalonia.Thickness(22), Child = layout };
        return result;
    }

    private async Task<string?> GetDesktopPath(IStorageItem? item, string unsupportedMessage)
    {
        if (item is null) return null;
        if (item.Path.IsAbsoluteUri && item.Path.IsFile) return item.Path.LocalPath;
        await ShowErrorAsync("Unsupported file location", unsupportedMessage);
        return null;
    }

    private static IReadOnlyList<FilePickerFileType> FileTypes(string filter)
    {
        var parts = filter.Split('|', StringSplitOptions.TrimEntries);
        var result = new List<FilePickerFileType>();
        for (var index = 0; index + 1 < parts.Length; index += 2)
        {
            var patterns = parts[index + 1].Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (patterns.Length == 0) continue;
            result.Add(new FilePickerFileType(parts[index]) { Patterns = patterns });
        }
        return result;
    }
}
