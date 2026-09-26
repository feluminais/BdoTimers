using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

public enum SetupPage { Loading, Welcome, Options, Progress, Done, Error, Maintenance }

/// <summary>Whether the user can create the app's folder in a place; the install is per-user, so it never elevates.</summary>
public enum FolderAccess { Writable, NeedsAdmin, Missing }

internal sealed class SetupViewModel : INotifyPropertyChanged
{
    readonly SetupFlow _flow;
    readonly string? _installedRoot = SetupFlow.InstalledRoot;
    LaunchAction _running;
    /// <summary>Set while moving: the app is removed (bundle kept), then installed into this folder.</summary>
    string? _moveTo;
    bool _moveInstalling;
    SetupPage _page = SetupPage.Loading;
    string _installRoot;
    FolderAccess _access;
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
        _access = CheckAccess(_installRoot);
        flow.Detected += status => OnUi(() =>
        {
            if (status < 0) ShowError(status, null);
            else if (_moveTo is not null) InstallMoved();
            else Page = flow.IsInstalled ? SetupPage.Maintenance : SetupPage.Welcome;
        });
        // A move runs two applies; each fills half of the bar.
        flow.ProgressChanged += percent => OnUi(() =>
            Progress = _moveTo is null ? percent : _moveInstalling ? 50 + percent / 2 : percent / 2);
        flow.Finished += (status, message) => OnUi(() => Finish(status, message));

        InstallCommand = new Command(Confirm, () => IsInstallRootValid && _access == FolderAccess.Writable
                                                    && (!IsMoving || !IsSameFolder(InstallRoot, _installedRoot)));
        NextCommand = new Command(() => Page = SetupPage.Options);
        MoveCommand = new Command(() => { IsMoving = true; Page = SetupPage.Options; });
        OpenFolderCommand = new Command(OpenFolder);
        BackCommand = new Command(() =>
        {
            Page = IsMoving ? SetupPage.Maintenance : SetupPage.Welcome;
            IsMoving = false;
        });
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

    public ICommand InstallCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand OpenFolderCommand { get; }
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
            _access = CheckAccess(value);
            OnPropertyChanged(nameof(IsInstallRootValid));
            OnPropertyChanged(nameof(LocationProblem));
            OnPropertyChanged(nameof(FolderSuffix));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>The folder setup creates inside the chosen one, shown right after the typed path.</summary>
    public string FolderSuffix => InstallRoot.TrimEnd().EndsWith(@"\") || InstallRoot.TrimEnd().EndsWith("/") ? "BdoTimers" : @"\BdoTimers";

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

    /// <summary>Why the folder can't be used; empty when it can.</summary>
    public string LocationProblem =>
        !IsInstallRootValid ? "Not a full folder path"
        : _access == FolderAccess.Missing ? "That drive isn't available"
        : _access == FolderAccess.NeedsAdmin ? "Needs admin rights"
        : IsMoving && IsSameFolder(InstallRoot, _installedRoot) ? "It's already there"
        : "";

    /// <summary>The Options page doubles as the Move page when the app is already installed.</summary>
    public bool IsMoving
    {
        get => _isMoving;
        private set
        {
            if (!Set(ref _isMoving, value)) return;
            OnPropertyChanged(nameof(OptionsTitle));
            OnPropertyChanged(nameof(ConfirmText));
            OnPropertyChanged(nameof(LocationProblem));
            CommandManager.InvalidateRequerySuggested();
        }
    }
    bool _isMoving;

    public string OptionsTitle => IsMoving ? "Move to" : "Install location";
    public string ConfirmText => IsMoving ? "Move" : "Install";

    public string InstalledFolder => _installedRoot is null ? "" : Path.Combine(_installedRoot, "BdoTimers");
    public bool HasInstalledFolder => _installedRoot is not null;

    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string ProgressTitle { get => _progressTitle; private set => Set(ref _progressTitle, value); }
    public string DoneTitle { get => _doneTitle; private set => Set(ref _doneTitle, value); }
    public string DoneText { get => _doneText; private set => Set(ref _doneText, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public bool LaunchApp { get => _launchApp; set => Set(ref _launchApp, value); }
    public bool CanLaunch => _running is LaunchAction.Install or LaunchAction.Repair || _moveTo is not null;

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

    void Confirm()
    {
        if (!IsMoving)
        {
            Start(LaunchAction.Install);
            return;
        }
        _moveTo = InstallRoot.Trim();
        _moveInstalling = false;
        _running = LaunchAction.Modify;
        Progress = 0;
        ProgressTitle = "Moving";
        Page = SetupPage.Progress;
        OnPropertyChanged(nameof(CanLaunch));
        _flow.Start(LaunchAction.Modify, packageState: RequestState.Absent);
    }

    /// <summary>Second half of a move, once the removal is detected: install into the new folder.</summary>
    void InstallMoved()
    {
        _moveInstalling = true;
        _flow.Start(LaunchAction.Modify, _moveTo, RequestState.Present);
    }

    void Finish(int status, string? message)
    {
        ExitCode = status;
        if (status < 0)
        {
            if (_moveInstalling) message = "BDO Timers was removed from the old folder but couldn't be installed in the new one. "
                                           + "Run setup again and choose Repair. " + message;
            ShowError(status, message);
            return;
        }
        if (_moveTo is not null && !_moveInstalling)
        {
            _flow.Detect();
            return;
        }
        if (_moveTo is not null)
        {
            var folder = Path.Combine(_moveTo, "BdoTimers");
            _flow.RepointAutostart(Path.Combine(folder, "BdoTimers.exe"));
            _flow.ForgetExe(Path.Combine(InstalledFolder, "BdoTimers.exe"));
            _flow.RemoveIfEmpty(InstalledFolder);
            (DoneTitle, DoneText) = ("Moved", folder);
            Page = SetupPage.Done;
            return;
        }
        (DoneTitle, DoneText) = _running switch
        {
            LaunchAction.Uninstall => ("Removed", "Your timers and settings are kept."),
            LaunchAction.Repair => ("Repaired", ""),
            _ => ("Ready", ""),
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

    void OpenFolder()
    {
        if (Directory.Exists(InstalledFolder)) Process.Start(new ProcessStartInfo(InstalledFolder) { UseShellExecute = true });
    }

    /// <summary>Tries a throwaway file in the nearest folder that exists, which is where the app's folder would be created.</summary>
    static FolderAccess CheckAccess(string path)
    {
        string dir;
        try { dir = Path.GetFullPath(path.Trim()); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return FolderAccess.Missing; }
        while (!Directory.Exists(dir))
        {
            if (Path.GetDirectoryName(dir) is not { } parent) return FolderAccess.Missing;
            dir = parent;
        }
        try
        {
            using (File.Create(Path.Combine(dir, $".bdotimers-{Guid.NewGuid():N}.tmp"), 1, FileOptions.DeleteOnClose)) { }
            return FolderAccess.Writable;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return FolderAccess.NeedsAdmin; }
    }

    static bool IsSameFolder(string a, string? b) =>
        b is not null && string.Equals(a.Trim().TrimEnd('\\'), b.Trim().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    /// <summary>Launches from the folder the package just recorded: after a repair the typed folder may not be where
    /// the app is (a Move page edited then abandoned, or the default when repairing a half-finished move).</summary>
    void FinishAndClose()
    {
        if (CanLaunch && LaunchApp && SetupFlow.InstalledRoot is { } root)
        {
            var exe = Path.Combine(root, "BdoTimers", "BdoTimers.exe");
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
