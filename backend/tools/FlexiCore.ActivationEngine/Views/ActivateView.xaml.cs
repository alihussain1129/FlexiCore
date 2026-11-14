using System.Windows.Controls;

namespace FlexiCore.ActivationEngine.Views;

public partial class ActivateView : UserControl
{
    public ActivateView() => InitializeComponent();

    private void OnPasteRequestClick(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            var text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                System.Windows.MessageBox.Show("The clipboard contains no text.",
                    "Paste request code", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            RequestCodeBox.Text = text.Trim();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Could not read the clipboard: " + ex.Message,
                "Clipboard error", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private void OnCopyValueClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button ||
            button.Tag is not string value || string.IsNullOrEmpty(value))
            return;

        try
        {
            System.Windows.Clipboard.SetText(value);
            button.Content = "Copied";
            button.ToolTip = "Copied to clipboard";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("Could not copy the value: " + ex.Message,
                "Clipboard error", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}
