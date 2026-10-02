using System.IO;
using System.Windows.Threading;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BdoTimers.App;

/// <summary>Channel faults stay visible until that channel succeeds; background alerts never open dialogs.</summary>
public sealed partial class AppHealth(IClock clock, Dispatcher dispatcher) : ObservableObject
{
    readonly FailureTracker _failures = new(clock);
    [ObservableProperty] string? _status;
    [ObservableProperty] string? _diagnosticStatus;
    [ObservableProperty] bool _diagnosticsBusy;
    [ObservableProperty] string? _testStatus;
    [ObservableProperty] bool _testBusy;

    public FailureInfo? LastFailure => _failures.Latest;

    public void Failed(string area, Exception error)
    {
        string? savedFile = null;
        if (error is StateSaveException save)
        {
            savedFile = Path.GetFileName(save.FilePath);
            area = "Saving:" + savedFile;
        }
        var message = area switch
        {
            "Voice" when error is FileNotFoundException => "Voice files are missing. Repair the installation, then retry.",
            "Voice" when error is DllNotFoundException or BadImageFormatException => "Voice couldn't load. Repair the installation, then retry.",
            "Voice" => "Voice couldn't play. Retry the voice test.",
            "Sound" => "Sound couldn't play. Check the output device, then retry.",
            "Notifications" => "A notification failed. Check Windows notification settings, then retry.",
            "Saving" => "Changes couldn't be saved. Check disk space and folder access, then retry.",
            _ when savedFile is not null => $"{SavedLabel(savedFile)} couldn't be saved. Check disk space and folder access, then retry.",
            "To-do reset" => "To-do reset couldn't be saved. Check disk space and folder access.",
            "Startup" => "Some timers couldn't be reconciled. Restart the app.",
            "Scheduler" => "Timer scheduling failed. Restart the app.",
            "Diagnostics" => "Diagnostics couldn't be opened or copied. Try again.",
            _ => "An unexpected error occurred. Restart the app.",
        };
        if (_failures.Report(area, message)) Refresh();
    }

    public void Succeeded(string area)
    {
        if (_failures.Clear(area)) Refresh();
    }

    public void Saved(string filePath) => Succeeded("Saving:" + Path.GetFileName(filePath));

    static string SavedLabel(string name) => name.ToLowerInvariant() switch
    {
        "settings.json" => "Settings", "timers.json" => "Timers", "todos.json" => "To-do lists", _ => "Changes",
    };

    void Refresh()
    {
        if (dispatcher.HasShutdownStarted) return;
        void Apply() => Status = _failures.Active.Count == 0 ? null : string.Join("\n", _failures.Active.Select(f => f.Message));
        if (dispatcher.CheckAccess()) Apply();
        else
        {
            try { dispatcher.BeginInvoke(Apply); }
            catch (InvalidOperationException) when (dispatcher.HasShutdownStarted) { }
        }
    }
}
