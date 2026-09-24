using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BdoTimers.App.Art;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>Bosses screen: the previous / next / followed-by strip and this week's spawn grid.</summary>
public sealed partial class BossesViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    object? _gridKey;
    int _tooltipMinute = -1;

    public StripTileViewModel Previous { get; } = new("Previous", elapsed: true);
    public StripTileViewModel Next { get; } = new("Next", elapsed: false);
    public StripTileViewModel FollowedBy { get; } = new("Followed by", elapsed: false);
    public ObservableCollection<DayHeaderViewModel> Days { get; } = [];
    public ObservableCollection<GridRowViewModel> Rows { get; } = [];

    public BossesViewModel(AppServices services, IPanelHost host)
    {
        _services = services;
        _host = host;
        services.UiClock.Tick += Refresh;
        // Changed can fire on the scheduler thread (countdown completion).
        services.Timers.Changed += () => Application.Current.Dispatcher.BeginInvoke(() => Refresh(DateTimeOffset.UtcNow));
        Refresh(DateTimeOffset.UtcNow);
    }

    void Refresh(DateTimeOffset now)
    {
        var data = _services.Timers.Current;
        var board = BossBoard.Build(data, now);
        Previous.Update(board.Previous, now, _services.Art, OpenBoss);
        Next.Update(board.Next, now, _services.Art, OpenBoss);
        FollowedBy.Update(board.FollowedBy, now, _services.Art, OpenBoss);

        // The grid only changes when the data, the next spawn or the local day does.
        var key = (data, board.Next?.AtUtc, DateOnly.FromDateTime(now.LocalDateTime));
        if (!key.Equals(_gridKey))
        {
            _gridKey = key;
            RebuildGrid(data, now);
        }
        if (now.Minute != _tooltipMinute)
        {
            _tooltipMinute = now.Minute;
            foreach (var entry in Rows.SelectMany(r => r.Cells).SelectMany(c => c.Entries)) entry.RefreshTooltip(now);
        }
    }

    void RebuildGrid(AppData data, DateTimeOffset now)
    {
        var grid = WeekGrid.Build(data, now, TimeZoneInfo.Local);
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        Days.Clear();
        for (var i = 0; i < 7; i++)
        {
            var date = grid.WeekStart.AddDays(i);
            Days.Add(new DayHeaderViewModel(date.ToString("ddd d", CultureInfo.InvariantCulture), date == today));
        }
        Rows.Clear();
        foreach (var row in grid.Rows)
        {
            var cells = row.Days.Select((entries, i) => new GridCellViewModel(
                grid.WeekStart.AddDays(i) == today,
                entries.Any(e => e.State == CellState.Next),
                entries.Select(e => new GridEntryViewModel(e, _services.Timers, OpenBoss)).ToList())).ToList();
            Rows.Add(new GridRowViewModel(row.Time.ToString("HH:mm", CultureInfo.InvariantCulture), cells));
        }
        _tooltipMinute = -1;
    }

    void OpenBoss(Guid id)
    {
        if (_services.Timers.Current.Timers.FirstOrDefault(t => t.Id == id) is { } boss)
            _host.OpenPanel(new BossPanelViewModel(_services, boss));
    }
}

/// <summary>A boss name that opens the boss panel when clicked.</summary>
public sealed class BossLink(Guid id, string name, Action<Guid> open)
{
    public string Name => name;
    public IRelayCommand OpenCommand { get; } = new RelayCommand(() => open(id));
}

public sealed partial class StripTileViewModel(string caption, bool elapsed) : ObservableObject
{
    string _signature = "";

    [ObservableProperty] private bool _hasSpawn;
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private IReadOnlyList<BossLink> _names = [];
    [ObservableProperty] private IReadOnlyList<ImageSource> _images = [];
    [ObservableProperty] private string _clock = "";
    [ObservableProperty] private bool _skipped;

    public void Update(SpawnGroup? group, DateTimeOffset now, ArtLibrary art, Action<Guid> open)
    {
        HasSpawn = group is not null;
        if (group is null) return;
        var signature = $"{group.AtUtc:O}|{string.Join(",", group.Bosses.Select(b => b.Id))}";
        if (signature != _signature)
        {
            _signature = signature;
            Names = group.Bosses.Select(b => new BossLink(b.Id, b.Name, open)).ToList();
            Images = art.For(group.Bosses);
            Label = $"{caption} · {group.AtUtc.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture)}";
        }
        Skipped = group.Skipped;
        Clock = elapsed ? "−" + DurationFormat.Clock(now - group.AtUtc) : DurationFormat.Clock(group.AtUtc - now);
    }
}

public sealed record DayHeaderViewModel(string Label, bool IsToday);

public sealed record GridRowViewModel(string Time, IReadOnlyList<GridCellViewModel> Cells);

public sealed record GridCellViewModel(bool IsToday, bool IsNext, IReadOnlyList<GridEntryViewModel> Entries);

public sealed partial class GridEntryViewModel : ObservableObject
{
    readonly GridEntry _entry;

    [ObservableProperty] private string _tooltip = "";

    public string Name => _entry.Boss.Name;
    public CellState State => _entry.State;
    public bool CanSkip => _entry.State is CellState.Upcoming or CellState.Next or CellState.Skipped;
    public string SkipLabel => _entry.State == CellState.Skipped ? "Unskip" : "Skip this spawn";
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand ToggleSkipCommand { get; }

    public GridEntryViewModel(GridEntry entry, Core.Storage.TimerStore store, Action<Guid> open)
    {
        _entry = entry;
        OpenCommand = new RelayCommand(() => open(entry.Boss.Id));
        ToggleSkipCommand = new RelayCommand(() => store.ToggleMute(entry.Boss.Id, entry.AtUtc));
    }

    public void RefreshTooltip(DateTimeOffset now)
    {
        var when = _entry.AtUtc.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture);
        var relative = _entry.AtUtc > now ? $"in {DurationFormat.Countdown(_entry.AtUtc - now)}" : "passed";
        var note = _entry.State switch
        {
            CellState.Skipped => " · skipped",
            CellState.Unfollowed => " · not followed",
            _ => "",
        };
        Tooltip = $"{Name} · {when} · {relative}{note}";
    }
}
