using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public sealed class TodoViewTests
{
    sealed class Host : IPanelHost
    {
        public void OpenPanel(object panel) { }
        public void ClosePanel() { }
        public bool IsOpen(object panel) => true;
    }

    [Fact]
    public void Lists_that_are_off_sit_apart_from_active_ones_and_a_switch_turns_one_on() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Todo." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var todo = new TodoViewModel(services, new Host());
            Assert.NotEmpty(todo.WeeklyOff);
            Assert.Empty(todo.WeeklyOn);
            var view = new TodoView { DataContext = todo };
            var window = new Window { Content = view, Width = 960, Height = 720, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                var list = todo.WeeklyOff[0];
                var on = VisualTree.FindDescendant<ToggleButton>(view, b => b.DataContext == list)!;
                Assert.False(on.IsChecked);
                on.IsChecked = true;
                WpfTest.Drain();
                Assert.Single(todo.WeeklyOn);
                Assert.Empty(todo.WeeklyOff);
                Assert.True(services.Todos.Current.Lists.Single(l => l.Id == list.Id).Enabled);
            }
            finally { window.Close(); }
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });

    [Fact]
    public void A_list_card_reports_how_far_along_it_is() => WpfTest.Run(() =>
    {
        var path = Path.Combine(Path.GetTempPath(), "BdoTimers.Todo." + Guid.NewGuid().ToString("N"));
        try
        {
            using var services = new AppServices(Application.Current, path);
            var weekly = services.Todos.Current.Lists.First(l => l.Cadence == BdoTimers.Core.Model.TodoCadence.Weekly);
            services.Todos.SetEnabled(weekly.Id, true);
            var todo = new TodoViewModel(services, new Host());
            var card = todo.WeeklyOn.Single();
            Assert.Equal(0, card.Fraction);
            services.Todos.Toggle(weekly.Id, weekly.Rows[0].Id);
            WpfTest.Drain();
            Assert.Equal("1/16", card.Summary);
            Assert.Equal(1.0 / 16, card.Fraction, 3);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    });
}
