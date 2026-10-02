using System.Collections.ObjectModel;
using BdoTimers.Core.Model;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Editable weekly times, with preset-specific limits. Saves whenever every row is valid.</summary>
public sealed partial class SlotListViewModel : ObservableObject
{
    readonly Action<IReadOnlyList<Slot>> _apply;
    readonly int _minimum;
    readonly int? _maximum;

    public ObservableCollection<SlotRowViewModel> Rows { get; } = [];
    public bool MayAddTime => _maximum is null || Rows.Count < _maximum;

    public SlotListViewModel(IEnumerable<Slot> slots, Action<IReadOnlyList<Slot>> apply, int minimum = 1, int? maximum = null)
    {
        _apply = apply;
        _minimum = minimum;
        _maximum = maximum;
        foreach (var slot in slots.OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.Time))
            Add(new SlotRowViewModel(slot));
    }

    [RelayCommand(CanExecute = nameof(CanAddTime))]
    void AddTime()
    {
        Add(new SlotRowViewModel(NextAvailableSlot()));
        LimitsChanged();
        TryApply();
    }

    Slot NextAvailableSlot()
    {
        HashSet<Slot> occupied = [];
        foreach (var row in Rows)
            if (row.Day.Value is DayOfWeek day && Parsing.TryParseTime(row.TimeText, out var time))
                occupied.Add(new Slot(day, time));
        // Prefer 20:00 on each unused day, then other hours, then remaining minutes.
        for (var minute = 0; minute < 60; minute++)
            for (var hour = 0; hour < 24; hour++)
                for (var day = 0; day < 7; day++)
                {
                    var slot = new Slot((DayOfWeek)((day + 1) % 7), new TimeOnly((20 + hour) % 24, minute));
                    if (!occupied.Contains(slot)) return slot;
                }
        return new Slot(DayOfWeek.Monday, new TimeOnly(20, 0));
    }

    bool CanAddTime() => MayAddTime;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    void Remove(SlotRowViewModel row)
    {
        Rows.Remove(row);
        LimitsChanged();
        TryApply();
    }

    bool CanRemove(SlotRowViewModel row) => Rows.Count > _minimum && Rows.Contains(row);

    void LimitsChanged()
    {
        OnPropertyChanged(nameof(MayAddTime));
        AddTimeCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    void Add(SlotRowViewModel row)
    {
        row.Changed += TryApply;
        Rows.Add(row);
    }

    void TryApply()
    {
        var slots = new List<Slot>();
        HashSet<Slot> seen = [];
        foreach (var row in Rows)
        {
            if (row.Day.Value is not DayOfWeek day || !Parsing.TryParseTime(row.TimeText, out var time))
            {
                row.Invalid = true;
                continue;
            }
            var slot = new Slot(day, time);
            row.Invalid = !seen.Add(slot);
            if (!row.Invalid) slots.Add(slot);
        }
        if (slots.Count == Rows.Count) _apply(slots);
    }
}

public sealed partial class SlotRowViewModel : ObservableObject
{
    [ObservableProperty] private Choice _day;
    [ObservableProperty] private string _timeText;
    [ObservableProperty] private bool _invalid;

    public event Action? Changed;

    public IReadOnlyList<Choice> Days => Choice.Days;

    public SlotRowViewModel(Slot slot)
    {
        _day = Choice.Days.First(d => (DayOfWeek)d.Value! == slot.Day);
        _timeText = Parsing.FormatTime(slot.Time);
    }

    partial void OnDayChanged(Choice value) => Changed?.Invoke();
    partial void OnTimeTextChanged(string value) => Changed?.Invoke();
}
