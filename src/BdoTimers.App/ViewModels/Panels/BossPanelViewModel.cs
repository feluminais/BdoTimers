using System.Globalization;
using System.Windows.Media;
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

    [ObservableProperty] private Choice _follow;
    [ObservableProperty] private bool _showTimes;

    public string Name { get; }
    public string NextText { get; }
    public IReadOnlyList<ImageSource> Images { get; }
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public AlertRowsViewModel Alerts { get; }
    public SlotListViewModel Slots { get; }
    public string TimeZoneNote { get; }

    public BossPanelViewModel(AppServices services, TimerDef boss)
    {
        _services = services;
        _id = boss.Id;
        _follow = Choice.For(boss.Enabled);
        Name = boss.Name;
        Images = [services.Art.For(boss)];
        NextText = NextSpawnText(boss, DateTimeOffset.UtcNow);
        Alerts = new AlertRowsViewModel(services.Timers, boss);
        var spec = boss.Scheduled ?? new ScheduledSpec();
        Slots = new SlotListViewModel(spec.Slots,
            slots => services.Timers.Modify(_id, t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }));
        TimeZoneNote = $"Times are in {TimeZones.Find(spec.TimeZoneId).StandardName} (server time).";
    }

    partial void OnFollowChanged(Choice value) => _services.Timers.Modify(_id, t => t with { Enabled = value.IsOn });

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
