using System.Windows;
using System.Windows.Controls;

namespace BdoTimers.App.Views;

public partial class TodayView : UserControl
{
    /// <summary>Below this width, in the screen's own units, the side column joins the main one.</summary>
    const double WideWidth = 820;

    bool _wide = true;

    public TodayView() => InitializeComponent();

    /// <summary>One column when the screen is narrow: hero, Running, Coming up, Daily tasks, Weekly tasks, Garmoth.</summary>
    void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= WideWidth;
        if (wide == _wide) return;
        _wide = wide;
        foreach (var panel in new FrameworkElement[] { Hero, RunningPanel, ComingUpPanel, DailyPanel, WeeklyPanel, GarmothPanel })
            (panel.Parent as Panel)?.Children.Remove(panel);
        if (wide)
        {
            MainHost.Children.Add(Hero);
            MainHost.Children.Add(ComingUpPanel);
            SideStack.Children.Add(RunningPanel);
            SideStack.Children.Add(DailyPanel);
            SideStack.Children.Add(WeeklyPanel);
            SideStack.Children.Add(GarmothPanel);
            Hero.Margin = new Thickness(0);
            ComingUpPanel.Margin = new Thickness(0, 16, 0, 0);
            ComingUpPanel.MaxHeight = double.PositiveInfinity;
            RunningPanel.Margin = new Thickness(0, 0, 0, 16);
        }
        else
        {
            foreach (var panel in new FrameworkElement[] { Hero, RunningPanel, ComingUpPanel, DailyPanel, WeeklyPanel, GarmothPanel })
            {
                NarrowStack.Children.Add(panel);
                panel.Margin = new Thickness(0, 0, 0, 16);
            }
            // The list scrolls inside itself, as in the wide layout.
            ComingUpPanel.MaxHeight = 380;
        }
        MainHost.Visibility = SideHost.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        NarrowHost.Visibility = wide ? Visibility.Collapsed : Visibility.Visible;
    }
}
