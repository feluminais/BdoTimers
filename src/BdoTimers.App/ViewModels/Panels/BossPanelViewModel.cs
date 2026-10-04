using BdoTimers.App.Controls;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>
/// A boss's alerts, spawn times and picture. Any boss can be removed; a boss the player added can also be renamed and
/// limited to dates, for event bosses.
/// </summary>
public sealed partial class BossPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Guid _id;

    [ObservableProperty] private bool _alertsOn;
    [ObservableProperty] private bool _showTimes;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AppliesText)), NotifyPropertyChangedFor(nameof(CanFinish))] private string _name;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanFinish))] private bool _nameInvalid;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images;
    [ObservableProperty] private bool _hasPicture;
    [ObservableProperty] private string? _pictureError;
    [ObservableProperty] private string _startDateText = "";
    [ObservableProperty] private string _endDateText = "";
    [ObservableProperty] private bool _startDateInvalid;
    [ObservableProperty] private bool _endDateInvalid;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ScheduleValid)), NotifyPropertyChangedFor(nameof(CanFinish))] private string _scheduleError = "";

    public bool IsAdded { get; }
    public string NextText { get; }
    public string AppliesText => $"Applies to every {Name} spawn";
    public AlertRowsViewModel Alerts { get; }
    public SlotListViewModel Slots { get; }
    public string TimeZoneNote { get; }
    public bool CanFinish => !NameInvalid && ScheduleValid && Slots.IsValid && !Alerts.VoiceLineInvalid;
    public bool ScheduleValid => ScheduleError.Length == 0;
    public Func<DateTime> TodayProvider { get; }
    public Confirmation Remove { get; }
    public string? RemoveTooltip => IsAdded ? null : "Settings → Bosses → Reset bosses brings it back";

    TimerDef Boss => _services.Timers.Current.Timers.First(t => t.Id == _id);

    public BossPanelViewModel(AppServices services, IPanelHost host, TimerDef boss)
    {
        _services = services;
        _host = host;
        _id = boss.Id;
        _alertsOn = boss.Enabled;
        _name = boss.Name;
        _images = [services.Art.For(boss)];
        _hasPicture = boss.ImageFile is not null;
        IsAdded = boss.AddedByUser;
        // A boss the player added is mostly its spawn times.
        _showTimes = IsAdded;
        NextText = NextSpawnText(boss, services.Clock.UtcNow);
        Alerts = new AlertRowsViewModel(services, boss);
        var spec = boss.Scheduled ?? new ScheduledSpec();
        _startDateText = spec.StartDate is { } start ? Parsing.FormatDate(start) : "";
        _endDateText = spec.EndDate is { } end ? Parsing.FormatDate(end) : "";
        Slots = new SlotListViewModel(spec.Slots,
            slots => services.Timers.Modify(_id, t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }),
            defaults: services.BundledSchedule(boss)?.Slots);
        TimeZoneNote = $"Server time ({TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneId).StandardName})";
        Alerts.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanFinish));
        Slots.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanFinish));
        var zone = TimeZoneInfo.FindSystemTimeZoneById(spec.TimeZoneId);
        TodayProvider = () => TimeZoneInfo.ConvertTime(services.Clock.UtcNow, zone).Date;
        Remove = new Confirmation(RemoveBoss, hideAfter: false);
    }

    partial void OnAlertsOnChanged(bool value) => _services.Timers.SetEnabled(_id, value);

    partial void OnNameChanged(string value)
    {
        if (!IsAdded) return;
        NameInvalid = !_services.Timers.RenameBoss(_id, value);
        if (!NameInvalid) Images = [_services.Art.For(Boss)];
    }

    partial void OnStartDateTextChanged(string value) => ApplyDateRange();
    partial void OnEndDateTextChanged(string value) => ApplyDateRange();

    void ApplyDateRange()
    {
        if (!IsAdded) return;
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

    [RelayCommand]
    void ToggleTimes() => ShowTimes = !ShowTimes;

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
        var old = Boss.ImageFile;
        _services.Timers.Modify(_id, t => t with { ImageFile = file });
        _services.Undo.ReleasePicture(old);
        Images = [_services.Art.For(Boss)];
        HasPicture = file is not null;
    }

    void RemoveBoss()
    {
        _host.CompletePanelEdits();
        _services.Undo.DeleteBoss(_id);
        _host.ClosePanel();
    }

    internal static string NextSpawnText(TimerDef timer, DateTimeOffset now)
    {
        var next = OccurrenceSource.Next(timer, now);
        return next is { } at
            ? $"Next · {Formats.DayTime(at)} · in {DurationFormat.Countdown(at - now)}"
            : "No upcoming spawns";
    }
}
