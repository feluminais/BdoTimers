using System.Collections.ObjectModel;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App.ViewModels;

/// <summary>Today's list of what happens in the next 24 hours. Rebuilt when the data, the minute or an item changes; the
/// times to each item move every tick in place.</summary>
public sealed partial class ComingUpViewModel(AppServices services, IPanelHost host) : ObservableObject
{
    AppData? _data;
    AppSettings? _settings;
    int _minute = -1;
    DateTimeOffset _changesAt;

    public ObservableCollection<ComingUpRowViewModel> Rows { get; } = [];
    /// <summary>The current time, which heads the list.</summary>
    [ObservableProperty] private string _nowText = "";
    [ObservableProperty] private bool _isEmpty = true;

    public void Refresh(DateTimeOffset now)
    {
        var data = services.Timers.Current;
        var settings = services.Settings.Current;
        if (!ReferenceEquals(data, _data) || !ReferenceEquals(settings, _settings) || now.Minute != _minute || now >= _changesAt)
        {
            _data = data;
            _settings = settings;
            _minute = now.Minute;
            var items = BdoTimers.Core.Scheduling.ComingUp.Between(data, settings, now, TimeZoneInfo.Local, services.Boards);
            _changesAt = items.Select(i => (DateTimeOffset?)i.AtUtc).FirstOrDefault(at => at > now) ?? DateTimeOffset.MaxValue;
            Rows.Sync(items, (row, item) => row.Matches(item), item => new ComingUpRowViewModel(item, now, Open, ToggleSkip),
                (row, item) => row.Show(item, now));
            IsEmpty = Rows.Count == 0;
        }
        NowText = Formats.Time(now);
        foreach (var row in Rows) row.Update(now);
    }

    void Open(Guid id) => TimerPanels.Open(services, host, id);

    void ToggleSkip(Guid id, DateTimeOffset at) => services.Timers.ToggleMute(id, at);
}

/// <summary>One item of the list: its time, name, what kind it is and how long until it.</summary>
public sealed partial class ComingUpRowViewModel : ObservableObject
{
    readonly Action<Guid> _open;
    readonly Action<Guid, DateTimeOffset> _toggleSkip;
    CalendarItem _item;

    /// <summary>The name, state and commands the Calendar's day list uses for the same item.</summary>
    [ObservableProperty] private CalendarRowViewModel _row;
    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _until = "";

    public ComingUpRowViewModel(CalendarItem item, DateTimeOffset now, Action<Guid> open, Action<Guid, DateTimeOffset> toggleSkip)
    {
        _open = open;
        _toggleSkip = toggleSkip;
        _item = item;
        _row = new CalendarRowViewModel(item, open, toggleSkip);
        Update(now);
    }

    public CalendarKind Kind => _item.Kind;
    public bool IsNext => _item.State == CellState.Next;
    public bool IsSkipped => _item.State == CellState.Skipped;
    /// <summary>What sort of timer it is, for anything but a boss, whose name says it.</summary>
    public string? Sub => _item.Kind switch
    {
        CalendarKind.Weekly => "Weekly",
        CalendarKind.Event => "Event",
        CalendarKind.Countdown => "Countdown",
        CalendarKind.DailyReset or CalendarKind.WeeklyReset => "To-do",
        _ => null,
    };

    public bool Matches(CalendarItem item) =>
        item.Kind == _item.Kind && item.Timer?.Id == _item.Timer?.Id && item.AtUtc == _item.AtUtc;

    /// <summary>Takes the new state of an item it <see cref="Matches"/>.</summary>
    public void Show(CalendarItem item, DateTimeOffset now)
    {
        if (item.State != _item.State) Row = new CalendarRowViewModel(item, _open, _toggleSkip);
        _item = item;
        OnPropertyChanged(nameof(IsNext));
        OnPropertyChanged(nameof(IsSkipped));
        Update(now);
    }

    public void Update(DateTimeOffset now)
    {
        var sameDay = _item.AtUtc.ToLocalTime().Date == now.ToLocalTime().Date;
        Time = sameDay ? Formats.Time(_item.AtUtc) : Formats.DayTime(_item.AtUtc);
        Until = DurationFormat.Until(_item.AtUtc - now);
    }
}
