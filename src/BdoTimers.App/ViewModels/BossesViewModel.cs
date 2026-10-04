using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using BdoTimers.App.Art;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>Bosses screen: the previous / next / followed-by strip, this week's spawn grid and a tile per boss.</summary>
public sealed partial class BossesViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    object? _gridKey;
    AppData? _gridData;
    string _gridContent = "";
    DateOnly _gridWeekStart;
    DateOnly _gridToday;
    /// <summary>The data the tiles last showed; cleared each minute so their next-spawn times move on.</summary>
    AppData? _tilesData;
    int _tooltipMinute = -1;

    public StripTileViewModel Previous { get; } = new("Previous", elapsed: true);
    public StripTileViewModel Next { get; } = new("Next", elapsed: false);
    public StripTileViewModel FollowedBy { get; } = new("Followed by", elapsed: false);
    public ObservableCollection<DayHeaderViewModel> Days { get; } = [];
    public ObservableCollection<GridRowViewModel> Rows { get; } = [];
    public ObservableCollection<BossTileViewModel> Tiles { get; } = [];

    public BossesViewModel(AppServices services, IPanelHost host)
    {
        _services = services;
        _host = host;
        // Changed can fire on the scheduler thread (countdown completion).
        services.Timers.Changed += () => Application.Current.Dispatcher.BeginInvoke(() => Refresh(services.Clock.UtcNow));
        Refresh(services.Clock.UtcNow);
    }

    public void Refresh(DateTimeOffset now)
    {
        var data = _services.Timers.Current;
        var board = _services.Boards.Get(data, now);
        Previous.Update(board.Previous, now, _services.Art, OpenBoss);
        Next.Update(board.Next, now, _services.Art, OpenBoss);
        FollowedBy.Update(board.FollowedBy, now, _services.Art, OpenBoss);

        // The grid only changes with what it shows of the bosses, the next spawn or the local day; a countdown or a horse
        // registration starting leaves it alone.
        var key = (GridContent(data), board.Next?.AtUtc, DateOnly.FromDateTime(now.LocalDateTime));
        if (!key.Equals(_gridKey))
        {
            _gridKey = key;
            ShowGrid(data, now);
        }
        if (now.Minute != _tooltipMinute)
        {
            _tooltipMinute = now.Minute;
            foreach (var entry in Rows.SelectMany(r => r.Cells).SelectMany(c => c.Entries)) entry.RefreshTooltip(now);
            _tilesData = null;
        }
        if (!ReferenceEquals(data, _tilesData))
        {
            _tilesData = data;
            SyncTiles(data, now);
        }
    }

    static IEnumerable<TimerDef> BuiltInBosses(AppData data) =>
        data.Timers.Where(t => BossRegions.IsSelected(data, t));

    /// <summary>Keeps each boss's tile, with Morning Light bosses first, and updates it in place.</summary>
    void SyncTiles(AppData data, DateTimeOffset now) =>
        Tiles.Sync(BossOrder.Sort(BuiltInBosses(data)),
            (tile, boss) => tile.Id == boss.Id, boss => new BossTileViewModel(boss, _services.Art.For(boss), OpenBoss, now),
            (tile, boss) => tile.Show(boss, now));

    /// <summary>What the grid draws from <paramref name="data"/>: the built-in bosses' names, spawn times, alerts on or off
    /// and own-settings marks, and their skipped spawns.</summary>
    string GridContent(AppData data)
    {
        if (ReferenceEquals(data, _gridData)) return _gridContent;
        var bosses = data.Timers.Where(t => BossRegions.IsSelected(data, t) && t.Scheduled is not null).ToList();
        var ids = bosses.Select(b => b.Id).ToHashSet();
        _gridData = data;
        return _gridContent = string.Join('\n', bosses
            .Select(b => $"{b.Id}|{b.Name}|{b.Enabled}|{b.Alerts.OverridesDefaults}|{b.Scheduled!.TimeZoneId}|"
                + string.Join(",", b.Scheduled.Slots.Select(s => $"{s.Day} {s.Time.Ticks}")))
            .Concat(data.Muted.Where(m => ids.Contains(m.TimerId)).Select(m => $"{m.TimerId}@{m.OccurrenceUtc.UtcTicks}")));
    }

    /// <summary>
    /// A passed spawn, a skip or a boss's alerts turned on or off only change the cells' states, which are updated in
    /// place; the grid's ~100 buttons and their menus are rebuilt only when its rows, bosses or days change.
    /// </summary>
    void ShowGrid(AppData data, DateTimeOffset now)
    {
        var grid = WeekGrid.Build(data, now, TimeZoneInfo.Local, _services.Boards);
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        _tooltipMinute = -1;
        if (grid.WeekStart == _gridWeekStart && today == _gridToday && SameCells(grid))
        {
            foreach (var (row, rowModel) in grid.Rows.Zip(Rows))
            foreach (var (entries, cell) in row.Days.Zip(rowModel.Cells))
            {
                cell.IsNext = entries.Any(e => e.State == CellState.Next);
                foreach (var (entry, model) in entries.Zip(cell.Entries)) model.Show(entry);
            }
            return;
        }
        _gridWeekStart = grid.WeekStart;
        _gridToday = today;
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
            Rows.Add(new GridRowViewModel(RowTime(row), cells));
        }
    }

    /// <summary>The same rows as shown, each cell with the same bosses at the same spawns.</summary>
    bool SameCells(WeekGridState grid) =>
        grid.Rows.Count == Rows.Count
        && grid.Rows.Zip(Rows).All(r => RowTime(r.First) == r.Second.Time
            && r.First.Days.Zip(r.Second.Cells).All(c => c.First.Count == c.Second.Entries.Count
                && c.First.Zip(c.Second.Entries).All(e => e.Second.Shows(e.First))));

    static string RowTime(GridRow row) => row.Time.ToString("HH:mm", CultureInfo.InvariantCulture);

    void OpenBoss(Guid id)
    {
        var data = _services.Timers.Current;
        if (data.Timers.FirstOrDefault(t => t.Id == id && BossRegions.IsSelected(data, t)) is { } boss)
            _host.OpenPanel(new BossPanelViewModel(_services, boss));
    }
}

/// <summary>A boss name that opens the boss panel when clicked.</summary>
public sealed partial class BossLink(Guid id, string name, Action<Guid> open, ArtPicture picture)
{
    public string Name => name;
    public IReadOnlyList<ArtPicture> Images { get; } = [picture];

    [RelayCommand]
    void Open() => open(id);
}

public sealed partial class StripTileViewModel(string caption, bool elapsed) : ObservableObject
{
    string _signature = "";

    [ObservableProperty] private bool _hasSpawn;
    [ObservableProperty] private string _label = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleBosses))]
    private IReadOnlyList<BossLink> _names = [];
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images = [];
    [ObservableProperty] private string _clock = "";
    [ObservableProperty] private bool _skipped;

    public bool HasMultipleBosses => Names.Count > 1;

    public void Update(SpawnGroup? group, DateTimeOffset now, ArtLibrary art, Action<Guid> open)
    {
        HasSpawn = group is not null;
        if (group is null) return;
        var signature = Formats.SpawnKey(group);
        if (signature != _signature)
        {
            _signature = signature;
            Names = group.Bosses.Select(b => new BossLink(b.Id, b.Name, open, art.For(b))).ToList();
            Images = Names.SelectMany(b => b.Images).Take(2).ToList();
            Label = $"{caption} · {Formats.DayTime(group.AtUtc)}";
        }
        Skipped = group.Skipped;
        Clock = elapsed ? "−" + DurationFormat.Clock(now - group.AtUtc) : DurationFormat.Clock(group.AtUtc - now);
    }
}

public sealed record DayHeaderViewModel(string Label, bool IsToday);

public sealed record GridRowViewModel(string Time, IReadOnlyList<GridCellViewModel> Cells);

public sealed partial class GridCellViewModel(bool isToday, bool isNext, IReadOnlyList<GridEntryViewModel> entries)
    : ObservableObject
{
    [ObservableProperty] private bool _isNext = isNext;

    public bool IsToday { get; } = isToday;
    public IReadOnlyList<GridEntryViewModel> Entries { get; } = entries;
}

public sealed partial class GridEntryViewModel : ObservableObject
{
    readonly Core.Storage.TimerStore _store;
    readonly Action<Guid> _open;
    GridEntry _entry;

    [ObservableProperty] private string _tooltip = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSkip), nameof(SkipLabel))]
    private CellState _state;

    public string Name => _entry.Boss.Name;
    /// <summary>Marks bosses with their own alert times or sound, which don't follow the defaults in Settings.</summary>
    public bool OwnSettings => _entry.Boss.Alerts.OverridesDefaults;
    public bool CanSkip => State is CellState.Upcoming or CellState.Next or CellState.Skipped;
    public string SkipLabel => State == CellState.Skipped ? "Unskip" : "Skip this spawn";

    public GridEntryViewModel(GridEntry entry, Core.Storage.TimerStore store, Action<Guid> open)
    {
        _store = store;
        _open = open;
        _entry = entry;
        State = entry.State;
    }

    [RelayCommand]
    void Open() => _open(_entry.Boss.Id);

    [RelayCommand]
    void ToggleSkip() => _store.ToggleMute(_entry.Boss.Id, _entry.AtUtc);

    /// <summary>Whether <paramref name="entry"/> is this boss at this spawn and looks the same apart from its state.</summary>
    public bool Shows(GridEntry entry) =>
        entry.Boss.Id == _entry.Boss.Id && entry.AtUtc == _entry.AtUtc
        && entry.Boss.Name == Name && entry.Boss.Alerts.OverridesDefaults == OwnSettings;

    /// <summary>Takes the new state of an entry it <see cref="Shows"/>.</summary>
    public void Show(GridEntry entry)
    {
        _entry = entry;
        State = entry.State;
    }

    public void RefreshTooltip(DateTimeOffset now)
    {
        var when = Formats.DayTime(_entry.AtUtc);
        var relative = _entry.AtUtc > now ? $"in {DurationFormat.Countdown(_entry.AtUtc - now)}" : "passed";
        var note = State switch
        {
            CellState.Skipped => " · skipped",
            CellState.Unfollowed => " · alerts off",
            _ => "",
        };
        Tooltip = $"{Name} · {when} · {relative}{note}{(OwnSettings ? " · own alert settings" : "")}";
    }
}
