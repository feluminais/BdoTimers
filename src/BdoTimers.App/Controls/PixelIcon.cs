using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>
/// An icon drawn on whole device pixels, so its parts are the same length and weight wherever it sits and at any scale.
/// The icon font's glyphs are drawn from hairlines that land unevenly on small sizes and sit off-centre in their cell.
/// An icon says what it draws and where the pixels are; <see cref="Draw"/> places it on them.
/// </summary>
public abstract class PixelIcon : FrameworkElement
{
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(PixelIcon),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>The icon's height in device-independent units.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(PixelIcon),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Where the pixel grid was when it was last drawn.</summary>
    (double Scale, Point Origin)? _drawnAt;

    protected PixelIcon()
    {
        // A parent that scales its content sets that scale after its children are drawn, so draw again once layout is done.
        Loaded += (_, _) =>
        {
            LayoutUpdated += OnLayoutUpdated;
            OnLayoutUpdated(this, EventArgs.Empty);
        };
        Unloaded += (_, _) => LayoutUpdated -= OnLayoutUpdated;
    }

    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!TryDeviceSpace(out var scale, out var origin)) return;
        if (_drawnAt is { } drawn && Math.Abs(drawn.Scale - scale) < 1e-6 && (drawn.Origin - origin).Length < 1e-6) return;
        InvalidateVisual();
    }

    protected sealed override void OnRender(DrawingContext drawing)
    {
        // Out of a window there are no device pixels; whole units stand in until it is in one.
        var placed = TryDeviceSpace(out var scale, out var origin);
        _drawnAt = placed ? (scale, origin) : null;
        Draw(drawing, scale, origin);
    }

    /// <summary>Draws the icon; <paramref name="scale"/> is the device pixels in one unit and <paramref name="origin"/> where this element's corner falls in them.</summary>
    protected abstract void Draw(DrawingContext drawing, double scale, Point origin);

    /// <summary>A rectangle given in device pixels, as this element draws it.</summary>
    protected static Rect Local(Point origin, double scale, double x, double y, double width, double height) =>
        new(Local(origin, scale, x, y), new Size(width / scale, height / scale));

    /// <summary>A point given in device pixels, as this element draws it.</summary>
    protected static Point Local(Point origin, double scale, double x, double y) => new((x - origin.X) / scale, (y - origin.Y) / scale);

    /// <summary>Device pixels in one unit here, and where this element's corner falls in device pixels.</summary>
    bool TryDeviceSpace(out double scale, out Point origin)
    {
        scale = 1;
        origin = default;
        if (PresentationSource.FromVisual(this) is not { RootVisual: Visual root, CompositionTarget: { } target } || root == this) return false;
        try
        {
            var toRoot = TransformToAncestor(root);
            var toDevice = target.TransformToDevice;
            var corner = toDevice.Transform(toRoot.Transform(new Point(0, 0)));
            var along = toDevice.Transform(toRoot.Transform(new Point(1, 0)));
            var unit = (along - corner).Length;
            if (unit <= 0) return false;
            (scale, origin) = (unit, corner);
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }
}
