using System.Windows;
using FlexiCore.ActivationEngine.Services;
using FlexiCore.ActivationEngine.ViewModels;

namespace FlexiCore.ActivationEngine;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var work = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, work.Width);
        MinHeight = Math.Min(MinHeight, work.Height);
        Width = Math.Min(Width, work.Width - 40);
        Height = Math.Min(Height, work.Height - 40);
        DataContext = new MainViewModel(new WpfDialogService());
    }
}
