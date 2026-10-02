using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
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

    public sealed record Choices(ICommand CountdownCommand)
    {
        public ICommand WeeklyCommand => CountdownCommand;
        public ICommand OneTimeCommand => CountdownCommand;
    }
}
