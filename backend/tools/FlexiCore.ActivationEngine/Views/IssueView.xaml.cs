using System.Windows;
using System.Windows.Controls;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationEngine.Views;

public partial class IssueView : UserControl
{
    public IssueView()
    {
        InitializeComponent();
        Loaded += OnViewLoaded;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        // Convenience: if the clipboard already holds a valid request code, fill it in.
        if (DataContext is not IssueViewModel vm || !string.IsNullOrWhiteSpace(vm.RequestText)) return;
        try
        {
            if (!Clipboard.ContainsText()) return;
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4000) return;
            var parsed = RequestService.Parse(text);
            vm.RequestText = parsed.RequestCode;
        }
        catch (Exception)
        {
            // Not a request code, or the clipboard is busy. Nothing to fill in.
        }
    }

    private async void OnIssueClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not IssueViewModel vm) return;
        try
        {
            // The passphrase lives in the PasswordBox (and, only if the owner ticked the box, in memory for 30 minutes).
            var typed = Passphrase.Password;
            var pass = string.IsNullOrEmpty(typed) ? (vm.GetCachedPassphrase() ?? "") : typed;
            var used = await vm.IssueAsync(pass);
            if (used)
            {
                if (vm.StatusKindName == "Success") vm.RememberPassphrase(pass);
                Passphrase.Clear();
            }
        }
        catch
        {
            Passphrase.Clear();
            throw;
        }
    }

    private void OnLockClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is IssueViewModel vm) vm.LockKey();
        Passphrase.Clear();
    }

    private void OnPasteRequestClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("The clipboard contains no text.", "Paste request code",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RequestCodeBox.Text = text.Trim();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not read the clipboard: " + ex.Message,
                "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCopyValueClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string value ||
            string.IsNullOrEmpty(value))
            return;

        try
        {
            Clipboard.SetText(value);
            button.Content = "Copied";
            button.ToolTip = "Copied to clipboard";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not copy the value: " + ex.Message,
                "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}