using System.IO;
using System.Windows;
using System.Windows.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public class MainWindowTabsTests
{
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
