using System.Globalization;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class TimerTileViewModel : ObservableObject
{
    const string PlayGlyph = "";
    const string PauseGlyph = "";
    // Icon font: Next and Undo.
    const string SkipNextGlyph = "";
    const string UnskipNextGlyph = "";

    readonly AppServices _services;
    readonly IPanelHost _host;
    TimerDef _timer;
    DateTimeOffset? _nextOccurrence;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images = [];
    [ObservableProperty] private string _digits = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private bool _isDimmed;
    [ObservableProperty] private string _playPauseGlyph = PlayGlyph;
    [ObservableProperty] private string _playPauseTip = "Start";
    [ObservableProperty] private string _skipLabel = "Skip next";
    [ObservableProperty] private string _skipGlyph = SkipNextGlyph;
    [ObservableProperty] private bool _hasNextOccurrence;
    /// <summary>The dot beside the name: green while a countdown or stopwatch runs, amber while it is paused.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanStop))] private bool _isRunning;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanStop))] private bool _isPaused;
    /// <summary>How far along a countdown is, 0 to 1 (Farm's growth stops at the ring); null for what has no end.</summary>
    [ObservableProperty] private double? _progress;
    [ObservableProperty] private bool _canStartHorse;
    [ObservableProperty] private string _horseStartTip = "Start registration";
    /// <summary>Farm only: the crops' growth %, like the game shows it; null while the countdown is idle.</summary>
    [ObservableProperty] private int? _growth;

    // "Started earlier": the clock time the user really started, picked in a small popup on the tile. A countdown can
    // be given how far along it is instead (a crop's growth %, on a slider); each follows the other, and Start uses the
    // one edited last.
    [ObservableProperty] private bool _isPickingStart;
    [ObservableProperty] private string _startHour = "";
    [ObservableProperty] private string _startMinute = "";
    [ObservableProperty] private int _startPercent;
    [ObservableProperty] private bool _startInvalid;
    [ObservableProperty] private string _startProblem = "";
    bool _fromPercent;
    /// <summary>Set while one field is rewritten to follow the other, so that doesn't count as an edit.</summary>
    bool _following;

    public Guid Id => _timer.Id;
    /// <summary>Countdowns and stopwatches: start, pause and reset from the tile.</summary>
    public bool HasControls => !IsHorseTemplate && _timer.Kind is (TimerKind.Countdown or TimerKind.Stopwatch);
    public bool IsHorseTemplate => _timer.Preset == Presets.HorseRegistration;
    /// <summary>A countdown or stopwatch that has been started has a Stop button; one at rest has nothing to stop.</summary>
    public bool CanStop => HasControls && (IsRunning || IsPaused);
    public bool IsFarm => _timer.Preset == Presets.Farm;
    /// <summary>Only a countdown has an end, so only it can be started from a percent.</summary>
    public bool HasPercent => _timer.Kind == TimerKind.Countdown;
    public int MaxStartPercent => IsFarm ? 200 : 100;
    public bool IsWeekly => _timer.Kind == TimerKind.Scheduled;
    /// <summary>What a preset whose use isn't obvious is for; the tile shows it in an (i) beside the name.</summary>
    public string? Info => _timer.Preset == Presets.HorseRegistration
        ? "Start a registration at the game's notice; each ends when the horse goes on sale."
        : null;

    public TimerTileViewModel(TimerDef timer, AppServices services, IPanelHost host, DateTimeOffset now)
    {
        _services = services;
        _host = host;
        _timer = timer;
        SetTimer(timer, now);
    }

    public void SetTimer(TimerDef timer, DateTimeOffset now)
    {
        var pictureChanged = timer.ImageFile != _timer.ImageFile || Images.Count == 0;
        _timer = timer;
        _nextOccurrence = null;
        Name = timer.Name;
        if (pictureChanged) Images = [_services.Art.For(timer)];
        Refresh(now);
    }

    public void Refresh(DateTimeOffset now)
    {
        var off = _timer.Enabled ? "" : "Alerts off · ";
        if (IsHorseTemplate && _timer.Countdown is { } horse)
        {
            var runs = _services.Timers.Current.Timers.Where(Presets.IsActiveHorseRun).ToList();
            CanStartHorse = runs.Count < TimerStore.MaxHorseRegistrations;
            HorseStartTip = CanStartHorse ? "Start registration" : Formats.HorseRegistrations(runs.Count);
            var nextHorse = runs.Where(t => t.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: not null })
                .MinBy(t => t.Countdown!.EndsAtUtc);
            Digits = nextHorse?.Countdown?.EndsAtUtc is { } end ? DurationFormat.Clock(end - now) : DurationFormat.Clock(horse.Duration);
            Detail = off + (runs.Count == 0 ? "Ready" : Formats.HorseRegistrations(runs.Count));
            IsDimmed = runs.Count == 0 || !_timer.Enabled;
            return;
        }
        if (_timer.Countdown is { } c)
        {
            string Remaining(TimeSpan left) => IsFarm ? DurationFormat.SignedClock(left) : DurationFormat.Clock(left);
            (Digits, Detail, IsDimmed) = c.Status switch
            {
                CountdownStatus.Running when c.EndsAtUtc is { } end => (Remaining(end - now), StartedText(c.StartedAtUtc), false),
                CountdownStatus.Paused when c.Remaining is { } left => (Remaining(left), "Paused", true),
                _ => (DurationFormat.Clock(c.Duration), "Ready", true),
            };
            Detail = off + Detail;
            IsDimmed |= !_timer.Enabled;
            Growth = IsFarm ? CountdownOps.Progress(c, now, overgrows: true) : null;
            Progress = Math.Min((CountdownOps.Progress(c, now, IsFarm) ?? 0) / 100.0, 1);
            ShowStatus(c.Status);
            return;
        }

        if (_timer.Stopwatch is { } s)
        {
            Digits = DurationFormat.Clock(StopwatchOps.Elapsed(s, now));
            (Detail, IsDimmed) = s.Status switch
            {
                CountdownStatus.Running => (StartedText(s.StartedAtUtc), false),
                CountdownStatus.Paused => ("Paused", true),
                _ => ("Ready", true),
            };
            ShowStatus(s.Status);
            return;
        }

        if (_timer.OneTime is { Finished: true })
        {
            (Digits, Detail, IsDimmed) = ("--:--:--", "Finished", true);
            return;
        }

        // Dated and weekly timers: keep the next occurrence until it passes or the saved schedule changes.
        if (_nextOccurrence is not { } cached || cached <= now)
            _nextOccurrence = OccurrenceSource.Next(_timer, now);
        HasNextOccurrence = IsWeekly && _nextOccurrence is not null;
        if (_nextOccurrence is not { } next)
        {
            var state = _timer.Kind == TimerKind.OneTime ? "Finished"
                : _timer.Scheduled is { Off: true } ? "Off"
                : _timer.Scheduled is { } spec && ScheduleMath.IsExpired(spec, now) ? "Expired"
                : _timer.Preset is Presets.GuildBosses or Presets.GuildWar ? "Not set" : "No times set";
            (Digits, Detail, IsDimmed) = ("--:--:--", off + state, true);
            return;
        }
        var skipped = _services.Timers.Current.Muted.Contains(new MutedOccurrence(_timer.Id, next));
        Digits = DurationFormat.Clock(next - now);
        var format = _timer.Kind == TimerKind.OneTime ? "MMM d, yyyy HH:mm" : "ddd HH:mm";
        var label = _timer.Scheduled is { } schedule ? ScheduleMath.SlotAt(schedule, next)?.Label : null;
        var named = string.IsNullOrWhiteSpace(label) ? "" : $"{label} · ";
        Detail = $"{off}Next {named}{(_timer.Kind == TimerKind.OneTime ? next.ToLocalTime().ToString(format, CultureInfo.InvariantCulture) : Formats.DayTime(next))}{(skipped ? " · skipped" : "")}";
        IsDimmed = !_timer.Enabled || skipped;
        SkipLabel = skipped ? "Unskip next" : "Skip next";
        SkipGlyph = skipped ? UnskipNextGlyph : SkipNextGlyph;
    }

    void ShowStatus(CountdownStatus status)
    {
        var running = status == CountdownStatus.Running;
        IsRunning = running;
        IsPaused = status == CountdownStatus.Paused;
        PlayPauseGlyph = running ? PauseGlyph : PlayGlyph;
        PlayPauseTip = running ? "Pause" : status == CountdownStatus.Paused ? "Resume" : "Start";
    }

    static string StartedText(DateTimeOffset? startedAtUtc) =>
        startedAtUtc is { } started
            ? $"Started {started.ToLocalTime().ToString("HH:mm:ss · MMM d", CultureInfo.InvariantCulture)}"
            : "Running";

    [RelayCommand]
    void Open() => _host.OpenPanel(new CustomPanelViewModel(_services, _host, _timer));

    [RelayCommand]
    void StartHorseRegistration() => _services.StartHorseRegistration(announce: false);

    [RelayCommand]
    void StartPause()
    {
        if (CustomCountdowns.Includes(_timer))
        {
            _services.ControlCustomCountdown(_timer.Id);
            return;
        }
        var now = _services.Clock.UtcNow;
        var timers = _services.Timers;
        switch (_timer.Stopwatch?.Status ?? _timer.Countdown?.Status)
        {
            case CountdownStatus.Running: timers.Pause(_timer.Id, now); break;
            case CountdownStatus.Paused: timers.Resume(_timer.Id, now); break;
            case CountdownStatus.Idle: timers.Start(_timer.Id, now); break;
        }
    }

    /// <summary>
    /// Opens the picker at the current run's start, or at the current time. A running countdown's start is where its
    /// end puts it, so time spent paused doesn't count as progress.
    /// </summary>
    [RelayCommand]
    void PickStart()
    {
        var now = _services.Clock.UtcNow;
        var running = _timer.Countdown is { Status: CountdownStatus.Running, EndsAtUtc: { } end } c ? end - c.Duration
            : _timer.Stopwatch is { Status: CountdownStatus.Running } s ? s.StartedAtUtc
            : null;
        var from = running ?? now;
        _fromPercent = false;
        Follow(() =>
        {
            ShowTime(from);
            if (_timer.Countdown is { } countdown) StartPercent = PercentFor(countdown.Duration, now - from);
        });
        CheckStart();
        IsPickingStart = true;
    }

    [RelayCommand] void HourUp() => StartHour = Step(StartHour, 24, 1);
    [RelayCommand] void HourDown() => StartHour = Step(StartHour, 24, -1);
    [RelayCommand] void MinuteUp() => StartMinute = Step(StartMinute, 60, 1);
    [RelayCommand] void MinuteDown() => StartMinute = Step(StartMinute, 60, -1);

    [RelayCommand]
    void CancelStart() => IsPickingStart = false;

    [RelayCommand(CanExecute = nameof(CanConfirmStart))]
    void ConfirmStart()
    {
        if (PickedStart() is not { } at) return;
        _services.Timers.Start(_timer.Id, at);
        IsPickingStart = false;
    }

    bool CanConfirmStart() => !StartInvalid && StartProblem.Length == 0;

    partial void OnStartHourChanged(string value) => TimeEdited();
    partial void OnStartMinuteChanged(string value) => TimeEdited();
    partial void OnStartPercentChanged(int value) => PercentEdited();

    void TimeEdited()
    {
        if (_following) return;
        _fromPercent = false;
        if (TimeStart() is { } at && _timer.Countdown is { } c)
            Follow(() => StartPercent = PercentFor(c.Duration, _services.Clock.UtcNow - at));
        CheckStart();
    }

    void PercentEdited()
    {
        if (_following) return;
        _fromPercent = true;
        if (PercentStart() is { } at) Follow(() => ShowTime(at));
        CheckStart();
    }

    /// <summary>How far along a countdown is after <paramref name="elapsed"/>; Farm's as its crops' growth.</summary>
    int PercentFor(TimeSpan duration, TimeSpan elapsed) =>
        CountdownOps.ProgressPercent(duration, elapsed, Presets.Overgrows(_timer.Preset));

    void Follow(Action update)
    {
        _following = true;
        try { update(); }
        finally { _following = false; }
    }

    void ShowTime(DateTimeOffset at)
    {
        var local = at.ToLocalTime();
        StartHour = Two(local.Hour);
        StartMinute = Two(local.Minute);
    }

    /// <summary>A countdown that would already have ended by now can't start there; Farm can, since it keeps growing.</summary>
    void CheckStart()
    {
        StartInvalid = TimeStart() is null;
        var at = PickedStart();
        StartProblem = !IsFarm && at is { } start && _timer.Countdown is { } c && start + c.Duration <= _services.Clock.UtcNow
            ? $"Would have ended at {Formats.Time(start + c.Duration)}"
            : "";
        ConfirmStartCommand.NotifyCanExecuteChanged();
    }

    DateTimeOffset? PickedStart() => _fromPercent ? PercentStart() : TimeStart();

    /// <summary>The picked local time, today or, if that's still ahead, yesterday.</summary>
    DateTimeOffset? TimeStart() =>
        int.TryParse(StartHour, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) && hour < 24
        && int.TryParse(StartMinute, NumberStyles.None, CultureInfo.InvariantCulture, out var minute) && minute < 60
            ? ScheduleMath.MostRecent(new TimeOnly(hour, minute), _services.Clock.UtcNow, TimeZoneInfo.Local)
            : null;

    /// <summary>Taken against the current time, so a pause before pressing Start doesn't shift it.</summary>
    DateTimeOffset? PercentStart() =>
        _timer.Countdown is { } c ? CountdownOps.StartForProgress(c.Duration, StartPercent, _services.Clock.UtcNow) : null;

    static string Step(string text, int modulo, int delta)
    {
        var value = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0;
        return Two(((value + delta) % modulo + modulo) % modulo);
    }

    static string Two(int value) => value.ToString("00", CultureInfo.InvariantCulture);

    [RelayCommand]
    void Reset()
    {
        _services.Undo.ResetTimer(_timer.Id);
    }

    [RelayCommand]
    void ToggleSkipNext()
    {
        if (_nextOccurrence is { } next) _services.Timers.ToggleMute(_timer.Id, next);
    }
}
