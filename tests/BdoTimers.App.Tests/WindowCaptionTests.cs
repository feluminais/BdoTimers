using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using BdoTimers.App.Overlay;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public class WindowCaptionTests
{
    [Fact]
    public void MaximizePressBypassesNativeCaptionDrawingAndTogglesOnce() => WpfTest.Run(() =>
    {
        var button = new Button { Content = "Maximize" };
        var window = new Window { Content = button, Width = 300, Height = 180 };
        var toggles = 0;
        try
        {
            window.Show();
            WpfTest.Drain();
            var point = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            var packed = new IntPtr(((int)point.Y << 16) | ((int)point.X & 0xffff));
            Assert.True(CaptionMaximize.Handle(button, 0x00A1, new IntPtr(9), packed, () => toggles++));
            Assert.Equal(0, toggles);
            Assert.True(CaptionMaximize.Handle(button, 0x00A2, new IntPtr(9), packed, () => toggles++));
            Assert.Equal(1, toggles);
            button.IsEnabled = false;
            Assert.False(CaptionMaximize.Handle(button, 0x00A2, new IntPtr(9), packed, () => toggles++));
            Assert.Equal(1, toggles);
        }
        finally { window.Close(); }
    });

#if DEBUG
    [Fact]
    public void BehindModeReleasesOnExplicitActivation() => WpfTest.Run(() =>
    {
        var window = new Window { Width = 300, Height = 180, ShowActivated = false };
        var other = new Window { Width = 300, Height = 180 };
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var source = HwndSource.FromHwnd(hwnd);
        source.AddHook(NativeMethods.KeepAtBottom);
        try
        {
            window.Show();
            other.Show();
            WpfTest.Drain();
            Assert.False(window.ShowActivated);
            var otherHwnd = new WindowInteropHelper(other).Handle;
            var flags = NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE;
            Assert.True(NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, flags));
            Assert.False(IsAbove(hwnd, otherHwnd));
            // Activation is sent on the undisplayed desktop, whose windows cannot become
            // the foreground window of the user's active desktop.
            SendMessage(hwnd, 0x0006, new IntPtr(1), IntPtr.Zero);
            WpfTest.Drain();
            Assert.True(window.ShowActivated);
            Assert.True(NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, flags));
            Assert.True(IsAbove(hwnd, otherHwnd));
        }
        finally { window.Close(); other.Close(); }
    });

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    static bool IsAbove(IntPtr hwnd, IntPtr other)
    {
        for (var previous = NativeMethods.GetWindow(hwnd, NativeMethods.GW_HWNDPREV);
            previous != IntPtr.Zero; previous = NativeMethods.GetWindow(previous, NativeMethods.GW_HWNDPREV))
            if (previous == other) return false;
        return true;
    }
#endif
}
