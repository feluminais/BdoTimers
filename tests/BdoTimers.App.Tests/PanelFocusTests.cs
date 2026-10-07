using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using BdoTimers.App.Views;
using BdoTimers.App.Controls;

namespace BdoTimers.App.Tests;

public class PanelFocusTests
{
    [Fact]
    public void OpeningPanelFocusesInputCyclesTabAndReturnsToOpener() => WpfTest.Run(() =>
    {
        var opener = new Button { Content = "Open" };
        var background = new Grid();
        background.Children.Add(opener);
        var close = new Button { Content = "Close", ToolTip = "Close" };
        var text = new TextBox { Text = "Timer name" };
        Ui.SetInitialFocus(text, true);
        var done = new Button { Content = "Done" };
        var panel = new StackPanel { Visibility = Visibility.Collapsed };
        panel.Children.Add(close);
        panel.Children.Add(text);
        panel.Children.Add(done);
        var root = new Grid();
        root.Children.Add(background);
        root.Children.Add(panel);
        var window = new Window { Width = 300, Height = 180, Content = root };
        try
        {
            var focus = new PanelFocusScope(window, background, panel, () => { panel.Visibility = Visibility.Collapsed; });
            window.Show();
            opener.Focus();
            Assert.Same(opener, Keyboard.FocusedElement);
            panel.Visibility = Visibility.Visible;
            focus.Open();
            WpfTest.Drain();
            Assert.Same(text, Keyboard.FocusedElement);
            Assert.False(opener.IsEnabled);
            Assert.False(opener.Focus());
            done.Focus();
            done.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            Assert.Same(close, Keyboard.FocusedElement);
            close.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            Assert.Same(done, Keyboard.FocusedElement);
            panel.Visibility = Visibility.Collapsed;
            focus.Close();
            Assert.True(opener.IsEnabled);
            Assert.Same(opener, Keyboard.FocusedElement);
        }
        finally { window.Close(); }
    });

    /// <summary>WPF shows a tooltip on keyboard focus when the last input was a key, so Escape or Enter closing a panel would
    /// pop the opener's tooltip with the mouse elsewhere; focus goes back with that turned off.</summary>
    [Fact]
    public void Returning_focus_to_the_opener_does_not_ask_for_its_tooltip() => WpfTest.Run(() =>
    {
        var opener = new Button { Content = "Open", ToolTip = "Settings" };
        var background = new Grid();
        background.Children.Add(opener);
        var panel = new StackPanel { Visibility = Visibility.Collapsed };
        panel.Children.Add(new TextBox());
        var root = new Grid();
        root.Children.Add(background);
        root.Children.Add(panel);
        var window = new Window { Width = 300, Height = 180, Content = root, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            var focus = new PanelFocusScope(window, background, panel, () => { });
            window.Show();
            opener.Focus();
            panel.Visibility = Visibility.Visible;
            focus.Open();
            WpfTest.Drain();
            bool? duringFocus = null;
            opener.GotKeyboardFocus += (_, _) => duringFocus = ToolTipService.GetShowsToolTipOnKeyboardFocus(opener);
            panel.Visibility = Visibility.Collapsed;
            focus.Close();
            Assert.Same(opener, Keyboard.FocusedElement);
            Assert.False(duringFocus);
            Assert.Null(ToolTipService.GetShowsToolTipOnKeyboardFocus(opener));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void The_background_can_stay_enabled_until_it_is_told_to_go_dim_and_a_closed_panel_ignores_that() => WpfTest.Run(() =>
    {
        var background = new Button { Content = "Open" };
        var panel = new Grid { Visibility = Visibility.Collapsed };
        panel.Children.Add(new TextBox());
        var root = new Grid();
        root.Children.Add(background);
        root.Children.Add(panel);
        var window = new Window { Width = 300, Height = 180, Content = root };
        try
        {
            var focus = new PanelFocusScope(window, background, panel, () => { });
            window.Show();
            panel.Visibility = Visibility.Visible;
            focus.Open(disableBackground: false);
            Assert.True(background.IsEnabled);
            focus.DisableBackground();
            Assert.False(background.IsEnabled);
            focus.Close();
            Assert.True(background.IsEnabled);
            focus.DisableBackground();
            Assert.True(background.IsEnabled);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void EscapeDismissesPanelAndTextUndoIsNotIntercepted() => WpfTest.Run(() =>
    {
        var background = new Button { Content = "Open" };
        var text = new TextBox();
        var panel = new Grid();
        panel.Children.Add(text);
        var root = new Grid();
        root.Children.Add(background);
        root.Children.Add(panel);
        var window = new Window { Width = 300, Height = 180, Content = root };
        var dismissed = false;
        try
        {
            var focus = new PanelFocusScope(window, background, panel, () => dismissed = true);
            window.Show();
            focus.Open();
            WpfTest.Drain();
            var source = PresentationSource.FromVisual(window)!;
            var nativeUndo = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Z) { RoutedEvent = Keyboard.KeyDownEvent };
            text.RaiseEvent(nativeUndo);
            Assert.False(nativeUndo.Handled);
            Assert.False(focus.HandleKeyDown(Key.Escape, ModifierKeys.Control));
            Assert.False(dismissed);
            Assert.True(focus.HandleKeyDown(Key.Escape, ModifierKeys.None));
            Assert.True(dismissed);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void EscapeCancelsHotkeyCaptureBeforeDismissal() => WpfTest.Run(() =>
    {
        var hotkey = new HotkeyBox();
        var panel = new Grid();
        panel.Children.Add(hotkey);
        var window = new Window { Width = 300, Height = 180, Content = panel };
        var dismissed = false;
        try
        {
            var focus = new PanelFocusScope(window, new Button(), panel, () => dismissed = true);
            window.Show();
            focus.Open();
            WpfTest.Drain();
            hotkey.Focus();
            var source = PresentationSource.FromVisual(window)!;
            Assert.True(hotkey.HandleKeyDown(Key.Enter, ModifierKeys.None));
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
                Handled = hotkey.HandleKeyDown(Key.Escape, ModifierKeys.None)
            };
            Assert.True(escape.Handled);
            escape.RoutedEvent = Keyboard.KeyDownEvent;
            hotkey.RaiseEvent(escape);
            Assert.False(dismissed);
            Assert.Null(hotkey.Combo);
        }
        finally { window.Close(); }
    });
}
