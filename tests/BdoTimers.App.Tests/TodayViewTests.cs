using System.IO;
using System.Windows;
using System.Windows.Controls;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public class TodayViewTests
{
    sealed class Host : IPanelHost
    {
        public void OpenPanel(object panel) { }
        public void ClosePanel() { }
        public bool IsOpen(object panel) => true;
    }

    [Fact]
    public void Today_shows_the_next_spawn_with_names_that_open_and_one_column_when_narrow() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Today." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var host = new Host();
            var todo = new TodoViewModel(services, host);
            var today = new TodayViewModel(services, host, new CustomViewModel(services, host), todo, () => { }, () => { }, () => { });
            var view = new TodayView { DataContext = today };
            var window = new Window
            {
                Content = new Border { Padding = new Thickness(22, 18, 22, 20), Child = view }, Width = 960, Height = 720,
                Left = -10000, Top = -10000, ShowInTaskbar = false, FontSize = 13,
            };
            window.SetResourceReference(Control.BackgroundProperty, "BgBrush");
            window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            window.SetResourceReference(Control.FontFamilyProperty, "UiFont");
            try
            {
                window.Show();
                WpfTest.Drain();
                var hero = (FrameworkElement)view.FindName("Hero");
                var names = VisualTree.FindDescendant<ItemsControl>(hero, _ => true)!;
                Assert.Equal(today.Hero.Next.Names.Count, names.Items.Count);
                var buttons = PanelFocusScope.Descendants(hero).OfType<Button>().Where(b => b.DataContext is BossLink).ToList();
                Assert.Equal(today.Hero.Next.Names.Count, buttons.Count);
                Assert.Contains(PanelFocusScope.Descendants(hero).OfType<TextBlock>(), t => t.IsVisible && t.Text == today.Hero.Next.Clock);
                Assert.NotEmpty(today.ComingUp.Rows);
                UiCapture.Save(window, "today-view.png");

                var side = (FrameworkElement)view.FindName("SideHost");
                var narrow = (FrameworkElement)view.FindName("NarrowHost");
                Assert.True(side.IsVisible);
                Assert.False(narrow.IsVisible);

                window.Width = 700;
                WpfTest.Drain();

                Assert.False(side.IsVisible);
                Assert.True(narrow.IsVisible);
                Assert.True(((FrameworkElement)view.FindName("NarrowStack")).IsAncestorOf(hero));
                UiCapture.Save(window, "today-view-narrow.png");

                window.Width = 960;
                WpfTest.Drain();

                Assert.True(side.IsVisible);
                Assert.True(((FrameworkElement)view.FindName("MainHost")).IsAncestorOf(hero));
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });
}
