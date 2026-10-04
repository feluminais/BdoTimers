using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>
/// Calendar screen: a month of everything the app schedules, in local time, and the chosen day's list. Month cells show
/// the user's own timers and events with a count of boss spawns; the day list shows every item.
/// </summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    object? _key;
    /// <summary>When the shown states next change: the first item after the last build.</summary>
    DateTimeOffset? _changesAt;
    CalendarMonth? _month;
    DateOnly _shown;
    DateOnly _selected;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _selectedTitle = "";
    [ObservableProperty] private bool _hasNoItems;
    [ObservableProperty] private bool _canAddEvent;

    public ObservableCollection<string> WeekDays { get; } = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];
    public ObservableCollection<CalendarRowViewModel> Rows { get; } = [];

    public CalendarViewModel(AppServices services, IPanelHost host)
    {
        _services = services;
        _host = host;
        for (var i = 0; i < 42; i++) Days.Add(new CalendarDayViewModel(Select));
        var today = Today(services.Clock.UtcNow);
        _shown = new DateOnly(today.Year, today.Month, 1);
        _selected = today;
        // Changed can fire on the scheduler thread (countdown completion).
        services.Timers.Changed += () => Application.Current.Dispatcher.BeginInvoke(() => Refresh(services.Clock.UtcNow));
        Refresh(services.Clock.UtcNow);
    }

    CalendarSettings Filters => _services.Settings.Current.Calendar;

    public bool ShowBosses { get => Filters.ShowBosses; set => SetFilter(f => f with { ShowBosses = value }); }
    public bool ShowTimers { get => Filters.ShowTimers; set => SetFilter(f => f with { ShowTimers = value }); }
    public bool ShowEvents { get => Filters.ShowEvents; set => SetFilter(f => f with { ShowEvents = value }); }
    public bool ShowResets { get => Filters.ShowResets; set => SetFilter(f => f with { ShowResets = value }); }

    void SetFilter(Func<CalendarSettings, CalendarSettings> change)
    {
        _services.Settings.Update(s => s with { Calendar = change(s.Calendar) });
        Refresh(_services.Clock.UtcNow);
    }

    static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime);

    /// <summary>Builds the month again when what it shows changed or an item has just passed; otherwise does nothing.</summary>
    public void Refresh(DateTimeOffset now)
    {
        var data = _services.Timers.Current;
        var settings = _services.Settings.Current;
        var today = Today(now);
        var key = (data, settings, _shown, _selected, today, TimeZoneInfo.Local.Id);
        if (key.Equals(_key) && !(now >= _changesAt)) return;
        _key = key;
        _month = CalendarQuery.Month(data, settings, _shown.Year, _shown.Month, now, TimeZoneInfo.Local, _services.Boards);
        _changesAt = _month.Days.SelectMany(d => d.Items).Select(i => (DateTimeOffset?)i.AtUtc).FirstOrDefault(at => at > now);
        Title = _shown.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        foreach (var (day, model) in _month.Days.Zip(Days))
            model.Show(day, Visible(day.Items).ToList(), day.Date == today, day.Date == _selected);
        ShowSelected(today);
        OnPropertyChanged(nameof(ShowBosses));
        OnPropertyChanged(nameof(ShowTimers));
        OnPropertyChanged(nameof(ShowEvents));
        OnPropertyChanged(nameof(ShowResets));
    }

    IEnumerable<CalendarItem> Visible(IEnumerable<CalendarItem> items)
    {
        var f = Filters;
        return items.Where(i => i.Kind switch
        {
            CalendarKind.Boss => f.ShowBosses,
            CalendarKind.Weekly or CalendarKind.Countdown => f.ShowTimers,
            CalendarKind.Event => f.ShowEvents,
            _ => f.ShowResets,
        });
    }

    void ShowSelected(DateOnly today)
    {
        SelectedTitle = _selected.ToString("dddd d MMMM", CultureInfo.InvariantCulture);
        CanAddEvent = _selected >= today;
        var items = _month?.Days.FirstOrDefault(d => d.Date == _selected)?.Items ?? [];
        Rows.Clear();
        foreach (var item in Visible(items)) Rows.Add(new CalendarRowViewModel(item, Open, ToggleSkip));
        HasNoItems = Rows.Count == 0;
    }

    void Select(DateOnly date)
    {
        _selected = date;
        if (date.Year != _shown.Year || date.Month != _shown.Month) _shown = new DateOnly(date.Year, date.Month, 1);
        Refresh(_services.Clock.UtcNow);
    }

    [RelayCommand]
    void PreviousMonth() => ShowMonth(_shown.AddMonths(-1));

    [RelayCommand]
    void NextMonth() => ShowMonth(_shown.AddMonths(1));

    [RelayCommand]
    void GoToToday() => Select(Today(_services.Clock.UtcNow));

    /// <summary>Shows <paramref name="first"/>'s month with today chosen when it's in it, else the month's first day.</summary>
    void ShowMonth(DateOnly first)
    {
        var today = Today(_services.Clock.UtcNow);
        Select(today.Year == first.Year && today.Month == first.Month ? today : first);
    }

    /// <summary>A one-time event on the chosen day at 20:00, or the next whole hour when that has passed today.</summary>
    [RelayCommand]
    void NewEvent()
    {
        var now = TimeZoneInfo.ConvertTime(_services.Clock.UtcNow, TimeZoneInfo.Local);
        var time = new TimeOnly(20, 0);
        if (_selected == DateOnly.FromDateTime(now.DateTime) && now.TimeOfDay >= time.ToTimeSpan())
            time = now.Hour < 23 ? new TimeOnly(now.Hour + 1, 0) : new TimeOnly(23, 59);
        var timer = new TimerDef
        {
            Name = "New event", Kind = TimerKind.OneTime,
            OneTime = new OneTimeSpec { Date = _selected, Time = time, TimeZoneId = TimeZoneInfo.Local.Id },
        };
        _services.Timers.Upsert(timer);
        _host.OpenPanel(new CustomPanelViewModel(_services, _host, timer));
    }

    void Open(Guid id)
    {
        if (_services.Timers.Current.Timers.FirstOrDefault(t => t.Id == id) is not { } timer) return;
        _host.OpenPanel(timer.IsBuiltIn
            ? new BossPanelViewModel(_services, _host, timer)
            : new CustomPanelViewModel(_services, _host, timer));
    }

    void ToggleSkip(Guid id, DateTimeOffset at) => _services.Timers.ToggleMute(id, at);
}

/// <summary>A day in the month grid.</summary>
public sealed partial class CalendarDayViewModel(Action<DateOnly> select) : ObservableObject
{
    /// <summary>Lines of own timers and events a cell has room for, "+N more" included.</summary>
    const int CellLines = 3;

    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private string _number = "";
    [ObservableProperty] private bool _inMonth;
    [ObservableProperty] private bool _isToday;
    [ObservableProperty] private bool _isSelected;
    /// <summary>The next boss spawn is on this day.</summary>
    [ObservableProperty] private bool _isNext;
    [ObservableProperty] private IReadOnlyList<CalendarLine> _lines = [];
    [ObservableProperty] private string? _more;
    [ObservableProperty] private string? _bossSummary;
    [ObservableProperty] private string _automationName = "";

    [RelayCommand]
    void Select() => select(Date);

    public void Show(CalendarDay day, IReadOnlyList<CalendarItem> items, bool isToday, bool isSelected)
    {
        Date = day.Date;
        Number = day.Date.Day.ToString(CultureInfo.InvariantCulture);
        InMonth = day.InMonth;
        IsToday = isToday;
        IsSelected = isSelected;
        IsNext = items.Any(i => i.State == CellState.Next);
        // Own timers and events first: bosses spawn every day, so they're counted rather than listed.
        var own = items.Where(i => i.Kind is not (CalendarKind.Boss or CalendarKind.DailyReset)).ToList();
        var lines = own.Take(own.Count > CellLines ? CellLines - 1 : CellLines)
            .Select(i => new CalendarLine(Formats.Time(i.AtUtc), CalendarRowViewModel.NameOf(i), i.State)).ToList();
        if (!Lines.SequenceEqual(lines)) Lines = lines;
        More = own.Count > lines.Count ? $"+{own.Count - lines.Count} more" : null;
        var spawns = items.Where(i => i.Kind == CalendarKind.Boss).Select(i => i.AtUtc).Distinct().Count();
        BossSummary = spawns switch { 0 => null, 1 => "1 boss spawn", _ => $"{spawns} boss spawns" };
        AutomationName = day.Date.ToString("dddd d MMMM", CultureInfo.InvariantCulture);
    }
}

/// <summary>A line in a month cell.</summary>
public sealed record CalendarLine(string Time, string Name, CellState State);

/// <summary>An item in the chosen day's list; clicking opens its panel, and a boss or weekly timer can be skipped.</summary>
public sealed partial class CalendarRowViewModel(CalendarItem item, Action<Guid> open, Action<Guid, DateTimeOffset> toggleSkip)
{
    public string Time { get; } = Formats.Time(item.AtUtc);
    public string Name { get; } = NameOf(item);
    public string Detail { get; } = item.Kind switch
    {
        CalendarKind.Boss => "Boss",
        CalendarKind.Weekly => "Weekly",
        CalendarKind.Event => "Event",
        CalendarKind.Countdown => "Ends",
        _ => "To-do",
    };
    public CellState State { get; } = item.State;
    public bool CanOpen { get; } = item.Timer is not null;
    public bool CanSkip { get; } = item.Kind is CalendarKind.Boss or CalendarKind.Weekly
        && item.State is CellState.Upcoming or CellState.Next or CellState.Skipped;
    public string SkipLabel => State == CellState.Skipped ? "Unskip" : "Skip this one";
    public string Tooltip { get; } = item.AtUtc.ToLocalTime().ToString("ddd d MMM HH:mm", CultureInfo.InvariantCulture) + item.State switch
    {
        CellState.Skipped => " · skipped",
        CellState.Unfollowed => " · alerts off",
        _ => "",
    };

    public static string NameOf(CalendarItem item) => item.Kind switch
    {
        CalendarKind.DailyReset => "Daily reset",
        CalendarKind.WeeklyReset => "Weekly reset",
        _ => OccurrenceSource.NameAt(item.Timer!, item.AtUtc),
    };

    [RelayCommand]
    void Open()
    {
        if (item.Timer is { } timer) open(timer.Id);
    }

    [RelayCommand]
    void ToggleSkip()
    {
        if (item.Timer is { } timer) toggleSkip(timer.Id, item.AtUtc);
    }
}
