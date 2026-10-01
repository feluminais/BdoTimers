using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class TimetableChangeRow : ObservableObject
{
    public TimetableChange Change { get; }
    public string Name => Change.Name;
    public string? CustomLabel => Change.HasCustomTimes ? "Custom times" : null;
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    [ObservableProperty] private Choice _apply;
    public string Details { get; }

    public TimetableChangeRow(TimetableChange change)
    {
        Change = change;
        _apply = Choice.For(!change.HasCustomTimes);
        Details = Describe(change);
    }

    static string Describe(TimetableChange change)
    {
        if (change.Replacement is null) return "Remove from timetable";
        if (change.Current is null) return $"Add · {Times(change.Replacement.Slots)}";
        if (change.Current.TimeZoneId != change.Replacement.TimeZoneId)
            return $"{change.Current.TimeZoneId} → {change.Replacement.TimeZoneId}\n{Times(change.Replacement.Slots)}";
        var removed = change.Current.Slots.Except(change.Replacement.Slots).ToList();
        var added = change.Replacement.Slots.Except(change.Current.Slots).ToList();
        return string.Join("\n", new[] { removed.Count > 0 ? $"− {Times(removed)}" : null,
            added.Count > 0 ? $"+ {Times(added)}" : null }.OfType<string>());
    }

    static string Times(IEnumerable<Slot> slots) => string.Join(" · ", slots.OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.Time)
        .Select(s => $"{s.Day.ToString()[..3]} {s.Time:HH:mm}"));
}
