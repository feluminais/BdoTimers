using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;

namespace BdoTimers.App.Views;

public partial class CustomView : UserControl
{
    public CustomView() => InitializeComponent();

    /// <summary>The hour field takes the keyboard as soon as the start picker opens.</summary>
    void StartPicker_Opened(object? sender, EventArgs e)
    {
        if (sender is Popup { Child: { } child } && VisualTree.FindDescendant<TextBox>(child, box => box.Tag is "Hour") is { } hour)
            Dispatcher.BeginInvoke(() => hour.Focus());
    }

    void ClockBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Step((TextBox)sender, e.Delta > 0 ? 1 : -1);
        e.Handled = true;
    }

    void ClockBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down)) return;
        Step((TextBox)sender, e.Key == Key.Up ? 1 : -1);
        e.Handled = true;
    }

    void ClockBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ((TextBox)sender).SelectAll();

    static void Step(TextBox box, int delta)
    {
        if (box.DataContext is not TimerTileViewModel tile) return;
        var command = (box.Tag as string, delta > 0) switch
        {
            ("Hour", true) => tile.HourUpCommand,
            ("Hour", false) => tile.HourDownCommand,
            (_, true) => tile.MinuteUpCommand,
            _ => tile.MinuteDownCommand,
        };
        command.Execute(null);
        box.SelectAll();
    }

    void PercentSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        (e.Delta > 0 ? Slider.IncreaseSmall : Slider.DecreaseSmall).Execute(null, (Slider)sender);
        e.Handled = true;
    }
}
