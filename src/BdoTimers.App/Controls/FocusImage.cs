using System.Windows;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>A picture for <see cref="ArtImage"/>; <see cref="Focus"/> is the point to keep in view, such as a boss's
/// head, as fractions of the picture's width and height. Null crops around the centre.</summary>
/// <remarks>A class, not a record: the setup window links this file and builds for .NET Framework, which lacks
/// the runtime support records need.</remarks>
public sealed class ArtPicture(ImageSource source, Point? focus = null)
{
    public ImageSource Source { get; } = source;
    public Point? Focus { get; } = focus;
}

/// <summary>
/// Fills its box like UniformToFill, but cropped so the picture's focus sits a little left of centre, in the part
/// ArtImage leaves unfaded. Zooms in (at most <see cref="MaxZoom"/> times) when filling alone can't get it there.
/// </summary>
public sealed class FocusImage : FrameworkElement
{
    const double MaxZoom = 2.5;
    static readonly Point Target = new(0.38, 0.45);

    public static readonly DependencyProperty PictureProperty = DependencyProperty.Register(
        nameof(Picture), typeof(ArtPicture), typeof(FocusImage),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public FocusImage()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        ClipToBounds = true;
    }

    public ArtPicture? Picture
    {
        get => (ArtPicture?)GetValue(PictureProperty);
        set => SetValue(PictureProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Picture?.Source is not { Width: > 0, Height: > 0 } source || ActualWidth <= 0 || ActualHeight <= 0) return;
        dc.DrawImage(source, Place(new Size(source.Width, source.Height), new Size(ActualWidth, ActualHeight), Picture.Focus));
    }

    /// <summary>Where to draw a picture of <paramref name="image"/> size so it covers <paramref name="box"/>.</summary>
    static Rect Place(Size image, Size box, Point? focus)
    {
        var f = focus ?? new Point(0.5, 0.5);
        var t = focus is null ? new Point(0.5, 0.5) : Target;
        var fill = Math.Max(box.Width / image.Width, box.Height / image.Height);
        var scale = Math.Max(Needed(t.X * box.Width, (1 - t.X) * box.Width, f.X, image.Width),
                             Needed(t.Y * box.Height, (1 - t.Y) * box.Height, f.Y, image.Height));
        scale = Math.Min(Math.Max(scale, fill), fill * MaxZoom);
        var width = image.Width * scale;
        var height = image.Height * scale;
        // Never show an edge: the picture always covers the whole box.
        var x = Math.Min(0, Math.Max(box.Width - width, t.X * box.Width - f.X * width));
        var y = Math.Min(0, Math.Max(box.Height - height, t.Y * box.Height - f.Y * height));
        return new Rect(x, y, width, height);
    }

    /// <summary>The scale at which the picture reaches <paramref name="before"/> pixels before its focus and
    /// <paramref name="after"/> pixels after it along one axis.</summary>
    static double Needed(double before, double after, double focus, double length) =>
        Math.Max(focus > 0 ? before / (focus * length) : 0, focus < 1 ? after / ((1 - focus) * length) : 0);
}
