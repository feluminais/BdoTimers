using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

/// <summary>
/// Detect, plan and apply against the bundle engine. Engine callbacks arrive on engine threads; listeners marshal
/// them as needed.
/// </summary>
internal sealed class SetupFlow
{
    const int ErrorCancelled = unchecked((int)0x800704C7);

    readonly IEngine _engine;
    readonly IBootstrapperCommand _command;
    volatile bool _cancel;
    LaunchAction _action;
    RequestState? _packageState;
    string? _lastError;

    public SetupFlow(BootstrapperApplication ba, IEngine engine, IBootstrapperCommand command)
    {
        _engine = engine;
        _command = command;
        ba.DetectBegin += (_, e) => IsInstalled = e.RegistrationType == RegistrationType.Full;
        ba.DetectComplete += (_, e) => Detected?.Invoke(e.Status);
        ba.PlanPackageBegin += (_, e) => { if (_packageState is { } state) e.State = state; };
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
            if (e.Status >= 0 && _action == LaunchAction.Uninstall) RemoveAutostart();
            Finished?.Invoke(e.Status, _lastError);
        };
    }

    /// <summary>Raised with the detect status; <see cref="IsInstalled"/> is known by then.</summary>
    public event Action<int>? Detected;

    public event Action<int>? ProgressChanged;

    /// <summary>Raised with the final status (negative on failure) and the last engine error message, if any.</summary>
    public event Action<int, string?>? Finished;

    public bool IsInstalled { get; private set; }

    public LaunchAction RequestedAction => _command.Action;

    /// <summary>Parent for engine prompts. The engine rejects a null handle, so quiet runs use the desktop window.</summary>
    public IntPtr WindowHandle { get; set; } = GetDesktopWindow();

    public string LogPath => _engine.ContainsVariable("WixBundleLog") ? _engine.GetVariableString("WixBundleLog") : "";


    public static bool IsCancelled(int status) => status == ErrorCancelled;

    /// <summary>InstallRoot from the command line if given, else the installed one, else %LocalAppData%\Programs.</summary>
    public string InitialInstallRoot =>
        _command.ParseCommandLine().Variables
            .FirstOrDefault(v => string.Equals(v.Key, "InstallRoot", StringComparison.OrdinalIgnoreCase)).Value
        is { Length: > 0 } fromCommandLine
            ? fromCommandLine
            : InstalledRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

    /// <summary>The folder holding the BdoTimers folder, as the package recorded it; null when not installed.</summary>
    public static string? InstalledRoot
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\BdoTimers");
            return key?.GetValue("InstallRoot") as string is { Length: > 0 } saved ? saved : null;
        }
    }

    public void Detect() => _engine.Detect();

    /// <summary>Plans and applies <paramref name="action"/>. <paramref name="packageState"/> overrides what happens to
    /// the app package, e.g. Modify with Absent removes the app while the bundle stays registered.</summary>
    public void Start(LaunchAction action, string? installRoot = null, RequestState? packageState = null)
    {
        CloseInstalledApp();
        _action = action;
        _packageState = packageState;
        _cancel = false;
        _lastError = null;
        if (installRoot is not null)
            _engine.SetVariableString("InstallRoot", installRoot.TrimEnd('\\') + "\\", false);
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

    /// <summary>
    /// The app adds itself to the Run key only when the user turns on Start with Windows, so the package doesn't own
    /// that value and uninstall would leave it pointing at a deleted exe.
    /// </summary>
    void RemoveAutostart()
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            run?.DeleteValue("BdoTimers", throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _engine.Log(LogLevel.Error, $"Couldn't remove the Start with Windows entry: {ex.Message}");
        }
    }

    /// <summary>
    /// Ends a copy of the app running from the installed folder and waits until it has exited. The package's own
    /// CloseApplication ends it too, but carries on before Windows lets go of its files, so a locked file would be left
    /// for deletion at the next restart. Copies running from anywhere else are left alone.
    /// </summary>
    void CloseInstalledApp()
    {
        if (InstalledRoot is not { } root) return;
        var folder = Path.Combine(root, "BdoTimers") + "\\";
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

    /// <summary>
    /// After a move: removes the old BdoTimers folder when nothing is left in it. The package removes it too, but not
    /// while the app it just closed still holds the folder open.
    /// </summary>
    public void RemoveIfEmpty(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _engine.Log(LogLevel.Standard, $"Left the old folder {folder}: {ex.Message}");
        }
    }

    /// <summary>After a move: points an existing Start with Windows entry at the moved exe (same format the app writes).</summary>
    public void RepointAutostart(string exePath)
    {
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (run?.GetValue("BdoTimers") is not null) run.SetValue("BdoTimers", $"\"{exePath}\" --minimized");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _engine.Log(LogLevel.Error, $"Couldn't update the Start with Windows entry: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    static extern IntPtr GetDesktopWindow();
}
