using System.Diagnostics;
using System.IO;
using System.Windows;
using BdoTimers.App.Alerts;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.App;

/// <summary>The Settings buttons that test the alerts and help with problems: a test alert, the data and log folders,
/// and the diagnostics report.</summary>
public sealed class SupportTools(AlertDispatcher alerts, AppHealth health, IClock clock, string dataDir, CancellationToken shutdown)
{
    public async void SendTestAlert(AppSettings previewSettings)
    {
        if (health.TestBusy) return;
        health.TestBusy = true;
        health.TestStatus = "Sending…";
        try
        {
            await alerts.SendTestAsync(new AlertEvent([new TimerDef { Name = "Test boss" }], clock.UtcNow.AddMinutes(5), 5, 5), previewSettings);
            health.TestStatus = health.Status is null ? "Test sent" : "Some channels failed. See Diagnostics.";
        }
        catch (Exception ex)
        {
            Log.Error("Test alert failed", ex);
            health.Failed("App", ex);
            health.TestStatus = "Couldn't send the test. See Diagnostics.";
        }
        finally { health.TestBusy = false; }
    }

    public void OpenDataFolder()
    {
        Directory.CreateDirectory(dataDir);
        Process.Start(new ProcessStartInfo(dataDir) { UseShellExecute = true });
    }

    public void OpenLogs()
    {
        try
        {
            var directory = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
            health.Succeeded("Diagnostics");
            health.DiagnosticStatus = null;
        }
        catch (Exception ex) { Log.Error("Couldn't open logs", ex); health.Failed("Diagnostics", ex); }
    }

    public async void CopyDiagnostics()
    {
        if (health.DiagnosticsBusy) return;
        health.DiagnosticsBusy = true;
        health.DiagnosticStatus = "Measuring…";
        try
        {
            var report = await PerformanceMetrics.ReportAsync(health.LastFailure, shutdown);
            Clipboard.SetText(report);
            health.Succeeded("Diagnostics");
            health.DiagnosticStatus = "Copied";
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        catch (Exception ex) { Log.Error("Couldn't copy diagnostics", ex); health.Failed("Diagnostics", ex); health.DiagnosticStatus = null; }
        finally { health.DiagnosticsBusy = false; }
    }
}
