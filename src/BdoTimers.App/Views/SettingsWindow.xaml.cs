using System.Windows;
using BdoTimers.App.ViewModels;

namespace BdoTimers.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Closed += () => DialogResult = true;
    }
}
