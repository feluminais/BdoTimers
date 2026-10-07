using System.Windows;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>One of Garmoth's weekly kills: a button that marks it and the ones before it, or takes it back.</summary>
public sealed partial class GarmothKillViewModel(int number, Action<int> press) : ObservableObject
{
    public int Number { get; } = number;
    public string AutomationName => $"Garmoth kill {Number}";
    [ObservableProperty] private bool _isDone;

    [RelayCommand]
    void Press() => press(Number);
}

/// <summary>
/// Today's Garmoth panel, there while the tracker is on in Settings: his kills this week as buttons, and once the third is
/// marked, when he is back. It follows the week kept in the timer data.
/// </summary>
public sealed partial class GarmothViewModel : ObservableObject
{
    readonly AppServices _services;

    public IReadOnlyList<GarmothKillViewModel> Kills { get; }
    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private string _summary = "";
    /// <summary>"Back Thu 03:00" while all the kills are marked; otherwise null.</summary>
    [ObservableProperty] private string? _backText;

    public GarmothViewModel(AppServices services)
    {
        _services = services;
        Kills = Enumerable.Range(1, GarmothTracker.Limit).Select(n => new GarmothKillViewModel(n, Press)).ToList();
        services.Settings.Changed += () => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        services.Timers.Changed += () => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    void Press(int kill) => _services.Timers.MarkGarmoth(kill, _services.Clock.UtcNow);

    void Refresh()
    {
        var week = _services.Timers.Current.Garmoth;
        IsOn = _services.Settings.Current.GarmothTracker;
        foreach (var kill in Kills) kill.IsDone = week.Kills >= kill.Number;
        Summary = $"{week.Kills}/{GarmothTracker.Limit}";
        BackText = week is { Kills: >= GarmothTracker.Limit } && week.ResetUtc > _services.Clock.UtcNow
            ? "Back " + Formats.DayTime(week.ResetUtc) : null;
    }
}
