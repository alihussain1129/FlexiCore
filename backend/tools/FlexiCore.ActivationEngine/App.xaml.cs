using System.Windows;
using System.Windows.Threading;

namespace FlexiCore.ActivationEngine;

public partial class App : Application
{
    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Show only the exception type: details could contain paths or other sensitive data.
        MessageBox.Show(
            "Something went wrong in the Activation Engine (" + e.Exception.GetType().Name + "). Please try again.",
            "FlexiCore Activation Engine", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
