using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>
/// A Border that also rounds its child's corners; Border itself clips to the rectangle, so a picture running to the edge
/// would poke out of the corner. Meant for a uniform corner radius and border and no padding.
/// </summary>
public sealed class ClippingBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (Child is { } child)
        {
            var thickness = Math.Max(Math.Max(BorderThickness.Left, BorderThickness.Top), Math.Max(BorderThickness.Right, BorderThickness.Bottom));
            var radius = Math.Max(0, CornerRadius.TopLeft - thickness);
            child.Clip = new RectangleGeometry(new Rect(child.RenderSize), radius, radius);
        }
        return size;
    }
}
