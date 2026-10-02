using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BdoTimers.App.Views.Panels;
using BdoTimers.App.Views;

namespace BdoTimers.App.Tests;

public sealed class FocusAppearanceTests
{
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
