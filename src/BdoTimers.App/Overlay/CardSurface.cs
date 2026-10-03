using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BdoTimers.App.Overlay;

/// <summary>One continuous card silhouette, with curved shoulders around its clock row.</summary>
public sealed class CardSurface : Decorator
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush),
        typeof(CardSurface), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillOpacityProperty = DependencyProperty.Register(nameof(FillOpacity), typeof(double),
        typeof(CardSurface), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OutlineProperty = DependencyProperty.Register(nameof(Outline), typeof(Brush),
        typeof(CardSurface), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double FillOpacity { get => (double)GetValue(FillOpacityProperty); set => SetValue(FillOpacityProperty, value); }
    public Brush? Outline { get => (Brush?)GetValue(OutlineProperty); set => SetValue(OutlineProperty, value); }

    Geometry? _silhouette;

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = base.ArrangeOverride(arrangeSize);
        if (Child is not Grid grid || size.Width <= 0 || size.Height <= 0) return size;
        var top = grid.RowDefinitions[0].ActualHeight;
        var clockWidth = ((FrameworkElement)grid.Children[0]).ActualWidth;
        var width = size.Width;
        var height = size.Height;
        const double radius = 6;
        var shape = new StreamGeometry();
        using (var path = shape.Open())
        {
            path.BeginFigure(new Point(radius, top), true, true);
            if (top > 0)
            {
                var left = (width - clockWidth) / 2;
                var right = left + clockWidth;
                const double shoulder = 12;
                path.LineTo(new Point(left - shoulder, top), true, false);
                path.BezierTo(new Point(left, top), new Point(left, 0), new Point(left + shoulder, 0), true, false);
                path.LineTo(new Point(right - shoulder, 0), true, false);
                path.BezierTo(new Point(right, 0), new Point(right, top), new Point(right + shoulder, top), true, false);
            }
            path.LineTo(new Point(width - radius, top), true, false);
            path.QuadraticBezierTo(new Point(width, top), new Point(width, top + radius), true, false);
            path.LineTo(new Point(width, height - radius), true, false);
            path.QuadraticBezierTo(new Point(width, height), new Point(width - radius, height), true, false);
            path.LineTo(new Point(radius, height), true, false);
            path.QuadraticBezierTo(new Point(0, height), new Point(0, height - radius), true, false);
            path.LineTo(new Point(0, top + radius), true, false);
            path.QuadraticBezierTo(new Point(0, top), new Point(radius, top), true, false);
        }
        shape.Freeze();
        _silhouette = shape;
        Child.Clip = shape;
        InvalidateVisual();
        return size;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_silhouette is null) return;
        drawingContext.PushOpacity(FillOpacity);
        drawingContext.DrawGeometry(Fill, null, _silhouette);
        drawingContext.Pop();
        // A near-invisible fill keeps transparent previews draggable.
        drawingContext.DrawGeometry(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Outline is null ? null : new Pen(Outline, 1), _silhouette);
    }
}
