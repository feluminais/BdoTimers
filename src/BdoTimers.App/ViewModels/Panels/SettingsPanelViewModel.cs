using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class SettingsPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    bool _syncingSound;
    bool _closed;
    PreparedRestore? _preparedRestore;

    [ObservableProperty] private Choice _autostart;
    [ObservableProperty] private Choice _closeToTray;
    [ObservableProperty] private IReadOnlyList<Choice> _alertSounds = [];
    [ObservableProperty] private Choice? _alertSound;
    [ObservableProperty] private string? _soundError;
    [ObservableProperty] private double _volume;
    [ObservableProperty] private Choice? _voice;
    [ObservableProperty] private double _speechRate;
    [ObservableProperty] private bool _confirmingReset;
    [ObservableProperty] private bool _confirmingAlertReset;
    [ObservableProperty] private bool _hasDeletedTodoDefaults;
    [ObservableProperty] private bool _needsTimetableReview;
    [ObservableProperty] private bool _reviewingTimetable;
    [ObservableProperty] private bool _confirmingRestore;
    [ObservableProperty] private string? _restoreDescription;
    [ObservableProperty] private string? _dataStatus;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportBackupCommand), nameof(ChooseRestoreCommand), nameof(ConfirmRestoreCommand))]
    private bool _dataBusy;

    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> Voices { get; }
    public ObservableCollection<UserSoundRow> UserSounds { get; } = [];
    public LeadChipsViewModel DefaultLeads { get; }
    public TodoScheduleEditorViewModel DailyTodoReset { get; }
    public TodoScheduleEditorViewModel WeeklyTodoReset { get; }
    public string Version { get; } = AppVersion();
    public string TimetableVerified => $"EU · Verified {_services.Seed.VerifiedOn ?? "unknown"}";
    public string? TimetableSource => _services.Seed.Source;
    public string TimetableZone => $"Times in {_services.Seed.TimeZoneId}";
    public ObservableCollection<TimetableChangeRow> TimetableChanges { get; } = [];
    public bool CanUseData => !DataBusy;

    public SettingsPanelViewModel(AppServices services)
    {
        _services = services;
        var s = services.Settings.Current;
        _autostart = Choice.For(s.Autostart);
        _closeToTray = Choice.For(s.CloseToTray);
        _volume = s.Volume;
        ReloadSounds();
        Voices = services.Tts.Voices().Select(v => new Choice(v.Label, v.Id)).ToList();
        _voice = Voices.FirstOrDefault(v => (string)v.Value! == s.TtsVoice)
                 ?? Voices.FirstOrDefault(v => (string)v.Value! == services.Tts.DefaultVoiceId)
                 ?? Voices.FirstOrDefault();
        // So Test voice speaks without first waiting for the model.
        services.Tts.Warm(s.TtsVoice);
        _speechRate = s.TtsRate;
        DefaultLeads = new LeadChipsViewModel(s.DefaultLeadTimesMinutes,
            leads => services.Settings.Update(x => x with { DefaultLeadTimesMinutes = leads }));
        DailyTodoReset = new TodoScheduleEditorViewModel(s.DailyTodoReset,
            schedule => services.SetTodoReset(TodoCadence.Daily, schedule));
        WeeklyTodoReset = new TodoScheduleEditorViewModel(s.WeeklyTodoReset,
            schedule => services.SetTodoReset(TodoCadence.Weekly, schedule));
        HasDeletedTodoDefaults = services.Todos.Current.Lists.Any(list => list.IsBuiltIn && list.Deleted)
            || services.Todos.Current.Lists.All(list => list.Id != TodoSeed.DailyId)
            || services.Todos.Current.Lists.All(list => list.Id != TodoSeed.WeeklyId);
        RefreshTimetable();
    }

    partial void OnAutostartChanged(Choice value)
    {
        _services.Settings.Update(s => s with { Autostart = value.IsOn });
        BdoTimers.App.Autostart.Apply(value.IsOn);
    }

    partial void OnCloseToTrayChanged(Choice value) =>
        _services.Settings.Update(s => s with { CloseToTray = value.IsOn });

    partial void OnAlertSoundChanged(Choice? value)
    {
        if (!_syncingSound && value?.Value is string key) _services.Settings.Update(s => s with { AlertSound = key });
    }

    /// <summary>Rebuilds the sound list and the "Your sounds" rows; shows the saved app-wide sound.</summary>
    void ReloadSounds()
    {
        _syncingSound = true;
        AlertSounds = SoundChoices.ForApp(_services.Sounds);
        var saved = _services.PlayableSound(null);
        AlertSound = AlertSounds.FirstOrDefault(c => (string)c.Value! == saved) ?? AlertSounds[0];
        _syncingSound = false;
        UserSounds.Clear();
        foreach (var key in _services.Sounds.Keys())
            UserSounds.Add(new UserSoundRow(SoundChoices.Label(key), key, _services.PlaySound, RemoveSound));
    }

    [RelayCommand]
    void AddSound()
    {
        var (key, error) = _services.AddSound();
        SoundError = error;
        if (key is not null) ReloadSounds();
    }

    void RemoveSound(string key)
    {
        SoundError = _services.RemoveSound(key);
        ReloadSounds();
    }

    partial void OnVolumeChanged(double value) => _services.Settings.Update(s => s with { Volume = (float)value });

    partial void OnVoiceChanged(Choice? value)
    {
        _services.Settings.Update(s => s with { TtsVoice = value?.Value as string });
        // A UK voice needs the model loaded for British English, and a US one for American.
        _services.Tts.Warm(value?.Value as string);
    }

    partial void OnSpeechRateChanged(double value) =>
        _services.Settings.Update(s => s with { TtsRate = (int)Math.Round(value) });

    [RelayCommand]
    void PreviewSound() => _services.PlaySound(null);

    [RelayCommand]
    void TestAlert() => _services.SendTestAlert();

    /// <summary>Two respelled boss names, so the test also shows pronunciation.</summary>
    [RelayCommand]
    void TestVoice() => _services.Speak("Kzarka and Uturi in 5 minutes");


    [RelayCommand]
    void OpenNotificationSettings() => ToastChannel.OpenWindowsNotificationSettings();

    [RelayCommand]
    void AskReset() => ConfirmingReset = true;

    [RelayCommand]
    void CancelReset() => ConfirmingReset = false;

    [RelayCommand]
    void ConfirmReset()
    {
        _services.ResetBossTimetable();
        ConfirmingReset = false;
        RefreshTimetable();
    }

    [RelayCommand]
    void AskAlertReset() => ConfirmingAlertReset = true;

    [RelayCommand]
    void CancelAlertReset() => ConfirmingAlertReset = false;

    [RelayCommand]
    void ConfirmAlertReset()
    {
        _services.Timers.ResetBossAlerts();
        ConfirmingAlertReset = false;
    }

    [RelayCommand]
    void RestoreTodoDefaults()
    {
        _services.Todos.RestoreDefaults(_services.Settings.Current);
        HasDeletedTodoDefaults = false;
    }

    void RefreshTimetable()
    {
        var review = TimetableUpdates.Review(_services.Timers.Current, _services.Seed);
        NeedsTimetableReview = review.NeedsReview;
        TimetableChanges.Clear();
        foreach (var change in review.Changes) TimetableChanges.Add(new(change));
    }

    [RelayCommand]
    void ReviewTimetable()
    {
        RefreshTimetable();
        ReviewingTimetable = true;
    }

    [RelayCommand]
    void CancelTimetable() => ReviewingTimetable = false;

    [RelayCommand]
    void ApplyTimetable()
    {
        _services.ApplyTimetable(TimetableChanges.Where(c => c.Apply.IsOn).Select(c => c.Change.Name));
        ReviewingTimetable = false;
        RefreshTimetable();
    }

    [RelayCommand]
    void KeepTimetable()
    {
        _services.ApplyTimetable([]);
        ReviewingTimetable = false;
        RefreshTimetable();
    }

    [RelayCommand(CanExecute = nameof(CanUseData))]
    async Task ExportBackup()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export backup", Filter = "BDO Timers backup (*.zip)|*.zip", DefaultExt = ".zip",
            FileName = $"BdoTimers-backup-{_services.Clock.UtcNow.ToLocalTime():yyyy-MM-dd}.zip",
        };
        if (dialog.ShowDialog() != true) return;
        DataBusy = true;
        DataStatus = "Saving backup…";
        try
        {
            await Task.Run(() => _services.ExportBackup(dialog.FileName, Version));
            DataStatus = "Backup saved";
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't export backup", ex);
            DataStatus = $"Couldn't save backup: {ex.Message}";
        }
        finally { DataBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUseData))]
    async Task ChooseRestore()
    {
        var dialog = new OpenFileDialog { Title = "Restore backup", Filter = "BDO Timers backup (*.zip)|*.zip" };
        if (dialog.ShowDialog() != true) return;
        CancelRestore();
        DataBusy = true;
        DataStatus = "Checking backup…";
        try
        {
            var prepared = await Task.Run(() => _services.PrepareRestore(dialog.FileName));
            if (_closed) { BackupArchive.Discard(prepared); return; }
            _preparedRestore = prepared;
            RestoreDescription = $"{Path.GetFileName(dialog.FileName)} · {prepared.Manifest.CreatedAtUtc.ToLocalTime():g}";
            ConfirmingRestore = true;
            DataStatus = null;
        }
        catch (Exception ex)
        {
            Log.Error("Couldn't prepare backup restore", ex);
            DataStatus = $"Couldn't restore backup: {ex.Message}";
        }
        finally { DataBusy = false; }
    }

    [RelayCommand]
    void CancelRestore()
    {
        if (_preparedRestore is { } prepared)
        {
            try { BackupArchive.Discard(prepared); }
            catch (Exception ex) { Log.Error("Couldn't remove prepared restore", ex); }
        }
        _preparedRestore = null;
        ConfirmingRestore = false;
    }

    [RelayCommand(CanExecute = nameof(CanUseData))]
    void ConfirmRestore()
    {
        if (_preparedRestore is not { } prepared) return;
        _preparedRestore = null;
        ConfirmingRestore = false;
        _services.RestartForRestore(prepared);
    }

    public void OnClosed()
    {
        _closed = true;
        CancelRestore();
    }

    [RelayCommand]
    void OpenDataFolder() => _services.OpenDataFolder();

    /// <summary>The GPL asks that the source be offered to everyone who gets the app.</summary>
    [RelayCommand]
    void OpenSource() => Open("https://github.com/feluminais/BdoTimers");

    [RelayCommand]
    void OpenLicenses() => Open(Path.Combine(AppContext.BaseDirectory, "licenses"));

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });

    static string AppVersion()
    {
        var info = typeof(SettingsPanelViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info?.Split('+')[0] ?? "";
    }
}

/// <summary>A line in Settings' "Your sounds": the sound's name with play and remove buttons.</summary>
public sealed class UserSoundRow(string name, string key, Action<string> play, Action<string> remove)
{
    public string Name => name;
    public IRelayCommand PlayCommand { get; } = new RelayCommand(() => play(key));
    public IRelayCommand RemoveCommand { get; } = new RelayCommand(() => remove(key));
}
