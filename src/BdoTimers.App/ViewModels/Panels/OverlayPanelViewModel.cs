using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>
/// The Overlay panel (spec: docs/superpowers/specs/2026-09-28-overlay-design.md). While it's open the overlay is a
/// draggable preview, and every change applies at once.
/// </summary>
public sealed partial class OverlayPanelViewModel : ObservableObject, IPanel
{
    static readonly string[] SwatchColors = ["#0B0B0C", "#2A2118", "#3A1417", "#141B2E", "#16261C", "#23272B", "#2B1E33", "#3B3222"];

    readonly AppServices _services;
    // Picking in the colour square changes the colour many times a second; it's saved once the picking pauses.
    readonly DispatcherTimer _colorSave = new() { Interval = TimeSpan.FromMilliseconds(150) };
    bool _syncing;

    [ObservableProperty] private Choice _enabled = Choice.OnOff[0];
    [ObservableProperty] private Choice _alwaysShow = Choice.OnOff[1];
    [ObservableProperty] private Hotkey? _alwaysShowHotkey;
    [ObservableProperty] private bool _alwaysShowRefused;
    [ObservableProperty] private bool _listeningAlwaysShow;
    [ObservableProperty] private Choice _showOnHotkey = Choice.OnOff[1];
    [ObservableProperty] private Hotkey? _showHotkey;
    [ObservableProperty] private Hotkey? _horseHotkey;
    [ObservableProperty] private bool _showRefused;
    [ObservableProperty] private bool _listeningShow;
    [ObservableProperty] private Choice _showSeconds;
    [ObservableProperty] private Choice _layout;
    [ObservableProperty] private double _scale;
    [ObservableProperty] private bool _showClock;
    [ObservableProperty] private bool _showPrevious;
    [ObservableProperty] private bool _showNext;
    [ObservableProperty] private bool _showFarm;
    [ObservableProperty] private bool _showFishing;
    [ObservableProperty] private bool _showHorseRegistrations;
    [ObservableProperty] private bool _pickerOpen;
    [ObservableProperty] private Color _customColor;
    [ObservableProperty] private bool _isCustomColor;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPicture))]
    private ImageSource? _picture;
    [ObservableProperty] private double _backgroundOpacity;
    [ObservableProperty] private double _textOpacity;

    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> SecondsChoices { get; } = new[] { 5, 10, 15, 30, 60 }.Select(s => new Choice($"{s} s", s)).ToList();
    public IReadOnlyList<Choice> Layouts { get; } = Enum.GetValues<OverlayLayout>().Select(l => new Choice(l.ToString(), l)).ToList();
    public IReadOnlyList<Swatch> Swatches { get; }
    public bool HasPicture => Picture is not null;

    public OverlayPanelViewModel(AppServices services)
    {
        _services = services;
        Swatches = SwatchColors.Select(hex => new Swatch(hex, PickSwatch)).ToList();
        _horseHotkey = services.Timers.Current.Timers.FirstOrDefault(t => t.Preset == Presets.HorseRegistration)?.StartHotkey;
        _showSeconds = SecondsChoices[1];
        _layout = Layouts[0];
        _colorSave.Tick += (_, _) => SaveCustomColor();
        Load(services.Settings.Current.Overlay);
        LoadRefused();
        services.Settings.Changed += OnSettingsChanged;
        services.Overlay.Hotkeys.RefusedChanged += LoadRefused;
        services.Overlay.BeginPreview();
    }

    void Load(OverlaySettings o)
    {
        _syncing = true;
        Enabled = Choice.For(o.Enabled);
        AlwaysShow = Choice.For(o.AlwaysShow);
        AlwaysShowHotkey = o.AlwaysShowHotkey;
        ShowOnHotkey = Choice.For(o.ShowOnHotkey);
        ShowHotkey = o.ShowHotkey;
        ShowSeconds = SecondsChoices.FirstOrDefault(c => (int)c.Value! == o.ShowSeconds) ?? SecondsChoices[1];
        Layout = Layouts.First(c => (OverlayLayout)c.Value! == o.Layout);
        Scale = o.Scale;
        ShowClock = o.ShowClock;
        ShowPrevious = o.ShowPrevious;
        ShowNext = o.ShowNext;
        ShowFarm = o.ShowFarm;
        ShowFishing = o.ShowFishing;
        ShowHorseRegistrations = o.ShowHorseRegistrations;
        BackgroundOpacity = o.BackgroundOpacity;
        TextOpacity = o.TextOpacity;
        if (RgbColor.TryParseHex(o.BackgroundColor, out var rgb)) CustomColor = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        Picture = o.BackgroundImage is { } file ? _services.Art.UserPicture(file) : null;
        MarkColor(Picture is null ? o.BackgroundColor : null);
        _syncing = false;
    }

    /// <summary>Only Always show changes from outside the panel, by its hotkey.</summary>
    void OnSettingsChanged()
    {
        _syncing = true;
        AlwaysShow = Choice.For(_services.Settings.Current.Overlay.AlwaysShow);
        _syncing = false;
    }

    void LoadRefused()
    {
        var refused = _services.Overlay.Hotkeys.Refused;
        AlwaysShowRefused = refused.Contains(HotkeyAction.AlwaysShow);
        ShowRefused = refused.Contains(HotkeyAction.Show);
    }

    void Modify(Func<OverlaySettings, OverlaySettings> change)
    {
        if (_syncing) return;
        _services.Settings.Update(s => change(s.Overlay) is var next && next != s.Overlay ? s with { Overlay = next } : s);
    }

    partial void OnEnabledChanged(Choice value) => Modify(o => o with { Enabled = value.IsOn });
    partial void OnAlwaysShowChanged(Choice value) => Modify(o => o with { AlwaysShow = value.IsOn });
    partial void OnAlwaysShowHotkeyChanged(Hotkey? value) => Modify(o => o with { AlwaysShowHotkey = value });
    partial void OnShowOnHotkeyChanged(Choice value) => Modify(o => o with { ShowOnHotkey = value.IsOn });
    partial void OnShowHotkeyChanged(Hotkey? value) => Modify(o => o with { ShowHotkey = value });
    partial void OnShowSecondsChanged(Choice value) => Modify(o => o with { ShowSeconds = (int)value.Value! });
    partial void OnLayoutChanged(Choice value) => Modify(o => o with { Layout = (OverlayLayout)value.Value! });
    partial void OnScaleChanged(double value) => Modify(o => o with { Scale = Math.Round(value, 2) });
    partial void OnShowClockChanged(bool value) => Modify(o => o with { ShowClock = value });
    partial void OnShowPreviousChanged(bool value) => Modify(o => o with { ShowPrevious = value });
    partial void OnShowNextChanged(bool value) => Modify(o => o with { ShowNext = value });
    partial void OnShowFarmChanged(bool value) => Modify(o => o with { ShowFarm = value });
    partial void OnShowFishingChanged(bool value) => Modify(o => o with { ShowFishing = value });
    partial void OnShowHorseRegistrationsChanged(bool value) => Modify(o => o with { ShowHorseRegistrations = value });
    partial void OnBackgroundOpacityChanged(double value) => Modify(o => o with { BackgroundOpacity = Math.Round(value, 2) });
    partial void OnTextOpacityChanged(double value) => Modify(o => o with { TextOpacity = Math.Round(value, 2) });
    partial void OnListeningAlwaysShowChanged(bool value) => Listen(value);
    partial void OnListeningShowChanged(bool value) => Listen(value);

    /// <summary>While a hotkey field listens, the hotkeys are released so the combo reaches it.</summary>
    void Listen(bool listening)
    {
        if (listening) _services.Overlay.Hotkeys.Suspend();
        else _services.Overlay.Hotkeys.Resume();
    }

    partial void OnCustomColorChanged(Color value)
    {
        if (_syncing) return;
        MarkColor(null);
        IsCustomColor = true;
        _colorSave.Stop();
        _colorSave.Start();
    }

    void SaveCustomColor()
    {
        _colorSave.Stop();
        SetColor(new RgbColor(CustomColor.R, CustomColor.G, CustomColor.B).ToHex());
    }

    [RelayCommand]
    void OpenCustomColor() => PickerOpen = true;

    void PickSwatch(Swatch swatch)
    {
        PickerOpen = false;
        MarkColor(swatch.Hex);
        _syncing = true;
        CustomColor = swatch.Color;
        _syncing = false;
        SetColor(swatch.Hex);
    }

    /// <summary>A colour replaces the picture.</summary>
    void SetColor(string hex)
    {
        Modify(o => o with { BackgroundColor = hex });
        ReplacePicture(null);
    }

    /// <summary>Rings the swatch for <paramref name="hex"/>, or Custom… for a colour no swatch has; null rings none.</summary>
    void MarkColor(string? hex)
    {
        foreach (var s in Swatches) s.IsSelected = hex is not null && string.Equals(s.Hex, hex, StringComparison.OrdinalIgnoreCase);
        IsCustomColor = hex is not null && !Swatches.Any(s => s.IsSelected);
    }

    [RelayCommand]
    void ChoosePicture()
    {
        var dialog = new OpenFileDialog { Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" };
        if (dialog.ShowDialog() != true) return;
        string file;
        try { file = _services.Art.Import(dialog.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Couldn't import picture {dialog.FileName}", ex);
            return;
        }
        if (_services.Art.UserPicture(file) is null)
        {
            _services.Art.Delete(file);
            return;
        }
        PickerOpen = false;
        MarkColor(null);
        ReplacePicture(file);
    }

    [RelayCommand]
    void RemovePicture()
    {
        ReplacePicture(null);
        MarkColor(_services.Settings.Current.Overlay.BackgroundColor);
    }

    void ReplacePicture(string? file)
    {
        var old = _services.Settings.Current.Overlay.BackgroundImage;
        if (file == old) return;
        Modify(o => o with { BackgroundImage = file });
        _services.Art.Delete(old);
        Picture = file is null ? null : _services.Art.UserPicture(file);
    }

    public void OnClosed()
    {
        if (_colorSave.IsEnabled) SaveCustomColor();
        ListeningAlwaysShow = false;
        ListeningShow = false;
        _services.Settings.Changed -= OnSettingsChanged;
        _services.Overlay.Hotkeys.RefusedChanged -= LoadRefused;
        _services.Overlay.EndPreview();
    }
}

/// <summary>A background colour in the Overlay panel; the chosen one is ringed.</summary>
public sealed partial class Swatch : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public Swatch(string hex, Action<Swatch> pick)
    {
        Hex = hex;
        RgbColor.TryParseHex(hex, out var c);
        Color = Color.FromRgb(c.R, c.G, c.B);
        Brush = new SolidColorBrush(Color);
        Brush.Freeze();
        PickCommand = new RelayCommand(() => pick(this));
    }

    public string Hex { get; }
    public Color Color { get; }
    public SolidColorBrush Brush { get; }
    public IRelayCommand PickCommand { get; }
}
