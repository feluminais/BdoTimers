using System.Collections.ObjectModel;
using System.Windows;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Seed;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>Timers screen: Farm and Fishing, then the user's own timers in creation order, then the "+ New timer" tile.</summary>
public sealed partial class CustomViewModel
{
    readonly AppServices _services;
    readonly IPanelHost _host;

    /// <summary>Timer tiles followed by this view model itself, which the view renders as the "+ New timer" tile.</summary>
    public ObservableCollection<object> Items { get; } = [];

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
        foreach (var tile in Items.OfType<TimerTileViewModel>()) tile.Refresh(now);
    }

    [RelayCommand]
    void NewTimer() => _host.OpenPanel(new NewTimerPanelViewModel(_services, _host));

    /// <summary>
    /// Inserts, moves and removes tiles to match the timers and updates the others in place, so a new or finished timer
    /// doesn't rebuild every tile and hover state and visuals survive.
    /// </summary>
    void Sync()
    {
        var timers = _services.Timers.Current.Timers.Where(t => !t.IsBuiltIn).OrderBy(t => Presets.Rank(t.Preset)).ToList();
        var now = _services.Clock.UtcNow;
        if (Items.Count == 0) Items.Add(this);
        for (var i = 0; i < timers.Count; i++)
        {
            var at = IndexOfTile(timers[i].Id, from: i);
            if (at < 0) Items.Insert(i, new TimerTileViewModel(timers[i], _services, _host, now));
            else
            {
                if (at != i) Items.Move(at, i);
                ((TimerTileViewModel)Items[i]).SetTimer(timers[i], now);
            }
        }
        // Left between the tiles and the "+ New timer" tile: the tiles of deleted timers.
        while (Items.Count > timers.Count + 1) Items.RemoveAt(timers.Count);
    }

    int IndexOfTile(Guid id, int from)
    {
        for (var i = from; i < Items.Count; i++)
            if (Items[i] is TimerTileViewModel tile && tile.Id == id) return i;
        return -1;
    }
}
