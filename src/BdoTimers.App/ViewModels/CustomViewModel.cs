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

    /// <summary>Updates tiles in place when the set of timers is unchanged, so hover state and visuals survive.</summary>
    void Sync()
    {
        var timers = _services.Timers.Current.Timers.Where(t => !t.IsBuiltIn).OrderBy(t => Presets.Rank(t.Preset)).ToList();
        var tiles = Items.OfType<TimerTileViewModel>().ToList();
        var now = DateTimeOffset.UtcNow;
        if (tiles.Select(t => t.Id).SequenceEqual(timers.Select(t => t.Id)))
        {
            for (var i = 0; i < timers.Count; i++) tiles[i].SetTimer(timers[i], now);
            return;
        }
        Items.Clear();
        foreach (var timer in timers) Items.Add(new TimerTileViewModel(timer, _services, _host, now));
        Items.Add(this);
    }
}
