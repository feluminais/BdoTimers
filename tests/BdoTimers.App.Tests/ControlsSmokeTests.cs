using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using BdoTimers.App.Controls;
using BdoTimers.App.Theme;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.App.Views.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;

namespace BdoTimers.App.Tests;

public sealed class ControlsSmokeTests
{
    [Fact]
    public void CycleSelector_wraps_with_keys_and_reports_setting_and_value()
    {
        WpfTest.Run(() =>
        {
            var model = new SelectionModel { Value = Choice.OnOff[0] };
            var selector = new CycleSelector { ItemsSource = (IList)Choice.OnOff, ValueWidth = 110 };
            AutomationProperties.SetName(selector, "Close to tray");
            selector.SetBinding(CycleSelector.SelectedItemProperty, new Binding(nameof(SelectionModel.Value)) { Source = model });
            var window = new Window { Content = selector, Width = 250, Height = 100, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                Assert.True(selector.Focusable);
                Assert.True(selector.IsTabStop);
                Assert.All(Descendants(selector).OfType<Button>(), button => Assert.False(button.IsTabStop));
                var peer = UIElementAutomationPeer.CreatePeerForElement(selector)!;
                var provider = Assert.IsAssignableFrom<IValueProvider>(peer.GetPattern(PatternInterface.Value));
                Assert.Equal("Close to tray", peer.GetName());
                Assert.Equal("On", provider.Value);
                Key(selector, window, System.Windows.Input.Key.Left);
                Assert.Equal("Off", provider.Value);
                Assert.Equal(Choice.OnOff[1], model.Value);
                Key(selector, window, System.Windows.Input.Key.Right);
                Assert.Equal("On", provider.Value);
                provider.SetValue("Off");
                Assert.Equal(Choice.OnOff[1], model.Value);
                Assert.True(BindingOperations.IsDataBound(selector, CycleSelector.SelectedItemProperty));
                selector.IsEnabled = false;
                Assert.True(provider.IsReadOnly);
                Assert.Throws<ElementNotEnabledException>(() => provider.SetValue("On"));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void DateInput_preserves_invalid_edits_and_calendar_updates_the_bound_ISO_text()
    {
        WpfTest.Run(() =>
        {
            var model = new DateModel { Text = "2026-10-02" };
            var input = new DateInput { Today = new DateTime(2026, 10, 2) };
            AutomationProperties.SetName(input, "Event date");
            input.SetBinding(DateInput.TextProperty, new Binding(nameof(DateModel.Text)) { Source = model });
            var box = (TextBox)input.FindName("DateBox");
            var calendar = (Calendar)input.FindName("Picker");
            box.Text = "2026-02-31";
            Assert.Equal("2026-02-31", input.Text);
            Assert.Equal("2026-02-31", model.Text);
            Assert.Null(calendar.SelectedDate);
            input.HasError = true;
            Assert.True(Ui.GetHasError(box));
            Assert.Equal("Event date", AutomationProperties.GetName(box));
            calendar.SelectedDate = new DateTime(2026, 10, 5);
            Assert.Equal("2026-10-05", model.Text);
            Assert.True(BindingOperations.IsDataBound(input, DateInput.TextProperty));
            box.Clear();
            Assert.Equal("", model.Text);
            Assert.Null(calendar.SelectedDate);
            var popup = (Popup)input.FindName("CalendarPopup");
            var tomorrow = Descendants(popup.Child).OfType<Button>().Single(button => Equals(button.Content, "Tomorrow"));
            tomorrow.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal("2026-10-03", model.Text);
            var currentDate = new DateTime(2026, 10, 2);
            input.TodayProvider = () => currentDate;
            currentDate = currentDate.AddDays(1);
            tomorrow.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Assert.Equal("2026-10-04", model.Text);
        });
    }

    [Fact]
    public void Calendar_popup_keeps_arrow_navigation_open_and_Enter_confirms()
    {
        WpfTest.Run(() =>
        {
            var input = new DateInput { Today = new DateTime(2026, 10, 2), Text = "2026-10-02" };
            var window = new Window { Content = input, Width = 300, Height = 100, ShowInTaskbar = false };
            var popup = (Popup)input.FindName("CalendarPopup");
            try
            {
                window.Show();
                ((Button)input.FindName("CalendarButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WpfTest.Drain();
                var calendar = (Calendar)input.FindName("Picker");
                Assert.True(popup.IsOpen);
                Assert.NotEmpty(Descendants(calendar).OfType<CalendarDayButton>());
                Key(calendar, window, System.Windows.Input.Key.Right);
                Assert.Equal("2026-10-03", input.Text);
                Assert.True(popup.IsOpen);
                calendar.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(calendar), 0, System.Windows.Input.Key.Enter)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                Assert.False(popup.IsOpen);
            }
            finally { popup.IsOpen = false; window.Close(); }
        });
    }

    [Fact]
    public void Combo_box_typing_selects_items_by_any_word_and_Escape_restores_an_open_list()
    {
        WpfTest.Run(() =>
        {
            Zone[] zones = [new("(UTC+01:00) Warsaw"), new("(UTC+02:00) Helsinki, Kyiv"), new("(UTC+02:00) Kaliningrad"), new("(UTC+09:00) Tokyo")];
            var box = new ComboBox { ItemsSource = zones, DisplayMemberPath = nameof(Zone.DisplayName), SelectedIndex = 0 };
            var window = new Window { Content = box, Width = 300, Height = 100, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                Assert.True(box.Focus());
                Type(box, "ka");
                Assert.Equal(2, box.SelectedIndex);
                Type(box, "x");
                Assert.Equal(2, box.SelectedIndex);
                Key(box, window, System.Windows.Input.Key.Back, Keyboard.PreviewKeyDownEvent);
                Key(box, window, System.Windows.Input.Key.Back, Keyboard.PreviewKeyDownEvent);
                Assert.Equal(1, box.SelectedIndex);

                box.IsDropDownOpen = true;
                WpfTest.Drain();
                Type((UIElement)Keyboard.FocusedElement, "tok");
                var tokyo = (ComboBoxItem)box.ItemContainerGenerator.ContainerFromIndex(3);
                Assert.Equal(3, box.SelectedIndex);
                Assert.True(tokyo.IsKeyboardFocused);
                Assert.True(box.IsDropDownOpen);
                Key(tokyo, window, System.Windows.Input.Key.Escape, Keyboard.PreviewKeyDownEvent);
                Assert.Equal(1, box.SelectedIndex);

                box.IsDropDownOpen = false;
                box.IsDropDownOpen = true;
                WpfTest.Drain();
                Type((UIElement)Keyboard.FocusedElement, "war");
                Key((UIElement)Keyboard.FocusedElement, window, System.Windows.Input.Key.Enter);
                Assert.False(box.IsDropDownOpen);
                Assert.Equal(0, box.SelectedIndex);
            }
            finally { box.IsDropDownOpen = false; window.Close(); }
        });
    }

    [Fact]
    public void Settings_search_filters_rows_and_headers_and_handles_no_matches()
    {
        WpfTest.Run(() =>
        {
            var panel = new SettingsPanel();
            var window = new Window { Content = panel, Width = 500, Height = 600, ShowInTaskbar = false };
            try
            {
                window.Show();
                WpfTest.Drain();
                var search = (TextBox)panel.FindName("SearchBox");
                var items = (StackPanel)panel.FindName("SettingsItems");
                search.Text = "volume";
                var rows = items.Children.OfType<FrameworkElement>().Where(row => row is not TextBlock && row.Visibility == Visibility.Visible).ToList();
                Assert.Single(rows);
                Assert.Equal("Volume", Assert.IsType<HeaderedContentControl>(rows[0]).Header);
                Assert.Single(items.Children.OfType<TextBlock>(), title => title.Visibility == Visibility.Visible);
                search.Text = "no-such-setting";
                Assert.Equal(Visibility.Visible, ((TextBlock)panel.FindName("NoResults")).Visibility);
                Assert.All(items.Children.OfType<FrameworkElement>(), row => Assert.Equal(Visibility.Collapsed, row.Visibility));
                search.Clear();
                Assert.Equal(Visibility.Collapsed, ((TextBlock)panel.FindName("NoResults")).Visibility);
                Assert.All(items.Children.OfType<FrameworkElement>(), row => Assert.Equal(Visibility.Visible, row.Visibility));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void Text_size_updates_the_live_scale_and_danger_text_has_readable_contrast()
    {
        WpfTest.Run(() =>
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"BdoTimers-text-{Guid.NewGuid():N}.json");
            var settings = new PersistentState<AppSettings>(new JsonFileStore<AppSettings>(path, () => new()), new());
            var application = Application.Current;
            var original = application.Resources["UiScaleTransform"];
            try
            {
                using var preferences = ThemePreferences.Initialize(application, settings);
                var firstScale = ((ScaleTransform)application.FindResource("UiScaleTransform")).ScaleX;
                settings.Update(value => value with { TextScale = 1.5 });
                Assert.Equal(firstScale * 1.5, ((ScaleTransform)application.FindResource("UiScaleTransform")).ScaleX, precision: 5);
                var danger = ((SolidColorBrush)application.FindResource("DangerBrush")).Color;
                var background = ((SolidColorBrush)application.FindResource("PanelBrush")).Color;
                var foregroundLight = Luminance(danger);
                var backgroundLight = Luminance(background);
                Assert.True((Math.Max(foregroundLight, backgroundLight) + .05) / (Math.Min(foregroundLight, backgroundLight) + .05) >= 4.5);
                foreach (var name in new[] { "BareButton", "NameButton", "TileButton", "TabButton" })
                    Assert.NotNull(((Style)application.FindResource(name)).Setters.OfType<Setter>()
                        .Single(setter => setter.Property == Control.FocusVisualStyleProperty).Value);
            }
            finally
            {
                if (original is not null) application.Resources["UiScaleTransform"] = original;
                else application.Resources.Remove("UiScaleTransform");
                System.IO.File.Delete(path);
            }
        });
    }

    static void Key(UIElement selector, Window window, Key key, RoutedEvent? routedEvent = null) => selector.RaiseEvent(new KeyEventArgs(
        Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = routedEvent ?? Keyboard.KeyDownEvent });

    static void Type(UIElement target, string text)
    {
        foreach (var letter in text)
            target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, target, letter.ToString()))
                { RoutedEvent = UIElement.PreviewTextInputEvent });
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    static double Luminance(Color color)
    {
        static double Channel(byte value) => value / 255d <= .04045 ? value / 255d / 12.92 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
    }

    sealed class SelectionModel { public object? Value { get; set; } }
    sealed class DateModel { public string Text { get; set; } = ""; }
    sealed record Zone(string DisplayName);
}
