using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Model;

namespace BdoTimers.App;

/// <summary>Actual monitor work areas, excluding taskbars and gaps in a multi-monitor layout.</summary>
static class VirtualScreen
{
    public static IReadOnlyList<WindowRect> WorkAreas(Window? window = null)
    {
        var fromDevice = window is not null && PresentationSource.FromVisual(window)?.CompositionTarget is { } target
            ? target.TransformFromDevice : Matrix.Identity;
        if (window is null)
        {
            var dpi = NativeMethods.GetDpiForSystem();
            fromDevice = new Matrix(96d / dpi, 0, 0, 96d / dpi, 0, 0);
        }
        var areas = new List<WindowRect>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref NativeMethods.Rect __, IntPtr ___) =>
        {
            var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                var start = fromDevice.Transform(new Point(info.Work.Left, info.Work.Top));
                var end = fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom));
                areas.Add(new WindowRect(start.X, start.Y, end.X - start.X, end.Y - start.Y));
            }
            return true;
        }, IntPtr.Zero);
        if (areas.Count == 0)
        {
            var fallback = SystemParameters.WorkArea;
            areas.Add(new WindowRect(fallback.Left, fallback.Top, fallback.Width, fallback.Height));
        }
        return areas;
    }

    public static bool Contains(Point point) => WorkAreas().Any(area =>
        point.X >= area.Left && point.X < area.Right && point.Y >= area.Top && point.Y < area.Bottom);
}
