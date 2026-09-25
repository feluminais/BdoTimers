using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.Views.Panels;

public partial class CustomPanel : UserControl
{
    public CustomPanel() => InitializeComponent();

    void PictureButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = PictureButton.ContextMenu!;
        menu.DataContext = DataContext;
        menu.PlacementTarget = PictureButton;
        menu.IsOpen = true;
    }

    void ClockBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomPanelViewModel vm) vm.EditingClock = true;
    }

    void ClockBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitClock();
        if (DataContext is CustomPanelViewModel vm) vm.EditingClock = false;
    }

    /// <summary>Enter saves the time; it then also reaches Done, like Enter in the other boxes.</summary>
    void ClockBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitClock();
    }

    /// <summary>Closing the panel keeps what was typed, as it does for every other box.</summary>
    void ClockBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomPanelViewModel { EditingClock: true }) CommitClock();
    }

    void CommitClock() => ClockBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
}
