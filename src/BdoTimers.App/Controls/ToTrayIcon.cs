using System.Windows;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>
/// The caption button that sends the window to the tray: a chevron pointing down at a line. Its strokes are as thin as the
/// other caption icons' and sit on whole device pixels, so they stay as light and as even as those at any scale.
/// <see cref="PixelIcon.Size"/> is its width and height.
/// </summary>
public sealed class ToTrayIcon : PixelIcon
{
    protected override void Draw(DrawingContext drawing, double scale, Point origin)
    {
        var (centreX2, centreY2) = CentreDoubled(origin, scale);
        var extent = Math.Max(4, Math.Round(Size * scale));
        var (width, height) = (Fit(extent, centreX2), Fit(extent, centreY2));
        var (left, top) = ((centreX2 - width) / 2, (centreY2 - height) / 2);
        // A little under the scale, as the font's own thin strokes are lighter than a whole pixel row at fractional scales.
        var weight = Math.Max(1, Math.Round(scale * 0.9));
        // The chevron's arms run at 45°, so a stroke of this weight is this wide across the arm's own width.
        var across = weight * Math.Sqrt(2);
        var depth = width / 2;
        var tip = left + width / 2;
        var chevron = new StreamGeometry();
        using (var figure = chevron.Open())
        {
            figure.BeginFigure(Local(origin, scale, left, top), true, true);
            figure.LineTo(Local(origin, scale, left + across, top), false, false);
            figure.LineTo(Local(origin, scale, tip, top + depth - across), false, false);
            figure.LineTo(Local(origin, scale, left + width - across, top), false, false);
            figure.LineTo(Local(origin, scale, left + width, top), false, false);
            figure.LineTo(Local(origin, scale, tip, top + depth), false, false);
        }
        chevron.Freeze();
        drawing.DrawGeometry(Foreground, null, chevron);
        drawing.DrawRectangle(Foreground, null, Local(origin, scale, left, top + height - weight, width, weight));
    }
}
