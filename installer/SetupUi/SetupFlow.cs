using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using BdoTimers.App;
using BdoTimers.App.Alerts;
using Microsoft.Win32;
using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

/// <summary>
/// Detect, plan and apply against the bundle engine. Engine callbacks arrive on engine threads; listeners marshal
/// them as needed.
/// </summary>
internal sealed class SetupFlow
{
    /// <summary>HRESULT_FROM_WIN32(ERROR_CANCELLED): a cancelled apply, and a cancelled folder picker.</summary>
    internal const int ErrorCancelled = unchecked((int)0x800704C7);

    /// <summary>The bundle variable the MSI's INSTALLROOT comes from.</summary>
    const string InstallRootVariable = "InstallRoot";

    readonly IEngine _engine;
    readonly IBootstrapperCommand _command;
    volatile bool _cancel;
    LaunchAction _action;
    /// <summary>The installed exe when the current action started; uninstall removes the record of where it was.</summary>
    string? _installedExe;
    string? _lastError;

    public SetupFlow(BootstrapperApplication ba, IEngine engine, IBootstrapperCommand command)
    {
        _engine = engine;
        _command = command;
        ba.DetectBegin += (_, e) => IsInstalled = e.RegistrationType == RegistrationType.Full;
        ba.DetectComplete += (_, e) => Detected?.Invoke(e.Status);
        ba.PlanComplete += (_, e) =>
        {
            if (e.Status < 0 || _cancel)
            {
                Finished?.Invoke(_cancel ? ErrorCancelled : e.Status, _lastError);
                return;
            }
            try { _engine.Apply(WindowHandle); }
            catch (COMException ex) { Finished?.Invoke(ex.HResult, ex.Message); }
        };
        ba.Progress += (_, e) => { e.Cancel = _cancel; ProgressChanged?.Invoke(e.OverallPercentage); };
        ba.ExecuteProgress += (_, e) => e.Cancel = _cancel;
        ba.CacheAcquireProgress += (_, e) => e.Cancel = _cancel;
        ba.Error += (_, e) => _lastError = e.ErrorMessage;
        ba.ApplyComplete += (_, e) =>
        {
            // Only once the app is really gone: an upgrade uninstalls the previous bundle after installing the new
            // app, and a leftover registration of an older build uninstalls without touching the installed app.
            if (e.Status >= 0 && _action == LaunchAction.Uninstall && (_installedExe is null || !File.Exists(_installedExe)))
            {
                RemoveAutostart();
                RemoveNotifications(_installedExe);
                if (_installedExe is not null && DeletePersonalData) RemoveData(Path.GetDirectoryName(_installedExe)!);
            }
            Finished?.Invoke(e.Status, _lastError);
        };

        // Sets the variables the bundle marks overridable (InstallRoot) from the command line; the engine leaves that
        // to the bootstrapper application.
        command.ParseCommandLine().SetOverridableVariables(new BootstrapperApplicationData().Bundle.OverridableVariables, engine);
        DeletePersonalData = engine.ContainsVariable("DeleteData") && engine.GetVariableNumeric("DeleteData") == 1;
    }

    /// <summary>Raised with the detect status; <see cref="IsInstalled"/> is known by then.</summary>
    public event Action<int>? Detected;

    public event Action<int>? ProgressChanged;

    /// <summary>Raised with the final status (negative on failure) and the last engine error message, if any.</summary>
    public event Action<int, string?>? Finished;

    public bool IsInstalled { get; private set; }

    /// <summary>The app's Data folder that the last uninstall couldn't delete; null when it went.</summary>
    public string? UndeletedData { get; private set; }

    /// <summary>Personal data is kept unless the player explicitly opts in to deletion.</summary>
    public bool DeletePersonalData { get; set; }

    public LaunchAction RequestedAction => _command.Action;

    /// <summary>Parent for engine prompts. The engine rejects a null handle, so quiet runs use the desktop window.</summary>
    public IntPtr WindowHandle { get; set; } = GetDesktopWindow();

    public string LogPath => _engine.ContainsVariable("WixBundleLog") ? _engine.GetVariableString("WixBundleLog") : "";

    public static bool IsCancelled(int status) => status == ErrorCancelled;

    /// <summary>InstallRoot from the command line if given, else the installed one, else %LocalAppData%\Programs.</summary>
    public string InitialInstallRoot
    {
        get
        {
            // The quote goes: Windows reads "D:\My Games\" as D:\My Games" because \" escapes it.
            var fromCommandLine = _engine.ContainsVariable(InstallRootVariable)
                ? _engine.GetVariableString(InstallRootVariable).Trim().Trim('"')
                : "";
            return fromCommandLine.Length > 0
                ? fromCommandLine
                : InstalledRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
        }
    }

    /// <summary>The folder holding the BdoTimers folder, as the package recorded it; null when not installed.</summary>
    public static string? InstalledRoot
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\BdoTimers");
            return key?.GetValue("InstallRoot") as string is { Length: > 0 } saved ? saved : null;
        }
    }

    /// <summary>The app's own folder inside the chosen one (the package's INSTALLFOLDER).</summary>
    public static string AppFolder(string root) => Path.Combine(root, "BdoTimers");

    public static string ExePath(string root) => Path.Combine(AppFolder(root), "BdoTimers.exe");

    /// <summary>Where the app keeps timers, settings, logs and the user's sounds and pictures (the app's App.OnStartup).
    /// The package doesn't own it; deletion on uninstall is optional.</summary>
    public static string DataFolder(string appFolder) => Path.Combine(appFolder, "Data");

    public void Detect() => _engine.Detect();

    /// <summary>Plans and applies <paramref name="action"/>; <paramref name="installRoot"/> is the chosen folder for an
    /// install.</summary>
    public void Start(LaunchAction action, string? installRoot = null)
    {
        CloseInstalledApp();
        _installedExe = InstalledRoot is { } root ? ExePath(root) : null;
        _action = action;
        _cancel = false;
        _lastError = null;
        UndeletedData = null;
        // Repair and uninstall keep the recorded folder even when the command line names another.
        _engine.SetVariableString(InstallRootVariable, installRoot is null ? "" : installRoot.TrimEnd('\\') + "\\", false);
        _engine.Plan(action);
    }

    public void Cancel() => _cancel = true;

    /// <summary>
    /// For /quiet and /passive: performs the requested action without a window. Engine calls are made from this
    /// thread, never from inside an engine callback, which would wait on the engine that is waiting on it.
    /// </summary>
    public int RunQuiet()
    {
        using var detected = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        int detectStatus = 0, result = 0;
        Detected += status => { detectStatus = status; detected.Set(); };
        Finished += (status, _) => { result = status; finished.Set(); };

        Detect();
        detected.Wait();
        if (detectStatus < 0) return detectStatus;
        var action = RequestedAction == LaunchAction.Unknown ? LaunchAction.Install : RequestedAction;
        Start(action, action == LaunchAction.Install ? InitialInstallRoot : null);
        finished.Wait();
        return result;
    }

    /// <summary>After uninstall: the Start with Windows entry, which would otherwise point at a deleted exe.</summary>
    void RemoveAutostart()
    {
        try { Autostart.Remove(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _engine.Log(LogLevel.Error, $"Couldn't remove the Start with Windows entry: {ex.Message}");
        }
    }

    /// <summary>After uninstall: the app's notification registration and settings, and any earlier version's.</summary>
    void RemoveNotifications(string? exePath)
    {
        try
        {
            NotificationRegistration.Remove(NotificationRegistration.InstalledAppId);
            if (exePath is not null) NotificationRegistration.RemoveLegacy(exePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _engine.Log(LogLevel.Error, $"Couldn't remove the notification registration: {ex.Message}");
        }
    }

    /// <summary>
    /// Ends a copy of the app running from the installed folder and waits until it has exited, before any action
    /// (quiet ones too): a locked file would be left for replacement or deletion at the next restart. Copies running from
    /// anywhere else, such as a dev build, are left alone.
    /// </summary>
    void CloseInstalledApp()
    {
        if (InstalledRoot is not { } root) return;
        var folder = AppFolder(root) + "\\";
        foreach (var process in Process.GetProcessesByName("BdoTimers"))
        {
            using (process)
            {
                try
                {
                    if (!process.MainModule.FileName.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) continue;
                    process.Kill();
                    process.WaitForExit(10000);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    _engine.Log(LogLevel.Standard, $"Couldn't close BDO Timers before applying: {ex.Message}");
                }
            }
        }
    }

    /// <summary>Explicit deletion also removes the recovery copies made by backup restore.</summary>
    void RemoveData(string appFolder)
    {
        var data = DataFolder(appFolder);
        try { PersonalDataCleanup.Delete(appFolder); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _engine.Log(LogLevel.Error, $"Couldn't delete {data}: {ex.Message}");
            UndeletedData = data;
            return;
        }
        RemoveIfEmpty(appFolder);
    }

    /// <summary>Removes <paramref name="folder"/> if it is empty. The package's own removal skips it while it holds the
    /// data or the app it just closed still has it open.</summary>
    void RemoveIfEmpty(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _engine.Log(LogLevel.Standard, $"Left the folder {folder}: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetDesktopWindow();
}
