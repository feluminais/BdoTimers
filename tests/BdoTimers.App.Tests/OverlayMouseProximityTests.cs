using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Data;
using System.Windows.Threading;
using BdoTimers.App.Art;
using BdoTimers.App.Controls;
using BdoTimers.App.Overlay;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Tests;

public class OverlayMouseProximityTests
{
    static readonly WindowRect Bounds = new(100, 100, 200, 80);

    [Theory]
    [InlineData(OverlayMouseProximity.Fade, .15)]
    [InlineData(OverlayMouseProximity.Hide, 0)]
    public void Suppressed_overlay_keeps_polling_and_returns_when_the_pointer_leaves(OverlayMouseProximity mode, double opacity) => WpfTest.Run(() =>
    {
        var x = 80.0;
        var reads = 0;
        var window = Create(_ => { reads++; return (Bounds, x, 140); });
        try
        {
            window.SetMouseProximity(mode);
            Assert.Equal(0, reads);
            window.Show();
            Pump();
            Assert.Equal(opacity, window.Opacity, 5);
            Assert.True(window.IsVisible);
            var previousReads = reads;
            x = 70; // Beyond the entry margin, inside the wider exit margin.
            Pump();
            Assert.True(reads > previousReads);
            Assert.Equal(opacity, window.Opacity, 5);
            x = 59;
            Pump();
            Assert.Equal(1, window.Opacity, 5);
            Assert.Equal(1.0, window.GetAnimationBaseValue(UIElement.OpacityProperty));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Preview_and_off_restore_immediately_and_stop_pointer_reads() => WpfTest.Run(() =>
    {
        var reads = 0;
        var window = Create(_ => { reads++; return (Bounds, 200, 140); });
        try
        {
            window.SetMouseProximity(OverlayMouseProximity.Hide);
            window.Show();
            Pump();
            Assert.Equal(0, window.Opacity, 5);
            window.SetClickThrough(false);
            Assert.Equal(1, window.Opacity);
            var stoppedAt = reads;
            Pump();
            Assert.Equal(stoppedAt, reads);
            window.SetMouseProximity(OverlayMouseProximity.Fade);
            Assert.Equal(1, window.Opacity);
            Assert.Equal(stoppedAt, reads);
            window.SetClickThrough(true);
            Pump();
            Assert.Equal(.15, window.Opacity, 5);
            window.SetMouseProximity(OverlayMouseProximity.Hide);
            Pump();
            Assert.Equal(0, window.Opacity, 5);
            window.SetMouseProximity(OverlayMouseProximity.Off);
            Assert.Equal(1, window.Opacity);
            stoppedAt = reads;
            Pump();
            Assert.Equal(stoppedAt, reads);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Hiding_or_closing_stops_polling_and_a_new_show_starts_with_fresh_proximity() => WpfTest.Run(() =>
    {
        var x = 80.0;
        var reads = 0;
        var window = Create(_ => { reads++; return (Bounds, x, 140); });
        try
        {
            window.SetMouseProximity(OverlayMouseProximity.Hide);
            window.Show();
            Pump();
            Assert.Equal(0, window.Opacity, 5);
            window.Hide();
            Assert.Equal(1, window.Opacity);
            var stoppedAt = reads;
            Pump();
            Assert.Equal(stoppedAt, reads);
            x = 70;
            window.Show();
            Pump();
            Assert.Equal(1, window.Opacity, 5);
            Assert.True(reads > stoppedAt);
        }
        finally { window.Close(); }
        var closedAt = reads;
        Pump();
        Assert.Equal(closedAt, reads);
    });

    [Fact]
    public void Missing_cursor_read_restores_the_overlay_and_later_reads_can_hide_it_again() => WpfTest.Run(() =>
    {
        var available = true;
        var window = Create(_ => available ? (Bounds, 200, 140) : null);
        try
        {
            window.SetMouseProximity(OverlayMouseProximity.Hide);
            window.Show();
            Pump();
            Assert.Equal(0, window.Opacity, 5);
            available = false;
            Pump();
            Assert.Equal(1, window.Opacity, 5);
            available = true;
            Pump();
            Assert.Equal(0, window.Opacity, 5);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Selector_saves_each_mode_and_follows_external_settings_changes() => WpfTest.Run(() =>
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BdoTimers-proximity-{Guid.NewGuid():N}.json");
        var settings = new PersistentState<AppSettings>(new JsonFileStore<AppSettings>(path, () => new()), new());
        var services = (AppServices)RuntimeHelpers.GetUninitializedObject(typeof(AppServices));
        SetField(services, "<Settings>k__BackingField", settings);
        var model = (OverlayPanelViewModel)RuntimeHelpers.GetUninitializedObject(typeof(OverlayPanelViewModel));
        SetField(model, "_services", services);
        SetField(model, "_lastSettings", settings.Current.Overlay);
        SetField(model, "<MouseProximityChoices>k__BackingField", Enum.GetValues<OverlayMouseProximity>().Select(m => new Choice(m.ToString(), m)).ToList());
        var changed = (Action)typeof(OverlayPanelViewModel).GetMethod("OnSettingsChanged", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate(typeof(Action), model);
        settings.Changed += changed;
        var selector = new CycleSelector { ItemsSource = model.MouseProximityChoices.ToList() };
        System.Windows.Automation.AutomationProperties.SetName(selector, "Mouse proximity");
        selector.SetBinding(CycleSelector.SelectedItemProperty, new Binding(nameof(model.MouseProximity)) { Source = model, Mode = BindingMode.TwoWay });
        var window = new Window { Content = selector, Width = 250, Height = 100 };
        try
        {
            window.Show();
            WpfTest.Drain();
            var peer = UIElementAutomationPeer.CreatePeerForElement(selector)!;
            var provider = Assert.IsAssignableFrom<IValueProvider>(peer.GetPattern(PatternInterface.Value));
            Assert.Equal("Mouse proximity", peer.GetName());
            Assert.Equal("Off", provider.Value);
            foreach (var mode in Enum.GetValues<OverlayMouseProximity>())
            {
                provider.SetValue(mode.ToString());
                Assert.Equal(mode, settings.Current.Overlay.MouseProximity);
                Assert.Equal(mode, new JsonFileStore<AppSettings>(path, () => new()).Load().Value.Overlay.MouseProximity);
            }
            settings.Update(s => s with { Overlay = s.Overlay with { MouseProximity = OverlayMouseProximity.Off } });
            WpfTest.Drain();
            Assert.Equal("Off", provider.Value);
        }
        finally
        {
            settings.Changed -= changed;
            window.Close();
            System.IO.File.Delete(path);
        }
    });

    static OverlayWindow Create(Func<IntPtr, (WindowRect Bounds, double X, double Y)?> readPointer) =>
        new(new OverlayViewModel(new ArtLibrary(System.IO.Path.GetTempPath())) { Clock = "12:00", HasClocks = true }, readPointer);

    static void Pump()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
}
