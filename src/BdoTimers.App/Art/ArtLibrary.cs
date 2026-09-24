using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Art;

/// <summary>
/// Pictures for tiles: bundled art for built-in bosses (Assets/Bosses/&lt;slug&gt;.jpg) and user pictures copied
/// into <see cref="ImagesDir"/>. Anything missing or undecodable shows the placeholder art. UI thread only.
/// </summary>
public sealed class ArtLibrary(string imagesDir)
{
    readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public string ImagesDir { get; } = imagesDir;

    public ImageSource Placeholder => (ImageSource)Application.Current.FindResource("PlaceholderArt");

    public ImageSource For(TimerDef timer) =>
        timer.IsBuiltIn ? Load($"pack://application:,,,/Assets/Bosses/{Slug(timer.Name)}.jpg")
        : timer.ImageFile is { } file ? Load(Path.Combine(ImagesDir, file))
        : Placeholder;

    /// <summary>Pictures for a spawn group: at most two, since tiles split into two stacked halves.</summary>
    public IReadOnlyList<ImageSource> For(IEnumerable<TimerDef> timers) => timers.Take(2).Select(For).ToList();

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
            bitmap.DecodePixelWidth = 480;
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
