using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Text;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace BdoTimers.App.Alerts;

/// <summary>Windows notifications through the toast API built into Windows.</summary>
public sealed class ToastChannel
{
    /// <summary>Windows 11 22H2 added the urgent scenario, which gets through Do Not Disturb; older builds use alarm.</summary>
    const int UrgentScenarioBuild = 22621;
    const int KeptToasts = 20;

    readonly ToastNotifier _notifier;
    // A click is reported through the toast's own object, so recent toasts are kept alive to report it.
    readonly Queue<ToastNotification> _recent = new();

    public event Action? Activated;

    public ToastChannel(string appId, string displayName)
    {
        try
        {
            NotificationRegistration.RemoveLegacy(Environment.ProcessPath!);
            NotificationRegistration.Register(appId, displayName, Path.Combine(AppContext.BaseDirectory, "app.png"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            Log.Error("Couldn't update the notification registration", ex);
        }
        SetCurrentProcessExplicitAppUserModelID(appId);
        _notifier = ToastNotificationManager.CreateToastNotifier(appId);
    }

    /// <summary>Urgent (or alarm) scenario so it breaks through gaming Do Not Disturb; our own sound plays instead of the toast's.</summary>
    public void ShowUrgent(AlertMessage message)
    {
        var scenario = Environment.OSVersion.Version.Build >= UrgentScenarioBuild ? "urgent" : "alarm";
        Show($"""
            <toast scenario="{scenario}">
              <visual><binding template="ToastGeneric"><text>{Escape(message.Title)}</text><text>{Escape(message.Body)}</text></binding></visual>
              <audio silent="true"/>
              <actions><action content="Dismiss" arguments="dismiss" activationType="system"/></actions>
            </toast>
            """);
    }

    public void ShowInfo(string title, string body, bool withNotificationSettingsButton = false)
    {
        var actions = withNotificationSettingsButton
            ? """<actions><action content="Open notification settings" arguments="ms-settings:notifications" activationType="protocol"/></actions>"""
            : "";
        Show($"""
            <toast>
              <visual><binding template="ToastGeneric"><text>{Escape(title)}</text><text>{Escape(body)}</text></binding></visual>
              {actions}
            </toast>
            """);
    }

    public static void OpenWindowsNotificationSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });

    void Show(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        var toast = new ToastNotification(doc);
        toast.Activated += (_, _) => Activated?.Invoke();
        // Alerts arrive on the scheduler thread, notices on the UI thread.
        lock (_recent)
        {
            _recent.Enqueue(toast);
            if (_recent.Count > KeptToasts) _recent.Dequeue();
        }
        _notifier.Show(toast);
    }

    static string Escape(string text) => SecurityElement.Escape(text);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SetCurrentProcessExplicitAppUserModelID(string appId);
}
