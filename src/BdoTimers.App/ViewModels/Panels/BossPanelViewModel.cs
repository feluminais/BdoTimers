using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class BossPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    readonly Guid _id;

    [ObservableProperty] private bool _alertsOn;
    [ObservableProperty] private bool _showTimes;

    public string Name { get; }
    public string NextText { get; }
    public string AppliesText { get; }
    public IReadOnlyList<ArtPicture> Images { get; }
    public AlertRowsViewModel Alerts { get; }
    public SlotListViewModel Slots { get; }
    public string TimeZoneNote { get; }
    public bool CanFinish => Slots.IsValid && !Alerts.VoiceLineInvalid;

    public BossPanelViewModel(AppServices services, TimerDef boss)
    {
        _services = services;
        _id = boss.Id;
        _alertsOn = boss.Enabled;
        Name = boss.Name;
        Images = [services.Art.For(boss)];
        NextText = NextSpawnText(boss, services.Clock.UtcNow);
        AppliesText = $"Applies to every {boss.Name} spawn";
        Alerts = new AlertRowsViewModel(services, boss);
        var spec = boss.Scheduled ?? new ScheduledSpec();
        Slots = new SlotListViewModel(spec.Slots,
            slots => services.Timers.Modify(_id, t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }),
            defaults: services.BundledSchedule(boss)?.Slots);
        TimeZoneNote = $"Server time ({TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneId).StandardName})";
        Alerts.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanFinish));
        Slots.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanFinish));
    }

    partial void OnAlertsOnChanged(bool value) => _services.Timers.SetEnabled(_id, value);

    [RelayCommand]
    void ToggleTimes() => ShowTimes = !ShowTimes;

    internal static string NextSpawnText(TimerDef timer, DateTimeOffset now)
    {
        var next = OccurrenceSource.Next(timer, now);
        return next is { } at
            ? $"Next · {Formats.DayTime(at)} · in {DurationFormat.Countdown(at - now)}"
            : "No upcoming spawns";
    }
}
