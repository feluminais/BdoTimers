using System.Windows;
using System.Windows.Controls;

namespace BdoTimers.App.Views.Panels;

public partial class CustomPanel : UserControl
{
    public CustomPanel() => InitializeComponent();

    void PictureButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = PictureButton.ContextMenu!;
        menu.DataContext = DataContext;
        menu.PlacementTarget = PictureButton;
        menu.IsOpen = true;
    }
}
