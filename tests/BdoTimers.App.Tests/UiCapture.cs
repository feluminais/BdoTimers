using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BdoTimers.App.Tests;

/// <summary>Saves a PNG of a shown element when BDOTIMERS_SHOTS names a folder, so a layout can be looked at; does
/// nothing otherwise.</summary>
internal static class UiCapture
{
    public static void Save(FrameworkElement element, string name)
    {
        if (Environment.GetEnvironmentVariable("BDOTIMERS_SHOTS") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        Settle();
        element.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight),
            96, 96, PixelFormats.Pbgra32);
        image.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(folder, name));
        encoder.Save(file);
    }

    /// <summary>Lets running animations finish, so a panel is captured at rest and not mid-fade.</summary>
    static void Settle()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(450) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
