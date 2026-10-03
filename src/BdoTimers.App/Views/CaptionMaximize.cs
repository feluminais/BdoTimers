using System.Windows;
using System.Windows.Controls;

namespace BdoTimers.App.Views;

internal static class CaptionMaximize
{
    internal static Rect Bounds(Button button) => new(button.PointToScreen(new Point()),
        button.PointToScreen(new Point(button.ActualWidth, button.ActualHeight)));

    internal static bool Handle(Button button, int message, IntPtr hit, IntPtr position, Action toggle)
    {
        const int NcDown = 0x00A1, NcUp = 0x00A2, NcDoubleClick = 0x00A3, MaxButton = 9;
        if (message is not (NcDown or NcUp or NcDoubleClick) || hit.ToInt32() != MaxButton
            || !button.IsEnabled || !button.IsVisible) return false;
        // DefWindowProc draws a native caption button and enters its own mouse loop on down,
        // which consumes the release before the custom button can maximize the window.
        if (message == NcUp)
        {
            var packed = position.ToInt64();
            var point = new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff));
            if (Bounds(button).Contains(point)) toggle();
        }
        return true;
    }
}
