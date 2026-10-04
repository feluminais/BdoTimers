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

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class CustomPanelViewModel : ObservableObject, IDraftPanel
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Guid _id;
    readonly TimerEditor _editor;
    bool _discarded;
    public bool HasChanges => !_discarded && (_editor.HasChanges || _typedDuration is not null);
    public bool IsBusy => _editor.IsBusy;
    public async Task SaveAsync() { CommitDuration(); await _editor.SaveAsync(); }
    bool _syncingDuration;
    bool _loadingSchedule;
    string _repeatError = "";
    string _dateRangeError = "";
    // An active countdown waits until typing is done: applied per keystroke, a half-typed
    // "2" would move the countdown's end into the past and the scheduler would end it.
    TimeSpan? _typedDuration;

    [ObservableProperty] private string _name;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanFinish))] private bool _nameInvalid;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images;
    [ObservableProperty] private bool _hasPicture;
    [ObservableProperty] private string? _pictureError;
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanFinish))] private bool _durationInvalid;
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
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasWeekAnchor))] private string _everyWeeksText = "1";
    [ObservableProperty] private string _weekAnchorText = "";
    [ObservableProperty] private bool _everyWeeksInvalid;
    [ObservableProperty] private bool _weekAnchorInvalid;
    [ObservableProperty] private SlotListViewModel? _slots;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ScheduleValid)), NotifyPropertyChangedFor(nameof(CanFinish))] private string _scheduleError = "";
    [ObservableProperty] private bool _alertsOn;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowsWeekly), nameof(HasTimeZone))] private bool _active;
    [ObservableProperty] private Hotkey? _horseHotkey;
    [ObservableProperty] private Hotkey? _controlHotkey;

    public bool IsCountdown { get; }
    public bool IsFarm { get; }
    public bool IsHorseTemplate { get; }
    public bool IsHorseRun { get; }
    public bool IsCustomCountdown { get; }
    public bool CanChangePicture { get; }
    bool HasHotkey => IsHorseTemplate || IsCustomCountdown;
    public HotkeyService Hotkeys => _services.Overlay.Hotkeys;
    public HotkeyTarget HotkeyTarget => IsHorseTemplate ? new(HotkeyAction.StartHorseRegistration)
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
    /// <summary>Guild bosses keeps its weekly time when turned off.</summary>
    public bool HasActive { get; }
    public bool ShowsWeekly => IsWeekly && Active;
    public bool HasWeekAnchor => int.TryParse(EveryWeeksText, out var weeks) && weeks > 1;
    public bool IsWarOfTheRoses { get; }
    public string ResetTimesLabel => $"Reset to {_services.Region.ShortLabel} times";
    public Confirmation ResetTimes { get; }
    public string WeeklyHeading => Timer.Preset == Presets.GuildBosses ? "Weekly time" : "Weekly times";
    public bool HasWeeklyDateRange => IsWeekly && Timer.Preset is not (Presets.GuildBosses or Presets.GuildWar);
    public bool IsOneTime { get; }
    public bool HasTimeZone => ShowsWeekly || IsOneTime;
    public DateTime Today => TimeZoneInfo.ConvertTime(_services.Clock.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId ?? "UTC")).Date;
    public Func<DateTime> TodayProvider => () => Today;
    public bool ScheduleValid => ScheduleError.Length == 0;
    public bool CanFinish => !NameInvalid && !DurationInvalid && ScheduleValid && (Slots?.IsValid ?? true) && !Alerts.VoiceLineInvalid;
    /// <summary>Stopwatches never alert, so they have no alert settings or on/off.</summary>
    public bool HasAlerts => !IsStopwatch;
    public bool CanDelete { get; }
    public Confirmation Delete { get; }
    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();
    public AlertRowsViewModel Alerts { get; }

    TimerDef Timer => _editor.Current;

    public CustomPanelViewModel(AppServices services, IPanelHost host, TimerDef timer)
    {
        _services = services;
        _host = host;
        _id = timer.Id;
        _editor = new(services, timer);
        _name = timer.Name;
        _images = [services.Art.For(timer)];
        _hasPicture = timer.ImageFile is not null;
        _alertsOn = timer.Enabled;
        IsCountdown = timer.Kind == TimerKind.Countdown;
        IsWeekly = timer.Kind == TimerKind.Scheduled;
        IsWarOfTheRoses = timer.Preset == Presets.WarOfTheRoses;
        HasActive = IsWeekly && timer.Preset == Presets.GuildBosses;
        _active = timer.Scheduled is not { Off: true };
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
            services.Timers.Changed += OnHotkeyConfigChanged;
            services.Settings.Changed += OnHotkeyConfigChanged;
        }
        IsStopwatch = timer.Kind == TimerKind.Stopwatch;
        CanDelete = Presets.CanDelete(timer.Preset);
        Delete = new Confirmation(DeleteTimer, hideAfter: false);
        ResetTimes = new Confirmation(ResetSchedule);
        if (IsWarOfTheRoses) services.Timers.Changed += OnRegionChanged;
        if (timer.Countdown is { } c) _durationText = Parsing.FormatDuration(c.Duration);
        _farmGrowth = GrowthOption(timer.Countdown?.Duration);
        if (timer.Scheduled is { } spec)
        {
            LoadSchedule(spec);
        }
        if (timer.OneTime is { } oneTime)
        {
            _timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(oneTime.TimeZoneId, out var windowsId) ? windowsId : oneTime.TimeZoneId;
            _eventDateText = Parsing.FormatDate(oneTime.Date);
            _eventTimeText = Parsing.FormatTime(oneTime.Time);
        }
        Alerts = new AlertRowsViewModel(services, timer, _editor);
        Alerts.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanFinish));
        if (IsHorseRun) services.Timers.Changed += OnHorseRunChanged;
    }

    partial void OnSlotsChanged(SlotListViewModel? oldValue, SlotListViewModel? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnSlotsValidityChanged;
        if (newValue is not null) newValue.PropertyChanged += OnSlotsValidityChanged;
        OnPropertyChanged(nameof(CanFinish));
    }

    void OnSlotsValidityChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(CanFinish));

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
        // A horse run can finish and disappear while its last text update is still queued.
        if (_services.Timers.Current.Timers.All(t => t.Id != _id)) return;
        var timer = Timer;
        if (timer.Countdown is { Status: not CountdownStatus.Idle })
            _typedDuration = duration;
        else
            _editor.Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) }, "duration");
        _syncingDuration = true;
        FarmGrowth = GrowthOption(duration);
        _syncingDuration = false;
    }

    /// <summary>Applies an active countdown's duration when typing is done.</summary>
    public void CommitDuration()
    {
        if (_typedDuration is not { } duration) return;
        _typedDuration = null;
        _editor.Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) }, "duration");
    }

    public void OnClosed()
    {
        _editor?.Close();
        if (IsWarOfTheRoses) _services.Timers.Changed -= OnRegionChanged;
        if (IsHorseRun) _services.Timers.Changed -= OnHorseRunChanged;
        if (!HasHotkey) return;
        _services.Timers.Changed -= OnHotkeyConfigChanged;
        _services.Settings.Changed -= OnHotkeyConfigChanged;
    }

    /// <summary>A finished run is removed by the scheduler; close its panel before another edit targets it.</summary>
    void OnHorseRunChanged() => Application.Current.Dispatcher.BeginInvoke((Action)(() =>
    {
        if (_host.IsOpen(this) && _services.Timers.Current.Timers.All(t => t.Id != _id)) { _discarded = true; _host.ClosePanel(); }
    }));

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

    FarmGrowthOption GrowthOption(TimeSpan? duration) =>
        FarmGrowthOptions.FirstOrDefault(o => o.Duration == duration) ?? FarmGrowthOptions[^1];

    partial void OnFarmGrowthChanged(FarmGrowthOption value)
    {
        if (_syncingDuration || !IsFarm || value.Duration is not { } duration) return;
        _typedDuration = null;
        _editor.Modify(t => t with { Countdown = CountdownOps.ChangeDuration(t.Countdown ?? new CountdownSpec(), duration, IsFarm) }, "duration");
        _syncingDuration = true;
        DurationText = Parsing.FormatDuration(duration);
        _syncingDuration = false;
    }

    partial void OnTimeZoneIdChanged(string? value)
    {
        OnPropertyChanged(nameof(Today));
        if (_loadingSchedule || value is null) return;
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
        try
        {
            var previous = Timer.OneTime!;
            if (previous.Date == date && previous.Time == time && previous.TimeZoneId == zone) return;
            var next = new OneTimeSpec { Date = date, Time = time, TimeZoneId = zone };
            var at = OneTimeEvents.AtUtc(next);
            Modify(t => t with { OneTime = next with { Finished = t.OneTime!.Finished && at <= _services.Clock.UtcNow } });
        }
        catch (ArgumentOutOfRangeException) { ScheduleError = "Date is out of range."; EventDateInvalid = true; }
    }

    partial void OnStartDateTextChanged(string value) => ApplyDateRange();
    partial void OnEndDateTextChanged(string value) => ApplyDateRange();

    partial void OnEveryWeeksTextChanged(string value) => ApplyRepeat();
    partial void OnWeekAnchorTextChanged(string value) => ApplyRepeat();

    void ApplyRepeat()
    {
        if (_loadingSchedule) return;
        EveryWeeksInvalid = !int.TryParse(EveryWeeksText, out var weeks) || weeks is < 1 or > ScheduleMath.MaxEveryWeeks;
        var validAnchor = Parsing.TryParseDate(WeekAnchorText, out var anchor);
        WeekAnchorInvalid = HasWeekAnchor && !validAnchor;
        _repeatError = EveryWeeksInvalid ? "Repeat every 1 to 52 weeks." : WeekAnchorInvalid ? "Enter a date (YYYY-MM-DD)." : "";
        RefreshScheduleError();
        if (_repeatError.Length == 0) Modify(t => t with { Scheduled = t.Scheduled! with { EveryWeeks = weeks, WeekAnchor = validAnchor ? anchor : null } });
    }

    void RefreshScheduleError() => ScheduleError = _repeatError.Length > 0 ? _repeatError : _dateRangeError;

    void LoadSchedule(ScheduledSpec spec)
    {
        _loadingSchedule = true;
        try
        {
            TimeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(spec.TimeZoneId, out var windowsId) ? windowsId : spec.TimeZoneId;
            StartDateText = spec.StartDate is { } start ? Parsing.FormatDate(start) : "";
            EndDateText = spec.EndDate is { } end ? Parsing.FormatDate(end) : "";
            EveryWeeksText = spec.EveryWeeks.ToString(System.Globalization.CultureInfo.InvariantCulture);
            WeekAnchorText = Parsing.FormatDate(spec.WeekAnchor ?? spec.StartDate
                ?? (spec.EveryWeeks > 1 ? new DateOnly(2001, 1, 1) : DateOnly.FromDateTime(Today)));
            Slots = new SlotListViewModel(spec.Slots,
                slots => Modify(t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }),
                Presets.MinimumSlots(Timer.Preset), Presets.MaximumSlots(Timer.Preset), hasLabels: true);
            StartDateInvalid = EndDateInvalid = EveryWeeksInvalid = WeekAnchorInvalid = false;
            _repeatError = _dateRangeError = "";
            RefreshScheduleError();
        }
        finally { _loadingSchedule = false; }
    }

    void ResetSchedule()
    {
        var schedule = Presets.WarOfTheRosesSchedule(_services.Timers.Current.SelectedBossRegion);
        Modify(t => t with { Scheduled = schedule });
        LoadSchedule(Timer.Scheduled!);
    }

    void OnRegionChanged()
    {
        if (Application.Current.Dispatcher.CheckAccess()) OnPropertyChanged(nameof(ResetTimesLabel));
        else Application.Current.Dispatcher.BeginInvoke((Action)(() => OnPropertyChanged(nameof(ResetTimesLabel))));
    }

    void ApplyDateRange()
    {
        if (_loadingSchedule) return;
        StartDateInvalid = !Parsing.TryParseDate(StartDateText, out var start) && !string.IsNullOrWhiteSpace(StartDateText);
        EndDateInvalid = !Parsing.TryParseDate(EndDateText, out var end) && !string.IsNullOrWhiteSpace(EndDateText);
        _dateRangeError = StartDateInvalid || EndDateInvalid ? "Enter a date (YYYY-MM-DD)." : "";
        RefreshScheduleError();
        if (_dateRangeError.Length > 0) return;
        try
        {
            var first = string.IsNullOrWhiteSpace(StartDateText) ? (DateOnly?)null : start;
            var last = string.IsNullOrWhiteSpace(EndDateText) ? (DateOnly?)null : end;
            Modify(t => t with { Scheduled = t.Scheduled! with { StartDate = first, EndDate = last } });
        }
        catch (ArgumentException ex) { _dateRangeError = ex.Message; RefreshScheduleError(); EndDateInvalid = true; }
    }

    partial void OnAlertsOnChanged(bool value) => Modify(t => t with { Enabled = value });

    partial void OnActiveChanged(bool value) =>
        Modify(t => t.Scheduled is { } spec ? t with { Scheduled = spec with { Off = !value } } : t);

    [RelayCommand]
    void ChoosePicture()
    {
        var (file, error) = _services.ChoosePicture();
        PictureError = error;
        if (file is not null) ReplacePicture(file);
    }

    [RelayCommand]
    void RemovePicture()
    {
        PictureError = null;
        ReplacePicture(null);
    }

    void ReplacePicture(string? file)
    {
        Modify(t => t with { ImageFile = file });
        Images = [_services.Art.For(Timer)];
        HasPicture = file is not null;
    }

    void DeleteTimer()
    {
        _host.CompletePanelEdits();
        CommitDuration();
        _services.Undo.DeleteTimer(_id);
        _discarded = true;
        _host.ClosePanel();
    }

    void Modify(Func<TimerDef, TimerDef> change) => _editor.Modify(change);
}

public sealed record FarmGrowthOption(string Label, TimeSpan? Duration);
