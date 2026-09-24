using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BdoTimers.App.Controls;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Art;

/// <summary>
/// Pictures for tiles: bundled art for built-in bosses (Assets/Bosses/&lt;slug&gt;.jpg) and user pictures copied
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

    readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string ImagesDir { get; } = imagesDir;

    ImageSource Placeholder => (ImageSource)Application.Current.FindResource("PlaceholderArt");

    /// <summary>A boss's picture framed on its head; a custom timer's picture, or the placeholder, cropped around the centre.</summary>
    public ArtPicture For(TimerDef timer)
    {
        if (!timer.IsBuiltIn)
            return new ArtPicture(timer.ImageFile is { } file ? Load(Path.Combine(ImagesDir, file)) : Placeholder);
        var slug = Slug(timer.Name);
        return new ArtPicture(Load($"pack://application:,,,/Assets/Bosses/{slug}.jpg"),
            BossHeads.TryGetValue(slug, out var head) ? head : null);
    }

    /// <summary>Pictures for a spawn group: at most two, since tiles split into two stacked halves.</summary>
    public IReadOnlyList<ArtPicture> For(IEnumerable<TimerDef> timers) => timers.Take(2).Select(For).ToList();

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
