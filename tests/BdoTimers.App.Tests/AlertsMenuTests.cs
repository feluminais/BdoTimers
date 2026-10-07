using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public class AlertsMenuTests
{
    static void WithWindow(Action<MainWindow> test) => WpfTest.Run(() =>
    {
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Bell." + Guid.NewGuid());
        try
        {
            using var services = new AppServices(Application.Current, root);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                WpfTest.Drain();
                test(window);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });

    static MouseButtonEventArgs Press(UIElement target)
    {
        var press = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent, Source = target };
        target.RaiseEvent(press);
        return press;
    }

    /// <summary>A press outside an open menu closes it, and the bell sees that press just before or just after the menu is closed.</summary>
    [Fact]
    public void Pressing_the_bell_while_its_menu_is_open_closes_it_and_does_not_open_it_again() => WithWindow(window =>
    {
        var bell = (Button)window.FindName("BellButton");
        var menu = bell.ContextMenu!;
        void Click()
        {
            ((IInvokeProvider)new ButtonAutomationPeer(bell).GetPattern(PatternInterface.Invoke)).Invoke();
            WpfTest.Drain();
        }

        Click();
        Assert.True(menu.IsOpen);
        // The bell stays lit while its menu is open.
        Assert.Same(window.FindResource("HoverFillBrush"), bell.Background);

        // The menu closed itself on the press, and the press reaches the bell just after: it is not a click that opens it.
        menu.IsOpen = false;
        WpfTest.Drain();
        Assert.NotSame(window.FindResource("HoverFillBrush"), bell.Background);
        Assert.True(Press(bell).Handled);
        Assert.False(menu.IsOpen);

        // The menu is still open when the bell sees the press: the press closes it.
        Click();
        Assert.True(menu.IsOpen);
        Assert.True(Press(bell).Handled);
        Assert.False(menu.IsOpen);

        // Some time later a press is a press again, and a click opens the menu.
        WpfTest.Wait(400);
        Assert.False(Press(bell).Handled);
        Click();
        Assert.True(menu.IsOpen);
        menu.IsOpen = false;
    });
}
