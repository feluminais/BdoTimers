using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BdoTimers.App.Controls;

namespace BdoTimers.App.Tests;

public class PlusIconTests
{
    [Theory]
    [InlineData(1.0, 0, 0)]
    [InlineData(1.25, 3.3, 2.7)]
    [InlineData(1.5, 8, 0)]
    [InlineData(1.5, 7.3, 5.9)]
    [InlineData(1.65, 0, 0)]
    [InlineData(1.65, 8.4, 3.1)]
    [InlineData(1.75, 5.5, 6.5)]
    [InlineData(2.0, 1.3, 1.7)]
    [InlineData(2.25, 9.1, 4.4)]
    public void Both_arms_are_as_long_and_as_heavy_at_any_scale_and_position(double scale, double left, double top) => WpfTest.Run(() =>
    {
        var plus = new PlusIcon
        {
            Size = 11, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10 + left, 10 + top, 0, 0),
        };
        var grid = new Grid { Width = 40, Height = 40, Background = Brushes.Black, LayoutTransform = new ScaleTransform(scale, scale), Children = { plus } };
        var root = new Border { Child = grid, UseLayoutRounding = true };
        var window = new Window { Content = root, SizeToContent = SizeToContent.WidthAndHeight, Left = -10000, Top = -10000, ShowInTaskbar = false, WindowStyle = WindowStyle.None, UseLayoutRounding = true };
        try
        {
            window.Show();
            WpfTest.Drain();
            // The bitmap has the window's own pixel grid, as the screen does.
            var dpi = VisualTreeHelper.GetDpi(window).DpiScaleX;
            var width = (int)Math.Ceiling(root.ActualWidth * dpi);
            var height = (int)Math.Ceiling(root.ActualHeight * dpi);
            var image = new RenderTargetBitmap(width, height, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
            image.Render(root);
            var pixels = new int[width * height];
            image.CopyPixels(pixels, width * 4, 0);
            bool On(int x, int y) => (pixels[y * width + x] & 0xFF) > 127;
            var points = Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Where(x => On(x, y)).Select(x => (x, y))).ToList();
            Assert.NotEmpty(points);
            var (minX, maxX, minY, maxY) = (points.Min(p => p.x), points.Max(p => p.x), points.Min(p => p.y), points.Max(p => p.y));
            Assert.Equal(maxX - minX, maxY - minY);
            // The bar through the middle of each axis has the same number of pixels across and along.
            var midX = (minX + maxX) / 2;
            var midY = (minY + maxY) / 2;
            Assert.Equal(Enumerable.Range(minX, maxX - minX + 1).Count(x => On(x, midY)), Enumerable.Range(minY, maxY - minY + 1).Count(y => On(midX, y)));
        }
        finally { window.Close(); }
    });
}
