using System.Windows;
using BdoTimers.App.ViewModels.Panels;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Text;
using BdoTimers.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BdoTimers.App.ViewModels;

public enum Section { Bosses, Custom, Todo }

public sealed partial class MainViewModel : ObservableObject, IPanelHost, IDisposable
{
    readonly AppServices _services;
    bool _shown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBossesSection), nameof(IsCustomSection), nameof(IsTodoSection))]
    private Section _section;

    [ObservableProperty] private object? _panel;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private string _pausedText = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenUpdateDetailsCommand))]
    private bool _hasUpdate;

    public BossesViewModel Bosses { get; }
    public CustomViewModel Custom { get; }
    public TodoViewModel Todo { get; }

    public bool IsBossesSection
    {
        get => Section == Section.Bosses;
        set { if (value) Section = Section.Bosses; }
    }

    public bool IsCustomSection
    {
        get => Section == Section.Custom;
        set { if (value) Section = Section.Custom; }
    }

    public bool IsTodoSection
    {
        get => Section == Section.Todo;
        set { if (value) Section = Section.Todo; }
    }

    public MainViewModel(AppServices services)
    {
        _services = services;
        Bosses = new BossesViewModel(services, this);
        Custom = new CustomViewModel(services, this);
        Todo = new TodoViewModel(services, this);
        RefreshPaused(DateTimeOffset.UtcNow);
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

    [RelayCommand(CanExecute = nameof(HasUpdate))]
    void OpenUpdateDetails()
    {
        if (_services.Updates.Current.Release is { } release)
            OpenPanel(new UpdatePanelViewModel(release, this));
    }

    public void Dispose()
    {
        _services.Updates.Changed -= UpdatesChanged;
        SetShown(false);
        ClosePanel();
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
        Tick(DateTimeOffset.UtcNow);
    }

    void Tick(DateTimeOffset now)
    {
        Bosses.Refresh(now);
        Custom.Refresh(now);
        Todo.UpdateResetLabels();
        RefreshPaused(now);
    }

    public void OpenPanel(object panel)
    {
        ClosePanel();
        Panel = panel;
    }

    public bool IsOpen(object panel) => ReferenceEquals(Panel, panel);

    [RelayCommand]
    public void ClosePanel()
    {
        (Panel as IPanel)?.OnClosed();
        Panel = null;
    }

    [RelayCommand]
    void OpenSettings() => OpenPanel(new SettingsPanelViewModel(_services));

    [RelayCommand]
    public void OpenOverlaySettings()
    {
        ClosePanel();
        Panel = new OverlayPanelViewModel(_services);
    }

    [RelayCommand]
    void Resume() => _services.ResumeAlerts();

    void RefreshPaused(DateTimeOffset now)
    {
        var s = _services.Settings.Current;
        IsPaused = AlertPause.IsPaused(s, now);
        PausedText = !IsPaused ? ""
            : AlertPause.Remaining(s, now) is { } left ? $"Alerts paused · {DurationFormat.Countdown(left)}"
            : "Alerts paused";
    }
}
