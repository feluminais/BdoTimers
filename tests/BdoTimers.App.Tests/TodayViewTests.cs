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
                // Every name of Coming up is a button, and bosses that spawn together are one row of them.
                var panel = (FrameworkElement)view.FindName("ComingUpPanel");
                var listed = PanelFocusScope.Descendants(panel).OfType<Button>().Where(b => b.DataContext is ComingUpName).ToList();
                Assert.Equal(today.ComingUp.Rows.Sum(row => row.Names.Count), listed.Count);
                Assert.Contains(today.ComingUp.Rows, row => row.Names.Count > 1);
                UiCapture.Save(window, "today-view.png");

                var side = (FrameworkElement)view.FindName("SideHost");
                var narrow = (FrameworkElement)view.FindName("NarrowHost");
                Assert.True(side.IsVisible);
                Assert.False(narrow.IsVisible);

                window.Width = 700;
                WpfTest.Drain();

                Assert.False(side.IsVisible);
                Assert.True(narrow.IsVisible);
                var narrowStack = (FrameworkElement)view.FindName("NarrowStack");
                Assert.True(narrowStack.IsAncestorOf(hero));
                Assert.True(narrowStack.IsAncestorOf((FrameworkElement)view.FindName("DailyPanel")));
                Assert.True(narrowStack.IsAncestorOf((FrameworkElement)view.FindName("WeeklyPanel")));
                UiCapture.Save(window, "today-view-narrow.png");

                window.Width = 960;
                WpfTest.Drain();

                Assert.True(side.IsVisible);
                Assert.True(((FrameworkElement)view.FindName("MainHost")).IsAncestorOf(hero));
                var sideStack = (FrameworkElement)view.FindName("SideStack");
                Assert.True(sideStack.IsAncestorOf((FrameworkElement)view.FindName("DailyPanel")));
                Assert.True(sideStack.IsAncestorOf((FrameworkElement)view.FindName("WeeklyPanel")));
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void A_task_with_sub_tasks_opens_them_under_itself_and_they_are_ticked_there() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Today." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            foreach (var list in services.Todos.Current.Lists) services.Todos.SetEnabled(list.Id, true);
            var host = new Host();
            var todo = new TodoViewModel(services, host);
            var today = new TodayViewModel(services, host, new CustomViewModel(services, host), todo, () => { }, () => { }, () => { });
            var view = new TodayView { DataContext = today };
            var window = new Window { Content = new Border { Padding = new Thickness(22, 18, 22, 20), Child = view }, Width = 960, Height = 900,
                Left = -10000, Top = -10000, ShowInTaskbar = false, FontSize = 13 };
            window.SetResourceReference(Control.BackgroundProperty, "BgBrush");
            window.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            window.SetResourceReference(Control.FontFamilyProperty, "UiFont");
            try
            {
                window.Show();
                WpfTest.Drain();
                var weekly = (FrameworkElement)view.FindName("WeeklyPanel");
                var parent = today.Weekly.Tasks.First(task => task.HasChildren);
                Button Part(FrameworkElement panel, ListedTask task, string name) =>
                    PanelFocusScope.Descendants(panel).OfType<Button>().Single(b => b.Name == name && b.DataContext == task);
                CheckBox[] Sub(FrameworkElement panel, ListedTask task) => PanelFocusScope.Descendants(panel).OfType<CheckBox>()
                    .Where(c => task.Row.Children.Any(child => child.ToggleCommand == c.Command)).ToArray();

                // Closed: the line says how far along it is, and its name and arrow are what opens it.
                Assert.True(Part(weekly, parent, "Arrow").IsVisible);
                Assert.True(Part(weekly, parent, "OpenTask").IsVisible);
                Assert.False(Part(weekly, parent, "OpenList").IsVisible);
                Assert.All(Sub(weekly, parent), box => Assert.False(box.IsVisible));

                Part(weekly, parent, "Arrow").Command.Execute(null);
                WpfTest.Drain();

                var boxes = Sub(weekly, parent);
                Assert.Equal(parent.Row.Children.Count, boxes.Length);
                Assert.All(boxes, box => Assert.True(box.IsVisible));
                Assert.Equal("Hide sub-tasks", Part(weekly, parent, "Arrow").ToolTip);
                UiCapture.Save(window, "today-weekly-open.png");

                // A sub-task is ticked in place; the task keeps its line and stays open.
                var first = parent.Row.Children[0];
                first.ToggleCommand.Execute(null);
                WpfTest.Drain();

                Assert.Same(parent, today.Weekly.Tasks.Single(task => task.Row.Id == parent.Row.Id));
                Assert.True(parent.IsExpanded);
                Assert.Equal($"1/{parent.Row.Children.Count}", parent.Row.PartialProgress);
                Assert.Equal("1/16", today.Weekly.Summary);
                Assert.All(Sub(weekly, parent), box => Assert.True(box.IsVisible));

                // Its name closes it again.
                Part(weekly, parent, "OpenTask").Command.Execute(null);
                WpfTest.Drain();

                Assert.False(parent.IsExpanded);
                Assert.All(Sub(weekly, parent), box => Assert.False(box.IsVisible));
                Assert.Equal("Show sub-tasks", Part(weekly, parent, "Arrow").ToolTip);

                // A task with no sub-tasks has no arrow, and its name opens the list as before.
                var daily = (FrameworkElement)view.FindName("DailyPanel");
                var plain = today.Daily.Tasks.First(task => !task.HasChildren);
                Assert.False(Part(daily, plain, "Arrow").IsVisible);
                Assert.True(Part(daily, plain, "OpenList").IsVisible);
                Assert.False(Part(daily, plain, "OpenTask").IsVisible);
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });
}
