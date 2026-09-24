using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>One or more pictures stacked vertically, fading to transparent toward the right.</summary>
public partial class ArtImage : UserControl
{
    public static readonly DependencyProperty SourcesProperty =
        DependencyProperty.Register(nameof(Sources), typeof(IReadOnlyList<ImageSource>), typeof(ArtImage));

    public ArtImage() => InitializeComponent();

    public IReadOnlyList<ImageSource>? Sources
    {
        get => (IReadOnlyList<ImageSource>?)GetValue(SourcesProperty);
        set => SetValue(SourcesProperty, value);
    }
}
