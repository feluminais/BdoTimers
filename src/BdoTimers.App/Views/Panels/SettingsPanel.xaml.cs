using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
                var terms = new StringBuilder(heading.Text).Append(' ').Append(heading.Tag);
                CollectTerms(row, terms);
                var match = SettingsFilter.Matches(SearchBox.Text, terms.ToString());
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

    static void CollectTerms(DependencyObject item, StringBuilder terms)
    {
        if (item is FrameworkElement element)
        {
            terms.Append(' ').Append(element.Tag).Append(' ').Append(AutomationProperties.GetName(element));
            if (element is TextBlock text) terms.Append(' ').Append(text.Text);
            if (element is ContentControl content && content.Content is string label) terms.Append(' ').Append(label);
            if (element is HeaderedContentControl header) terms.Append(' ').Append(header.Header);
        }
        foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) CollectTerms(child, terms);
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
