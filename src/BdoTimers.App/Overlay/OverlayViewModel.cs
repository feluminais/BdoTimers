using System.Globalization;
using System.Windows.Media;
using BdoTimers.App.Art;
using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App.Overlay;

/// <summary>A boss spawn on the overlay: its names, the time to or since it, and its pictures for the Card banner.</summary>
public sealed record OverlaySpawn(string Names, string Time, bool Skipped, IReadOnlyList<ArtPicture> Images, bool IsSample);

/// <summary>A named time on the overlay: a pop-up timer, Farm or Fishing.</summary>
public sealed record OverlayLine(string Name, string Time, bool IsSample);

/// <summary>What the overlay window shows. In preview, a section with no live data gets a dimmed sample.</summary>
public sealed partial class OverlayViewModel(ArtLibrary art) : ObservableObject
{
    static readonly RgbColor FallbackColor = new(0x0B, 0x0B, 0x0C);

    string _imagesKey = "";
    IReadOnlyList<ArtPicture> _images = [];
    string? _backgroundKey;

    [ObservableProperty] private OverlayLayout _layout;
    [ObservableProperty] private double _scale = 1;
    [ObservableProperty] private Brush _background = Brushes.Black;
    [ObservableProperty] private double _backgroundOpacity = 0.85;
    [ObservableProperty] private double _textOpacity = 1;
    [ObservableProperty] private bool _isPreview;
    [ObservableProperty] private string? _clock;
    [ObservableProperty] private OverlaySpawn? _previous;
    [ObservableProperty] private OverlaySpawn? _next;
    [ObservableProperty] private IReadOnlyList<OverlayLine> _popUps = [];
    [ObservableProperty] private OverlayLine? _farm;
    [ObservableProperty] private OverlayLine? _fishing;
    [ObservableProperty] private bool _showDivider;

    public void Update(OverlaySnapshot content, OverlaySettings settings, DateTimeOffset now, bool preview)
    {
        Layout = settings.Layout;
        Scale = settings.Scale;
        BackgroundOpacity = settings.BackgroundOpacity;
        TextOpacity = settings.TextOpacity;
        IsPreview = preview;
        UpdateBackground(settings);

        Clock = content.Clock ? now.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) : null;
        Previous = content.Previous is { } previous
            ? Spawn(previous, "−" + DurationFormat.Clock(now - previous.AtUtc), [])
            : preview && settings.ShowPrevious ? new OverlaySpawn("Kzarka", "−00:12:05", false, [], true) : null;
        Next = content.Next is { } next
            ? Spawn(next, DurationFormat.Clock(next.AtUtc - now), ImagesFor(next))
            : preview && settings.ShowNext ? new OverlaySpawn("Nouver", "00:47:12", false, [], true) : null;
        var popUps = content.PopUps.Select(i => new OverlayLine(i.Timer.Name, DurationFormat.Clock(i.AtUtc - now), false)).ToList();
        if (popUps.Count == 0 && preview) popUps.Add(new OverlayLine("Timer", "00:05:00", true));
        if (!popUps.SequenceEqual(PopUps)) PopUps = popUps;
        Farm = Line("Farm", content.FarmLeft, preview && settings.ShowFarm, "21:59:59");
        Fishing = Line("Fishing", content.FishingElapsed, preview && settings.ShowFishing, "00:42:10");
        ShowDivider = (Previous is not null || Next is not null || PopUps.Count > 0) && (Farm is not null || Fishing is not null);
    }

    static OverlaySpawn Spawn(SpawnGroup group, string time, IReadOnlyList<ArtPicture> images) =>
        new(string.Join(" · ", group.Bosses.Select(b => b.Name)), time, group.Skipped, images, false);

    static OverlayLine? Line(string name, TimeSpan? time, bool sample, string sampleTime) =>
        time is { } t ? new OverlayLine(name, DurationFormat.Clock(t), false)
        : sample ? new OverlayLine(name, sampleTime, true)
        : null;

    /// <summary>The same list while the spawn stays the same, so the banner isn't reloaded every second.</summary>
    IReadOnlyList<ArtPicture> ImagesFor(SpawnGroup group)
    {
        var key = $"{group.AtUtc:O}|{string.Join(",", group.Bosses.Select(b => b.Id))}";
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
