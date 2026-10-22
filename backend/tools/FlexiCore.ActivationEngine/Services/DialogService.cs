using System.Diagnostics;
using System.Windows;

namespace FlexiCore.ActivationEngine.Services;

public interface IDialogService
{
    string? PickSavePath(string title, string defaultFileName, string filter);
    string? PickOpenPath(string title, string filter);
    bool Confirm(string title, string message);
    void Reveal(string filePath);
    void OpenFolder(string folderPath);
}

public sealed class WpfDialogService : IDialogService
{
    public string? PickSavePath(string title, string defaultFileName, string filter)
    {
        // OverwritePrompt is off on purpose: the service refuses to overwrite, so the dialog must not offer it.
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true,
            CheckPathExists = true,
            OverwritePrompt = false,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOpenPath(string title, string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
        == MessageBoxResult.Yes;

    public void Reveal(string filePath)
    {
        if (!File.Exists(filePath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + filePath + "\"") { UseShellExecute = true });
    }

    public void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folderPath + "\"") { UseShellExecute = true });
    }
}
