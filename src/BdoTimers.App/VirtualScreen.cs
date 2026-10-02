using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
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
            var dpi = GetDpiForSystem();
            fromDevice = new Matrix(96d / dpi, 0, 0, 96d / dpi, 0, 0);
        }
        var areas = new List<WindowRect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref NativeRect __, IntPtr ___) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
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

    delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref NativeRect rect, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
}
