using System.Windows.Media;
using System.Windows.Threading;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>
/// The Overlay panel. While it's open the overlay is a
/// draggable preview, and every change applies at once.
/// </summary>
public sealed partial class OverlayPanelViewModel : ObservableObject, IPanel
{
    static readonly string[] SwatchColors = ["#0B0B0C", "#2A2118", "#3A1417", "#141B2E", "#16261C", "#23272B", "#2B1E33", "#3B3222"];

    readonly AppServices _services;
    // Picking in the colour square changes the colour many times a second; it's saved once the picking pauses.
    readonly DispatcherTimer _colorSave = new() { Interval = TimeSpan.FromMilliseconds(150) };
    bool _syncing;

    [ObservableProperty] private bool _pickerOpen;
    [ObservableProperty] private Color _customColor;
    [ObservableProperty] private bool _isCustomColor;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPicture))]
    private ImageSource? _picture;

    public IReadOnlyList<Choice> SecondsChoices { get; } = new[] { 5, 10, 15, 30, 60 }.Select(s => new Choice($"{s} s", s)).ToList();
    public IReadOnlyList<Choice> Layouts { get; } = Enum.GetValues<OverlayLayout>().Select(l => new Choice(l.ToString(), l)).ToList();
    public IReadOnlyList<Choice> GuildBossChoices { get; }
    public IReadOnlyList<Swatch> Swatches { get; }
    public bool HasPicture => Picture is not null;
    public HotkeyService Hotkeys => _services.Overlay.Hotkeys;
    /// <summary>The combos the other hotkeys hold, which each hotkey field may not repeat.</summary>
    public HotkeyTarget AlwaysShowTarget => new(HotkeyAction.AlwaysShow);
    public HotkeyTarget ShowTarget => new(HotkeyAction.Show);
    public IReadOnlyList<Hotkey> TakenForAlwaysShow => HotkeyCatalog.OtherKeys(_services.Timers.Current, Current, AlwaysShowTarget);
    public IReadOnlyList<Hotkey> TakenForShow => HotkeyCatalog.OtherKeys(_services.Timers.Current, Current, ShowTarget);

    public OverlayPanelViewModel(AppServices services)
    {
        _services = services;
        Swatches = SwatchColors.Select(hex => new Swatch(hex, PickSwatch)).ToList();
        GuildBossChoices = PopUpChoices.For(PopUpChoices.GuildBossMinutes, Current.GuildBosses);
        _colorSave.Tick += (_, _) => SaveCustomColor();
        var o = Current;
        if (RgbColor.TryParseHex(o.BackgroundColor, out var rgb)) _customColor = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        _picture = o.BackgroundImage is { } file ? services.Art.UserPicture(file) : null;
        MarkColor(_picture is null ? o.BackgroundColor : null);
        services.Settings.Changed += OnSettingsChanged;
        services.Timers.Changed += OnSettingsChanged;
        services.Overlay.BeginPreview();
    }

    OverlaySettings Current => _services.Settings.Current.Overlay;

    public Choice Enabled { get => Choice.For(Current.Enabled); set => Modify(o => o with { Enabled = value.IsOn }); }
    public Choice AlwaysShow { get => Choice.For(Current.AlwaysShow); set => Modify(o => o with { AlwaysShow = value.IsOn }); }
    public Hotkey? AlwaysShowHotkey { get => Current.AlwaysShowHotkey; set => Modify(o => o with { AlwaysShowHotkey = value }); }
    public Choice ShowOnHotkey { get => Choice.For(Current.ShowOnHotkey); set => Modify(o => o with { ShowOnHotkey = value.IsOn }); }
    public Hotkey? ShowHotkey { get => Current.ShowHotkey; set => Modify(o => o with { ShowHotkey = value }); }
    public Choice ShowSeconds
    {
        get => SecondsChoices.FirstOrDefault(c => (int)c.Value! == Current.ShowSeconds) ?? SecondsChoices[1];
        set => Modify(o => o with { ShowSeconds = (int)value.Value! });
    }
    public Choice GuildBosses
    {
        get => PopUpChoices.Matching(GuildBossChoices, Current.GuildBosses);
        set => Modify(o => o with { GuildBosses = PopUpChoices.Apply(o.GuildBosses, value) });
    }
    public Choice Layout
    {
        get => Layouts.First(c => (OverlayLayout)c.Value! == Current.Layout);
        set => Modify(o => o with { Layout = (OverlayLayout)value.Value! });
    }
    public double Scale { get => Current.Scale; set => Modify(o => o with { Scale = Math.Round(value, 2) }); }
    public bool ShowClock { get => Current.ShowClock; set => Modify(o => o with { ShowClock = value }); }
    public bool ShowPrevious { get => Current.ShowPrevious; set => Modify(o => o with { ShowPrevious = value }); }
    public bool ShowNext { get => Current.ShowNext; set => Modify(o => o with { ShowNext = value }); }
    public bool ShowFarm { get => Current.ShowFarm; set => Modify(o => o with { ShowFarm = value }); }
    public bool ShowCustomTimers { get => Current.ShowCustomTimers; set => Modify(o => o with { ShowCustomTimers = value }); }
    public bool ShowFishing { get => Current.ShowFishing; set => Modify(o => o with { ShowFishing = value }); }
    public bool ShowHorseRegistrations
    {
        get => Current.ShowHorseRegistrations;
        set => Modify(o => o with { ShowHorseRegistrations = value });
    }
    public double BackgroundOpacity
    {
        get => Current.BackgroundOpacity;
        set => Modify(o => o with { BackgroundOpacity = Math.Round(value, 2) });
    }
    public double TextOpacity { get => Current.TextOpacity; set => Modify(o => o with { TextOpacity = Math.Round(value, 2) }); }

    /// <summary>Settings also change from outside the panel: Always show by its hotkey.</summary>
    void OnSettingsChanged()
    {
        if (System.Windows.Application.Current.Dispatcher.CheckAccess()) OnPropertyChanged(string.Empty);
        else System.Windows.Application.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(string.Empty));
    }

    void Modify(Func<OverlaySettings, OverlaySettings> change) =>
        _services.Settings.Update(s => change(s.Overlay) is var next && next != s.Overlay ? s with { Overlay = next } : s);

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
        if (_services.ChoosePicture() is not { } file) return;
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
        _services.Settings.Changed -= OnSettingsChanged;
        _services.Timers.Changed -= OnSettingsChanged;
        _services.Overlay.EndPreview();
    }
}

/// <summary>A background colour in the Overlay panel; the chosen one is ringed.</summary>
public sealed partial class Swatch : ObservableObject
{
    readonly Action<Swatch> _pick;

    [ObservableProperty] private bool _isSelected;

    public Swatch(string hex, Action<Swatch> pick)
    {
        _pick = pick;
        Hex = hex;
        RgbColor.TryParseHex(hex, out var c);
        Color = Color.FromRgb(c.R, c.G, c.B);
        Brush = new SolidColorBrush(Color);
        Brush.Freeze();
    }

    public string Hex { get; }
    public Color Color { get; }
    public SolidColorBrush Brush { get; }

    [RelayCommand]
    void Pick() => _pick(this);
}
