using System.IO;
using BdoTimers.App.Controls;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class CustomPanelViewModel : ObservableObject
{
    readonly AppServices _services;
    readonly IPanelHost _host;
    readonly Guid _id;

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _nameInvalid;
    [ObservableProperty] private IReadOnlyList<ArtPicture> _images;
    [ObservableProperty] private bool _hasPicture;
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private bool _durationInvalid;
    [ObservableProperty] private string? _timeZoneId;
    [ObservableProperty] private Choice _alertsOn;
    [ObservableProperty] private bool _confirmingDelete;

    public bool IsCountdown { get; }
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
        IsStopwatch = timer.Kind == TimerKind.Stopwatch;
        CanDelete = timer.Preset is null;
        if (timer.Countdown is { } c) _durationText = Parsing.FormatDuration(c.Duration);
        if (timer.Scheduled is { } spec)
        {
            _timeZoneId = TimeZoneInfo.TryConvertIanaIdToWindowsId(spec.TimeZoneId, out var windowsId) ? windowsId : spec.TimeZoneId;
            Slots = new SlotListViewModel(spec.Slots,
                slots => Modify(t => t with { Scheduled = (t.Scheduled ?? spec) with { Slots = slots } }));
        }
        Alerts = new AlertRowsViewModel(services, timer);
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
