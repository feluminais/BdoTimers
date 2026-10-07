using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BdoTimers.App.ViewModels;
using BdoTimers.App.Views;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.Tests;

public class CalendarViewTests
{
    [Fact]
    public void Month_navigation_days_and_filters_bind() => WpfTest.Run(() =>
    {
        var calls = 0;
        var command = new RelayCommand(() => calls++);
        DateOnly? selected = null;
        var day = new CalendarDayViewModel(date => selected = date);
        day.Show(new CalendarDay(new DateOnly(2026, 10, 4), true, []), [], isToday: true, isSelected: true);
        var calendar = new CalendarStandIn(command, day);
        var view = new CalendarView { DataContext = calendar };
        var window = new Window { Content = view, Width = 960, Height = 640, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            WpfTest.Drain();
            var buttons = PanelFocusScope.Descendants(view).OfType<Button>().ToArray();
            string Name(Button b) => new ButtonAutomationPeer(b).GetName();

            foreach (var name in new[] { "Previous month", "Next month" })
                buttons.Single(b => Name(b) == name).Command!.Execute(null);
            Assert.Equal(2, calls);

            buttons.Single(b => Name(b) == "Sunday 4 October").Command!.Execute(null);
            Assert.Equal(new DateOnly(2026, 10, 4), selected);

            var chips = PanelFocusScope.Descendants(view).OfType<ToggleButton>().Where(t => t is not RadioButton).ToArray();
            Assert.Equal(4, chips.Length);
            chips[0].IsChecked = false;
            Assert.False(calendar.ShowBosses);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void A_days_number_is_larger_and_lighter_than_the_lines_under_it() => WpfTest.Run(() =>
    {
        var day = new CalendarDayViewModel(_ => { });
        var boss = new BdoTimers.Core.Scheduling.CalendarItem(CalendarKind.Boss, new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero), null, CellState.Upcoming);
        day.Show(new CalendarDay(new DateOnly(2026, 10, 4), true, []), [boss, boss, boss], isToday: false, isSelected: false);
        var view = new CalendarView { DataContext = new CalendarStandIn(new RelayCommand(() => { }), day) };
        var window = new Window { Content = view, Width = 960, Height = 640, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            WpfTest.Drain();
            var texts = PanelFocusScope.Descendants(view).OfType<TextBlock>().ToList();
            var (number, bosses) = (texts.Single(t => t.Text == "4"), texts.Single(t => t.Text == "3 bosses"));
            Assert.True(number.FontSize >= bosses.FontSize * 1.3);
            Assert.NotEqual(((SolidColorBrush)number.Foreground).Color, ((SolidColorBrush)bosses.Foreground).Color);
        }
        finally { window.Close(); }
    });

    public sealed class CalendarStandIn(ICommand command, CalendarDayViewModel day)
    {
        public ICommand PreviousMonthCommand => command;
        public ICommand NextMonthCommand => command;
        public ICommand GoToTodayCommand => command;
        public ICommand NewEventCommand => command;
        public string Title => "October 2026";
        public string SelectedTitle => "Sunday 4 October";
        public bool CanAddEvent => true;
        public bool HasNoItems => true;
        public bool ShowBosses { get; set; } = true;
        public bool ShowTimers { get; set; } = true;
        public bool ShowEvents { get; set; } = true;
        public bool ShowResets { get; set; } = true;
        public IReadOnlyList<string> WeekDays { get; } = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        public IReadOnlyList<CalendarDayViewModel> Days { get; } = [day];
        public IReadOnlyList<CalendarRowViewModel> Rows { get; } = [];
    }
}
