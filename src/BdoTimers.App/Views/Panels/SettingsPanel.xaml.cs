using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.Views.Panels;

public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplySearch();
            if (DataContext is SettingsPanelViewModel { OpenAtRegion: true })
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ScrollToBosses);
        };
        DataContextChanged += (_, _) => Dispatcher.BeginInvoke(ApplySearch);
    }

    /// <summary>Puts the Bosses heading, with Region right under it, at the top of the list.</summary>
    void ScrollToBosses() =>
        Scroller.ScrollToVerticalOffset(BossesHeading.TransformToAncestor(SettingsItems).Transform(default).Y);

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SettingsItems is not null) ApplySearch();
    }

    void ClearSearch_Click(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }

    void Panel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Controls.Ui.UseKeyboardFocus(this);
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocused && SearchBox.Text.Length > 0)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
    }

    void ApplySearch()
    {
        TextBlock? heading = null;
        var rows = new List<FrameworkElement>();
        var found = false;
        void FilterSection()
        {
            if (heading is null) return;
            var sectionFound = false;
            foreach (var row in rows)
            {
                var match = SettingsFilter.Matches(SearchBox.Text, $"{heading.Text} {heading.Tag} {row.Tag}");
                SettingsFilter.SetIsMatch(row, match);
                sectionFound |= match;
            }
            SettingsFilter.SetIsMatch(heading, sectionFound);
            found |= sectionFound;
        }
        foreach (FrameworkElement item in SettingsItems.Children)
        {
            if (item is TextBlock title)
            {
                FilterSection();
                heading = title;
                rows.Clear();
            }
            else rows.Add(item);
        }
        FilterSection();
        NoResults.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
    }

}

/// <summary>Search visibility is separate from settings availability and validation.</summary>
public static class SettingsFilter
{
    public static readonly DependencyProperty IsMatchProperty = DependencyProperty.RegisterAttached("IsMatch", typeof(bool),
        typeof(SettingsFilter), new FrameworkPropertyMetadata(true));
    public static bool GetIsMatch(DependencyObject item) => (bool)item.GetValue(IsMatchProperty);
    public static void SetIsMatch(DependencyObject item, bool value) => item.SetValue(IsMatchProperty, value);

    public static bool Matches(string query, string terms) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .All(word => terms.Contains(word, StringComparison.OrdinalIgnoreCase));
}
