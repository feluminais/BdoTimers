using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Documents;
using BdoTimers.App.Controls;
using BdoTimers.App.Views.Panels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public sealed class FocusAppearanceTests
{
    [Fact]
    public void Focus_cue_stays_outside_text_and_hides_when_mouse_input_resumes() => WpfTest.Run(() =>
    {
        var cue = new Control { Style = (Style)Application.Current.FindResource("KeyboardFocusVisual") };
        var button = new Button { Content = "Timers" };
        var panel = new StackPanel();
        panel.Children.Add(button);
        var window = new Window { Content = new AdornerDecorator { Child = panel }, Width = 240, Height = 120, ShowInTaskbar = false };
        Ui.SetTrackKeyboardFocus(window, true);
        try
        {
            window.Show();
            AdornerLayer.GetAdornerLayer(button).Add(new TestFocusAdorner(button, cue));
            button.Focus();
            WpfTest.Drain();
            var rectangle = PanelFocusScope.Descendants(cue).OfType<Rectangle>().Single();
            Assert.True(rectangle.Margin.Left < -rectangle.StrokeThickness / 2);
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(button), 0, Key.Tab)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            WpfTest.Drain();
            Assert.Equal(Visibility.Visible, rectangle.Visibility);
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent });
            WpfTest.Drain();
            Assert.True(button.IsKeyboardFocused);
            Assert.Equal(Visibility.Collapsed, rectangle.Visibility);
            button.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(button), 0, Key.Right)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            WpfTest.Drain();
            Assert.Equal(Visibility.Visible, rectangle.Visibility);
        }
        finally { window.Close(); }
    });

    sealed class TestFocusAdorner : Adorner
    {
        readonly Control _cue;

        public TestFocusAdorner(UIElement target, Control cue) : base(target)
        {
            _cue = cue;
            AddVisualChild(cue);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _cue;
        protected override Size MeasureOverride(Size constraint)
        {
            // Match WPF's focus visual: the template lives in the adorner layer, outside the target's content.
            _cue.Measure(AdornedElement.RenderSize);
            return AdornedElement.RenderSize;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            _cue.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }

    [Theory]
    [InlineData("CaptionButton")]
    [InlineData("IconButton")]
    [InlineData("LinkButton")]
    public void Restored_programmatic_focus_does_not_paint_a_permanent_button_border(string style) => WpfTest.Run(() =>
    {
        var button = new Button { Content = "Open", Style = (Style)Application.Current.FindResource(style) };
        var window = new Window { Content = button, Width = 240, Height = 100, ShowInTaskbar = false };
        try
        {
            window.Show();
            WpfTest.Drain();
            button.Focus();
            WpfTest.Drain();
            var border = (Border)button.Template.FindName("Bd", button);
            Assert.Equal(button.BorderThickness, border.BorderThickness);
            Assert.NotNull(button.FocusVisualStyle);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Tile_action_does_not_keep_a_hover_border_after_focus_is_restored() => WpfTest.Run(() =>
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("TileButton") };
        var window = new Window { Content = button, Width = 240, Height = 100, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            button.Focus();
            WpfTest.Drain();
            Assert.False(button.IsMouseOver);
            Assert.Same(Application.Current.FindResource("HairlineBrush"), button.BorderBrush);
            Assert.NotNull(button.FocusVisualStyle);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Picture_action_does_not_keep_a_hover_label_after_dialog_focus_returns() => WpfTest.Run(() =>
    {
        var resources = new CustomPanel().Resources;
        var button = new Button { Style = (Style)resources["PictureButton"] };
        var window = new Window { Content = button, Width = 240, Height = 100, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            button.Focus();
            WpfTest.Drain();
            Assert.False(button.IsMouseOver);
            var label = (TextBlock)button.Template.FindName("Label", button);
            Assert.Same(Application.Current.FindResource("SubtleBrush"), label.Foreground);
            Assert.NotNull(button.FocusVisualStyle);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void Todo_checkbox_does_not_keep_a_bright_outer_frame_after_click_focus() => WpfTest.Run(() =>
    {
        var check = new CheckBox { Style = (Style)new TodoView().Resources["TodoCheck"] };
        var window = new Window { Content = check, Width = 240, Height = 100, ShowInTaskbar = false };
        try
        {
            window.Show();
            check.Focus();
            WpfTest.Drain();
            var hit = (Border)check.Template.FindName("Hit", check);
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)hit.BorderBrush).Color);
            Assert.NotNull(check.FocusVisualStyle);
        }
        finally { window.Close(); }
    });
}
