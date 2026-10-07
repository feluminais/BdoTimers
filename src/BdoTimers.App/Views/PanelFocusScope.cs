using System.Windows;
using BdoTimers.App.Controls;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace BdoTimers.App.Views;

/// <summary>Keep panel keyboard interaction inside the visible modal layer and return to its opener on dismissal.</summary>
internal sealed class PanelFocusScope
{
    readonly UIElement _background;
    readonly FrameworkElement _panel;
    readonly Action _close;
    IInputElement? _opener;
    bool _open;
    int _revision;

    public PanelFocusScope(Window window, UIElement background, FrameworkElement panel, Action close)
    {
        _background = background;
        _panel = panel;
        _close = close;
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(panel, KeyboardNavigationMode.Cycle);
        FocusManager.SetIsFocusScope(panel, true);
        // Bubbling lets hotkey capture and native popups consume Escape first.
        window.KeyDown += (_, e) =>
        {
            if (HandleKeyDown(e.Key, Keyboard.Modifiers)) e.Handled = true;
        };
    }

    internal bool HandleKeyDown(Key key, ModifierKeys modifiers)
    {
        if (!_open || key != Key.Escape || modifiers != ModifierKeys.None) return false;
        _close();
        return true;
    }

    /// <param name="disableBackground">False leaves the background enabled until <see cref="DisableBackground"/>, so that it can go dim under a scrim that is still fading in.</param>
    public void Open(bool disableBackground = true)
    {
        if (!_open) _opener = Keyboard.FocusedElement;
        _open = true;
        if (disableBackground) DisableBackground();
        var revision = ++_revision;
        _panel.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!_open || revision != _revision) return;
            _panel.UpdateLayout();
            var controls = Descendants(_panel).OfType<Control>()
                .Where(c => c.Focusable && c.IsVisible && c.IsEnabled && KeyboardNavigation.GetIsTabStop(c)).ToArray();
            var target = controls.FirstOrDefault(Ui.GetInitialFocus) ?? controls.FirstOrDefault();
            if (target is not null) FocusQuietly(target);
        });
    }

    public void DisableBackground()
    {
        if (_open) _background.IsEnabled = false;
    }

    public void Close()
    {
        _open = false;
        _revision++;
        _background.IsEnabled = true;
        if (_opener is UIElement { IsVisible: true, IsEnabled: true, Focusable: true } element)
            FocusQuietly(element);
        else
            _background.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        _opener = null;
    }

    /// <summary>After a key, WPF shows the tooltip of an element focused from code, though the pointer is elsewhere.</summary>
    static void FocusQuietly(UIElement element)
    {
        ToolTipService.SetShowsToolTipOnKeyboardFocus(element, false);
        element.Focus();
        element.ClearValue(ToolTipService.ShowsToolTipOnKeyboardFocusProperty);
    }

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
