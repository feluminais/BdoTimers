using System.Windows;
using System.Windows.Controls;

namespace BdoTimers.App.Controls;

/// <summary>One or more pictures stacked vertically, each cropped around its focus, fading to transparent toward the right.</summary>
public partial class ArtImage : UserControl
{
    public static readonly DependencyProperty SourcesProperty =
        DependencyProperty.Register(nameof(Sources), typeof(IReadOnlyList<ArtPicture>), typeof(ArtImage));

    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register(nameof(MaxZoom), typeof(double), typeof(ArtImage), new PropertyMetadata(2.5));

    public ArtImage() => InitializeComponent();

    public IReadOnlyList<ArtPicture>? Sources
    {
        get => (IReadOnlyList<ArtPicture>?)GetValue(SourcesProperty);
        set => SetValue(SourcesProperty, value);
    }

    /// <summary>See <see cref="FocusImage.MaxZoom"/>.</summary>
    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }
}
