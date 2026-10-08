using System.Windows.Media;
using System.Windows.Threading;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>
/// The Overlay options in Settings. While they show, the overlay is a draggable preview, and every change applies at
/// once, apart from Settings' own Save.
/// </summary>
public sealed partial class OverlayOptionsViewModel : ObservableObject
{
    static readonly string[] SwatchColors = ["#0B0B0C", "#2A2118", "#3A1417", "#141B2E", "#16261C", "#23272B", "#2B1E33", "#3B3222"];

    readonly AppServices _services;
    readonly Action _showRegion;
    // Picking in the colour square changes the colour many times a second; it's saved once the picking pauses.
    readonly DispatcherTimer _colorSave = new() { Interval = TimeSpan.FromMilliseconds(150) };
    bool _syncing;
    bool _closed;
    OverlaySettings _lastSettings;

    [ObservableProperty] private bool _pickerOpen;
    [ObservableProperty] private Color _customColor;
    [ObservableProperty] private bool _isCustomColor;
    [ObservableProperty] private string? _pictureError;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPicture))]
    private ImageSource? _picture;

    public IReadOnlyList<Choice> SecondsChoices { get; } = new[] { 5, 10, 15, 30, 60 }.Select(s => new Choice($"{s} s", s)).ToList();
    public IReadOnlyList<Choice> Layouts { get; } = Enum.GetValues<OverlayLayout>().Select(l => new Choice(l.ToString(), l)).ToList();
    public IReadOnlyList<Choice> MouseProximityChoices { get; } = Enum.GetValues<OverlayMouseProximity>().Select(m => new Choice(m.ToString(), m)).ToList();
    public IReadOnlyList<Choice> GuildBossChoices { get; }
    public IReadOnlyList<Swatch> Swatches { get; }
    /// <summary>The timers that can have a row on the overlay. Fixed while Settings is open, as timers can't change under it.</summary>
    public IReadOnlyList<OverlayTimerRow> Upcoming { get; }
    public bool HasUpcoming => Upcoming.Count > 0;
    public bool HasPicture => Picture is not null;
    public HotkeyService Hotkeys => _services.Overlay.Hotkeys;
    /// <summary>The combos the other hotkeys hold, which each hotkey field may not repeat.</summary>
    public HotkeyTarget AlwaysShowTarget => new(HotkeyAction.AlwaysShow);
    public HotkeyTarget ShowTarget => new(HotkeyAction.Show);
    public HotkeyTarget MoveTarget => new(HotkeyAction.MoveOverlay);
    public IReadOnlyList<Hotkey> TakenForAlwaysShow => HotkeyCatalog.OtherKeys(_services.Timers.Current, Current, AlwaysShowTarget);
    public IReadOnlyList<Hotkey> TakenForShow => HotkeyCatalog.OtherKeys(_services.Timers.Current, Current, ShowTarget);
    public IReadOnlyList<Hotkey> TakenForMove => HotkeyCatalog.OtherKeys(_services.Timers.Current, Current, MoveTarget);
    /// <summary>The boss region whose server time the overlay shows.</summary>
    public string ServerRegion => _services.Region.Label;

    /// <param name="showRegion">Shows where the boss region is set, which gives the overlay's server time.</param>
    public OverlayOptionsViewModel(AppServices services, Action showRegion)
    {
        _services = services;
        _showRegion = showRegion;
        Swatches = SwatchColors.Select(hex => new Swatch(hex, PickSwatch)).ToList();
        GuildBossChoices = PopUpChoices.For(PopUpChoices.GuildBossMinutes, Current.GuildBosses);
        Upcoming = services.Timers.Current.Timers.Where(Listable)
            .Select(t => new OverlayTimerRow(t.Name, Current.Timers.FirstOrDefault(w => w.TimerId == t.Id)?.Minutes ?? 0,
                minutes => SetRow(t.Id, minutes)))
            .ToList();
        _colorSave.Tick += (_, _) => SaveCustomColor();
        var o = Current;
        _lastSettings = o;
        if (RgbColor.TryParseHex(o.BackgroundColor, out var rgb)) _customColor = Color.FromRgb(rgb.R, rgb.G, rgb.B);
        _picture = o.BackgroundImage is { } file ? services.Art.UserPicture(file) : null;
        MarkColor(_picture is null ? o.BackgroundColor : null);
        services.Settings.Changed += OnSettingsChanged;
        services.Timers.Changed += OnTimersChanged;
    }

    OverlaySettings Current => _services.Settings.Current.Overlay;

    /// <summary>The overlay shows as a draggable preview while its options are in view.</summary>
    public bool Previewing { get; private set; }

    public void ShowPreview(bool show)
    {
        if (_closed || show == Previewing) return;
        Previewing = show;
        if (show) _services.Overlay.BeginPreview();
        else _services.Overlay.EndPreview();
    }

    public bool Enabled { get => Current.Enabled; set => Modify(o => o with { Enabled = value }); }
    public bool AlwaysShow { get => Current.AlwaysShow; set => Modify(o => o with { AlwaysShow = value }); }
    public Hotkey? AlwaysShowHotkey { get => Current.AlwaysShowHotkey; set => Modify(o => o with { AlwaysShowHotkey = value }); }
    public bool ShowOnHotkey { get => Current.ShowOnHotkey; set => Modify(o => o with { ShowOnHotkey = value }); }
    public Hotkey? ShowHotkey { get => Current.ShowHotkey; set => Modify(o => o with { ShowHotkey = value }); }
    public Hotkey? MoveHotkey { get => Current.MoveHotkey; set => Modify(o => o with { MoveHotkey = value }); }
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
    public Choice MouseProximity
    {
        get => MouseProximityChoices.FirstOrDefault(c => (OverlayMouseProximity)c.Value! == Current.MouseProximity) ?? MouseProximityChoices[0];
        set => Modify(o => o with { MouseProximity = (OverlayMouseProximity)value.Value! });
    }
    public double Scale { get => Current.Scale; set => Modify(o => o with { Scale = Math.Round(value, 2) }); }
    public bool Outline { get => Current.ShowOutline; set => Modify(o => o with { ShowOutline = value }); }
    public bool BossIcons { get => Current.BossIcons; set => Modify(o => o with { BossIcons = value }); }
    public bool ShowClock { get => Current.ShowClock; set => Modify(o => o with { ShowClock = value }); }
    public bool ShowServerTime { get => Current.ShowServerTime; set => Modify(o => o with { ShowServerTime = value }); }
    public bool ShowGameTime { get => Current.ShowGameTime; set => Modify(o => o with { ShowGameTime = value }); }
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

    [RelayCommand]
    void OpenRegionSettings() => _showRegion();

    /// <summary>Settings also change from outside the panel: Always show by its hotkey.</summary>
    void OnSettingsChanged()
    {
        if (_closed) return;
        if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(OnSettingsChanged);
            return;
        }
        var previous = _lastSettings;
        var next = _lastSettings = Current;
        if (previous.Enabled != next.Enabled) OnPropertyChanged(nameof(Enabled));
        if (previous.AlwaysShow != next.AlwaysShow) OnPropertyChanged(nameof(AlwaysShow));
        if (previous.ShowOnHotkey != next.ShowOnHotkey) OnPropertyChanged(nameof(ShowOnHotkey));
        if (previous.AlwaysShowHotkey != next.AlwaysShowHotkey) OnPropertyChanged(nameof(AlwaysShowHotkey));
        if (previous.ShowHotkey != next.ShowHotkey) OnPropertyChanged(nameof(ShowHotkey));
        if (previous.MoveHotkey != next.MoveHotkey) OnPropertyChanged(nameof(MoveHotkey));
        if (previous.ShowSeconds != next.ShowSeconds) OnPropertyChanged(nameof(ShowSeconds));
        if (previous.GuildBosses != next.GuildBosses) OnPropertyChanged(nameof(GuildBosses));
        if (previous.Layout != next.Layout) OnPropertyChanged(nameof(Layout));
        if (previous.MouseProximity != next.MouseProximity) OnPropertyChanged(nameof(MouseProximity));
        if (previous.Scale != next.Scale) OnPropertyChanged(nameof(Scale));
        if (previous.ShowOutline != next.ShowOutline) OnPropertyChanged(nameof(Outline));
        if (previous.BossIcons != next.BossIcons) OnPropertyChanged(nameof(BossIcons));
        if (previous.ShowClock != next.ShowClock) OnPropertyChanged(nameof(ShowClock));
        if (previous.ShowServerTime != next.ShowServerTime) OnPropertyChanged(nameof(ShowServerTime));
        if (previous.ShowGameTime != next.ShowGameTime) OnPropertyChanged(nameof(ShowGameTime));
        if (previous.ShowPrevious != next.ShowPrevious) OnPropertyChanged(nameof(ShowPrevious));
        if (previous.ShowNext != next.ShowNext) OnPropertyChanged(nameof(ShowNext));
        if (previous.ShowFarm != next.ShowFarm) OnPropertyChanged(nameof(ShowFarm));
        if (previous.ShowCustomTimers != next.ShowCustomTimers) OnPropertyChanged(nameof(ShowCustomTimers));
        if (previous.ShowFishing != next.ShowFishing) OnPropertyChanged(nameof(ShowFishing));
        if (previous.ShowHorseRegistrations != next.ShowHorseRegistrations) OnPropertyChanged(nameof(ShowHorseRegistrations));
        if (previous.BackgroundOpacity != next.BackgroundOpacity) OnPropertyChanged(nameof(BackgroundOpacity));
        if (previous.TextOpacity != next.TextOpacity) OnPropertyChanged(nameof(TextOpacity));
        OnTimersChanged();
    }

    void OnTimersChanged()
    {
        if (_closed) return;
        if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(OnTimersChanged);
            return;
        }
        OnPropertyChanged(nameof(TakenForAlwaysShow));
        OnPropertyChanged(nameof(TakenForShow));
        OnPropertyChanged(nameof(TakenForMove));
    }

    /// <summary>Scheduled and one-time timers of the player's own and the presets; boss timetable entries have the Next and
    /// Previous boss sections, and countdowns the Custom timers one.</summary>
    static bool Listable(TimerDef t) =>
        !t.IsBuiltIn && (t.Kind == TimerKind.Scheduled || t is { Kind: TimerKind.OneTime, OneTime.Finished: false });

    void SetRow(Guid id, int minutes) => Modify(o =>
    {
        var others = o.Timers.Where(w => w.TimerId != id);
        return o with { Timers = (minutes > 0 ? others.Append(new OverlayTimerWindow(id, minutes)) : others).ToList() };
    });

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
        var (file, error) = _services.ChoosePicture();
        PictureError = error;
        if (file is null) return;
        PickerOpen = false;
        MarkColor(null);
        ReplacePicture(file);
    }

    [RelayCommand]
    void RemovePicture()
    {
        PictureError = null;
        ReplacePicture(null);
        MarkColor(_services.Settings.Current.Overlay.BackgroundColor);
    }

    void ReplacePicture(string? file)
    {
        var old = _services.Settings.Current.Overlay.BackgroundImage;
        if (file == old) return;
        Modify(o => o with { BackgroundImage = file });
        _services.Undo.ReleasePicture(old);
        Picture = file is null ? null : _services.Art.UserPicture(file);
    }

    public void OnClosed()
    {
        if (_colorSave.IsEnabled) SaveCustomColor();
        ShowPreview(false);
        _closed = true;
        _services.Settings.Changed -= OnSettingsChanged;
        _services.Timers.Changed -= OnTimersChanged;
    }
}

/// <summary>A timer in the Overlay options' Upcoming block, with how long before its next time the overlay lists it.</summary>
public sealed partial class OverlayTimerRow : ObservableObject
{
    readonly Action<int> _apply;

    [ObservableProperty] private Choice _lead;

    public OverlayTimerRow(string name, int minutes, Action<int> apply)
    {
        _apply = apply;
        Name = name;
        Choices = PopUpChoices.ForRow(minutes);
        _lead = Choices.First(c => (int)c.Value! == Math.Max(0, minutes));
    }

    public string Name { get; }
    public IReadOnlyList<Choice> Choices { get; }

    partial void OnLeadChanged(Choice value) => _apply((int)value.Value!);
}

/// <summary>A background colour in the Overlay options; the chosen one is ringed.</summary>
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
