using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BdoTimers.Core.Text;

namespace BdoTimers.App.Controls;

/// <summary>Attached properties the theme's styles and templates use.</summary>
public static class Ui
{
    /// <summary>The panel's first keyboard target, declared alongside its layout.</summary>
    public static readonly DependencyProperty InitialFocusProperty = DependencyProperty.RegisterAttached(
        "InitialFocus", typeof(bool), typeof(Ui), new PropertyMetadata(false));
    public static bool GetInitialFocus(DependencyObject d) => (bool)d.GetValue(InitialFocusProperty);
    public static void SetInitialFocus(DependencyObject d, bool value) => d.SetValue(InitialFocusProperty, value);

    /// <summary>Custom controls and focus shortcuts report intentional keyboard interaction here.</summary>
    public static void UseKeyboardFocus(DependencyObject target)
    {
        var window = target as Window ?? Window.GetWindow(target);
        if (window is null) return;
        window.SetValue(KeyboardPointerPositionProperty, Mouse.GetPosition(window));
        SetShowKeyboardFocus(window, true);
    }
    public static readonly DependencyProperty ShowKeyboardFocusProperty = DependencyProperty.RegisterAttached(
        "ShowKeyboardFocus", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetShowKeyboardFocus(DependencyObject d) => (bool)d.GetValue(ShowKeyboardFocusProperty);
    public static void SetShowKeyboardFocus(DependencyObject d, bool value) => d.SetValue(ShowKeyboardFocusProperty, value);

    /// <summary>Hide keyboard adorners on mouse input even when the focused control does not change.</summary>
    public static readonly DependencyProperty TrackKeyboardFocusProperty = DependencyProperty.RegisterAttached(
        "TrackKeyboardFocus", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, OnTrackKeyboardFocusChanged));

    public static bool GetTrackKeyboardFocus(DependencyObject d) => (bool)d.GetValue(TrackKeyboardFocusProperty);
    public static void SetTrackKeyboardFocus(DependencyObject d, bool value) => d.SetValue(TrackKeyboardFocusProperty, value);

    static readonly DependencyProperty KeyboardPointerPositionProperty = DependencyProperty.RegisterAttached(
        "KeyboardPointerPosition", typeof(Point), typeof(Ui));

    static void OnTrackKeyboardFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;
        window.RemoveHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(ShowFocusCue));
        window.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(HideFocusCue));
        window.RemoveHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(MouseMoved));
        window.RemoveHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(MouseScrolled));
        window.Deactivated -= HideInactiveFocusCue;
        if (!(bool)e.NewValue) { window.ClearValue(ShowKeyboardFocusProperty); return; }
        SetShowKeyboardFocus(window, false);
        window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(ShowFocusCue), true);
        window.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(HideFocusCue), true);
        window.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(MouseMoved), true);
        window.AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(MouseScrolled), true);
        window.Deactivated += HideInactiveFocusCue;
    }

    static void ShowFocusCue(object sender, KeyEventArgs e)
    {
        // Dismissal and text editing do not start keyboard navigation.
        if (e.Key != Key.Tab)
        {
            if (e.OriginalSource is TextBoxBase) return;
            if (e.Key is Key.Enter or Key.Space)
            {
                if (e.OriginalSource is not ButtonBase) return;
            }
            else if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown))
                return;
        }
        UseKeyboardFocus((Window)sender);
    }
    static void HideFocusCue(object sender, MouseButtonEventArgs e) => SetShowKeyboardFocus((Window)sender, false);
    static void MouseScrolled(object sender, MouseWheelEventArgs e) => SetShowKeyboardFocus((Window)sender, false);
    static void MouseMoved(object sender, MouseEventArgs e) => PointerMoved((Window)sender, e.GetPosition((Window)sender));

    internal static void PointerMoved(Window window, Point position)
    {
        // WPF also raises MouseMove when layout changes beneath a stationary pointer.
        if (GetShowKeyboardFocus(window) && position != (Point)window.GetValue(KeyboardPointerPositionProperty))
            SetShowKeyboardFocus(window, false);
    }
    static void HideInactiveFocusCue(object? sender, EventArgs e) => SetShowKeyboardFocus((Window)sender!, false);

    /// <summary>The corner radius a button style gives its template's border; square when unset.</summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(Ui), new FrameworkPropertyMetadata(new CornerRadius(0)));

    public static CornerRadius GetCornerRadius(DependencyObject d) => (CornerRadius)d.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject d, CornerRadius value) => d.SetValue(CornerRadiusProperty, value);

    /// <summary>The colour of a section's tab: its dot, and the line under it while it is chosen.</summary>
    public static readonly DependencyProperty SectionBrushProperty = DependencyProperty.RegisterAttached(
        "SectionBrush", typeof(Brush), typeof(Ui), new FrameworkPropertyMetadata(null));

    public static Brush? GetSectionBrush(DependencyObject d) => (Brush?)d.GetValue(SectionBrushProperty);
    public static void SetSectionBrush(DependencyObject d, Brush? value) => d.SetValue(SectionBrushProperty, value);

    /// <summary>Marks a text field as invalid; the theme draws its hairline in the danger colour.</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false));

    public static bool GetHasError(DependencyObject d) => (bool)d.GetValue(HasErrorProperty);
    public static void SetHasError(DependencyObject d, bool value) => d.SetValue(HasErrorProperty, value);

    /// <summary>
    /// On a 24-hour time field, puts in the colon as the digits are typed, so "2200" reads "22:00", and completes the
    /// time on Enter or leaving the field, so "9" reads "09:00".
    /// </summary>
    public static readonly DependencyProperty TimeEntryProperty = DependencyProperty.RegisterAttached(
        "TimeEntry", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, OnTimeEntryChanged));

    public static bool GetTimeEntry(DependencyObject d) => (bool)d.GetValue(TimeEntryProperty);
    public static void SetTimeEntry(DependencyObject d, bool value) => d.SetValue(TimeEntryProperty, value);

    static void OnTimeEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        box.TextChanged -= AddTimeColon;
        box.PreviewKeyDown -= FinishTimeOnEnter;
        box.LostKeyboardFocus -= FinishTimeOnLeave;
        if (!(bool)e.NewValue) return;
        box.TextChanged += AddTimeColon;
        box.PreviewKeyDown += FinishTimeOnEnter;
        box.LostKeyboardFocus += FinishTimeOnLeave;
    }

    static void FinishTimeOnEnter(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) FinishTime((TextBox)sender);
    }

    static void FinishTimeOnLeave(object sender, KeyboardFocusChangedEventArgs e) => FinishTime((TextBox)sender);

    /// <summary>The completed time goes to the binding at once, ahead of any update delay, so the Enter that finished it
    /// already finds the panel's Done enabled.</summary>
    static void FinishTime(TextBox box)
    {
        var text = TimeEntry.Finish(box.Text);
        if (text == box.Text) return;
        box.Text = text;
        box.CaretIndex = text.Length;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    static void AddTimeColon(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var text = TimeEntry.AddColon(box.Text);
        if (text == box.Text) return;
        // The caret keeps its place among the digits; one just past the hour stays before the colon, so a backspace
        // over the colon steps across it. A typed separator that became the colon leaves it after the colon.
        var caret = box.CaretIndex;
        var colon = text.IndexOf(':');
        box.Text = text;
        box.CaretIndex = colon == text.Length - 1 ? text.Length : caret <= colon ? caret : caret + 1;
    }

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
