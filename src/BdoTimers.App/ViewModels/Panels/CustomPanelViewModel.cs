using System.IO;
using System.Windows;
using BdoTimers.App.Controls;
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
    // An active countdown waits until typing is done: applied per keystroke, a half-typed
    // "2" would move the countdown's end into the past and the scheduler would end it.
    TimeSpan? _typedDuration;

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _nameInvalid;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images;
    [ObservableProperty] private bool _hasPicture;
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private bool _durationInvalid;
    [ObservableProperty] private FarmGrowthOption _farmGrowth;
    [ObservableProperty] private string? _timeZoneId;
    [ObservableProperty] private string _eventDateText = "";
    [ObservableProperty] private string _eventTimeText = "";
    [ObservableProperty] private bool _eventDateInvalid;
    [ObservableProperty] private bool _eventTimeInvalid;
    [ObservableProperty] private string _startDateText = "";
    [ObservableProperty] private string _endDateText = "";
    [ObservableProperty] private bool _startDateInvalid;
    [ObservableProperty] private bool _endDateInvalid;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ScheduleValid))] private string _scheduleError = "";
    [ObservableProperty] private Choice _alertsOn;
    [ObservableProperty] private bool _confirmingDelete;
    [ObservableProperty] private Hotkey? _horseHotkey;
    [ObservableProperty] private bool _horseHotkeyRefused;
    [ObservableProperty] private bool _listeningHorseHotkey;
    [ObservableProperty] private Hotkey? _controlHotkey;
    [ObservableProperty] private bool _controlHotkeyRefused;
    [ObservableProperty] private bool _listeningControlHotkey;

    public bool IsCountdown { get; }
    public bool IsFarm { get; }
    public bool IsHorseTemplate { get; }
    public bool IsHorseRun { get; }
    public bool IsCustomCountdown { get; }
    public bool CanChangePicture { get; }
    bool HasHotkey => IsHorseTemplate || IsCustomCountdown;
    HotkeyTarget HotkeyTarget => IsHorseTemplate ? new(HotkeyAction.StartHorseRegistration)
        : new(HotkeyAction.ControlCountdown, _id);
    public IReadOnlyList<Hotkey> OtherHotkeys =>
        HotkeyCatalog.OtherKeys(_services.Timers.Current, _services.Settings.Current.Overlay, HotkeyTarget);
    public IReadOnlyList<FarmGrowthOption> FarmGrowthOptions { get; } =
    [
        new("20 h · Suitable", TimeSpan.FromHours(20)),
        new("21 h · Unsuitable", TimeSpan.FromHours(21)),
        new("22 h · Highly unsuitable", TimeSpan.FromHours(22)),
        new("Custom duration", null),
    ];
    public bool IsStopwatch { get; }
    public bool IsWeekly { get; }
    public bool IsOneTime { get; }
    public bool HasTimeZone => IsWeekly || IsOneTime;
    public bool ScheduleValid => ScheduleError.Length == 0;
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
        IsWeekly = timer.Kind == TimerKind.Scheduled;
        IsOneTime = timer.Kind == TimerKind.OneTime;
        IsFarm = timer.Preset == Presets.Farm && IsCountdown;
        IsHorseTemplate = timer.Preset == Presets.HorseRegistration;
        IsHorseRun = timer.Preset == Presets.HorseRegistrationRun;
        IsCustomCountdown = CustomCountdowns.Includes(timer);
        CanChangePicture = timer.Preset != Presets.HorseRegistrationRun;
        _horseHotkey = timer.StartHotkey;
        _controlHotkey = timer.ControlHotkey;
        if (HasHotkey)
        {
            services.Overlay.Hotkeys.RefusedChanged += LoadHotkeyRefused;
            services.Timers.Changed += OnHotkeyConfigChanged;
            services.Settings.Changed += OnHotkeyConfigChanged;
            LoadHotkeyRefused();
        }
        IsStopwatch = timer.Kind == TimerKind.Stopwatch;
        CanDelete = Presets.CanDelete(timer.Preset);
        if (timer.Countdown is { } c) _durationText = Parsing.FormatDuration(c.Duration);
        _farmGrowth = GrowthOption(timer.Countdown?.Duration);
        if (timer.Scheduled is { } spec)
        {
            _timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(spec.TimeZoneId, out var windowsId) ? windowsId : spec.TimeZoneId;
            _startDateText = spec.StartDate is { } start ? Parsing.FormatDate(start) : "";
            _endDateText = spec.EndDate is { } end ? Parsing.FormatDate(end) : "";
            Slots = new SlotListViewModel(spec.Slots,
                slots => Modify(t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }));
        }
        if (timer.OneTime is { } oneTime)
        {
            _timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(oneTime.TimeZoneId, out var windowsId) ? windowsId : oneTime.TimeZoneId;
            _eventDateText = Parsing.FormatDate(oneTime.Date);
            _eventTimeText = Parsing.FormatTime(oneTime.Time);
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
        if (_syncingDuration) return;
        _typedDuration = null;
        if (DurationInvalid) return;
        if (Timer.Countdown is { Status: not CountdownStatus.Idle })
            _typedDuration = duration;
        else
            Modify(t => t with { Countdown = (t.Countdown ?? new CountdownSpec()) with { Duration = duration } });
        _syncingDuration = true;
        FarmGrowth = GrowthOption(duration);
        _syncingDuration = false;
    }

    /// <summary>Applies an active countdown's duration when typing is done.</summary>
    public void CommitDuration()
    {
        if (_typedDuration is not { } duration) return;
        _typedDuration = null;
        Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) });
    }

    public void OnClosed()
    {
        CommitDuration();
        if (IsHorseRun) _services.Timers.Changed -= OnHorseRunChanged;
        if (!HasHotkey) return;
        ListeningHorseHotkey = false;
        ListeningControlHotkey = false;
        _services.Overlay.Hotkeys.RefusedChanged -= LoadHotkeyRefused;
        _services.Timers.Changed -= OnHotkeyConfigChanged;
        _services.Settings.Changed -= OnHotkeyConfigChanged;
    }

    /// <summary>A finished run is removed by the scheduler; close its panel before another edit targets it.</summary>
    void OnHorseRunChanged() => Application.Current.Dispatcher.BeginInvoke((Action)(() =>
    {
        if (_host.IsOpen(this) && _services.Timers.Current.Timers.All(t => t.Id != _id)) _host.ClosePanel();
    }));

    void LoadHotkeyRefused()
    {
        var refused = _services.Overlay.Hotkeys.Refused;
        HorseHotkeyRefused = refused.Contains(new(HotkeyAction.StartHorseRegistration));
        ControlHotkeyRefused = refused.Contains(new(HotkeyAction.ControlCountdown, _id));
    }

    void OnHotkeyConfigChanged()
    {
        if (Application.Current.Dispatcher.CheckAccess()) OnPropertyChanged(nameof(OtherHotkeys));
        else Application.Current.Dispatcher.BeginInvoke((Action)(() => OnPropertyChanged(nameof(OtherHotkeys))));
    }

    partial void OnHorseHotkeyChanged(Hotkey? value)
    {
        if (IsHorseTemplate) Modify(t => t with { StartHotkey = value });
    }

    partial void OnControlHotkeyChanged(Hotkey? value)
    {
        if (IsCustomCountdown) Modify(t => t with { ControlHotkey = value });
    }

    partial void OnListeningHorseHotkeyChanged(bool value)
    {
        if (!IsHorseTemplate) return;
        Listen(value);
    }

    partial void OnListeningControlHotkeyChanged(bool value)
    {
        if (!IsCustomCountdown) return;
        Listen(value);
    }

    void Listen(bool value)
    {
        if (value) _services.Overlay.Hotkeys.Suspend();
        else _services.Overlay.Hotkeys.Resume();
    }

    FarmGrowthOption GrowthOption(TimeSpan? duration) =>
        FarmGrowthOptions.FirstOrDefault(o => o.Duration == duration) ?? FarmGrowthOptions[^1];

    partial void OnFarmGrowthChanged(FarmGrowthOption value)
    {
        if (_syncingDuration || !IsFarm || value.Duration is not { } duration) return;
        _typedDuration = null;
        Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) });
        _syncingDuration = true;
        DurationText = Parsing.FormatDuration(duration);
        _syncingDuration = false;
    }

    partial void OnTimeZoneIdChanged(string? value)
    {
        if (value is null) return;
        if (IsOneTime) ApplyEvent();
        else if (IsWeekly) Modify(t => t with { Scheduled = (t.Scheduled ?? new ScheduledSpec()) with { TimeZoneId = value } });
    }

    partial void OnEventDateTextChanged(string value) => ApplyEvent();
    partial void OnEventTimeTextChanged(string value) => ApplyEvent();

    void ApplyEvent()
    {
        EventDateInvalid = !Parsing.TryParseDate(EventDateText, out var date);
        EventTimeInvalid = !Parsing.TryParseTime(EventTimeText, out var time);
        ScheduleError = EventDateInvalid ? "Enter a date (YYYY-MM-DD)." : EventTimeInvalid ? "Enter a time (HH:mm)." : "";
        if (!ScheduleValid || TimeZoneId is not { } zone) return;
        try { _services.Timers.RescheduleEvent(_id, date, time, zone, _services.Clock); }
        catch (ArgumentOutOfRangeException) { ScheduleError = "Date is out of range."; EventDateInvalid = true; }
    }

    partial void OnStartDateTextChanged(string value) => ApplyDateRange();
    partial void OnEndDateTextChanged(string value) => ApplyDateRange();

    void ApplyDateRange()
    {
        StartDateInvalid = !Parsing.TryParseDate(StartDateText, out var start) && !string.IsNullOrWhiteSpace(StartDateText);
        EndDateInvalid = !Parsing.TryParseDate(EndDateText, out var end) && !string.IsNullOrWhiteSpace(EndDateText);
        ScheduleError = StartDateInvalid || EndDateInvalid ? "Enter a date (YYYY-MM-DD)." : "";
        if (!ScheduleValid) return;
        try
        {
            _services.Timers.SetWeeklyDateRange(_id, string.IsNullOrWhiteSpace(StartDateText) ? null : start,
                string.IsNullOrWhiteSpace(EndDateText) ? null : end);
        }
        catch (ArgumentException ex) { ScheduleError = ex.Message; EndDateInvalid = true; }
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
