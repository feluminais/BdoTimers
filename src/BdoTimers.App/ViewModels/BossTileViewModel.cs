using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>A boss in the Bosses section under the table; clicking it opens the boss panel.</summary>
public sealed partial class BossTileViewModel : ObservableObject
{
    readonly Action<Guid> _open;

    [ObservableProperty] private bool _isOff;
    [ObservableProperty] private bool _ownSettings;
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images = [];

    public Guid Id { get; }

    public BossTileViewModel(TimerDef boss, ArtPicture image, Action<Guid> open, DateTimeOffset now)
    {
        _open = open;
        Id = boss.Id;
        Show(boss, image, now);
    }

    [RelayCommand]
    void Open() => _open(Id);

    /// <summary>"Next · Fri 03:00", or "Alerts off" for a boss that doesn't alert.</summary>
    public void Show(TimerDef boss, ArtPicture image, DateTimeOffset now)
    {
        Name = boss.Name;
        // A fresh picture for the same art would redraw the tile every minute.
        if (Images is not [var shown] || shown.Source != image.Source || shown.Focus != image.Focus) Images = [image];
        IsOff = !boss.Enabled;
        OwnSettings = boss.Alerts.OverridesDefaults;
        if (IsOff)
        {
            Detail = "Alerts off";
            return;
        }
        var next = OccurrenceSource.Next(boss, now);
        Detail = next is { } at ? "Next · " + Formats.DayTime(at) : "No upcoming spawns";
    }
}
