using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using BdoTimers.Core.Text;

namespace BdoTimers.App.Controls;

/// <summary>Attached properties the theme's styles and templates use.</summary>
public static class Ui
{
    /// <summary>Marks a text field as invalid; the theme draws its hairline in the danger colour.</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false));

    public static bool GetHasError(DependencyObject d) => (bool)d.GetValue(HasErrorProperty);
    public static void SetHasError(DependencyObject d, bool value) => d.SetValue(HasErrorProperty, value);

    /// <summary>
    /// On a slider that moves its thumb to a press on the track (<see cref="Slider.IsMoveToPointEnabled"/>), hands the
    /// press on to the thumb, so moving the mouse before letting go drags it.
    /// </summary>
    public static readonly DependencyProperty DragFromTrackProperty = DependencyProperty.RegisterAttached(
        "DragFromTrack", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, OnDragFromTrackChanged));

    public static bool GetDragFromTrack(DependencyObject d) => (bool)d.GetValue(DragFromTrackProperty);
    public static void SetDragFromTrack(DependencyObject d, bool value) => d.SetValue(DragFromTrackProperty, value);

    static void OnDragFromTrackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider) return;
        // Handled ones too: the slider marks the press handled once it has moved the thumb.
        var handler = new MouseButtonEventHandler(PressTrack);
        if ((bool)e.NewValue) slider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, handler, handledEventsToo: true);
        else slider.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, handler);
    }

    /// <summary>Runs after the slider has moved the thumb under the pointer; a press on the thumb itself is left to it.</summary>
    static void PressTrack(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (!slider.IsMoveToPointEnabled
            || slider.Template?.FindName("PART_Track", slider) is not Track { Thumb: { IsMouseOver: false } thumb } track) return;
        // The thumb measures the drag from where the press lands on it, so it has to be laid out at its new place first.
        track.UpdateLayout();
        thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = thumb,
        });
    }

    /// <summary>
    /// Typing on a combo box selects an item through <see cref="ListSearch"/>, so "kyiv" reaches
    /// "(UTC+02:00) Helsinki, Kyiv, …"; nothing shows the typed text, and an open list follows the selection. Backspace
    /// edits the text, a pause or opening/closing the list starts over, and Escape on an open list puts back the item
    /// chosen before typing.
    /// </summary>
    public static readonly DependencyProperty TypeToSearchProperty = DependencyProperty.RegisterAttached(
        "TypeToSearch", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, OnTypeToSearchChanged));

    public static bool GetTypeToSearch(DependencyObject d) => (bool)d.GetValue(TypeToSearchProperty);
    public static void SetTypeToSearch(DependencyObject d, bool value) => d.SetValue(TypeToSearchProperty, value);

    static readonly DependencyProperty TypedTextProperty = DependencyProperty.RegisterAttached(
        "TypedText", typeof(TypedText), typeof(Ui));

    /// <summary>The same pause Windows lists allow between the letters of one search.</summary>
    static readonly TimeSpan TypingPause = TimeSpan.FromSeconds(1);

    sealed class TypedText
    {
        public string Text = "";
        public DateTime At;
        /// <summary>The selection when the open list was first searched, for Escape to restore.</summary>
        public int? Before;
    }

    static void OnTypeToSearchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ComboBox box) return;
        box.PreviewTextInput -= SearchTyped;
        box.PreviewKeyDown -= SearchKey;
        box.DropDownOpened -= ForgetTyped;
        box.DropDownClosed -= ForgetTyped;
        if (!(bool)e.NewValue) return;
        box.PreviewTextInput += SearchTyped;
        box.PreviewKeyDown += SearchKey;
        box.DropDownOpened += ForgetTyped;
        box.DropDownClosed += ForgetTyped;
    }

    static void SearchTyped(object sender, TextCompositionEventArgs e)
    {
        var box = (ComboBox)sender;
        var typed = Typed(box);
        if (typed.Text.Length == 0 && string.IsNullOrWhiteSpace(e.Text)) return;
        typed.Text += e.Text;
        Search(box, typed);
        e.Handled = true;
    }

    static void SearchKey(object sender, KeyEventArgs e)
    {
        var box = (ComboBox)sender;
        var typed = Typed(box);
        if (e.Key == Key.Back && typed.Text.Length > 0)
        {
            typed.Text = typed.Text.Substring(0, typed.Text.Length - 1);
            Search(box, typed);
            e.Handled = true;
        }
        // The list then closes as usual.
        else if (e.Key == Key.Escape && box.IsDropDownOpen && typed.Before is { } before) box.SelectedIndex = before;
    }

    static void ForgetTyped(object? sender, EventArgs e) => ((ComboBox)sender!).ClearValue(TypedTextProperty);

    /// <summary>What has been typed on <paramref name="box"/> since the last pause.</summary>
    static TypedText Typed(ComboBox box)
    {
        if (box.GetValue(TypedTextProperty) is not TypedText typed) box.SetValue(TypedTextProperty, typed = new TypedText());
        else if (DateTime.UtcNow - typed.At > TypingPause) typed.Text = "";
        return typed;
    }

    static void Search(ComboBox box, TypedText typed)
    {
        typed.At = DateTime.UtcNow;
        var index = ListSearch.Find(box.Items.Cast<object?>().Select(item => DisplayText(box, item)).ToList(), typed.Text);
        if (index < 0) return;
        if (box.IsDropDownOpen) typed.Before ??= box.SelectedIndex;
        box.SelectedIndex = index;
    }

    /// <summary>The item's text as the list shows it, following a plain <see cref="ItemsControl.DisplayMemberPath"/>.</summary>
    static string DisplayText(ItemsControl box, object? item)
    {
        if (!string.IsNullOrEmpty(box.DisplayMemberPath))
            foreach (var name in box.DisplayMemberPath.Split('.'))
                item = item?.GetType().GetProperty(name)?.GetValue(item);
        return item?.ToString() ?? "";
    }
}
