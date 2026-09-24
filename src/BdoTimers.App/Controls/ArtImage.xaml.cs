using System.Windows;
using System.Windows.Controls;

namespace BdoTimers.App.Controls;

/// <summary>One or more pictures stacked vertically, each cropped around its focus, fading to transparent toward the right.</summary>
public partial class ArtImage : UserControl
{
    public static readonly DependencyProperty SourcesProperty =
        DependencyProperty.Register(nameof(Sources), typeof(IReadOnlyList<ArtPicture>), typeof(ArtImage));

    public ArtImage() => InitializeComponent();

    public IReadOnlyList<ArtPicture>? Sources
    {
        get => (IReadOnlyList<ArtPicture>?)GetValue(SourcesProperty);
        set => SetValue(SourcesProperty, value);
    }
}
