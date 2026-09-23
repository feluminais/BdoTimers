using System.ComponentModel;
using System.Windows;
using BdoTimers.App.ViewModels;

namespace BdoTimers.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Closing hides to the tray; only Quit ends the app.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!((App)Application.Current).IsQuittingApp)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
