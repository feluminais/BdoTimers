using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Updates;
using CheckStatus = BdoTimers.Core.Updates.UpdateStatus;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels.Panels;

public sealed partial class SettingsPanelViewModel : ObservableObject, IPanel
{
    readonly AppServices _services;
    bool _syncingRegion;
    bool _closed;
    PreparedRestore? _preparedRestore;

    [ObservableProperty] private Choice _region;
    [ObservableProperty] private string? _regionError;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(AlertSound))] private IReadOnlyList<Choice> _alertSounds = [];
    [ObservableProperty] private string? _soundError;
    [ObservableProperty] private bool _hasDeletedTodoDefaults;
    [ObservableProperty] private bool _needsTimetableReview;
    [ObservableProperty] private bool _reviewingTimetable;
    [ObservableProperty] private bool _confirmingRestore;
    [ObservableProperty] private string? _restoreDescription;
    [ObservableProperty] private string? _dataStatus;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportBackupCommand), nameof(ChooseRestoreCommand), nameof(ConfirmRestoreCommand))]
    private bool _dataBusy;
    [ObservableProperty] private string? _updateStatus;
    [ObservableProperty] private string? _availableVersion;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _updateChecking;
    Uri? _releasePage;

    public IReadOnlyList<Choice> OnOff => Choice.OnOff;
    public IReadOnlyList<Choice> Regions { get; } = BossRegions.All.Select(r => new Choice(r.Label, r.Id)).ToList();
    public IReadOnlyList<Choice> Voices { get; } = KokoroEngine.Voices.Select(v => new Choice(v.Label, v.Id)).ToList();
    public ObservableCollection<UserSoundRow> UserSounds { get; } = [];
    public LeadChipsViewModel DefaultLeads { get; }
    public TodoScheduleEditorViewModel DailyTodoReset { get; }
    public TodoScheduleEditorViewModel WeeklyTodoReset { get; }
    public Confirmation AlertReset { get; }
    public Confirmation TimetableReset { get; }
    public string Version { get; } = ProductVersion.Number;
    public string TimetableVerified => $"{_services.Region.ShortLabel} · Verified {_services.Seed.VerifiedOn ?? "unknown"}";
    public string? TimetableSource => string.Join('\n', new[] { _services.Seed.Source }
        .Concat(_services.Seed.SourceUrls ?? []).Where(s => s is not null));
    public string TimetableZone => $"Times in {_services.Seed.TimeZoneId}";
    public string ResetTimetableLabel => $"Reset spawn times to the {_services.Region.ShortLabel} timetable";
    public string ResetTimetableConfirmation => $"{ResetTimetableLabel}?";
    public string ResetAlertLabel => $"Reset {_services.Region.ShortLabel} boss alert settings";
    public string ResetAlertConfirmation => $"{ResetAlertLabel}?";
    public ObservableCollection<TimetableChangeRow> TimetableChanges { get; } = [];
    public bool CanUseData => !DataBusy;

    public SettingsPanelViewModel(AppServices services)
    {
        _services = services;
        services.Updates.Changed += UpdateCheckChanged;
        RefreshUpdateCheck();
        var s = services.Settings.Current;
        _region = Regions.Single(r => (string)r.Value! == services.Timers.Current.SelectedBossRegion);
        ReloadSounds();
        // So Test voice speaks without first waiting for the model.
        services.Tts.Warm(s.TtsVoice);
        DefaultLeads = new LeadChipsViewModel(s.DefaultLeadTimesMinutes,
            leads => services.Settings.Update(x => x with { DefaultLeadTimesMinutes = leads }));
        DailyTodoReset = new TodoScheduleEditorViewModel(TodoCadence.Daily, s.DailyTodoReset,
            schedule => services.SetTodoReset(TodoCadence.Daily, schedule));
        WeeklyTodoReset = new TodoScheduleEditorViewModel(TodoCadence.Weekly, s.WeeklyTodoReset,
            schedule => services.SetTodoReset(TodoCadence.Weekly, schedule));
        AlertReset = new Confirmation(services.Timers.ResetBossAlerts);
        TimetableReset = new Confirmation(() => { services.ResetBossTimetable(); RefreshTimetable(); });
        HasDeletedTodoDefaults = services.Todos.Current.Lists.Any(list => list.IsBuiltIn && list.Deleted)
            || services.Todos.Current.Lists.All(list => list.Id != TodoSeed.DailyId)
            || services.Todos.Current.Lists.All(list => list.Id != TodoSeed.WeeklyId);
        RefreshTimetable();
        services.Settings.Changed += OnSettingsChanged;
    }

    AppSettings Current => _services.Settings.Current;

    public Choice Autostart
    {
        get => Choice.For(Current.Autostart);
        set
        {
            _services.Settings.Update(s => s with { Autostart = value.IsOn });
            BdoTimers.App.Autostart.Apply(value.IsOn);
        }
    }

    partial void OnRegionChanged(Choice value)
    {
        if (_syncingRegion) return;
        try
        {
            _services.SelectBossRegion((string)value.Value!);
            RegionError = null;
            ReviewingTimetable = false;
            TimetableReset.IsAsking = AlertReset.IsAsking = false;
            OnPropertyChanged(nameof(TimetableVerified));
            OnPropertyChanged(nameof(TimetableSource));
            OnPropertyChanged(nameof(TimetableZone));
            OnPropertyChanged(nameof(ResetTimetableLabel));
            OnPropertyChanged(nameof(ResetTimetableConfirmation));
            OnPropertyChanged(nameof(ResetAlertLabel));
            OnPropertyChanged(nameof(ResetAlertConfirmation));
            RefreshTimetable();
        }
        catch (StateSaveException ex)
        {
            Log.Error("Couldn't save boss region", ex);
            RegionError = "Couldn't save region";
            _syncingRegion = true;
            Region = Regions.Single(r => (string)r.Value! == _services.Timers.Current.SelectedBossRegion);
            _syncingRegion = false;
        }
    }

    public Choice CloseToTray
    {
        get => Choice.For(Current.CloseToTray);
        set => _services.Settings.Update(s => s with { CloseToTray = value.IsOn });
    }

    /// <summary>The saved app-wide sound, or the default when it's gone.</summary>
    public Choice? AlertSound
    {
        get => AlertSounds.FirstOrDefault(c => (string)c.Value! == _services.PlayableSound(null)) ?? AlertSounds[0];
        set
        {
            if (value?.Value is string key) _services.Settings.Update(s => s with { AlertSound = key });
        }
    }

    public double Volume { get => Current.Volume; set => _services.Settings.Update(s => s with { Volume = (float)value }); }

    public Choice? Voice
    {
        get => Voices.FirstOrDefault(v => (string)v.Value! == Current.TtsVoice)
               ?? Voices.FirstOrDefault(v => (string)v.Value! == KokoroEngine.Default.Id)
               ?? Voices.FirstOrDefault();
        set
        {
            _services.Settings.Update(s => s with { TtsVoice = value?.Value as string });
            // A UK voice needs the model loaded for British English, and a US one for American.
            _services.Tts.Warm(value?.Value as string);
        }
    }

    public double SpeechRate
    {
        get => Current.TtsRate;
        set => _services.Settings.Update(s => s with { TtsRate = (int)Math.Round(value) });
    }

    void OnSettingsChanged() => OnPropertyChanged(string.Empty);

    /// <summary>Rebuilds the sound list and the "Your sounds" rows.</summary>
    void ReloadSounds()
    {
        AlertSounds = SoundChoices.ForApp(_services.Sounds);
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
        _services.Settings.Changed -= OnSettingsChanged;
        _closed = true;
        _services.Updates.Changed -= UpdateCheckChanged;
        CancelRestore();
    }

    void UpdateCheckChanged() => Application.Current.Dispatcher.BeginInvoke(() =>
    {
        if (!_closed) RefreshUpdateCheck();
    });

    void RefreshUpdateCheck()
    {
        var result = _services.Updates.Current;
        UpdateChecking = result.Status == CheckStatus.Checking;
        UpdateStatus = result.Status switch
        {
            CheckStatus.Checking => "Checking",
            CheckStatus.UpToDate => "Up to date",
            CheckStatus.UpdateAvailable => "Update available",
            CheckStatus.CouldNotCheck => "Couldn’t check",
            _ => null,
        };
        AvailableVersion = result.Release?.Number;
        _releasePage = result.Release?.Page;
    }

    bool CanCheckForUpdates() => !UpdateChecking;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    async Task CheckForUpdates()
    {
        UpdateChecking = true;
        UpdateStatus = "Checking";
        AvailableVersion = null;
        _releasePage = null;
        await Task.Run(() => _services.Updates.CheckAsync());
        if (!_closed) RefreshUpdateCheck();
    }

    [RelayCommand]
    void OpenRelease()
    {
        if (_releasePage is not { } page) return;
        try { Open(page.AbsoluteUri); }
        catch (Exception ex)
        {
            Log.Error("Couldn't open the release page", ex);
            UpdateStatus = "Couldn’t open release";
        }
    }

    [RelayCommand]
    void OpenDataFolder() => _services.OpenDataFolder();

    /// <summary>The GPL asks that the source be offered to everyone who gets the app.</summary>
    [RelayCommand]
    void OpenSource() => Open(GitHubReleaseSource.Repository);

    [RelayCommand]
    void OpenLicenses() => Open(Path.Combine(AppContext.BaseDirectory, "licenses"));

    static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
}

/// <summary>A line in Settings' "Your sounds": the sound's name with play and remove buttons.</summary>
public sealed partial class UserSoundRow(string name, string key, Action<string> play, Action<string> remove)
{
    public string Name => name;

    [RelayCommand]
    void Play() => play(key);

    [RelayCommand]
    void Remove() => remove(key);
}
