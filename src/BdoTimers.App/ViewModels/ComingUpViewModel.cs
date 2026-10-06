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
            Rows.Sync(BdoTimers.Core.Scheduling.ComingUp.Rows(items), (row, group) => row.Matches(group),
                group => new ComingUpRowViewModel(group, now, Open, ToggleSkip), (row, group) => row.Show(group, now));
            IsEmpty = Rows.Count == 0;
        }
        NowText = Formats.Time(now);
        foreach (var row in Rows) row.Update(now);
    }

    void Open(Guid id) => TimerPanels.Open(services, host, id);

    void ToggleSkip(Guid id, DateTimeOffset at) => services.Timers.ToggleMute(id, at);
}

/// <summary>
/// One row of the list: its time, what is at it and how long until it. Bosses that spawn together share a row, each name a
/// button that opens it; anything else has a row to itself.
/// </summary>
public sealed partial class ComingUpRowViewModel : ObservableObject
{
    IReadOnlyList<CalendarItem> _items;

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _until = "";

    public ComingUpRowViewModel(IReadOnlyList<CalendarItem> items, DateTimeOffset now, Action<Guid> open, Action<Guid, DateTimeOffset> toggleSkip)
    {
        _items = items;
        Names = items.Select((item, i) => new ComingUpName(item, i > 0, open, toggleSkip)).ToList();
        Update(now);
    }

    public IReadOnlyList<ComingUpName> Names { get; }
    public CalendarKind Kind => _items[0].Kind;
    public bool IsNext => _items.Any(i => i.State == CellState.Next);
    /// <summary>What sort of timer it is, for anything but a boss, whose name says it.</summary>
    public string? Sub => Kind switch
    {
        CalendarKind.Weekly => "Weekly",
        CalendarKind.Event => "Event",
        CalendarKind.Countdown => "Countdown",
        CalendarKind.DailyReset or CalendarKind.WeeklyReset => "To-do",
        _ => null,
    };

    public bool Matches(IReadOnlyList<CalendarItem> items) =>
        items.Count == _items.Count && items.Zip(_items).All(p =>
            p.First.Kind == p.Second.Kind && p.First.AtUtc == p.Second.AtUtc && p.First.Timer?.Id == p.Second.Timer?.Id);

    /// <summary>Takes the new states of items it <see cref="Matches"/>.</summary>
    public void Show(IReadOnlyList<CalendarItem> items, DateTimeOffset now)
    {
        _items = items;
        foreach (var (name, item) in Names.Zip(items)) name.Show(item);
        OnPropertyChanged(nameof(IsNext));
        Update(now);
    }

    public void Update(DateTimeOffset now)
    {
        var at = _items[0].AtUtc;
        var sameDay = at.ToLocalTime().Date == now.ToLocalTime().Date;
        Time = sameDay ? Formats.Time(at) : Formats.DayTime(at);
        Until = DurationFormat.Until(at - now);
    }
}

/// <summary>A name in a row: a button that opens it, its skip in a menu, and a dot before it when it follows another.</summary>
public sealed partial class ComingUpName(CalendarItem item, bool follows, Action<Guid> open, Action<Guid, DateTimeOffset> toggleSkip)
    : ObservableObject
{
    CalendarItem _item = item;

    /// <summary>The name, state and commands the Calendar's day list uses for the same item.</summary>
    [ObservableProperty] private CalendarRowViewModel _row = new(item, open, toggleSkip);

    public bool Follows { get; } = follows;

    public void Show(CalendarItem next)
    {
        if (next.State != _item.State) Row = new CalendarRowViewModel(next, open, toggleSkip);
        _item = next;
    }
}
