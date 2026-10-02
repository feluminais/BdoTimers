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

    /// <summary>On a 24-hour time field, puts in the colon as the digits are typed, so "2200" reads "22:00".</summary>
    public static readonly DependencyProperty TimeEntryProperty = DependencyProperty.RegisterAttached(
        "TimeEntry", typeof(bool), typeof(Ui), new FrameworkPropertyMetadata(false, OnTimeEntryChanged));

    public static bool GetTimeEntry(DependencyObject d) => (bool)d.GetValue(TimeEntryProperty);
    public static void SetTimeEntry(DependencyObject d, bool value) => d.SetValue(TimeEntryProperty, value);

    static void OnTimeEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box) return;
        if ((bool)e.NewValue) box.TextChanged += AddTimeColon;
        else box.TextChanged -= AddTimeColon;
    }

    static void AddTimeColon(object sender, TextChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var text = Parsing.AddTimeColon(box.Text);
        if (text == box.Text) return;
        // The caret keeps its place among the digits; one just past the hour stays before the colon, so a backspace
        // over the colon steps across it.
        var caret = box.CaretIndex;
        var colon = text.IndexOf(':');
        box.Text = text;
        box.CaretIndex = caret <= colon ? caret : caret + 1;
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
}
