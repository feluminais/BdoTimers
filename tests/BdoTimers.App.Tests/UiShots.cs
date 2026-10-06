using System.IO;
using System.Windows;
using System.Windows.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Tests;

/// <summary>Opt-in: with BDOTIMERS_SHOTS set to a folder, saves a PNG of each screen and the main panels at 960 x 720.</summary>
public class UiShots
{
    [Fact]
    public void Screens_and_panels_render() => WpfTest.Run(() =>
    {
        if (Environment.GetEnvironmentVariable("BDOTIMERS_SHOTS") is null) return;
        var root = Path.Combine(Path.GetTempPath(), "BdoTimers.Shots." + Guid.NewGuid());
        try
        {
            using var services = new AppServices(Application.Current, root);
            var farm = services.Timers.Current.Timers.First(t => t.Preset == Presets.Farm);
            services.Timers.Start(farm.Id, services.Clock.UtcNow.AddHours(-12));
            using var main = new MainViewModel(services);
            var window = new MainWindow(main, services)
            { Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                main.SetShown(true);
                foreach (var (tab, name) in new[] { ("BossesTab", "bosses"), ("CustomTab", "timers"), ("TodoTab", "todo"), ("CalendarTab", "calendar") })
                {
                    ((RadioButton)window.FindName(tab)).IsChecked = true;
                    WpfTest.Drain();
                    UiCapture.Save(window, $"{name}.png");
                }
                main.OpenSettingsCommand.Execute(null);
                WpfTest.Drain();
                var settings = PanelFocusScope.Descendants(window).OfType<SettingsPanel>().Single();
                foreach (var (nav, name) in new[] { ("GeneralNav", "general"), ("AlertsNav", "alerts"), ("TodoNav", "todo"), ("BossesNav", "bosses"), ("DataNav", "data"), ("AboutNav", "about") })
                {
                    ((RadioButton)settings.FindName(nav)).IsChecked = true;
                    WpfTest.Drain();
                    UiCapture.Save(window, $"settings-{name}.png");
                }
                main.ClosePanel();
                WpfTest.Drain();
                var data = services.Timers.Current;
                var boss = data.Timers.First(t => BossRegions.IsSelected(data, t));
                main.OpenPanel(new BossPanelViewModel(services, main, boss));
                WpfTest.Drain();
                UiCapture.Save(window, "boss-panel.png");
            }
            finally
            {
                typeof(AppServices).GetField("<IsQuitting>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(services, true);
                window.Close();
            }
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    });
}
