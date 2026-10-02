using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
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
    void Weekly() => Create(new TimerDef { Name = "New weekly timer", Kind = TimerKind.Scheduled, Scheduled = NewWeek() });

    [RelayCommand]
    void OneTime() => Create(new TimerDef
    {
        Name = "New event", Kind = TimerKind.OneTime,
        OneTime = OneTimeEvents.Create(services.Clock, TimeZoneInfo.Local.Id),
    });

    /// <summary>A weekly timer for the times the guild summons its boss, shown on the overlay as the Overlay panel's
    /// Guild bosses setting says.</summary>
    [RelayCommand]
    void GuildBoss() => Create(new TimerDef { Name = "Guild boss", Kind = TimerKind.Scheduled, IsGuildBoss = true, Scheduled = NewWeek() });

    static ScheduledSpec NewWeek() => new()
    {
        TimeZoneId = TimeZoneInfo.Local.Id,
        Slots = [new Slot(DayOfWeek.Monday, new TimeOnly(20, 0))],
    };

    void Create(TimerDef timer)
    {
        services.Timers.Upsert(timer);
        host.OpenPanel(new CustomPanelViewModel(services, host, timer));
    }
}
