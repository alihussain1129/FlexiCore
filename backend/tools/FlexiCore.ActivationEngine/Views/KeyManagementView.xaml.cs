using System.Windows;
using System.Windows.Controls;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationEngine.Views;

public partial class KeyManagementView : UserControl
{
    public KeyManagementView() => InitializeComponent();

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not KeyManagementViewModel vm) return;
        try
        {
            if (await vm.CreateKeyAsync(NewPass.Password, NewPassConfirm.Password))
            {
                NewPass.Clear();
                NewPassConfirm.Clear();
            }
        }
        catch
        {
            NewPass.Clear();
            NewPassConfirm.Clear();
            throw;
        }
    }

    private void OnCopySnippetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not KeyManagementViewModel vm) return;
        var text = vm.GetSelectedPublicKeySnippet();
        if (string.IsNullOrEmpty(text))
        {
            MessageBox.Show("Select a key that has a valid public key file first.", "Copy public key entry",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not copy the value: " + ex.Message,
                "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}