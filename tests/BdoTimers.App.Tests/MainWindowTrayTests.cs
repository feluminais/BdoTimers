using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Tests;

public class MainWindowTrayTests
{
    static void WithWindow(Action<AppServices, MainViewModel, MainWindow> test) => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Tray." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                WpfTest.Drain();
                test(services, main, window);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    static Button TrayButton(MainWindow window) =>
        PanelFocusScope.Descendants(window).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Close to tray");

    static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void The_tray_button_hides_the_window_and_keeps_the_app_running_whatever_the_setting_says() => WithWindow((services, main, window) =>
    {
        Assert.False(services.Settings.Current.CloseToTray);
        var tray = TrayButton(window);
        Assert.Equal("Close to tray", tray.ToolTip);
        var minimize = PanelFocusScope.Descendants(window).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Minimize");
        Assert.Equal(44, tray.ActualHeight);
        Assert.Equal(minimize.TransformToAncestor(window).Transform(new Point()).X - tray.ActualWidth, tray.TransformToAncestor(window).Transform(new Point()).X, 0.01);

        Press(tray);
        WpfTest.Drain();

        Assert.False(window.IsVisible);
        Assert.False(services.IsQuitting);
        window.Show();
        Assert.True(window.IsVisible);
    });

    [Fact]
    public void The_tray_button_asks_about_unsaved_changes_first_like_closing_does() => WithWindow((services, main, window) =>
    {
        var timer = new TimerDef { Name = "Old title", Kind = TimerKind.Countdown, Countdown = new(), Alerts = new() { Tts = new() { Enabled = false } } };
        services.Timers.Upsert(timer);
        main.OpenPanel(new CustomPanelViewModel(services, main, timer) { Name = "Edited title" });
        WpfTest.Drain();
        var tray = TrayButton(window);

        Press(tray);
        WpfTest.Drain();
        Assert.True(main.AskingDiscard);
        Assert.True(window.IsVisible);

        main.KeepEditingCommand.Execute(null);
        Assert.True(window.IsVisible);
        Assert.NotNull(main.Panel);

        Press(tray);
        WpfTest.Drain();
        main.DiscardChangesCommand.Execute(null);
        WpfTest.Drain();
        Assert.False(window.IsVisible);
        Assert.Null(main.Panel);
        Assert.False(services.IsQuitting);
    });
}
