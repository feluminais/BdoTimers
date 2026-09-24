using System.Globalization;
using System.Windows.Media;
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
    [ObservableProperty] private IReadOnlyList<ImageSource> _images = [];
    [ObservableProperty] private string _digits = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private bool _isDimmed;
    [ObservableProperty] private string _playPauseGlyph = PlayGlyph;
    [ObservableProperty] private string _playPauseTip = "Start";
    [ObservableProperty] private string _skipLabel = "Skip next";

    public Guid Id => _timer.Id;
    public bool IsCountdown => _timer.Kind == TimerKind.Countdown;
    public bool IsWeekly => !IsCountdown;

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
        var off = _timer.Enabled ? "" : "Off · ";
        if (_timer.Countdown is { } c)
        {
            (Digits, Detail, IsDimmed) = c.Status switch
            {
                CountdownStatus.Running when c.EndsAtUtc is { } end => (DurationFormat.Clock(end - now), StartedText(c), false),
                CountdownStatus.Paused when c.Remaining is { } left => (DurationFormat.Clock(left), "Paused", true),
                _ => (DurationFormat.Clock(c.Duration), "Ready", true),
            };
            Detail = off + Detail;
            IsDimmed |= !_timer.Enabled;
            var running = c.Status == CountdownStatus.Running;
            PlayPauseGlyph = running ? PauseGlyph : PlayGlyph;
            PlayPauseTip = running ? "Pause" : c.Status == CountdownStatus.Paused ? "Resume" : "Start";
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

    static string StartedText(CountdownSpec c) =>
        c.StartedAtUtc is { } started
            ? $"Started {started.ToLocalTime().ToString("HH:mm:ss · MMM d", CultureInfo.InvariantCulture)}"
            : "Running";

    [RelayCommand]
    void Open() => _host.OpenPanel(new CustomPanelViewModel(_services, _host, _timer));

    [RelayCommand]
    void StartPause()
    {
        var now = DateTimeOffset.UtcNow;
        switch (_timer.Countdown?.Status)
        {
            case CountdownStatus.Running: _services.Timers.PauseCountdown(_timer.Id, now); break;
            case CountdownStatus.Paused: _services.Timers.ResumeCountdown(_timer.Id, now); break;
            case CountdownStatus.Idle: _services.Timers.StartCountdown(_timer.Id, now); break;
        }
    }

    [RelayCommand]
    void Reset() => _services.Timers.ResetCountdown(_timer.Id);

    [RelayCommand]
    void ToggleSkipNext()
    {
        if (_nextOccurrence is { } next) _services.Timers.ToggleMute(_timer.Id, next);
    }
}
