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
    /// Keeps each timer's tile and updates it in place, so a new or finished timer doesn't rebuild every tile and hover
    /// state and visuals survive. The "+ New timer" tile stays last.
    /// </summary>
    void Sync()
    {
        var timers = _services.Timers.Current.Timers.Where(t => !t.IsBuiltIn).OrderBy(t => Presets.Rank(t.Preset));
        var now = DateTimeOffset.UtcNow;
        if (Items.Count == 0) Items.Add(this);
        Items.Sync(timers, (item, timer) => item is TimerTileViewModel tile && tile.Id == timer.Id,
            timer => new TimerTileViewModel(timer, _services, _host, now),
            (item, timer) => ((TimerTileViewModel)item).SetTimer(timer, now), keepLast: 1);
    }
}
