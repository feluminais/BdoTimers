using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Model;

namespace BdoTimers.App.Controls;

/// <summary>
/// A field that listens for a key combo when clicked; Esc cancels and ✕ clears it. While it listens, the app's
/// hotkeys are released so the combo reaches it.
/// </summary>
public partial class HotkeyBox : UserControl
{
    const string RefusedText = "In use by another app";

    public static readonly DependencyProperty ComboProperty = DependencyProperty.Register(
        nameof(Combo), typeof(Hotkey), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Changed));

    /// <summary>The other hotkey fields' combos, which this one may not repeat.</summary>
    public static readonly DependencyProperty TakenProperty =
        DependencyProperty.Register(nameof(Taken), typeof(IEnumerable<Hotkey?>), typeof(HotkeyBox), new PropertyMetadata(null, Changed));

    /// <summary>Holds the app's hotkeys; it is released while the field listens and says when Windows refused one.</summary>
    public static readonly DependencyProperty HotkeysProperty = DependencyProperty.Register(
        nameof(Hotkeys), typeof(HotkeyService), typeof(HotkeyBox), new PropertyMetadata(null, HotkeysChanged));

    /// <summary>The hotkey this field sets, to tell whether Windows refused it.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target), typeof(HotkeyTarget), typeof(HotkeyBox), new PropertyMetadata(default(HotkeyTarget), Changed));

    string? _error;
    bool _listening;
    // Released when listening starts and resumed exactly once when it ends, even if Hotkeys changed meanwhile.
    HotkeyService? _released;
    HotkeyService? _watched;

    public HotkeyBox()
    {
        InitializeComponent();
        Loaded += (_, _) => Watch(Hotkeys);
        Unloaded += (_, _) =>
        {
            Listen(false);
            Watch(null);
        };
        Refresh();
    }

    public Hotkey? Combo
    {
        get => (Hotkey?)GetValue(ComboProperty);
        set => SetValue(ComboProperty, value);
    }

    public IEnumerable<Hotkey?>? Taken
    {
        get => (IEnumerable<Hotkey?>?)GetValue(TakenProperty);
        set => SetValue(TakenProperty, value);
    }

    public HotkeyService? Hotkeys
    {
        get => (HotkeyService?)GetValue(HotkeysProperty);
        set => SetValue(HotkeysProperty, value);
    }

    public HotkeyTarget Target
    {
        get => (HotkeyTarget)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyBox)d;
        if (e.Property == ComboProperty || e.Property == TakenProperty) box._error = null;
        box.Refresh();
    }

    static void HotkeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (HotkeyBox)d;
        if (box.IsLoaded) box.Watch((HotkeyService?)e.NewValue);
        else box.Refresh();
    }

    /// <summary>Follows which hotkeys Windows refused while the field is on screen.</summary>
    void Watch(HotkeyService? hotkeys)
    {
        if (_watched is not null) _watched.RefusedChanged -= Refresh;
        _watched = hotkeys;
        if (hotkeys is not null) hotkeys.RefusedChanged += Refresh;
        Refresh();
    }

    void Listen(bool listening)
    {
        if (listening == _listening) return;
        _listening = listening;
        if (listening) (_released = Hotkeys)?.Suspend();
        else
        {
            _released?.Resume();
            _released = null;
        }
        Refresh();
    }

    void Field_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _error = null;
        Listen(true);
        Focus();
        e.Handled = true;
    }

    void Clear_Click(object sender, RoutedEventArgs e)
    {
        _error = null;
        Listen(false);
        SetCurrentValue(ComboProperty, null);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Listen(false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        var key = e.Key switch { Key.System => e.SystemKey, Key.ImeProcessed => e.ImeProcessedKey, _ => e.Key };
        if (HandleKeyDown(key, Keyboard.Modifiers)) e.Handled = true;
    }

    internal bool HandleKeyDown(Key key, ModifierKeys pressedModifiers)
    {
        if (!_listening)
        {
            if (!ClearButton.IsKeyboardFocusWithin && (key is Key.Enter or Key.Space))
            {
                _error = null;
                Listen(true);
                return true;
            }
            return false;
        }
        // Wait for the key the modifiers go with.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin) return true;
        var modifiers = (HotkeyModifiers)(int)pressedModifiers;
        _error = null;
        try
        {
            if (key == Key.Escape && modifiers == HotkeyModifiers.None) return true;
            var combo = new Hotkey(modifiers, KeyInterop.VirtualKeyFromKey(key));
            _error = HotkeyRules.CheckAgainst(combo, (Taken ?? []).OfType<Hotkey>());
            if (_error is null) SetCurrentValue(ComboProperty, combo);
        }
        finally
        {
            // Save the chosen combo before registration resumes, so the old shortcut stays released.
            Listen(false);
        }
        return true;
    }

    void Refresh()
    {
        KeyText.Text = _listening ? "Press keys…" : Combo is { } combo ? HotkeyText.Format(combo) : "Set hotkey";
        KeyText.SetResourceReference(TextBlock.ForegroundProperty, _listening || Combo is not null ? "AccentTextBrush" : "SubtleBrush");
        ClearButton.Visibility = Combo is not null && !_listening ? Visibility.Visible : Visibility.Collapsed;
        var refused = Hotkeys?.Refused.Contains(Target) == true;
        var message = _listening ? null : _error
            ?? (Combo is { } key ? HotkeyRules.CheckAgainst(key, (Taken ?? []).OfType<Hotkey>()) : null)
            ?? (refused && Combo is not null ? RefusedText : null);
        Field.SetResourceReference(Border.BorderBrushProperty, _listening ? "AccentBrush" : message is not null ? "DangerBrush" : "HairlineStrongBrush");
        Message.Text = message ?? "";
        Message.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

}
