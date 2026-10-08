using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using BdoTimers.App.Art;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Tests;

public class OverlayMoveTests
{
    static readonly WindowRect Bounds = new(100, 100, 200, 80);

    static bool TakesMouse(OverlayWindow window) =>
        (NativeMethods.GetWindowLong(new WindowInteropHelper(window).Handle, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TRANSPARENT) == 0;

    [Fact]
    public void Holding_the_keys_makes_the_overlay_take_the_mouse_and_letting_go_gives_it_back() => WpfTest.Run(() =>
    {
        var held = false;
        var window = Create(() => held);
        try
        {
            window.SetMoveHotkey(DefaultHotkeys.Move);
            window.Show();
            Pump();
            Assert.False(TakesMouse(window));
            Assert.False(window.Model.IsMoveMode);
            Assert.NotEqual(Cursors.SizeAll, window.Cursor);

            held = true;
            Pump();
            Assert.True(TakesMouse(window));
            Assert.True(window.Model.IsMoveMode);
            Assert.True(window.Model.IsGrabbable);
            Assert.Equal(Cursors.SizeAll, window.Cursor);

            held = false;
            Pump();
            Assert.False(TakesMouse(window));
            Assert.False(window.Model.IsMoveMode);
            Assert.False(window.Model.IsGrabbable);
            Assert.NotEqual(Cursors.SizeAll, window.Cursor);
        }
        finally { window.Close(); }
    });

    [Theory]
    [InlineData(OverlayMouseProximity.Fade, .15)]
    [InlineData(OverlayMouseProximity.Hide, 0)]
    public void Mouse_proximity_keeps_working_while_the_keys_are_held(OverlayMouseProximity mode, double opacity) => WpfTest.Run(() =>
    {
        var x = 80.0;
        var window = Create(() => true, _ => (Bounds, x, 140));
        try
        {
            window.SetMouseProximity(mode);
            window.SetMoveHotkey(DefaultHotkeys.Move);
            window.Show();
            Pump();
            Assert.True(window.Model.IsMoveMode);
            Assert.True(TakesMouse(window));
            Assert.Equal(opacity, window.Opacity, 5);

            x = 20;
            Pump();
            Assert.True(window.Model.IsMoveMode);
            Assert.Equal(1, window.Opacity, 5);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Without_move_keys_nothing_is_read_and_clearing_them_gives_the_mouse_back() => WpfTest.Run(() =>
    {
        var reads = 0;
        var window = Create(() => { reads++; return true; });
        try
        {
            window.Show();
            Pump();
            Assert.Equal(0, reads);
            Assert.False(TakesMouse(window));

            window.SetMoveHotkey(DefaultHotkeys.Move);
            Pump();
            Assert.True(reads > 0);
            Assert.True(TakesMouse(window));

            window.SetMoveHotkey(null);
            Assert.False(TakesMouse(window));
            Assert.False(window.Model.IsMoveMode);
            var stoppedAt = reads;
            Pump();
            Assert.Equal(stoppedAt, reads);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void The_keys_are_read_only_while_the_overlay_shows_and_clicks_pass_through_it() => WpfTest.Run(() =>
    {
        var reads = 0;
        var window = Create(() => { reads++; return true; });
        try
        {
            window.SetMoveHotkey(DefaultHotkeys.Move);
            Pump();
            Assert.Equal(0, reads);

            window.Show();
            Pump();
            Assert.True(window.Model.IsMoveMode);

            window.Hide();
            Assert.False(window.Model.IsMoveMode);
            var stoppedAt = reads;
            Pump();
            Assert.Equal(stoppedAt, reads);

            // The Overlay settings' preview is draggable already, with or without the keys.
            window.Show();
            Pump();
            window.SetClickThrough(false);
            Assert.False(window.Model.IsMoveMode);
            Assert.True(TakesMouse(window));
            stoppedAt = reads;
            Pump();
            Assert.Equal(stoppedAt, reads);

            window.SetClickThrough(true);
            Pump();
            Assert.True(reads > stoppedAt);
            Assert.True(window.Model.IsMoveMode);
        }
        finally { window.Close(); }
    });

    static OverlayWindow Create(Func<bool> held, Func<IntPtr, (WindowRect Bounds, double X, double Y)?>? readPointer = null) =>
        new(new OverlayViewModel(new ArtLibrary(System.IO.Path.GetTempPath())) { Clock = "12:00", HasClocks = true },
            readPointer ?? (_ => null), _ => held());

    static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
