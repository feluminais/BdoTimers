using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BdoTimers.App.ViewModels.Panels;

namespace BdoTimers.App.Views.Panels;

public partial class SettingsPanel : UserControl
{
    string _category = "General";

    public SettingsPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplySearch();
            if (DataContext is SettingsPanelViewModel { OpenAtRegion: true }) BossesNav.IsChecked = true;
        };
        DataContextChanged += (_, _) => Dispatcher.BeginInvoke(ApplySearch);
    }

    /// <summary>Shows one category; picking one also ends a search.</summary>
    void Category_Checked(object sender, RoutedEventArgs e)
    {
        if (SettingsItems is null) return;
        _category = (string)((RadioButton)sender).Tag;
        if (SearchBox.Text.Length > 0) SearchBox.Clear();
        else ApplySearch();
        Scroller.ScrollToTop();
    }

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

    /// <summary>With a search, every category's matching rows under their headings; without, the chosen category's rows alone.</summary>
    void ApplySearch()
    {
        var searching = !string.IsNullOrWhiteSpace(SearchBox.Text);
        TextBlock? heading = null;
        var rows = new List<FrameworkElement>();
        var found = false;
        void FilterSection()
        {
            if (heading is null) return;
            var category = SettingsFilter.GetCategory(heading);
            var sectionFound = false;
            foreach (var row in rows)
            {
                SettingsFilter.SetCategory(row, category);
                var match = searching
                    ? SettingsFilter.Matches(SearchBox.Text, $"{heading.Text} {heading.Tag} {row.Tag}")
                    : category == _category;
                SettingsFilter.SetIsMatch(row, match);
                sectionFound |= match;
            }
            SettingsFilter.SetIsMatch(heading, searching && sectionFound);
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

    /// <summary>The category a heading, or a row under one, belongs to.</summary>
    public static readonly DependencyProperty CategoryProperty = DependencyProperty.RegisterAttached("Category", typeof(string),
        typeof(SettingsFilter), new PropertyMetadata(""));
    public static string GetCategory(DependencyObject item) => (string)item.GetValue(CategoryProperty);
    public static void SetCategory(DependencyObject item, string value) => item.SetValue(CategoryProperty, value);

    public static bool Matches(string query, string terms) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .All(word => terms.Contains(word, StringComparison.OrdinalIgnoreCase));
}
