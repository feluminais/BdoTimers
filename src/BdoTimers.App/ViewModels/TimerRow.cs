using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class TimerRow : ObservableObject
{
    readonly TimerStore _store;

    public TimerDef Timer { get; }
    public string Name => Timer.IsBuiltIn ? $"{Timer.Name} (boss)" : Timer.Name;
    public bool IsCountdown => Timer.Kind == TimerKind.Countdown;
    public string Summary { get; }
    public string StartPauseLabel => Timer.Countdown?.Status switch
    {
        CountdownStatus.Running => "Pause",
        CountdownStatus.Paused => "Resume",
        _ => "Start",
    };

    [ObservableProperty] private string _remaining = "";

    public TimerRow(TimerDef timer, TimerStore store)
    {
        Timer = timer;
        _store = store;
        Summary = timer.Kind == TimerKind.Scheduled
            ? $"{timer.Scheduled?.Slots.Count ?? 0} times a week · alerts {Parsing.FormatLeadTimes(timer.Alerts.LeadTimesMinutes)} min before"
            : $"{(int)(timer.Countdown?.Duration.TotalMinutes ?? 0)} min{(timer.Countdown?.AutoRepeat == true ? " · repeats" : "")}";
    }

    public bool Enabled
    {
        get => Timer.Enabled;
        set => _store.SetEnabled(Timer.Id, value);
    }

    public void RefreshRemaining(DateTimeOffset now)
    {
        if (Timer.Countdown is not { } c) return;
        Remaining = c.Status switch
        {
            CountdownStatus.Running => DurationFormat.Countdown(c.EndsAtUtc!.Value - now),
            CountdownStatus.Paused => DurationFormat.Countdown(c.Remaining!.Value),
            _ => DurationFormat.Countdown(c.Duration),
        };
    }

    [RelayCommand]
    void StartPause()
    {
        var now = DateTimeOffset.UtcNow;
        switch (Timer.Countdown?.Status)
        {
            case CountdownStatus.Running: _store.PauseCountdown(Timer.Id, now); break;
            case CountdownStatus.Paused: _store.ResumeCountdown(Timer.Id, now); break;
            default: _store.StartCountdown(Timer.Id, now); break;
        }
    }

    [RelayCommand]
    void Reset() => _store.ResetCountdown(Timer.Id);
}
