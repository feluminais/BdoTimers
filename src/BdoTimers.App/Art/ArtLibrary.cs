using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BdoTimers.App.Controls;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;

namespace BdoTimers.App.Art;

/// <summary>
/// Pictures for tiles: bundled art for built-in bosses (Assets/Bosses/&lt;slug&gt;.jpg) and preset timers, and user pictures copied
/// into <see cref="ImagesDir"/>. Anything missing or undecodable shows the placeholder art. UI thread only.
/// </summary>
public sealed class ArtLibrary(string imagesDir)
{
    /// <summary>Where each boss's head is in its bundled picture, as fractions of width and height.</summary>
    static readonly IReadOnlyDictionary<string, Point> BossHeads = new Dictionary<string, Point>
    {
        ["bulgasal"] = new(0.57, 0.33),
        ["garmoth"] = new(0.70, 0.30),
        ["golden-pig-king"] = new(0.57, 0.27),
        ["karanda"] = new(0.55, 0.27),
        ["kutum"] = new(0.63, 0.48),
        ["kzarka"] = new(0.60, 0.65),
        ["muraka"] = new(0.52, 0.25),
        ["nouver"] = new(0.64, 0.44),
        ["offin"] = new(0.51, 0.40),
        ["quint"] = new(0.46, 0.24),
        ["sangoon"] = new(0.43, 0.27),
        ["uturi"] = new(0.61, 0.38),
        ["vell"] = new(0.62, 0.22),
    };

    /// <summary>The same for preset timers' bundled pictures (Assets/Timers/&lt;preset&gt;.jpg); none means the centre.</summary>
    static readonly IReadOnlyDictionary<string, Point> PresetFocus = new Dictionary<string, Point>
    {
        [Presets.Fishing] = new(0.48, 0.42),
        [Presets.HorseRegistration] = new(0.22, 0.38),
        [Presets.GuildBosses] = new(0.50, 0.48),
        [Presets.GuildWar] = new(0.25, 0.45),
    };

    readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string ImagesDir { get; } = imagesDir;

    ImageSource Placeholder => (ImageSource)Application.Current.FindResource("PlaceholderArt");

    /// <summary>A boss's picture framed on its head; the user's picture for a timer, else a preset's bundled one, else
    /// the placeholder.</summary>
    public ArtPicture For(TimerDef timer)
    {
        if (timer.ImageFile is { } file) return new ArtPicture(Load(Path.Combine(ImagesDir, file)));
        if (timer.Preset is { } preset)
            return new ArtPicture(Load($"pack://application:,,,/Assets/Timers/{preset}.jpg"),
                PresetFocus.TryGetValue(preset, out var focus) ? focus : null);
        if (!timer.IsBuiltIn) return new ArtPicture(Placeholder);
        var slug = Slug(timer.Name);
        return new ArtPicture(Load($"pack://application:,,,/Assets/Bosses/{slug}.jpg"),
            BossHeads.TryGetValue(slug, out var head) ? head : null);
    }

    /// <summary>Pictures for a spawn group: at most two, since tiles split into two stacked halves.</summary>
    public IReadOnlyList<ArtPicture> For(IEnumerable<TimerDef> timers) => timers.Take(2).Select(For).ToList();

    /// <summary>A picture from the images folder, or null when it's missing or can't be read.</summary>
    public ImageSource? UserPicture(string file)
    {
        var picture = Load(Path.Combine(ImagesDir, file));
        return ReferenceEquals(picture, Placeholder) ? null : picture;
    }

    /// <summary>Copies a picture into the images folder and returns its new file name.</summary>
    public string Import(string sourcePath)
    {
        Directory.CreateDirectory(ImagesDir);
        var name = Guid.NewGuid().ToString("N") + Path.GetExtension(sourcePath).ToLowerInvariant();
        File.Copy(sourcePath, Path.Combine(ImagesDir, name));
        return name;
    }

    public void Delete(string? file)
    {
        if (file is null) return;
        var path = Path.Combine(ImagesDir, file);
        _cache.Remove(path);
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Couldn't delete picture {path}", ex);
        }
    }

    static string Slug(string name) => name.Trim().ToLowerInvariant().Replace(' ', '-');

    ImageSource Load(string location)
    {
        if (_cache.TryGetValue(location, out var cached)) return cached;
        ImageSource result;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(location, UriKind.Absolute);
            // Wide enough for the zoomed-in crops; the bundled pictures are at most this wide anyway.
            bitmap.DecodePixelWidth = 960;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            result = bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException
                                       or InvalidOperationException or ArgumentException)
        {
            Log.Error($"Picture unavailable, using placeholder: {location}", ex);
            result = Placeholder;
        }
        _cache[location] = result;
        return result;
    }
}
