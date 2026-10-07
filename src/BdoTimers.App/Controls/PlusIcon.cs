using System.Windows;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>
/// A plus drawn from two bars placed on whole device pixels, so both arms are the same length and weight wherever it
/// sits and at any scale. The icon font's plus is drawn from hairlines that land unevenly on small sizes, which makes one
/// arm look shorter. <see cref="PixelIcon.Size"/> is the length of an arm tip to arm tip.
/// </summary>
public sealed class PlusIcon : PixelIcon
{
    protected override void Draw(DrawingContext drawing, double scale, Point origin)
    {
        // Whole device pixels: the arms and the bar weight, with the same parity so the bars centre on each other.
        var thickness = Math.Max(1, (int)Math.Round(1.4 * scale));
        var length = (int)Math.Round(Size * scale);
        if (length % 2 != thickness % 2) length++;
        var left = Math.Round(origin.X + ActualWidth * scale / 2 - length / 2.0);
        var top = Math.Round(origin.Y + ActualHeight * scale / 2 - length / 2.0);
        var inset = (length - thickness) / 2;
        drawing.DrawRectangle(Foreground, null, Local(origin, scale, left, top + inset, length, thickness));
        drawing.DrawRectangle(Foreground, null, Local(origin, scale, left + inset, top, thickness, length));
    }
}
