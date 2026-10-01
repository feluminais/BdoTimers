using System.Windows;

namespace BdoTimers.App;

/// <summary>The area all screens span together.</summary>
static class VirtualScreen
{
    /// <summary>Whether <paramref name="point"/> is on it; a saved spot may not be once a monitor is unplugged.</summary>
    public static bool Contains(Point point) =>
        new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight).Contains(point);
}
