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
        ba.ApplyComplete += (_, e) => Finished?.Invoke(e.Status, _lastError);
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

    public string Version => _engine.GetVariableVersion("WixBundleVersion");

    public static bool IsCancelled(int status) => status == ErrorCancelled;

    /// <summary>InstallRoot from the command line if given, else the folder chosen last time, else the default.</summary>
    public string InitialInstallRoot =>
        _command.ParseCommandLine().Variables
            .FirstOrDefault(v => string.Equals(v.Key, "InstallRoot", StringComparison.OrdinalIgnoreCase)).Value
        is { Length: > 0 } fromCommandLine
            ? fromCommandLine
            : DefaultInstallRoot();

    /// <summary>The folder chosen at the last install, else %LocalAppData%\Programs.</summary>
    static string DefaultInstallRoot()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\BdoTimers");
        return key?.GetValue("InstallRoot") as string is { Length: > 0 } saved
            ? saved
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
    }

    public void Detect() => _engine.Detect();

    public void Start(LaunchAction action, string? installRoot = null)
    {
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

    [DllImport("user32.dll")]
    static extern IntPtr GetDesktopWindow();
}
