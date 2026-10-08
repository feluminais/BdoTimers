using System.Windows;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using BdoTimers.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

/// <summary>The screens of the top bar, in order.</summary>
public enum MainTab { Today, Schedule, Timers, Todo }

public sealed partial class MainViewModel : ObservableObject, IPanelHost, IDisposable
{
    readonly AppServices _services;
    bool _shown;
    Action? _pendingLeave;
    Action? _cancelLeave;
    public EditorSave Saving { get; } = new();
    [ObservableProperty] private bool _askingDiscard;

    [ObservableProperty] private object? _panel;
    /// <summary>How the open panel shows; kept while a panel leaves, so it leaves the way it came.</summary>
    [ObservableProperty] private PanelPresentation _panelPresentation;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";
    /// <summary>A newer release is available; the gear shows a dot, and Settings → About has the release.</summary>
    [ObservableProperty] private bool _hasUpdate;
    /// <summary>The window opens on Today.</summary>
    [ObservableProperty] private MainTab _selectedTab = MainTab.Today;

    public TodayViewModel Today { get; }
    public ScheduleViewModel Schedule { get; }
    public CustomViewModel Custom { get; }
    public TodoViewModel Todo { get; }
    public UndoService Undo => _services.Undo;

    public MainViewModel(AppServices services)
    {
        _services = services;
        Custom = new CustomViewModel(services, this);
        Todo = new TodoViewModel(services, this);
        Schedule = new ScheduleViewModel(new BossesViewModel(services, this), new CalendarViewModel(services, this), OpenFollowing);
        Today = new TodayViewModel(services, this, Custom, Todo, () => SelectedTab = MainTab.Timers, () => SelectedTab = MainTab.Todo,
            OpenFollowing);
        RefreshPaused(services.Clock.UtcNow);
        services.Updates.Changed += UpdatesChanged;
        RefreshUpdate();
    }

    void UpdatesChanged()
    {
        if (_services.IsQuitting) return;
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (!_services.IsQuitting) RefreshUpdate();
        });
    }

    void RefreshUpdate() => HasUpdate = _services.Updates.Current.Status == UpdateStatus.UpdateAvailable;

    public void Dispose()
    {
        _services.Updates.Changed -= UpdatesChanged;
        SetShown(false);
        _cancelLeave?.Invoke();
        ClosePanelNow();
    }

    /// <summary>
    /// The screens follow the clock only while the window can be seen; hidden to the tray or minimized, they skip the
    /// ticks and catch up when it comes back.
    /// </summary>
    public void SetShown(bool shown)
    {
        if (shown == _shown) return;
        _shown = shown;
        if (!shown)
        {
            _services.UiClock.Tick -= Tick;
            return;
        }
        _services.UiClock.Tick += Tick;
        Tick(_services.Clock.UtcNow);
    }

    void Tick(DateTimeOffset now)
    {
        Today.Refresh(now);
        Schedule.Week.Refresh(now);
        Schedule.Month.Refresh(now);
        Custom.Refresh(now);
        Todo.UpdateResetLabels();
        RefreshPaused(now);
    }

    partial void OnPanelChanged(object? value)
    {
        if (value is not null) PanelPresentation = (value as IPanel)?.Presentation ?? PanelPresentation.Drawer;
    }

    public void OpenPanel(object panel)
    {
        RequestLeave(() => { ClosePanelNow(); Panel = panel; }, () => (panel as IPanel)?.OnClosed());
    }

    public bool IsOpen(object panel) => ReferenceEquals(Panel, panel);

    public event Action? CompletingPanelEdits;
    public void CompletePanelEdits() => CompletingPanelEdits?.Invoke();

    [RelayCommand]
    public async Task FinishPanel()
    {
        if (Saving.IsBusy || Panel is IDraftPanel { IsBusy: true } || AskingDiscard) return;
        CompletePanelEdits();
        if (Panel is IPanel { CanFinish: false }) return;
        var panel = Panel;
        if (panel is IDraftPanel draft && !await Saving.RunAsync(draft.SaveAsync)) return;
        if (ReferenceEquals(panel, Panel)) ClosePanelNow();
    }

    [RelayCommand]
    public void ClosePanel()
    {
        RequestLeave(ClosePanelNow);
    }

    public bool RequestLeave(Action leave, Action? cancel = null, bool runImmediately = true)
    {
        if (AskingDiscard) { cancel?.Invoke(); return false; }
        if (Saving.IsBusy || Panel is IDraftPanel { IsBusy: true }) { cancel?.Invoke(); return false; }
        CompletePanelEdits();
        if (Panel is IDraftPanel draft && (draft.HasChanges || !draft.CanFinish))
        {
            _cancelLeave?.Invoke();
            _pendingLeave = leave;
            _cancelLeave = cancel;
            AskingDiscard = true;
            return false;
        }
        if (runImmediately) leave();
        return true;
    }

    [RelayCommand]
    void KeepEditing()
    {
        AskingDiscard = false;
        _pendingLeave = null;
        _cancelLeave?.Invoke();
        _cancelLeave = null;
    }

    [RelayCommand]
    void DiscardChanges()
    {
        var leave = _pendingLeave;
        _pendingLeave = _cancelLeave = null;
        AskingDiscard = false;
        ClosePanelNow();
        leave?.Invoke();
    }

    void ClosePanelNow()
    {
        if (Panel is null) return;
        (Panel as IPanel)?.OnClosed();
        Panel = null;
        Saving.Error = null;
    }

    [RelayCommand]
    void OpenSettings() => OpenPanel(new SettingsPanelViewModel(_services));

    /// <summary>Which bosses to follow, each with its alerts, and a way to add one.</summary>
    [RelayCommand]
    void OpenFollowing() => OpenPanel(new FollowingPanelViewModel(_services, this));

    [RelayCommand]
    void ShowToday() => SelectedTab = MainTab.Today;

    /// <summary>Settings on the Overlay category: the open Settings switches to it, so its unsaved edits stay.</summary>
    [RelayCommand]
    public void OpenOverlaySettings()
    {
        if (Panel is SettingsPanelViewModel settings) settings.ShowCategory("Overlay");
        else OpenPanel(new SettingsPanelViewModel(_services) { OpenAt = "Overlay" });
    }

    [RelayCommand]
    void PauseHour()
    {
        _services.PauseAlerts(TimeSpan.FromHours(1));
        RefreshPaused(_services.Clock.UtcNow);
    }

    [RelayCommand]
    void PauseUntilResumed()
    {
        _services.PauseAlerts(null);
        RefreshPaused(_services.Clock.UtcNow);
    }

    [RelayCommand]
    void Resume()
    {
        _services.ResumeAlerts();
        RefreshPaused(_services.Clock.UtcNow);
    }

    void RefreshPaused(DateTimeOffset now)
    {
        var s = _services.Settings.Current;
        IsPaused = AlertPause.IsPaused(s, now);
        PausedText = !IsPaused ? ""
            : AlertPause.Remaining(s, now) is { } left ? $"Alerts paused · {DurationFormat.Countdown(left)}"
            : "Alerts paused";
    }
}
