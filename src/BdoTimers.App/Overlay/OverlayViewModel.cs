using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using BdoTimers.App.Art;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App.Overlay;

/// <summary>A boss spawn on the overlay: its names, the time to or since it, and its pictures for the Card banner.
/// Kept from tick to tick and updated in place, so only the text that changed is drawn again.</summary>
public sealed partial class OverlaySpawn : ObservableObject
{
    [ObservableProperty] private string _names = "";
    [ObservableProperty] private string _time = "";
    [ObservableProperty] private bool _skipped;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images = [];
    [ObservableProperty] private bool _isSample;

    public OverlaySpawn Show(string names, string time, bool skipped, IReadOnlyList<ArtPicture> images, bool isSample)
    {
        Names = names;
        Time = time;
        Skipped = skipped;
        Images = images;
        IsSample = isSample;
        return this;
    }
}

/// <summary>A named time on the overlay: a pop-up or an active timer. <see cref="Key"/> is
/// what it stands for, so a list keeps the row from tick to tick and only its text changes.</summary>
public sealed partial class OverlayLine(object key) : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _time = "";
    [ObservableProperty] private bool _isSample;

    public object Key { get; } = key;

    public OverlayLine Show(string name, string time, bool isSample)
    {
        Name = name;
        Time = time;
        IsSample = isSample;
        return this;
    }
}

/// <summary>What the overlay window shows. In preview, a section with no live data gets a dimmed sample.</summary>
public sealed partial class OverlayViewModel(ArtLibrary art) : ObservableObject
{
    static readonly RgbColor FallbackColor = new(0x0B, 0x0B, 0x0C);
    static readonly IReadOnlyList<ArtPicture> NoImages = [];
    static readonly object SampleKey = new();

    readonly OverlaySpawn _previousRow = new();
    readonly OverlaySpawn _nextRow = new();
    readonly OverlayLine _farmRow = new("Farm");
    readonly OverlayLine _fishingRow = new("Fishing");
    string _imagesKey = "";
    IReadOnlyList<ArtPicture> _images = [];
    string? _backgroundKey;

    [ObservableProperty] private OverlayLayout _layout;
    [ObservableProperty] private double _scale = 1;
    [ObservableProperty] private Thickness _outlineThickness = new(1);
    [ObservableProperty] private Brush _background = Brushes.Black;
    [ObservableProperty] private double _backgroundOpacity = 0.85;
    [ObservableProperty] private double _textOpacity = 1;
    [ObservableProperty] private bool _isPreview;
    [ObservableProperty] private string? _clock;
    [ObservableProperty] private string? _serverClock;
    [ObservableProperty] private string? _gameClock;
    [ObservableProperty] private bool _isNight;
    [ObservableProperty] private bool _hasClocks;
    [ObservableProperty] private OverlaySpawn? _previous;
    [ObservableProperty] private OverlaySpawn? _next;
    [ObservableProperty] private OverlayLine? _farm;
    [ObservableProperty] private OverlayLine? _fishing;
    [ObservableProperty] private string? _moreHorseRegistrations;
    [ObservableProperty] private bool _hasCustomTimers;
    [ObservableProperty] private bool _showDivider;

    public ObservableCollection<OverlayLine> PopUps { get; } = [];
    public ObservableCollection<OverlayLine> HorseRegistrations { get; } = [];
    public ObservableCollection<OverlayLine> CustomTimers { get; } = [];

    public void Update(OverlaySnapshot content, OverlaySettings settings, DateTimeOffset now, bool preview)
    {
        Layout = settings.Layout;
        Scale = settings.Scale;
        OutlineThickness = new Thickness(settings.ShowOutline ? 1 : 0);
        BackgroundOpacity = settings.BackgroundOpacity;
        TextOpacity = settings.TextOpacity;
        IsPreview = preview;
        UpdateBackground(settings);

        Clock = content.Clock ? Formats.Time(now) : null;
        ServerClock = content.ServerTime is { } server ? Formats.ZoneTime(server) : null;
        GameClock = content.GameTime is { } game ? Formats.Time(game.Time) : null;
        IsNight = content.GameTime?.IsNight == true;
        HasClocks = Clock is not null || ServerClock is not null || GameClock is not null;
        Previous = content.Previous is { } previous
            ? _previousRow.Show(Names(previous), "−" + DurationFormat.Clock(now - previous.AtUtc), previous.Skipped, NoImages, false)
            : preview && settings.ShowPrevious ? _previousRow.Show("Kzarka", "−00:12:05", false, NoImages, true) : null;
        Next = content.Next is { } next
            ? _nextRow.Show(Names(next), DurationFormat.Clock(next.AtUtc - now), next.Skipped, ImagesFor(next), false)
            : preview && settings.ShowNext ? _nextRow.Show("Nouver", "00:47:12", false, NoImages, true) : null;
        var popUps = content.PopUps
            .Select(i => ((object)(i.Timer.Id, i.AtUtc), OccurrenceSource.NameAt(i.Timer, i.AtUtc), DurationFormat.Clock(i.AtUtc - now), false)).ToList();
        if (preview && settings.GuildBosses.Enabled && !content.PopUps.Any(i => i.Timer.Preset == Presets.GuildBosses))
            popUps.Add((SampleKey, "Guild bosses", DurationFormat.Clock(TimeSpan.FromMinutes(settings.GuildBosses.ShowMinutesBefore)), true));
        Sync(PopUps, popUps);
        Farm = content.FarmLeft is { } farmLeft
            ? _farmRow.Show("Farm", $"{DurationFormat.SignedClock(farmLeft)} · {content.FarmProgress}%", false)
            : preview && settings.ShowFarm ? _farmRow.Show("Farm", "21:59:59 · 0%", true) : null;
        Fishing = Line(_fishingRow, "Fishing", content.FishingElapsed, preview && settings.ShowFishing, "00:42:10");
        var horse = content.HorseRegistrations
            .Select(r => ((object)r.Id, r.Name, DurationFormat.Clock(r.EndsAtUtc - now), false)).ToList();
        if (horse.Count == 0 && preview && settings.ShowHorseRegistrations)
            horse.Add((SampleKey, "Horse 1", "00:08:30", true));
        Sync(HorseRegistrations, horse);
        MoreHorseRegistrations = content.MoreHorseRegistrations > 0 ? $"+{content.MoreHorseRegistrations} more running" : null;
        var custom = content.CustomTimers.Select(t =>
            ((object)t.Id, t.Name, DurationFormat.Clock(t.Remaining) + (t.Paused ? " · Paused" : ""), false)).ToList();
        if (custom.Count == 0 && preview && settings.ShowCustomTimers)
            custom.Add((SampleKey, "Custom timer", "00:15:00", true));
        Sync(CustomTimers, custom);
        HasCustomTimers = CustomTimers.Count > 0;
        ShowDivider = (Previous is not null || Next is not null || PopUps.Count > 0)
            && (Farm is not null || Fishing is not null || HorseRegistrations.Count > 0 || HasCustomTimers);
    }

    static string Names(SpawnGroup group) => string.Join(" · ", group.Bosses.Select(b => b.Name));

    static OverlayLine? Line(OverlayLine row, string name, TimeSpan? time, bool sample, string sampleTime) =>
        time is { } t ? row.Show(name, DurationFormat.Clock(t), false)
        : sample ? row.Show(name, sampleTime, true)
        : null;

    /// <summary>Keeps the row of each key that is still wanted, so the overlay only redraws its text rather than
    /// regenerating every row each second.</summary>
    static void Sync(ObservableCollection<OverlayLine> rows,
        IEnumerable<(object Key, string Name, string Time, bool IsSample)> wanted) =>
        rows.Sync(wanted, (row, line) => Equals(row.Key, line.Key),
            line => new OverlayLine(line.Key).Show(line.Name, line.Time, line.IsSample),
            (row, line) => row.Show(line.Name, line.Time, line.IsSample));

    /// <summary>The same list while the spawn stays the same, so the banner isn't reloaded every second.</summary>
    IReadOnlyList<ArtPicture> ImagesFor(SpawnGroup group)
    {
        var key = Formats.SpawnKey(group);
        if (key != _imagesKey)
        {
            _imagesKey = key;
            _images = art.For(group.Bosses);
        }
        return _images;
    }

    /// <summary>The picture when it can be read, else the colour.</summary>
    void UpdateBackground(OverlaySettings settings)
    {
        var key = $"{settings.BackgroundColor}|{settings.BackgroundImage}";
        if (key == _backgroundKey) return;
        _backgroundKey = key;
        Brush brush = settings.BackgroundImage is { } file && art.UserPicture(file) is { } picture
            ? new ImageBrush(picture) { Stretch = Stretch.UniformToFill }
            : new SolidColorBrush(ToColor(RgbColor.TryParseHex(settings.BackgroundColor, out var c) ? c : FallbackColor));
        brush.Freeze();
        Background = brush;
    }

    static Color ToColor(RgbColor c) => Color.FromRgb(c.R, c.G, c.B);
}
