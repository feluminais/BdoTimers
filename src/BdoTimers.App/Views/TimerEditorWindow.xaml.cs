using System.Windows;
using BdoTimers.App.ViewModels;

namespace BdoTimers.App.Views;

public partial class TimerEditorWindow : Window
{
    public TimerEditorWindow(TimerEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += () => DialogResult = true;
    }
}
