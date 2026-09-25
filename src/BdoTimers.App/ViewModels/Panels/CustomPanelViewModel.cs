using System.IO;
using BdoTimers.App.Controls;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class CustomPanelViewModel : ObservableObject, IPanel
{
    static readonly TimeSpan MaxElapsed = TimeSpan.FromDays(7);

    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Guid _id;
    bool _showingClock;

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _nameInvalid;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images;
    [ObservableProperty] private bool _hasPicture;
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private bool _durationInvalid;
    [ObservableProperty] private Choice _repeat = Choice.For(false);
    [ObservableProperty] private string? _timeZoneId;
    [ObservableProperty] private Choice _active;
    [ObservableProperty] private bool _confirmingDelete;
    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private bool _clockInvalid;
    [ObservableProperty] private bool _showsClock;

    public bool IsCountdown { get; }
    public bool IsStopwatch { get; }
    public bool IsWeekly => !IsCountdown && !IsStopwatch;
    /// <summary>Stopwatches never alert, so they have no alert settings or on/off.</summary>
    public bool HasAlerts => !IsStopwatch;
    public bool CanDelete { get; }
    public string ClockLabel => IsStopwatch ? "Elapsed" : "Time left";
    /// <summary>Set by the view while the time box has focus, so the running time doesn't overwrite what is typed.</summary>
    public bool EditingClock { get; set; }
    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();
    public AlertRowsViewModel Alerts { get; }
    public SlotListViewModel? Slots { get; }

    TimerDef Timer => _services.Timers.Current.Timers.First(t => t.Id == _id);
    TimerDef? TimerIfAny => _services.Timers.Current.Timers.FirstOrDefault(t => t.Id == _id);

    public CustomPanelViewModel(AppServices services, IPanelHost host, TimerDef timer)
    {
        _services = services;
        _host = host;
        _id = timer.Id;
        _name = timer.Name;
        _images = [services.Art.For(timer)];
        _hasPicture = timer.ImageFile is not null;
        _active = Choice.For(timer.Enabled);
        IsCountdown = timer.Kind == TimerKind.Countdown;
        IsStopwatch = timer.Kind == TimerKind.Stopwatch;
        CanDelete = timer.Preset is null;
        if (timer.Countdown is { } c)
        {
            _durationText = Parsing.FormatDuration(c.Duration);
            _repeat = Choice.For(c.AutoRepeat);
        }
        if (timer.Scheduled is { } spec)
        {
            _timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(spec.TimeZoneId, out var windowsId) ? windowsId : spec.TimeZoneId;
            Slots = new SlotListViewModel(spec.Slots,
                slots => Modify(t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }));
        }
        Alerts = new AlertRowsViewModel(services, timer);
        ShowClock(DateTimeOffset.UtcNow);
        services.UiClock.Tick += ShowClock;
    }

    public void OnClosed() => _services.UiClock.Tick -= ShowClock;

    /// <summary>Time left of a started countdown, rounded up like alerts count it, or a stopwatch's elapsed time.</summary>
    void ShowClock(DateTimeOffset now)
    {
        TimeSpan? value = TimerIfAny switch
        {
            { Stopwatch: { } s } => StopwatchOps.Elapsed(s, now),
            { Countdown: { Status: CountdownStatus.Running, EndsAtUtc: { } end } } =>
                TimeSpan.FromMinutes(Math.Max(0, Math.Ceiling((end - now).TotalMinutes))),
            { Countdown: { Status: CountdownStatus.Paused, Remaining: { } left } } => TimeSpan.FromMinutes(Math.Ceiling(left.TotalMinutes)),
            _ => null,
        };
        ShowsClock = value is not null;
        if (value is not { } span)
        {
            ClockInvalid = false;
            return;
        }
        // A rejected entry stays, marked, until it is corrected.
        if (EditingClock || ClockInvalid) return;
        _showingClock = true;
        ClockText = Parsing.FormatDuration(span);
        ClockInvalid = false;
        _showingClock = false;
    }

    /// <summary>The view commits the time box on Enter or when it loses focus, so half-typed values never start alerts.</summary>
    partial void OnClockTextChanged(string value)
    {
        if (_showingClock) return;
        var now = DateTimeOffset.UtcNow;
        if (IsStopwatch)
        {
            ClockInvalid = !Parsing.TryParseHoursMinutes(value, TimeSpan.Zero, MaxElapsed, out var elapsed);
            if (!ClockInvalid) _services.Timers.SetElapsed(_id, now, elapsed);
        }
        else
        {
            ClockInvalid = !Parsing.TryParseDuration(value, out var left);
            if (!ClockInvalid) _services.Timers.SetTimeLeft(_id, now, left);
        }
    }

    partial void OnNameChanged(string value)
    {
        NameInvalid = string.IsNullOrWhiteSpace(value);
        if (!NameInvalid) Modify(t => t with { Name = value.Trim() });
    }

    partial void OnDurationTextChanged(string value)
    {
        DurationInvalid = !Parsing.TryParseDuration(value, out var duration);
        if (!DurationInvalid)
            Modify(t => t with { Countdown = (t.Countdown ?? new CountdownSpec()) with { Duration = duration } });
    }

    partial void OnRepeatChanged(Choice value) =>
        Modify(t => t with { Countdown = (t.Countdown ?? new CountdownSpec()) with { AutoRepeat = value.IsOn } });

    partial void OnTimeZoneIdChanged(string? value)
    {
        if (value is not null) Modify(t => t with { Scheduled = (t.Scheduled ?? new ScheduledSpec()) with { TimeZoneId = value } });
    }

    partial void OnActiveChanged(Choice value) => Modify(t => t with { Enabled = value.IsOn });

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
