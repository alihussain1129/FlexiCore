using System.Diagnostics;
using System.Windows;
using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine;

public sealed class WpfDialogService : IDialogService
{
    public string? PickSavePath(string title, string defaultFileName, string filter)
    {
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
