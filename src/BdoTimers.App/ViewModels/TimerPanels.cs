using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.ViewModels;

internal static class TimerPanels
{
    /// <summary>Opens the boss panel for a built-in boss and the timer panel for any other timer.</summary>
    public static void Open(AppServices services, IPanelHost host, Guid id)
    {
        if (services.Timers.Current.Timers.FirstOrDefault(t => t.Id == id) is not { } timer) return;
        host.OpenPanel(timer.IsBuiltIn
            ? new BossPanelViewModel(services, host, timer)
            : new CustomPanelViewModel(services, host, timer));
    }
}
