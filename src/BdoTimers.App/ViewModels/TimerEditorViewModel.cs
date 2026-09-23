using System.Collections.ObjectModel;
using System.IO;
using BdoTimers.Core.Model;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BdoTimers.App.ViewModels;

public sealed partial class SlotRow : ObservableObject
{
    public static IReadOnlyList<DayOfWeek> Days { get; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
         DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    [ObservableProperty] private DayOfWeek _day = DayOfWeek.Monday;
    [ObservableProperty] private string _timeText = "20:00";
}

public sealed partial class TimerEditorViewModel : ObservableObject
{
    readonly TimerDef _original;
    readonly TimerStore _store;

    public event Action? Saved;

    public bool IsScheduled => _original.Kind == TimerKind.Scheduled;
    public bool IsCountdown => _original.Kind == TimerKind.Countdown;
    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();
    public ObservableCollection<SlotRow> Slots { get; } = [];

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _timeZoneId;
    [ObservableProperty] private string _durationMinutesText;
    [ObservableProperty] private bool _autoRepeat;
    [ObservableProperty] private string _leadTimesText;
    [ObservableProperty] private bool _soundEnabled;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(SoundDisplay))] private string? _soundFilePath;
    [ObservableProperty] private bool _toastEnabled;
    [ObservableProperty] private bool _ttsEnabled;
    [ObservableProperty] private string _ttsTemplate;
    [ObservableProperty] private bool _overlayEnabled;
    [ObservableProperty] private string _overlayMinutesText;
    [ObservableProperty] private string _error = "";

    public string SoundDisplay => SoundFilePath is null ? "Built-in chime" : Path.GetFileName(SoundFilePath);

    public TimerEditorViewModel(TimerDef timer, TimerStore store)
    {
        _original = timer;
        _store = store;
        _name = timer.Name;
        _timeZoneId = ToWindowsId(timer.Scheduled?.TimeZoneId ?? TimeZoneInfo.Local.Id);
        foreach (var slot in timer.Scheduled?.Slots ?? [])
            Slots.Add(new SlotRow { Day = slot.Day, TimeText = Parsing.FormatTime(slot.Time) });
        _durationMinutesText = ((int)(timer.Countdown?.Duration.TotalMinutes ?? 60)).ToString();
        _autoRepeat = timer.Countdown?.AutoRepeat ?? false;
        var a = timer.Alerts;
        _leadTimesText = Parsing.FormatLeadTimes(a.LeadTimesMinutes);
        _soundEnabled = a.Sound.Enabled;
        _soundFilePath = a.Sound.FilePath;
        _toastEnabled = a.Toast.Enabled;
        _ttsEnabled = a.Tts.Enabled;
        _ttsTemplate = a.Tts.Template;
        _overlayEnabled = a.Overlay.Enabled;
        _overlayMinutesText = a.Overlay.ShowMinutesBefore.ToString();
    }

    static string ToWindowsId(string id) =>
        TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) ? windowsId : id;

    [RelayCommand]
    void AddSlot() => Slots.Add(new SlotRow());

    [RelayCommand]
    void RemoveSlot(SlotRow? slot)
    {
        if (slot is not null) Slots.Remove(slot);
    }

    [RelayCommand]
    void BrowseSound()
    {
        var dialog = new OpenFileDialog { Filter = "Audio files|*.wav;*.mp3|All files|*.*" };
        if (dialog.ShowDialog() == true) SoundFilePath = dialog.FileName;
    }

    [RelayCommand]
    void ClearSound() => SoundFilePath = null;

    [RelayCommand]
    void Save()
    {
        if (string.IsNullOrWhiteSpace(Name)) { Error = "Give the timer a name."; return; }
        if (!Parsing.TryParseLeadTimes(LeadTimesText, out var leads, out var leadError)) { Error = leadError!; return; }
        if (!Parsing.TryParseMinutes(OverlayMinutesText, 1, 180, out var overlayMinutes))
        {
            Error = "Overlay minutes must be between 1 and 180.";
            return;
        }

        var timer = _original with { Name = Name.Trim() };
        if (IsScheduled)
        {
            if (Slots.Count == 0) { Error = "Add at least one weekly time."; return; }
            var slots = new List<Slot>();
            foreach (var row in Slots)
            {
                if (!Parsing.TryParseTime(row.TimeText, out var time))
                {
                    Error = $"\"{row.TimeText}\" isn't a valid time. Use HH:mm, e.g. 21:15.";
                    return;
                }
                slots.Add(new Slot(row.Day, time));
            }
            timer = timer with { Scheduled = new ScheduledSpec { TimeZoneId = TimeZoneId, Slots = slots } };
        }
        else
        {
            if (!Parsing.TryParseMinutes(DurationMinutesText, 1, 1440, out var minutes))
            {
                Error = "Duration must be between 1 and 1440 minutes.";
                return;
            }
            var countdown = _original.Countdown ?? new CountdownSpec();
            timer = timer with
            {
                Countdown = countdown with { Duration = TimeSpan.FromMinutes(minutes), AutoRepeat = AutoRepeat },
            };
        }

        var a = _original.Alerts;
        timer = timer with
        {
            Alerts = a with
            {
                LeadTimesMinutes = leads,
                Sound = a.Sound with { Enabled = SoundEnabled, FilePath = SoundFilePath },
                Toast = a.Toast with { Enabled = ToastEnabled },
                Tts = a.Tts with { Enabled = TtsEnabled, Template = TtsTemplate },
                Overlay = a.Overlay with { Enabled = OverlayEnabled, ShowMinutesBefore = overlayMinutes },
            },
        };
        _store.Upsert(timer);
        Saved?.Invoke();
    }
}
