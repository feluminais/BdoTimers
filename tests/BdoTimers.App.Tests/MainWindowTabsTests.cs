using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public class MainWindowTabsTests
{
    /// <summary>A miss beside a button is a click that does nothing, and a notice that runs into the tabs is worse; the buttons fill the bar and touch.</summary>
    [Fact]
    public void The_top_bar_buttons_fill_the_bar_touch_and_never_run_into_the_tabs() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Tabs." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                ((RadioButton)window.FindName("ScheduleTab")).IsChecked = true;
                WpfTest.Drain();
                Rect Bounds(FrameworkElement e) => e.TransformToAncestor(window).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
                var tabs = new[] { "TodayTab", "ScheduleTab", "CustomTab", "TodoTab" }.Select(name => (FrameworkElement)window.FindName(name)).ToArray();
                var chip = (FrameworkElement)window.FindName("NextChip");
                var paused = (FrameworkElement)window.FindName("PausedBlock");
                var buttons = (Panel)paused.Parent;
                var bell = (FrameworkElement)window.FindName("BellButton");
                var resume = PanelFocusScope.Descendants(paused).OfType<Button>().Single();
                var lastTab = Bounds(tabs[^1]);

                // Room to spare: the next boss fits whole and reaches the buttons.
                Assert.True(chip.IsVisible);
                Assert.False(paused.IsVisible);
                Assert.All(tabs.Append(chip).Concat(buttons.Children.OfType<FrameworkElement>().Where(c => c.IsVisible)), e => Assert.Equal(44, e.ActualHeight));
                for (var i = 0; i + 1 < tabs.Length; i++) Assert.Equal(Bounds(tabs[i]).Right, Bounds(tabs[i + 1]).Left, 0.01);
                var cluster = buttons.Children.OfType<FrameworkElement>().Where(c => c.IsVisible).ToArray();
                Assert.Equal(Bounds(chip).Right, Bounds(cluster[0]).Left, 0.01);
                for (var i = 0; i + 1 < cluster.Length; i++) Assert.Equal(Bounds(cluster[i]).Right, Bounds(cluster[i + 1]).Left, 0.01);

                // Paused: the notice takes room first, and the next boss gives way rather than run into the tabs.
                main.PauseHourCommand.Execute(null);
                WpfTest.Drain();
                Assert.True(paused.IsVisible);
                Assert.Equal(44, resume.ActualHeight);
                Assert.Equal(Bounds(resume).Right, Bounds(bell).Left, 0.01);
                Assert.True(Bounds(paused).Left >= lastTab.Right);
                Assert.True(!chip.IsVisible || Bounds(chip).Left >= lastTab.Right);

                // Narrower: the next boss shrinks to the room that is left, and the notice leaves the bar to the bell.
                main.ResumeCommand.Execute(null);
                window.Width = 900;
                WpfTest.Drain();
                Assert.True(chip.IsVisible);
                Assert.True(Bounds(chip).Left >= lastTab.Right);
                Assert.True(Bounds(chip).Right <= Bounds(cluster[0]).Left + 0.01);
                main.PauseHourCommand.Execute(null);
                window.Width = 700;
                WpfTest.Drain();
                Assert.False(paused.IsVisible);
                Assert.False(chip.IsVisible);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    /// <summary>"Time Tracking" is the longest tab and there are six buttons: the tabs sit closer in a narrow window so the buttons stay inside it.</summary>
    [Fact]
    public void The_buttons_stay_in_the_smallest_window_and_in_the_narrowest_one_with_the_paused_notice() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Tabs." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services) { Width = 700, Height = 540, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                WpfTest.Drain();
                Rect Bounds(FrameworkElement e) => e.TransformToAncestor(window).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight));
                var lastTab = Bounds((FrameworkElement)window.FindName("TodoTab"));
                var bell = Bounds((FrameworkElement)window.FindName("BellButton"));
                var close = Bounds(PanelFocusScope.Descendants(window).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Close"));
                Assert.Equal("Time Tracking", PanelFocusScope.Descendants((FrameworkElement)window.FindName("CustomTab")).OfType<TextBlock>().Single().Text);
                Assert.True(close.Right <= window.ActualWidth + 0.01);
                Assert.True(bell.Left >= lastTab.Right - 0.01);

                // The notice shows from 930 px, with every button still in the window.
                main.PauseHourCommand.Execute(null);
                window.Width = 930;
                WpfTest.Drain();
                var paused = (FrameworkElement)window.FindName("PausedBlock");
                Assert.True(paused.IsVisible);
                Assert.True(Bounds(paused).Left >= Bounds((FrameworkElement)window.FindName("TodoTab")).Right - 0.01);
                Assert.True(Bounds(PanelFocusScope.Descendants(window).OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Close")).Right <= window.ActualWidth + 0.01);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    /// <summary>The first view is only the bosses and the second everything, so the Schedule says which is which.</summary>
    [Fact]
    public void The_schedule_names_its_two_views_for_what_they_show() => WpfTest.Run(() =>
    {
        var view = new ScheduleView();
        var window = new Window { Content = view, Width = 600, Height = 300, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            Assert.Equal("Bosses", ((RadioButton)view.FindName("WeekToggle")).Content);
            Assert.Equal("Calendar", ((RadioButton)view.FindName("MonthToggle")).Content);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void The_window_opens_on_Today_and_the_next_boss_chip_shows_on_the_other_tabs_when_there_is_room() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Tabs." + Guid.NewGuid().ToString("N"));
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
                var chip = (FrameworkElement)window.FindName("NextChip");
                var today = (RadioButton)window.FindName("TodayTab");
                var schedule = (RadioButton)window.FindName("ScheduleTab");
                Assert.Equal(MainTab.Today, main.SelectedTab);
                Assert.True(today.IsChecked);
                Assert.True(main.Today.Hero.HasNext);
                Assert.False(chip.IsVisible);

                schedule.IsChecked = true;
                WpfTest.Drain();

                Assert.Equal(MainTab.Schedule, main.SelectedTab);
                Assert.True(chip.IsVisible);

                // A command that shows another screen picks its tab.
                main.ShowTodayCommand.Execute(null);
                WpfTest.Drain();

                Assert.True(today.IsChecked);
                Assert.False(chip.IsVisible);

                schedule.IsChecked = true;
                window.Width = 700;
                WpfTest.Drain();

                Assert.False(chip.IsVisible);
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });
}
