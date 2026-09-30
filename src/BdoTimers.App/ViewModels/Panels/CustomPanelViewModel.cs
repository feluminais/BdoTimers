using System.IO;
using System.Windows;
using BdoTimers.App.Controls;
using BdoTimers.App.Overlay;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class CustomPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Guid _id;
    bool _syncingDuration;
    // A duration typed for a running or paused Farm waits until typing is done: applied per keystroke, a half-typed
    // "2" would move the countdown's end into the past and the scheduler would end it.
    TimeSpan? _typedFarmDuration;

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _nameInvalid;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images;
    [ObservableProperty] private bool _hasPicture;
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private bool _durationInvalid;
    [ObservableProperty] private FarmGrowthOption _farmGrowth;
    [ObservableProperty] private string? _timeZoneId;
    [ObservableProperty] private Choice _alertsOn;
    [ObservableProperty] private bool _confirmingDelete;
    [ObservableProperty] private Hotkey? _horseHotkey;
    [ObservableProperty] private bool _horseHotkeyRefused;
    [ObservableProperty] private bool _listeningHorseHotkey;

    public bool IsCountdown { get; }
    public bool IsFarm { get; }
    public bool IsHorseTemplate { get; }
    public bool IsHorseRun { get; }
    public bool CanChangePicture { get; }
    public Hotkey? OverlayAlwaysHotkey { get; }
    public Hotkey? OverlayShowHotkey { get; }
    public IReadOnlyList<FarmGrowthOption> FarmGrowthOptions { get; } =
    [
        new("20 h · Suitable", TimeSpan.FromHours(20)),
        new("21 h · Unsuitable", TimeSpan.FromHours(21)),
        new("22 h · Highly unsuitable", TimeSpan.FromHours(22)),
        new("Custom duration", null),
    ];
    public bool IsStopwatch { get; }
    public bool IsWeekly => !IsCountdown && !IsStopwatch;
    /// <summary>Stopwatches never alert, so they have no alert settings or on/off.</summary>
    public bool HasAlerts => !IsStopwatch;
    public bool CanDelete { get; }
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();
    public AlertRowsViewModel Alerts { get; }
    public SlotListViewModel? Slots { get; }

    TimerDef Timer => _services.Timers.Current.Timers.First(t => t.Id == _id);

    public CustomPanelViewModel(AppServices services, IPanelHost host, TimerDef timer)
    {
        _services = services;
        _host = host;
        _id = timer.Id;
        _name = timer.Name;
        _images = [services.Art.For(timer)];
        _hasPicture = timer.ImageFile is not null;
        _alertsOn = Choice.For(timer.Enabled);
        IsCountdown = timer.Kind == TimerKind.Countdown;
        IsFarm = timer.Preset == Presets.Farm && IsCountdown;
        IsHorseTemplate = timer.Preset == Presets.HorseRegistration;
        IsHorseRun = timer.Preset == Presets.HorseRegistrationRun;
        CanChangePicture = timer.Preset != Presets.HorseRegistrationRun;
        _horseHotkey = timer.StartHotkey;
        OverlayAlwaysHotkey = services.Settings.Current.Overlay.AlwaysShowHotkey;
        OverlayShowHotkey = services.Settings.Current.Overlay.ShowHotkey;
        if (IsHorseTemplate)
        {
            services.Overlay.Hotkeys.RefusedChanged += LoadHorseHotkeyRefused;
            LoadHorseHotkeyRefused();
        }
        IsStopwatch = timer.Kind == TimerKind.Stopwatch;
        CanDelete = Presets.CanDelete(timer.Preset);
        if (timer.Countdown is { } c) _durationText = Parsing.FormatDuration(c.Duration);
        _farmGrowth = GrowthOption(timer.Countdown?.Duration);
        if (timer.Scheduled is { } spec)
        {
            _timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(spec.TimeZoneId, out var windowsId) ? windowsId : spec.TimeZoneId;
            Slots = new SlotListViewModel(spec.Slots,
                slots => Modify(t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }));
        }
        Alerts = new AlertRowsViewModel(services, timer);
        if (IsHorseRun) services.Timers.Changed += OnHorseRunChanged;
    }

    partial void OnNameChanged(string value)
    {
        NameInvalid = string.IsNullOrWhiteSpace(value);
        if (!NameInvalid) Modify(t => t with { Name = value.Trim() });
    }

    partial void OnDurationTextChanged(string value)
    {
        DurationInvalid = !Parsing.TryParseDuration(value, out var duration);
        if (DurationInvalid || _syncingDuration) return;
        if (IsFarm && Timer.Countdown is { Status: not CountdownStatus.Idle })
            _typedFarmDuration = duration;
        else
            Modify(t => t with { Countdown = (t.Countdown ?? new CountdownSpec()) with { Duration = duration } });
        _syncingDuration = true;
        FarmGrowth = GrowthOption(duration);
        _syncingDuration = false;
    }

    /// <summary>Applies a duration typed for a running or paused Farm; the panel calls it when typing is done.</summary>
    public void CommitDuration()
    {
        if (_typedFarmDuration is not { } duration) return;
        _typedFarmDuration = null;
        Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) });
    }

    public void OnClosed()
    {
        CommitDuration();
        if (IsHorseRun) _services.Timers.Changed -= OnHorseRunChanged;
        if (!IsHorseTemplate) return;
        ListeningHorseHotkey = false;
        _services.Overlay.Hotkeys.RefusedChanged -= LoadHorseHotkeyRefused;
    }

    /// <summary>A finished run is removed by the scheduler; close its panel before another edit targets it.</summary>
    void OnHorseRunChanged() => Application.Current.Dispatcher.BeginInvoke((Action)(() =>
    {
        if (_host.IsOpen(this) && _services.Timers.Current.Timers.All(t => t.Id != _id)) _host.ClosePanel();
    }));

    void LoadHorseHotkeyRefused() =>
        HorseHotkeyRefused = _services.Overlay.Hotkeys.Refused.Contains(HotkeyAction.StartHorseRegistration);

    partial void OnHorseHotkeyChanged(Hotkey? value)
    {
        if (IsHorseTemplate) Modify(t => t with { StartHotkey = value });
    }

    partial void OnListeningHorseHotkeyChanged(bool value)
    {
        if (!IsHorseTemplate) return;
        if (value) _services.Overlay.Hotkeys.Suspend();
        else _services.Overlay.Hotkeys.Resume();
    }

    FarmGrowthOption GrowthOption(TimeSpan? duration) =>
        FarmGrowthOptions.FirstOrDefault(o => o.Duration == duration) ?? FarmGrowthOptions[^1];

    partial void OnFarmGrowthChanged(FarmGrowthOption value)
    {
        if (_syncingDuration || !IsFarm || value.Duration is not { } duration) return;
        _typedFarmDuration = null;
        Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) });
        _syncingDuration = true;
        DurationText = Parsing.FormatDuration(duration);
        _syncingDuration = false;
    }

    partial void OnTimeZoneIdChanged(string? value)
    {
        if (value is not null) Modify(t => t with { Scheduled = (t.Scheduled ?? new ScheduledSpec()) with { TimeZoneId = value } });
    }

    partial void OnAlertsOnChanged(Choice value) => Modify(t => t with { Enabled = value.IsOn });

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
        ReplacePicture(file);
    }

    [RelayCommand]
    void RemovePicture() => ReplacePicture(null);

    void ReplacePicture(string? file)
    {
        var old = Timer.ImageFile;
        Modify(t => t with { ImageFile = file });
        _services.Art.Delete(old);
        Images = [_services.Art.For(Timer)];
        HasPicture = file is not null;
    }

    [RelayCommand]
    void AskDelete() => ConfirmingDelete = true;

    [RelayCommand]
    void CancelDelete() => ConfirmingDelete = false;

    [RelayCommand]
    void ConfirmDelete()
    {
        var image = Timer.ImageFile;
        _services.Timers.Delete(_id);
        _services.Art.Delete(image);
        _host.ClosePanel();
    }

    void Modify(Func<TimerDef, TimerDef> change) => _services.Timers.Modify(_id, change);
}

public sealed record FarmGrowthOption(string Label, TimeSpan? Duration);
