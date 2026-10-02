using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BdoTimers.App.Controls;

/// <summary>Preserves ISO date text, including invalid edits, while offering calendar selection.</summary>
public partial class DateInput : UserControl
{
    bool _syncingCalendar;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
        typeof(DateInput), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DateChanged));
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(nameof(HasError), typeof(bool),
        typeof(DateInput), new PropertyMetadata(false));
    public static readonly DependencyProperty TodayProperty = DependencyProperty.Register(nameof(Today), typeof(DateTime),
        typeof(DateInput), new PropertyMetadata(default(DateTime), DateChanged));
    public static readonly DependencyProperty TodayProviderProperty = DependencyProperty.Register(nameof(TodayProvider), typeof(Func<DateTime>),
        typeof(DateInput), new PropertyMetadata(null, DateChanged));

    public DateInput()
    {
        InitializeComponent();
        Picker.AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(Picker_MouseUp), handledEventsToo: true);
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool HasError { get => (bool)GetValue(HasErrorProperty); set => SetValue(HasErrorProperty, value); }
    public DateTime Today { get => (DateTime)GetValue(TodayProperty); set => SetValue(TodayProperty, value); }
    public Func<DateTime>? TodayProvider { get => (Func<DateTime>?)GetValue(TodayProviderProperty); set => SetValue(TodayProviderProperty, value); }
    DateTime CurrentDate => (TodayProvider?.Invoke() ?? Today).Date;

    static void DateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DateInput)d;
        if (control.Picker is not null) control.UpdateCalendar();
    }

    void UpdateCalendar()
    {
        _syncingCalendar = true;
        try
        {
            var valid = DateTime.TryParseExact(Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
            Picker.SelectedDate = valid ? date : null;
            Picker.DisplayDate = valid ? date : CurrentDate;
        }
        finally { _syncingCalendar = false; }
    }

    void CalendarButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateCalendar();
        CalendarPopup.IsOpen = !CalendarPopup.IsOpen;
    }

    void CalendarPopup_Opened(object? sender, EventArgs e) => Picker.Focus();

    void Picker_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncingCalendar && Picker.SelectedDate is { } date)
            SetCurrentValue(TextProperty, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    void Picker_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Picker.SelectedDate is { } date)
        {
            ChooseDate(date);
            e.Handled = true;
        }
    }

    void Picker_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        for (DependencyObject? target = e.OriginalSource as DependencyObject; target is not null; target = VisualTreeHelper.GetParent(target))
            if (target is CalendarDayButton)
            {
                // Calendar finishes its selection and releases mouse capture before its popup closes.
                Dispatcher.BeginInvoke(() => { if (Picker.SelectedDate is { } date) ChooseDate(date); });
                break;
            }
    }

    void Today_Click(object sender, RoutedEventArgs e) => ChooseDate(CurrentDate);
    void Tomorrow_Click(object sender, RoutedEventArgs e) => ChooseDate(CurrentDate.AddDays(1));

    void ChooseDate(DateTime date)
    {
        SetCurrentValue(TextProperty, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        CalendarPopup.IsOpen = false;
        DateBox.Focus();
    }

    void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            UpdateCalendar();
            CalendarPopup.IsOpen = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && CalendarPopup.IsOpen)
        {
            CalendarPopup.IsOpen = false;
            DateBox.Focus();
            e.Handled = true;
        }
    }
}
