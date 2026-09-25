using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>First step of "+ New timer": pick the kind, then the new timer's own panel opens.</summary>
public sealed partial class NewTimerPanelViewModel(AppServices services, IPanelHost host)
{
    [RelayCommand]
    void Countdown() => Create(new TimerDef
    {
        Name = "New countdown",
        Kind = TimerKind.Countdown,
        Countdown = new CountdownSpec { Duration = TimeSpan.FromMinutes(60) },
    });

    [RelayCommand]
    void Weekly() => Create(new TimerDef
    {
        Name = "New weekly timer",
        Kind = TimerKind.Scheduled,
        Scheduled = new ScheduledSpec
        {
            TimeZoneId = TimeZoneInfo.Local.Id,
            Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(20, 0))],
        },
    });

    void Create(TimerDef timer)
    {
        services.Timers.Upsert(timer);
        host.OpenPanel(new CustomPanelViewModel(services, host, timer));
    }
}
