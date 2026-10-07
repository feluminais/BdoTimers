using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BdoTimers.App.Controls;

namespace BdoTimers.App.Tests;

public class PlayPauseIconTests
{
    /// <summary>Draws the icon in a round button, on its own and in white, and returns its lit pixels and the circle's centre in the picture's pixels.</summary>
    static (List<(int X, int Y, int Level)> Lit, Point Centre) Draw(bool pause, double scale, double left, double top)
    {
        var button = new Button
        {
            Style = (Style)Application.Current.FindResource("RoundButton"), Content = new PlayPauseIcon { Size = 14, ShowsPause = pause },
            Foreground = Brushes.White, BorderBrush = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10 + left, 10 + top, 0, 0),
        };
        var grid = new Grid { Width = 60, Height = 60, Background = Brushes.Black, LayoutTransform = new ScaleTransform(scale, scale), Children = { button } };
        var root = new Border { Child = grid, UseLayoutRounding = true };
        var window = new Window { Content = root, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, Top = -10000, ShowInTaskbar = false, WindowStyle = WindowStyle.None, UseLayoutRounding = true };
        try
        {
            window.Show();
            WpfTest.Drain();
            var dpi = VisualTreeHelper.GetDpi(window).DpiScaleX;
            var width = (int)Math.Ceiling(root.ActualWidth * dpi);
            var height = (int)Math.Ceiling(root.ActualHeight * dpi);
            var image = new RenderTargetBitmap(width, height, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
            image.Render(root);
            var pixels = new int[width * height];
            image.CopyPixels(pixels, width * 4, 0);
            var lit = Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Select(x => (X: x, Y: y, Level: pixels[y * width + x] & 0xFF)))
                .Where(p => p.Level > 0).ToList();
            var middle = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), root);
            return (lit, new Point(middle.X * dpi, middle.Y * dpi));
        }
        finally { window.Close(); }
    }

    /// <summary>The middle, in pixels, of the stretch the lit pixels cover along one axis.</summary>
    static double Middle(IEnumerable<int> lit) => (lit.Min() + lit.Max() + 1) / 2.0;

    [Theory]
    [InlineData(1.0, 0, 0)]
    [InlineData(1.25, 3.3, 2.7)]
    [InlineData(1.5, 7.3, 5.9)]
    [InlineData(1.75, 5.5, 6.5)]
    [InlineData(2.0, 1.3, 1.7)]
    public void Pause_is_two_equal_bars_on_the_middle_of_the_circle_at_any_scale_and_position(double scale, double left, double top) => WpfTest.Run(() =>
    {
        var (lit, centre) = Draw(pause: true, scale, left, top);
        var strong = lit.Where(p => p.Level > 127).ToList();
        Assert.NotEmpty(strong);
        Assert.InRange(Math.Abs(Middle(strong.Select(p => p.X)) - centre.X), 0, 1);
        Assert.InRange(Math.Abs(Middle(strong.Select(p => p.Y)) - centre.Y), 0, 1);
        // The row through the middle has two runs of lit pixels of the same width.
        var row = (strong.Min(p => p.Y) + strong.Max(p => p.Y)) / 2;
        var widths = new List<int>();
        var previous = int.MinValue;
        foreach (var x in strong.Where(p => p.Y == row).Select(p => p.X).Order())
        {
            if (x == previous + 1) widths[^1]++;
            else widths.Add(1);
            previous = x;
        }
        Assert.Equal(2, widths.Count);
        Assert.Equal(widths[0], widths[1]);
    });

    [Theory]
    [InlineData(1.0, 0, 0)]
    [InlineData(1.25, 3.3, 2.7)]
    [InlineData(1.5, 7.3, 5.9)]
    [InlineData(1.75, 5.5, 6.5)]
    [InlineData(2.0, 1.3, 1.7)]
    public void Play_has_its_weight_on_the_middle_of_the_circle_at_any_scale_and_position(double scale, double left, double top) => WpfTest.Run(() =>
    {
        var (lit, centre) = Draw(pause: false, scale, left, top);
        Assert.NotEmpty(lit);
        Assert.InRange(Math.Abs(Middle(lit.Where(p => p.Level > 127).Select(p => p.Y)) - centre.Y), 0, 1);
        // The triangle's centre of weight, not the middle of its box, is where the circle's middle is.
        var weight = lit.Sum(p => (double)p.Level);
        var weightX = lit.Sum(p => (p.X + 0.5) * p.Level) / weight;
        Assert.InRange(Math.Abs(weightX - centre.X), 0, 1);
    });
}
