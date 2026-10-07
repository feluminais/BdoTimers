using System.Collections.ObjectModel;
using System.Windows;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Seed;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>
/// Time Tracking screen: Farm and Fishing, then the user's own timers in creation order. A horse registration under way is
/// not a timer of its own here: the Horse registration card counts them.
/// </summary>
public sealed partial class CustomViewModel
{
    readonly AppServices _services;
    readonly IPanelHost _host;

    public ObservableCollection<TimerTileViewModel> Items { get; } = [];

    public CustomViewModel(AppServices services, IPanelHost host)
    {
        _services = services;
        _host = host;
        // Changed can fire on the scheduler thread (countdown completion).
        services.Timers.Changed += () => Application.Current.Dispatcher.BeginInvoke(Sync);
        Sync();
    }

    public void Refresh(DateTimeOffset now)
    {
        foreach (var tile in Items) tile.Refresh(now);
    }

    [RelayCommand]
    void NewTimer() => _host.OpenPanel(new NewTimerPanelViewModel(_services, _host));

    /// <summary>
    /// Keeps each timer's tile and updates it in place, so a new or finished timer doesn't rebuild every tile and hover
    /// state and visuals survive.
    /// </summary>
    void Sync()
    {
        var timers = _services.Timers.Current.Timers.Where(t => !t.IsBuiltIn && t.Preset != Presets.HorseRegistrationRun)
            .OrderBy(t => Presets.Rank(t.Preset)).ToList();
        var now = _services.Clock.UtcNow;
        Items.Sync(timers, (tile, timer) => tile.Id == timer.Id, timer => new TimerTileViewModel(timer, _services, _host, now),
            (tile, timer) => tile.SetTimer(timer, now));
    }
}
