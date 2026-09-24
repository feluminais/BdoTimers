using System.Collections.ObjectModel;
using BdoTimers.Core.Model;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Editable weekly times. Saves whenever every row is valid; a timer always keeps at least one time.</summary>
public sealed partial class SlotListViewModel : ObservableObject
{
    readonly Action<IReadOnlyList<Slot>> _apply;

    public ObservableCollection<SlotRowViewModel> Rows { get; } = [];

    public SlotListViewModel(IEnumerable<Slot> slots, Action<IReadOnlyList<Slot>> apply)
    {
        _apply = apply;
        foreach (var slot in slots.OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.Time))
            Add(new SlotRowViewModel(slot));
    }

    [RelayCommand]
    void AddTime()
    {
        Add(new SlotRowViewModel(new Slot(DayOfWeek.Monday, new TimeOnly(20, 0))));
        TryApply();
    }

    [RelayCommand]
    void Remove(SlotRowViewModel row)
    {
        if (Rows.Count <= 1) return;
        Rows.Remove(row);
        TryApply();
    }

    void Add(SlotRowViewModel row)
    {
        row.Changed += TryApply;
        Rows.Add(row);
    }

    void TryApply()
    {
        var slots = new List<Slot>();
        foreach (var row in Rows)
        {
            row.Invalid = !Parsing.TryParseTime(row.TimeText, out var time);
            if (!row.Invalid) slots.Add(new Slot((DayOfWeek)row.Day.Value!, time));
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
