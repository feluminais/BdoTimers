using System.Windows;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>
/// Play, or pause when <see cref="ShowsPause"/> is set, for a round button: the two pause bars are the same width with the
/// same room either side, and the play triangle sits with its weight rather than its box on the middle, so neither is
/// off-centre in the circle the way the icon font's glyphs are. <see cref="PixelIcon.Size"/> is the height of both.
/// </summary>
public sealed class PlayPauseIcon : PixelIcon
{
    public static readonly DependencyProperty ShowsPauseProperty = DependencyProperty.Register(nameof(ShowsPause), typeof(bool), typeof(PlayPauseIcon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool ShowsPause { get => (bool)GetValue(ShowsPauseProperty); set => SetValue(ShowsPauseProperty, value); }

    protected override void Draw(DrawingContext drawing, double scale, Point origin)
    {
        // The centre doubled is even on a pixel edge and odd in a pixel's middle; an extent of the same parity puts both
        // of its ends on whole pixels, the same distance from the centre.
        var centreX2 = Math.Round(2 * (origin.X + ActualWidth * scale / 2));
        var centreY2 = Math.Round(2 * (origin.Y + ActualHeight * scale / 2));
        var height = Fit(Math.Max(2, Math.Round(Size * scale)), centreY2);
        var top = (centreY2 - height) / 2;
        if (ShowsPause)
        {
            var bar = Math.Max(1, Math.Round(height * 0.17));
            var gap = Fit(Math.Max(1, Math.Round(height * 0.26)), centreX2);
            var left = (centreX2 - 2 * bar - gap) / 2;
            drawing.DrawRectangle(Foreground, null, Local(origin, scale, left, top, bar, height));
            drawing.DrawRectangle(Foreground, null, Local(origin, scale, left + bar + gap, top, bar, height));
            return;
        }
        var width = Math.Round(height * 0.85);
        var edge = Math.Round(centreX2 / 2 - width / 2 + width / 7);
        var triangle = new StreamGeometry();
        using (var figure = triangle.Open())
        {
            figure.BeginFigure(Local(origin, scale, edge, top), true, true);
            figure.LineTo(Local(origin, scale, edge + width, top + height / 2), false, false);
            figure.LineTo(Local(origin, scale, edge, top + height), false, false);
        }
        triangle.Freeze();
        drawing.DrawGeometry(Foreground, null, triangle);
    }

    /// <summary>The extent or the next one up that has the parity of <paramref name="centre2"/>, twice the centre it is placed on.</summary>
    static double Fit(double extent, double centre2) => (centre2 - extent) % 2 == 0 ? extent : extent + 1;
}
