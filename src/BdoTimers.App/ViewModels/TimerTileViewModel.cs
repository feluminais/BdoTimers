using System.Globalization;
using BdoTimers.App.Controls;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public sealed partial class TimerTileViewModel : ObservableObject
{
    const string PlayGlyph = "";
    const string PauseGlyph = "";

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

    public Guid Id => _timer.Id;
    /// <summary>Countdowns and stopwatches: start, pause and reset from the tile.</summary>
    public bool HasControls => _timer.Kind is TimerKind.Countdown or TimerKind.Stopwatch;
    public bool IsWeekly => _timer.Kind == TimerKind.Scheduled;

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
        if (_timer.Countdown is { } c)
        {
            (Digits, Detail, IsDimmed) = c.Status switch
            {
                CountdownStatus.Running when c.EndsAtUtc is { } end => (DurationFormat.Clock(end - now), StartedText(c.StartedAtUtc), false),
                CountdownStatus.Paused when c.Remaining is { } left => (DurationFormat.Clock(left), "Paused", true),
                _ => (DurationFormat.Clock(c.Duration), "Ready", true),
            };
            Detail = off + Detail;
            IsDimmed |= !_timer.Enabled;
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

        // Weekly timers: recompute the next occurrence only once the cached one has passed.
        if (_nextOccurrence is not { } cached || cached <= now)
            _nextOccurrence = OccurrenceSource.Between(_timer, now, now + TimeSpan.FromDays(8)).Cast<DateTimeOffset?>().FirstOrDefault();
        if (_nextOccurrence is not { } next)
        {
            (Digits, Detail, IsDimmed) = ("--:--:--", off + "No times set", true);
            return;
        }
        var skipped = _services.Timers.Current.Muted.Contains(new MutedOccurrence(_timer.Id, next));
        Digits = DurationFormat.Clock(next - now);
        Detail = $"{off}Next {next.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture)}{(skipped ? " · skipped" : "")}";
        IsDimmed = !_timer.Enabled || skipped;
        SkipLabel = skipped ? "Unskip next" : "Skip next";
    }

    void ShowStatus(CountdownStatus status)
    {
        var running = status == CountdownStatus.Running;
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
    void StartPause()
    {
        var now = DateTimeOffset.UtcNow;
        var timers = _services.Timers;
        if (_timer.Stopwatch is { } s)
        {
            switch (s.Status)
            {
                case CountdownStatus.Running: timers.PauseStopwatch(_timer.Id, now); break;
                case CountdownStatus.Paused: timers.ResumeStopwatch(_timer.Id, now); break;
                case CountdownStatus.Idle: timers.StartStopwatch(_timer.Id, now); break;
            }
            return;
        }
        switch (_timer.Countdown?.Status)
        {
            case CountdownStatus.Running: timers.PauseCountdown(_timer.Id, now); break;
            case CountdownStatus.Paused: timers.ResumeCountdown(_timer.Id, now); break;
            case CountdownStatus.Idle: timers.StartCountdown(_timer.Id, now); break;
        }
    }

    [RelayCommand]
    void Reset()
    {
        if (_timer.Stopwatch is not null) _services.Timers.ResetStopwatch(_timer.Id);
        else _services.Timers.ResetCountdown(_timer.Id);
    }

    [RelayCommand]
    void ToggleSkipNext()
    {
        if (_nextOccurrence is { } next) _services.Timers.ToggleMute(_timer.Id, next);
    }
}
