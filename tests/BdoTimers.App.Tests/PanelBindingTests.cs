using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.Tests;

public class PanelBindingTests
{
    [Fact]
    public void NewTimerChoicesBindCommandsAndExposeSpokenNames() => WpfTest.Run(() =>
    {
        var calls = 0;
        var command = new RelayCommand(() => calls++);
        var panel = new NewTimerPanel { DataContext = new Choices(command) };
        var window = new Window { Content = panel, Width = 420, Height = 300 };
        try
        {
            window.Show();
            WpfTest.Drain();
            var buttons = PanelFocusScope.Descendants(panel).OfType<Button>().Where(b => b.Command == command).ToArray();
            Assert.Equal(3, buttons.Length);
            Assert.Equal(new[] { "Countdown", "Weekly", "One-time event" }, buttons.Select(b => new ButtonAutomationPeer(b).GetName()));
            foreach (var button in buttons) button.Command!.Execute(null);
            Assert.Equal(3, calls);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void OnlyAWeeklyListThatCanChangeLengthOffersRemoveAndAdd() => WpfTest.Run(() =>
    {
        Slot[] monday = [new(DayOfWeek.Monday, new TimeOnly(20, 0))];
        (int Remove, int Add) Visible(SlotListViewModel slots)
        {
            var list = new SlotList { DataContext = slots };
            var window = new Window { Content = list, Width = 420, Height = 200 };
            try
            {
                window.Show();
                WpfTest.Drain();
                var visible = PanelFocusScope.Descendants(list).OfType<Button>().Where(b => b.IsVisible).ToArray();
                return (visible.Count(b => b.Command == slots.RemoveCommand), visible.Count(b => b.Command == slots.AddTimeCommand));
            }
            finally { window.Close(); }
        }

        Assert.Equal((0, 0), Visible(new SlotListViewModel(monday, _ => { }, minimum: 1, maximum: 1)));
        Assert.Equal((1, 1), Visible(new SlotListViewModel(monday, _ => { })));
        Assert.Equal((1, 0), Visible(new SlotListViewModel(monday, _ => { }, minimum: 0, maximum: 1)));
    });

    public sealed record Choices(ICommand CountdownCommand)
    {
        public ICommand WeeklyCommand => CountdownCommand;
        public ICommand OneTimeCommand => CountdownCommand;
    }
}
