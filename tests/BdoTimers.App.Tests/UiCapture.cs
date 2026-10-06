using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BdoTimers.App.Tests;

/// <summary>Saves a PNG of a shown element when BDOTIMERS_SHOTS names a folder, so a layout can be looked at; does
/// nothing otherwise.</summary>
internal static class UiCapture
{
    public static void Save(FrameworkElement element, string name)
    {
        if (Environment.GetEnvironmentVariable("BDOTIMERS_SHOTS") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        // A panel opens after a short wait for the first frames and then slides in.
        WpfTest.Wait(750);
        element.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(folder, name));
        encoder.Save(file);
    }
}
