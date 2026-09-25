using System.Globalization;
using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>A boss in the Bosses section under the table; clicking it opens the boss panel.</summary>
public sealed partial class BossTileViewModel : ObservableObject
{
    [ObservableProperty] private bool _isOff;
    [ObservableProperty] private bool _ownSettings;
    [ObservableProperty] private string _detail = "";

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<ArtPicture> Images { get; }
    public IRelayCommand OpenCommand { get; }

    public BossTileViewModel(TimerDef boss, ArtPicture image, Action<Guid> open, DateTimeOffset now)
    {
        Id = boss.Id;
        Name = boss.Name;
        Images = [image];
        OpenCommand = new RelayCommand(() => open(Id));
        Show(boss, now);
    }

    /// <summary>"Next · Fri 03:00", or "Alerts off" for a boss that doesn't alert.</summary>
    public void Show(TimerDef boss, DateTimeOffset now)
    {
        IsOff = !boss.Enabled;
        OwnSettings = boss.Alerts.OverridesDefaults;
        if (IsOff)
        {
            Detail = "Alerts off";
            return;
        }
        var next = OccurrenceSource.Between(boss, now, now + TimeSpan.FromDays(8)).Cast<DateTimeOffset?>().FirstOrDefault();
        Detail = next is { } at ? "Next · " + at.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture) : "No upcoming spawns";
    }
}
