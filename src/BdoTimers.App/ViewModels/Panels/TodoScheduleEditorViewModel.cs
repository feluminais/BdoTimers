using BdoTimers.Core.Model;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class TodoScheduleEditorViewModel : ObservableObject
{
    readonly Action<TodoSchedule> _save;
    TodoSchedule _schedule;
    bool _loading;

    [ObservableProperty] private Choice? _day;
    [ObservableProperty] private Choice? _zone;
    [ObservableProperty] private string _time = "00:00";
    [ObservableProperty] private bool _invalidTime;

    public bool IsWeekly { get; }
    public IReadOnlyList<Choice> Days => Choice.Days;
    public IReadOnlyList<Choice> Zones { get; } = [new("UTC", false), new("Local", true)];

    public TodoScheduleEditorViewModel(TodoCadence cadence, TodoSchedule schedule, Action<TodoSchedule> save)
    {
        IsWeekly = cadence == TodoCadence.Weekly;
        _schedule = schedule;
        _save = save;
        Load();
    }

    void Load()
    {
        _loading = true;
        Day = Days.FirstOrDefault(c => (DayOfWeek)c.Value! == _schedule.Day)
            ?? Days.First(c => (DayOfWeek)c.Value! == DayOfWeek.Thursday);
        Zone = Zones.First(c => (bool)c.Value! == _schedule.LocalTime);
        var hour = _schedule.Hour is >= 0 and <= 23 ? _schedule.Hour : 0;
        var minute = _schedule.Minute is >= 0 and <= 59 ? _schedule.Minute : 0;
        Time = Parsing.FormatTime(new TimeOnly(hour, minute));
        InvalidTime = false;
        _loading = false;
    }

    partial void OnDayChanged(Choice? value) => Save(value?.Value is DayOfWeek day ? day : null, null, null);
    partial void OnZoneChanged(Choice? value) => Save(null, value?.Value is bool local ? local : null, null);
    partial void OnTimeChanged(string value)
    {
        if (_loading) return;
        InvalidTime = !Parsing.TryParseTime(value, out var time);
        if (!InvalidTime) Save(null, null, time);
    }

    void Save(DayOfWeek? day, bool? local, TimeOnly? time)
    {
        if (_loading) return;
        var next = _schedule with
        {
            Day = day ?? _schedule.Day,
            LocalTime = local ?? _schedule.LocalTime,
            Hour = time?.Hour ?? _schedule.Hour,
            Minute = time?.Minute ?? _schedule.Minute,
        };
        if (next == _schedule) return;
        try { _save(next); _schedule = next; }
        catch
        {
            Load();
            throw;
        }
    }
}
