using System.Globalization;
using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class BossPanelViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly Guid _id;

    [ObservableProperty] private Choice _alertsOn;
    [ObservableProperty] private bool _showTimes;

    public string Name { get; }
    public string NextText { get; }
    public string AppliesText { get; }
    public IReadOnlyList<ArtPicture> Images { get; }
    public AlertRowsViewModel Alerts { get; }
    public SlotListViewModel Slots { get; }
    public string TimeZoneNote { get; }

    public BossPanelViewModel(AppServices services, TimerDef boss)
    {
        _services = services;
        _id = boss.Id;
        _alertsOn = Choice.For(boss.Enabled);
        Name = boss.Name;
        Images = [services.Art.For(boss)];
        NextText = NextSpawnText(boss, DateTimeOffset.UtcNow);
        AppliesText = $"Applies to every {boss.Name} spawn";
        Alerts = new AlertRowsViewModel(services, boss);
        var spec = boss.Scheduled ?? new ScheduledSpec();
        Slots = new SlotListViewModel(spec.Slots,
            slots => services.Timers.Modify(_id, t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }));
        TimeZoneNote = $"Server time ({TimeZones.Find(spec.TimeZoneId).StandardName})";
    }

    partial void OnAlertsOnChanged(Choice value) => _services.Timers.SetEnabled(_id, value.IsOn);

    [RelayCommand]
    void ToggleTimes() => ShowTimes = !ShowTimes;

    internal static string NextSpawnText(TimerDef timer, DateTimeOffset now)
    {
        var next = OccurrenceSource.Between(timer, now, now + TimeSpan.FromDays(8)).Cast<DateTimeOffset?>().FirstOrDefault();
        return next is { } at
            ? $"Next · {at.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture)} · in {DurationFormat.Countdown(at - now)}"
            : "No upcoming spawns";
    }
}
