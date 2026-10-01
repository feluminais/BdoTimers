using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

public enum SetupPage { Loading, Welcome, Options, Progress, Done, Error, Maintenance, ConfirmUninstall }

/// <summary>Whether the user can create the app's folder in a place; the install is per-user, so it never elevates.</summary>
public enum FolderAccess { Writable, NeedsAdmin, Missing }

internal sealed class SetupViewModel : INotifyPropertyChanged
{
    readonly SetupFlow _flow;
    readonly string? _installedRoot = SetupFlow.InstalledRoot;
    LaunchAction _running;
    SetupPage _page = SetupPage.Loading;
    string _installRoot;
    /// <summary>Null until the folder is checked again after an edit.</summary>
    FolderAccess? _access;
    int _progress;
    string _progressTitle = "";
    string _doneTitle = "";
    string _doneText = "";
    string _errorText = "";
    string _userFilesWarning = "";
    /// <summary>The folder Open folder shows on the uninstall confirmation.</summary>
    string? _userFilesFolder;
    bool _launchApp = true;

    public SetupViewModel(SetupFlow flow)
    {
        _flow = flow;
        _installRoot = flow.InitialInstallRoot;
        _access = CheckAccess(_installRoot);
        flow.Detected += status => OnUi(() =>
        {
            if (status < 0) ShowError(status, null);
            else Page = flow.IsInstalled ? SetupPage.Maintenance : SetupPage.Welcome;
        });
        flow.ProgressChanged += percent => OnUi(() => Progress = percent);
        flow.Finished += (status, message) => OnUi(() => Finish(status, message));

        InstallCommand = new Command(Install, () => IsInstallRootValid && _access is null or FolderAccess.Writable);
        NextCommand = new Command(() => Page = SetupPage.Options);
        OpenFolderCommand = new Command(() => OpenFolder(InstalledFolder));
        BackCommand = new Command(() => Page = Page == SetupPage.ConfirmUninstall ? SetupPage.Maintenance : SetupPage.Welcome);
        BrowseCommand = new Command(Browse);
        RepairCommand = new Command(() => Start(LaunchAction.Repair));
        UninstallCommand = new Command(Uninstall);
        ConfirmUninstallCommand = new Command(() => Start(LaunchAction.Uninstall));
        OpenUserFilesCommand = new Command(() => OpenFolder(_userFilesFolder));
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
    public ICommand OpenFolderCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand ConfirmUninstallCommand { get; }
    public ICommand OpenUserFilesCommand { get; }
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
            _access = null;
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
        : "";

    /// <summary>
    /// Checks that the app's folder can be created in the typed folder. Typing only checks the syntax: this touches the
    /// disk, or the network for a share, so it runs when the box loses focus and on Browse and Install.
    /// </summary>
    public void CheckFolder()
    {
        _access = IsInstallRootValid ? CheckAccess(InstallRoot) : null;
        OnPropertyChanged(nameof(LocationProblem));
        CommandManager.InvalidateRequerySuggested();
    }

    public string InstalledFolder => _installedRoot is null ? "" : SetupFlow.AppFolder(_installedRoot);
    public bool HasInstalledFolder => _installedRoot is not null;

    public int Progress { get => _progress; private set => Set(ref _progress, value); }
    public string ProgressTitle { get => _progressTitle; private set => Set(ref _progressTitle, value); }
    public string DoneTitle { get => _doneTitle; private set => Set(ref _doneTitle, value); }
    public string DoneText { get => _doneText; private set => Set(ref _doneText, value); }
    public string ErrorText { get => _errorText; private set => Set(ref _errorText, value); }
    public string UserFilesWarning { get => _userFilesWarning; private set => Set(ref _userFilesWarning, value); }
    public bool LaunchApp { get => _launchApp; set => Set(ref _launchApp, value); }
    public bool CanLaunch => _running is LaunchAction.Install or LaunchAction.Repair;

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

    /// <summary>Uninstall deletes the app's data; sounds and pictures the user added get a chance to be copied first.</summary>
    void Uninstall()
    {
        var data = HasInstalledFolder ? SetupFlow.DataFolder(InstalledFolder) : null;
        var sounds = data is not null && HasFiles(Path.Combine(data, "sounds"));
        var pictures = data is not null && HasFiles(Path.Combine(data, "images"));
        if (!sounds && !pictures)
        {
            Start(LaunchAction.Uninstall);
            return;
        }
        (UserFilesWarning, _userFilesFolder) = sounds && pictures
            ? ("Your sounds and pictures will be deleted", data)
            : sounds
                ? ("Your sounds will be deleted", Path.Combine(data!, "sounds"))
                : ("Your pictures will be deleted", Path.Combine(data!, "images"));
        Page = SetupPage.ConfirmUninstall;
    }

    static bool HasFiles(string folder) => Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any();

    void Install()
    {
        CheckFolder();
        if (_access == FolderAccess.Writable) Start(LaunchAction.Install);
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
            LaunchAction.Uninstall => ("Removed", _flow.UndeletedData is { } left ? $"Couldn't delete {left}." : ""),
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
        if (FolderPicker.Pick(owner, InstallRoot) is not { } folder) return;
        InstallRoot = folder;
        CheckFolder();
    }

    static void OpenFolder(string? folder)
    {
        if (Directory.Exists(folder)) Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
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

    /// <summary>Launches from the folder the package just recorded.</summary>
    void FinishAndClose()
    {
        if (CanLaunch && LaunchApp && SetupFlow.InstalledRoot is { } root)
        {
            var exe = SetupFlow.ExePath(root);
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
