using System.Windows;
using System.Windows.Controls;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationEngine.Views;

public partial class KeysView : UserControl
{
    public KeysView() => InitializeComponent();

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not KeysViewModel vm) return;
        try
        {
            var created = await vm.CreateAsync(NewPass.Password, NewPassConfirm.Password);
            if (created)
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
}
