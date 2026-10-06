using System.Collections.ObjectModel;
using System.Windows;
using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>The bosses of the selected region, each with a switch for its alerts, and a way to add one.</summary>
public sealed partial class FollowingPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Action _changed;

    public ObservableCollection<FollowingRowViewModel> Bosses { get; } = [];
    /// <summary>A switch saves through the boss panel's own path, which also makes the voice lines, so it is busy until that ends.</summary>
    public EditorSave Saving { get; } = new();
    [ObservableProperty] private string _summary = "";

    public FollowingPanelViewModel(AppServices services, IPanelHost host)
    {
        _services = services;
        _host = host;
        // Changed can fire on the scheduler thread.
        _changed = () => Application.Current.Dispatcher.BeginInvoke(Reload);
        services.Timers.Changed += _changed;
        Reload();
    }

    public void OnClosed() => _services.Timers.Changed -= _changed;

    void Reload()
    {
        var data = _services.Timers.Current;
        var now = _services.Clock.UtcNow;
        var bosses = BossOrder.Sort(data.Timers.Where(t => BossRegions.IsSelected(data, t))).ToList();
        Bosses.Sync(bosses, (row, boss) => row.Id == boss.Id, boss => new FollowingRowViewModel(boss, _services, _host, Saving, now),
            (row, boss) => row.Show(boss, now));
        Summary = $"{bosses.Count(b => b.Enabled)} of {bosses.Count}";
    }

    /// <summary>Adds a boss to the selected region and opens it to be named and timed.</summary>
    [RelayCommand]
    void AddBoss()
    {
        var boss = _services.Timers.AddBoss();
        _host.OpenPanel(new BossPanelViewModel(_services, _host, boss));
    }
}

public sealed partial class FollowingRowViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly EditorSave _saving;
    /// <summary>Set while the row follows the saved boss, so that isn't taken for the user flipping the switch.</summary>
    bool _syncing;

    public Guid Id { get; }
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images = [];
    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _ownSettings;

    public FollowingRowViewModel(TimerDef boss, AppServices services, IPanelHost host, EditorSave saving, DateTimeOffset now)
    {
        _services = services;
        _host = host;
        _saving = saving;
        Id = boss.Id;
        Show(boss, now);
    }

    /// <summary>The next spawn, or "Alerts off" for a boss that doesn't alert.</summary>
    public void Show(TimerDef boss, DateTimeOffset now)
    {
        _syncing = true;
        Name = boss.Name;
        IsOn = boss.Enabled;
        OwnSettings = boss.Alerts.OverridesDefaults;
        var next = OccurrenceSource.Next(boss, now);
        Detail = !boss.Enabled ? "Alerts off" : next is { } at ? Formats.DayTime(at) : "No upcoming spawns";
        var image = _services.Art.For(boss);
        // A fresh picture for the same art would redraw the row.
        if (Images is not [var shown] || shown.Source != image.Source || shown.Focus != image.Focus) Images = [image];
        _syncing = false;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (!_syncing) _ = SetAsync(value);
    }

    async Task SetAsync(bool value)
    {
        var boss = _services.Timers.Current.Timers.FirstOrDefault(t => t.Id == Id);
        if (boss is null || boss.Enabled == value) return;
        var editor = new TimerEditor(_services, boss);
        editor.Modify(t => t with { Enabled = value });
        if (await _saving.RunAsync(editor.SaveAsync)) return;
        _syncing = true;
        IsOn = !value;
        _syncing = false;
    }

    [RelayCommand]
    void Open()
    {
        if (_services.Timers.Current.Timers.FirstOrDefault(t => t.Id == Id) is { } boss)
            _host.OpenPanel(new BossPanelViewModel(_services, _host, boss));
    }
}
