using System.Collections.ObjectModel;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>
/// The Today screen: the next spawn, what comes in the next 24 hours, the timers that are running, the daily and weekly
/// tasks and Garmoth's week, put together from what the other screens already keep. The 1 Hz tick refreshes it while the window is shown.
/// </summary>
public sealed partial class TodayViewModel : ObservableObject
{
    /// <summary>Running timers the panel has room for.</summary>
    const int MaxRunning = 4;

    readonly AppServices _services;
    readonly CustomViewModel _custom;
    readonly Action _showTimers;

    public HeroViewModel Hero { get; }
    public ComingUpViewModel ComingUp { get; }
    public TaskPanelViewModel Daily { get; }
    public TaskPanelViewModel Weekly { get; }
    public GarmothViewModel Garmoth { get; }
    /// <summary>The Timers screen's countdowns and stopwatches that are running or paused, and Horse registration while one is under way.</summary>
    public ObservableCollection<TimerTileViewModel> Running { get; } = [];
    [ObservableProperty] private bool _hasRunning;

    public TodayViewModel(AppServices services, IPanelHost host, CustomViewModel custom, TodoViewModel todo,
        Action showTimers, Action showTodo, Action openFollowing)
    {
        _services = services;
        _custom = custom;
        _showTimers = showTimers;
        Hero = new HeroViewModel(services, id => TimerPanels.Open(services, host, id), openFollowing);
        ComingUp = new ComingUpViewModel(services, host);
        Daily = new TaskPanelViewModel(services, todo, showTodo, TodoCadence.Daily);
        Weekly = new TaskPanelViewModel(services, todo, showTodo, TodoCadence.Weekly);
        Garmoth = new GarmothViewModel(services);
        Refresh(services.Clock.UtcNow);
    }

    public void Refresh(DateTimeOffset now)
    {
        var data = _services.Timers.Current;
        Hero.Update(_services.Boards.Get(data, now), now, _services.Settings.Current.DefaultLeadTimesMinutes);
        ComingUp.Refresh(now);
        var running = _custom.Items.Where(t => t.CanStop).Take(MaxRunning).ToList();
        Running.Sync(running, ReferenceEquals, tile => tile, (_, _) => { });
        HasRunning = Running.Count > 0;
    }

    [RelayCommand]
    void ShowTimers() => _showTimers();
}
