using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.Views.Panels;

public partial class CustomPanel : UserControl
{
    public CustomPanel() => InitializeComponent();

    // Enter (which also presses Done) or leaving the field ends typing a duration.
    void DurationBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitDuration();
    }

    void DurationBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitDuration();

    /// <summary>Sends the last keystrokes without waiting out the binding's delay, then applies the duration.</summary>
    void CommitDuration()
    {
        DurationBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        (DataContext as CustomPanelViewModel)?.CommitDuration();
    }

    void PictureButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = PictureButton.ContextMenu!;
        menu.DataContext = DataContext;
        menu.PlacementTarget = PictureButton;
        menu.IsOpen = true;
    }
}
