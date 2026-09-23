using System.Collections.ObjectModel;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed class UpcomingViewModel
{
    static readonly TimeSpan Horizon = TimeSpan.FromDays(7);
    const int MaxRows = 60;

    readonly AppServices _services;

    public ObservableCollection<UpcomingRow> Rows { get; } = [];

    public UpcomingViewModel(AppServices services)
    {
        _services = services;
        services.UiClock.Tick += Refresh;
        Refresh(DateTimeOffset.UtcNow);
    }

    void Refresh(DateTimeOffset now)
    {
        var items = UpcomingQuery.Next(_services.Timers.Current, now, Horizon, MaxRows);
        var sameShape = items.Count == Rows.Count
            && items.Zip(Rows).All(p => p.First.Timer.Id == p.Second.TimerId && p.First.AtUtc == p.Second.AtUtc);
        if (!sameShape)
        {
            Rows.Clear();
            foreach (var item in items) Rows.Add(new UpcomingRow(item, _services.Timers));
        }
        for (var i = 0; i < items.Count; i++) Rows[i].Update(items[i], now);
    }
}

public sealed partial class UpcomingRow : ObservableObject
{
    readonly TimerStore _store;

    public Guid TimerId { get; }
    public DateTimeOffset AtUtc { get; }
    public string Name { get; }
    public string When { get; }

    [ObservableProperty] private string _countdown = "";
    [ObservableProperty] private bool _muted;
    [ObservableProperty] private string _muteLabel = "Skip";

    public UpcomingRow(UpcomingItem item, TimerStore store)
    {
        _store = store;
        TimerId = item.Timer.Id;
        AtUtc = item.AtUtc;
        Name = item.Timer.Name;
        When = item.AtUtc.ToLocalTime().ToString("ddd HH:mm");
    }

    public void Update(UpcomingItem item, DateTimeOffset now)
    {
        Countdown = DurationFormat.Countdown(item.AtUtc - now);
        Muted = item.Muted;
        MuteLabel = item.Muted ? "Unskip" : "Skip";
    }

    [RelayCommand]
    void ToggleMute() => _store.ToggleMute(TimerId, AtUtc);
}
