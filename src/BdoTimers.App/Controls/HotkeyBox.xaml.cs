using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Controls;

/// <summary>A field that listens for a key combo when clicked; Esc cancels and ✕ clears it.</summary>
public partial class HotkeyBox : UserControl
{
    const string RefusedText = "In use by another app";

    public static readonly DependencyProperty ComboProperty = DependencyProperty.Register(
        nameof(Combo), typeof(Hotkey), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));

    /// <summary>Every other configured shortcut, including shortcuts whose feature is off.</summary>
    public static readonly DependencyProperty OtherKeysProperty = DependencyProperty.Register(
        nameof(OtherKeys), typeof(IReadOnlyList<Hotkey>), typeof(HotkeyBox), new PropertyMetadata(null, Changed));

    /// <summary>Windows refused the combo.</summary>
    public static readonly DependencyProperty IsRefusedProperty =
        DependencyProperty.Register(nameof(IsRefused), typeof(bool), typeof(HotkeyBox), new PropertyMetadata(false, Changed));

    public static readonly DependencyProperty IsListeningProperty = DependencyProperty.Register(
        nameof(IsListening), typeof(bool), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));

    string? _error;

    public HotkeyBox()
    {
        InitializeComponent();
        Unloaded += (_, _) => SetCurrentValue(IsListeningProperty, false);
        Refresh();
    }

    public Hotkey? Combo
    {
        get => (Hotkey?)GetValue(ComboProperty);
        set => SetValue(ComboProperty, value);
    }

    public IReadOnlyList<Hotkey>? OtherKeys
    {
        get => (IReadOnlyList<Hotkey>?)GetValue(OtherKeysProperty);
        set => SetValue(OtherKeysProperty, value);
    }

    public bool IsRefused
    {
        get => (bool)GetValue(IsRefusedProperty);
        set => SetValue(IsRefusedProperty, value);
    }

    public bool IsListening
    {
        get => (bool)GetValue(IsListeningProperty);
        set => SetValue(IsListeningProperty, value);
    }

    static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyBox)d;
        if (e.Property == ComboProperty || e.Property == OtherKeysProperty) box._error = null;
        box.Refresh();
    }

    void Field_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _error = null;
        SetCurrentValue(IsListeningProperty, true);
        Focus();
        e.Handled = true;
    }

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        _error = null;
        SetCurrentValue(ComboProperty, null);
        SetCurrentValue(IsListeningProperty, false);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        SetCurrentValue(IsListeningProperty, false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!IsListening)
        {
            if (!ClearButton.IsKeyboardFocusWithin && (e.Key is Key.Enter or Key.Space))
            {
                _error = null;
                SetCurrentValue(IsListeningProperty, true);
                e.Handled = true;
            }
            return;
        }
        e.Handled = true;
        var key = e.Key switch { Key.System => e.SystemKey, Key.ImeProcessed => e.ImeProcessedKey, _ => e.Key };
        // Wait for the key the modifiers go with.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin) return;
        var modifiers = (HotkeyModifiers)(int)Keyboard.Modifiers;
        _error = null;
        try
        {
            if (key == Key.Escape && modifiers == HotkeyModifiers.None) return;
            var combo = new Hotkey(modifiers, KeyInterop.VirtualKeyFromKey(key));
            _error = HotkeyRules.CheckAgainst(combo, OtherKeys ?? []);
            if (_error is null) SetCurrentValue(ComboProperty, combo);
        }
        finally
        {
            // Persist the chosen combo before registration resumes, so the old shortcut stays released.
            SetCurrentValue(IsListeningProperty, false);
            Refresh();
        }
    }

    void Refresh()
    {
        KeyText.Text = IsListening ? "Press keys…" : Combo is { } combo ? HotkeyText.Format(combo) : "Set hotkey";
        KeyText.Foreground = Brush(IsListening || Combo is not null ? "AccentTextBrush" : "SubtleBrush");
        ClearButton.Visibility = Combo is not null && !IsListening ? Visibility.Visible : Visibility.Collapsed;
        var message = IsListening ? null : _error
            ?? (Combo is { } key ? HotkeyRules.CheckAgainst(key, OtherKeys ?? []) : null)
            ?? (IsRefused && Combo is not null ? RefusedText : null);
        Field.BorderBrush = Brush(IsListening ? "AccentBrush" : message is not null ? "DangerBrush" : "HairlineStrongBrush");
        Message.Text = message ?? "";
        Message.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    Brush Brush(string key) => (Brush)FindResource(key);
}
