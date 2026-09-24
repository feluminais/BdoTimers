using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

public enum SetupPage { Loading, Welcome, Options, Progress, Done, Error, Maintenance }

internal sealed class SetupViewModel : INotifyPropertyChanged
{
    readonly SetupFlow _flow;
    LaunchAction _running;
    SetupPage _page = SetupPage.Loading;
    string _installRoot;
    int _progress;
    string _progressTitle = "";
    string _doneTitle = "";
    string _doneText = "";
    string _errorText = "";
    bool _launchApp = true;

    public SetupViewModel(SetupFlow flow)
    {
        _flow = flow;
        _installRoot = flow.InitialInstallRoot.Trim().Trim('"');
        flow.Detected += status => OnUi(() =>
        {
            if (status < 0) ShowError(status, null);
            else Page = flow.IsInstalled ? SetupPage.Maintenance : SetupPage.Welcome;
        });
        flow.ProgressChanged += percent => OnUi(() => Progress = percent);
        flow.Finished += (status, message) => OnUi(() => Finish(status, message));

        InstallCommand = new Command(() => Start(LaunchAction.Install), () => IsInstallRootValid);
        OptionsCommand = new Command(() => Page = SetupPage.Options);
        BackCommand = new Command(() => Page = SetupPage.Welcome);
        BrowseCommand = new Command(Browse);
        RepairCommand = new Command(() => Start(LaunchAction.Repair));
        UninstallCommand = new Command(() => Start(LaunchAction.Uninstall));
        CancelCommand = new Command(flow.Cancel);
        FinishCommand = new Command(FinishAndClose);
        CloseCommand = new Command(() => Application.Current.MainWindow?.Close());
        OpenLogCommand = new Command(() => { if (File.Exists(flow.LogPath)) Process.Start(flow.LogPath); });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ExitCode { get; private set; }

    public IntPtr WindowHandle { set => _flow.WindowHandle = value; }
    public string Version => "Version " + _flow.Version;

    public ICommand InstallCommand { get; }
    public ICommand OptionsCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand OpenLogCommand { get; }

    public SetupPage Page { get => _page; private set => Set(ref _page, value); }
    public bool IsBusy => Page == SetupPage.Progress;

    public string InstallRoot
    {
        get => _installRoot;
        set
        {
            if (!Set(ref _installRoot, value)) return;
            OnPropertyChanged(nameof(IsInstallRootValid));
            OnPropertyChanged(nameof(InstallFolderText));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>A full local or network path; relative paths and illegal characters are rejected up front.</summary>
    public bool IsInstallRootValid
    {
        get
        {
            var root = InstallRoot.Trim();
            // GetFullPath throws for illegal characters such as a stray quote.
            try { return Path.IsPathRooted(root) && Path.GetFullPath(root).Length > 0; }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        }
    }

    /// <summary>The app always gets its own BdoTimers folder inside the chosen one.</summary>
    public string InstallFolderText => IsInstallRootValid
        ? "Installs to " + Path.Combine(InstallRoot.Trim(), "BdoTimers")
        : @"Enter a full folder path, like D:\Games";

    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string ProgressTitle { get => _progressTitle; private set => Set(ref _progressTitle, value); }
    public string DoneTitle { get => _doneTitle; private set => Set(ref _doneTitle, value); }
    public string DoneText { get => _doneText; private set => Set(ref _doneText, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public bool LaunchApp { get => _launchApp; set => Set(ref _launchApp, value); }
    public bool CanLaunch => _running == LaunchAction.Install;

    void Start(LaunchAction action)
    {
        _running = action;
        Progress = 0;
        ProgressTitle = action switch
        {
            LaunchAction.Uninstall => "Removing",
            LaunchAction.Repair => "Repairing",
            _ => "Installing",
        };
        Page = SetupPage.Progress;
        OnPropertyChanged(nameof(CanLaunch));
        _flow.Start(action, action == LaunchAction.Install ? InstallRoot.Trim() : null);
    }

    void Finish(int status, string? message)
    {
        ExitCode = status;
        if (status < 0)
        {
            ShowError(status, message);
            return;
        }
        (DoneTitle, DoneText) = _running switch
        {
            LaunchAction.Uninstall => ("Removed", "BDO Timers is gone. Your timers and settings stay in %AppData%\\BdoTimers."),
            LaunchAction.Repair => ("Repaired", "BDO Timers is back in working order."),
            _ => ("Ready", "BDO Timers starts with Windows and lives in the tray."),
        };
        Page = SetupPage.Done;
    }

    void ShowError(int status, string? message)
    {
        ExitCode = status;
        ErrorText = SetupFlow.IsCancelled(status)
            ? "Setup was cancelled. Nothing was changed."
            : (message ?? "Something went wrong.") + $" (error 0x{status:X8})";
        Page = SetupPage.Error;
    }

    void Browse()
    {
        var owner = Application.Current.MainWindow;
        if (FolderPicker.Pick(owner, InstallRoot) is { } folder) InstallRoot = folder;
    }

    void FinishAndClose()
    {
        if (CanLaunch && LaunchApp)
        {
            var exe = Path.Combine(InstallRoot.Trim(), "BdoTimers", "BdoTimers.exe");
            if (File.Exists(exe)) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        Application.Current.MainWindow?.Close();
    }

    static void OnUi(Action action) => Application.Current.Dispatcher.BeginInvoke(action);

    bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        if (name == nameof(Page)) OnPropertyChanged(nameof(IsBusy));
        return true;
    }

    void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    sealed class Command(Action execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute();
    }
}
