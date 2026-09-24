using System.Collections.ObjectModel;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>"Alert before" chips: common lead times plus any the timer already uses, and a field to add others.</summary>
public sealed partial class LeadChipsViewModel : ObservableObject
{
    static readonly int[] Presets = [30, 15, 10, 5, 1, 0];

    readonly Action<IReadOnlyList<int>> _apply;

    [ObservableProperty] private string _newValue = "";
    [ObservableProperty] private bool _newValueInvalid;

    public ObservableCollection<LeadChip> Chips { get; } = [];

    public LeadChipsViewModel(IReadOnlyList<int> selected, Action<IReadOnlyList<int>> apply)
    {
        _apply = apply;
        foreach (var minutes in Presets.Union(selected).OrderDescending())
            AddChip(minutes, selected.Contains(minutes));
    }

    partial void OnNewValueChanged(string value) => NewValueInvalid = false;

    [RelayCommand]
    void Add()
    {
        if (!Parsing.TryParseMinutes(NewValue, 0, 1440, out var minutes))
        {
            NewValueInvalid = true;
            return;
        }
        if (Chips.FirstOrDefault(c => c.Minutes == minutes) is { } existing)
        {
            existing.IsOn = true;
        }
        else
        {
            var index = Chips.TakeWhile(c => c.Minutes > minutes).Count();
            AddChip(minutes, true, index);
            Apply();
        }
        NewValue = "";
    }

    void AddChip(int minutes, bool on, int? index = null)
    {
        var chip = new LeadChip(minutes) { IsOn = on };
        chip.Toggled += Apply;
        Chips.Insert(index ?? Chips.Count, chip);
    }

    void Apply() => _apply(Chips.Where(c => c.IsOn).Select(c => c.Minutes).ToList());
}

public sealed partial class LeadChip(int minutes) : ObservableObject
{
    [ObservableProperty] private bool _isOn;

    public event Action? Toggled;

    public int Minutes => minutes;
    public string Label => minutes == 0 ? "At spawn" : minutes.ToString();

    partial void OnIsOnChanged(bool value) => Toggled?.Invoke();
}
