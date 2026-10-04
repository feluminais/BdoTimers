using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Tests;

public class UiRegressionTests
{
    [Fact]
    public void MenuTemplateRendersNestedItems() => WpfTest.Run(() =>
    {
        var child = new MenuItem { Header = "Start timer" };
        var parent = new MenuItem { Header = "Custom timers" };
        parent.Items.Add(child);
        for (var index = 0; index < 150; index++) parent.Items.Add(new MenuItem { Header = $"Timer {index}" });
        var menu = new ContextMenu();
        menu.Items.Add(parent);
        try
        {
            menu.IsOpen = true;
            WpfTest.Drain();
            parent.IsSubmenuOpen = true;
            WpfTest.Drain();
            var popup = Assert.IsType<Popup>(parent.Template.FindName("PART_Popup", parent));
            Assert.True(popup.IsOpen);
            Assert.True(child.IsVisible);
            Assert.True(child.ActualWidth > 0);
            Assert.Contains(child, PanelFocusScope.Descendants(popup.Child));
            var scroll = PanelFocusScope.Descendants(popup.Child).OfType<ScrollViewer>().Single();
            Assert.True(scroll.ScrollableHeight > 0);
            scroll.ScrollToEnd();
            WpfTest.Drain();
            Assert.True(scroll.VerticalOffset > 0);
        }
        finally { menu.IsOpen = false; }
    });

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void ScrollbarUsesItsFullLengthAndMovesInTheExpectedDirection(Orientation orientation) => WpfTest.Run(() =>
    {
        var bar = new ScrollBar { Orientation = orientation, Maximum = 100, ViewportSize = 20, Value = 50 };
        var window = new Window { Content = bar, Width = 300, Height = 200 };
        try
        {
            window.Show();
            WpfTest.Drain();
            var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
            Assert.Equal(orientation, track.Orientation);
            Assert.Equal(orientation == Orientation.Vertical, track.IsDirectionReversed);
            if (orientation == Orientation.Horizontal)
            {
                Assert.Equal(4, bar.ActualHeight);
                Assert.True(bar.ActualWidth > 200);
                Assert.True(track.ValueFromDistance(10, 0) > 0);
                Assert.Same(ScrollBar.PageRightCommand, track.IncreaseRepeatButton.Command);
            }
            else
            {
                Assert.Equal(4, bar.ActualWidth);
                Assert.True(bar.ActualHeight > 100);
                Assert.True(track.ValueFromDistance(0, 10) > 0);
                Assert.Same(ScrollBar.PageDownCommand, track.IncreaseRepeatButton.Command);
            }
        }
        finally { window.Close(); }
    });

    [Fact]
    public void ChangingSpeechSpeedDoesNotOverwritePendingVolume() => WpfTest.Run(() =>
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BdoTimers-edits-{Guid.NewGuid():N}.json");
        var settings = new PersistentState<AppSettings>(new JsonFileStore<AppSettings>(path, () => new()), new());
        // Isolate the actual settings bindings from tray, audio and global hotkey initialization.
        var services = (AppServices)RuntimeHelpers.GetUninitializedObject(typeof(AppServices));
        SetField(services, "<Settings>k__BackingField", settings);
        var model = (SettingsPanelViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsPanelViewModel));
        SetField(model, "_services", services);
        SetField(model, "_lastSettings", settings.Current);
        var draft = new EditDraft<AppSettings>(settings.Current);
        SetField(model, "_draft", draft);
        var changed = (Action)typeof(SettingsPanelViewModel).GetMethod("OnSettingsChanged", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate(typeof(Action), model);
        settings.Changed += changed;
        var volume = new Slider { Minimum = 0, Maximum = 1 };
        volume.SetBinding(Slider.ValueProperty, new Binding(nameof(model.Volume)) { Source = model, Mode = BindingMode.TwoWay, Delay = 250 });
        var window = new Window { Content = volume, Width = 300, Height = 100 };
        try
        {
            window.Show();
            WpfTest.Drain();
            volume.Value = .2;
            model.SpeechRate = 1;
            Assert.Equal(.2, volume.Value, 5);
            PanelEdits.Complete(volume);
            Assert.Equal(.2f, draft.Current.Volume, 5);
            Assert.Equal(1, draft.Current.TtsRate);
            Assert.Equal(0, settings.Current.TtsRate);
        }
        finally
        {
            settings.Changed -= changed;
            window.Close();
            System.IO.File.Delete(path);
        }
    });

    [Fact]
    public void ChangingOverlayLayoutDoesNotOverwritePendingSize() => WpfTest.Run(() =>
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BdoTimers-overlay-edits-{Guid.NewGuid():N}.json");
        var settings = new PersistentState<AppSettings>(new JsonFileStore<AppSettings>(path, () => new()), new());
        var services = (AppServices)RuntimeHelpers.GetUninitializedObject(typeof(AppServices));
        SetField(services, "<Settings>k__BackingField", settings);
        var model = (OverlayPanelViewModel)RuntimeHelpers.GetUninitializedObject(typeof(OverlayPanelViewModel));
        SetField(model, "_services", services);
        SetField(model, "_lastSettings", settings.Current.Overlay);
        var changed = (Action)typeof(OverlayPanelViewModel).GetMethod("OnSettingsChanged", BindingFlags.NonPublic | BindingFlags.Instance)!
            .CreateDelegate(typeof(Action), model);
        settings.Changed += changed;
        var scale = new Slider { Minimum = .6, Maximum = 2 };
        scale.SetBinding(Slider.ValueProperty, new Binding(nameof(model.Scale)) { Source = model, Mode = BindingMode.TwoWay, Delay = 100 });
        var window = new Window { Content = scale, Width = 300, Height = 100 };
        try
        {
            window.Show();
            WpfTest.Drain();
            scale.Value = 1.4;
            model.Layout = new Choice("Card", OverlayLayout.Card);
            Assert.Equal(1.4, scale.Value, 5);
            PanelEdits.Complete(scale);
            Assert.Equal(1.4, settings.Current.Overlay.Scale, 5);
            Assert.Equal(OverlayLayout.Card, settings.Current.Overlay.Layout);
        }
        finally
        {
            settings.Changed -= changed;
            window.Close();
            System.IO.File.Delete(path);
        }
    });

    [Fact]
    public void FinalDurationUpdateOfAnExpiredHorseRunIsSafe() => WpfTest.Run(() =>
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BdoTimers-finished-{Guid.NewGuid():N}.json");
        var services = (AppServices)RuntimeHelpers.GetUninitializedObject(typeof(AppServices));
        var store = new TimerStore(new JsonFileStore<AppData>(path, () => new()), new());
        SetField(services, "<Timers>k__BackingField", store);
        var panel = (CustomPanelViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CustomPanelViewModel));
        SetField(panel, "_services", services);
        SetField(panel, "_id", Guid.NewGuid());
        panel.DurationText = "2:00";
        panel.OnClosed();
        Assert.Empty(store.Current.Timers);
        Assert.False(System.IO.File.Exists(path));
    });

    [Fact]
    public void FinishFlushesTextBeforeValidationAndCloseCleanup() => WpfTest.Run(() =>
    {
        var editor = new DraftPanel();
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, new Binding(nameof(editor.Text))
            { Source = editor, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Delay = 300 });
        var main = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
        SetField(main, "<Saving>k__BackingField", new EditorSave());
        main.Panel = editor;
        main.CompletingPanelEdits += () => PanelEdits.Complete(box);
        box.Text = "";
        main.FinishPanel();
        Assert.Same(editor, main.Panel);
        Assert.False(editor.Closed);
        box.Text = "Updated";
        main.FinishPanel();
        Assert.Null(main.Panel);
        Assert.Equal("Updated", editor.TextAtClose);
    });

    [Fact]
    public void InvalidWeeklyRowsBlockCompletionUntilCorrected()
    {
        var writes = 0;
        var slots = new SlotListViewModel([new Slot(DayOfWeek.Monday, new TimeOnly(20, 0))], _ => writes++);
        slots.Rows[0].TimeText = "25:00";
        Assert.False(slots.IsValid);
        Assert.Equal(0, writes);
        slots.Rows[0].TimeText = "21:00";
        Assert.True(slots.IsValid);
        Assert.Equal(1, writes);
        slots.AddTimeCommand.Execute(null);
        slots.Rows[1].Day = slots.Rows[0].Day;
        slots.Rows[1].TimeText = slots.Rows[0].TimeText;
        Assert.False(slots.IsValid);
    }

    [Fact]
    public void OverlayPanelFocusStartsAtItsSwitch() => WpfTest.Run(() =>
    {
        var panel = new OverlayPanel { DataContext = new OverlayModel() };
        var window = new Window { Content = panel, Width = 500, Height = 600 };
        try
        {
            window.Show();
            var focus = new PanelFocusScope(window, new Grid(), panel, () => { });
            focus.Open();
            WpfTest.Drain();
            var target = Assert.IsType<ToggleButton>(Keyboard.FocusedElement);
            Assert.Equal("Overlay", AutomationProperties.GetName(target));
            Assert.True(Ui.GetInitialFocus(target));
        }
        finally { window.Close(); }
    });

    [Fact]
    public void NativeSwitchReportsToggleStateAndUpdatesBooleanBinding() => WpfTest.Run(() =>
    {
        var model = new SwitchModel();
        var button = new ToggleButton { Style = (Style)Application.Current.FindResource("OnOffSwitch") };
        button.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(model.Enabled)) { Source = model });
        Assert.Equal("Off", button.Content);
        button.IsChecked = true;
        Assert.True(model.Enabled);
        Assert.Equal("On", button.Content);
    });

    [Fact]
    public void HotkeyKeyboardActivationStartsFocusCues() => WpfTest.Run(() =>
    {
        var hotkey = new HotkeyBox();
        var window = new Window { Content = hotkey, Width = 300, Height = 100 };
        Ui.SetTrackKeyboardFocus(window, true);
        try
        {
            window.Show();
            hotkey.Focus();
            Assert.False(Ui.GetShowKeyboardFocus(window));
            Assert.True(hotkey.HandleKeyDown(Key.Space, ModifierKeys.None));
            Assert.True(Ui.GetShowKeyboardFocus(window));
            hotkey.HandleKeyDown(Key.Escape, ModifierKeys.None);
        }
        finally { window.Close(); }
    });

    static void SetField(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(instance, value);

    sealed class SwitchModel { public bool Enabled { get; set; } }
    sealed class OverlayModel
    {
        public bool Enabled { get; set; } = true;
        public bool ShowOnHotkey { get; set; }
        public bool PickerOpen { get; set; }
    }
    sealed class DraftPanel : IPanel
    {
        public string Text { get; set; } = "Original";
        public bool CanFinish => !string.IsNullOrWhiteSpace(Text);
        public bool Closed { get; private set; }
        public string? TextAtClose { get; private set; }
        public void OnClosed() { Closed = true; TextAtClose = Text; }
    }
}
