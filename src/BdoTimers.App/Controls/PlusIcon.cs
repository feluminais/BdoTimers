using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>
/// A plus drawn from two bars placed on whole device pixels, so both arms are the same length and weight wherever it
/// sits and at any scale. The icon font's plus is drawn from hairlines that land unevenly on small sizes, which makes one
/// arm look shorter.
/// </summary>
public sealed class PlusIcon : FrameworkElement
{
    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(typeof(PlusIcon),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>The length of an arm tip to arm tip, in device-independent units.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(PlusIcon),
        new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Where the pixel grid was when it was last drawn.</summary>
    (double Scale, Point Origin)? _drawnAt;

    public PlusIcon()
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

    protected override void OnRender(DrawingContext drawing)
    {
        if (!TryDeviceSpace(out var scale, out var origin))
        {
            _drawnAt = null;
            var thin = Size * 0.14;
            drawing.DrawRectangle(Foreground, null, new Rect(0, (ActualHeight - thin) / 2, Size, thin));
            drawing.DrawRectangle(Foreground, null, new Rect((ActualWidth - thin) / 2, 0, thin, Size));
            return;
        }
        _drawnAt = (scale, origin);
        // Whole device pixels: the arms and the bar weight, with the same parity so the bars centre on each other.
        var thickness = Math.Max(1, (int)Math.Round(1.4 * scale));
        var length = (int)Math.Round(Size * scale);
        if (length % 2 != thickness % 2) length++;
        var left = Math.Round(origin.X + ActualWidth * scale / 2 - length / 2.0);
        var top = Math.Round(origin.Y + ActualHeight * scale / 2 - length / 2.0);
        var inset = (length - thickness) / 2;
        Rect Local(double x, double y, double width, double height) =>
            new((x - origin.X) / scale, (y - origin.Y) / scale, width / scale, height / scale);
        drawing.DrawRectangle(Foreground, null, Local(left, top + inset, length, thickness));
        drawing.DrawRectangle(Foreground, null, Local(left + inset, top, thickness, length));
    }

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
            scale = (along - corner).Length;
            origin = corner;
            return scale > 0;
        }
        catch (InvalidOperationException) { return false; }
    }
}
