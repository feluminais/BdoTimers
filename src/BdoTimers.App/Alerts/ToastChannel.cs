using System.Diagnostics;
using BdoTimers.Core.Text;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace BdoTimers.App.Alerts;

public sealed class ToastChannel : IDisposable
{
    const string ActionKey = "action";
    const string Dismiss = "dismiss";
    const string OpenNotificationSettings = "openNotificationSettings";

    public event Action? Activated;

    public ToastChannel()
    {
        AppNotificationManager.Default.NotificationInvoked += (_, args) =>
        {
            args.Arguments.TryGetValue(ActionKey, out var action);
            if (action == OpenNotificationSettings) OpenWindowsNotificationSettings();
            else if (action != Dismiss) Activated?.Invoke();
        };
        AppNotificationManager.Default.Register();
    }

    /// <summary>Urgent (or alarm) scenario so it breaks through gaming Do Not Disturb; our own sound plays instead of the toast's.</summary>
    public void ShowUrgent(AlertMessage message)
    {
        var builder = new AppNotificationBuilder()
            .AddText(message.Title)
            .AddText(message.Body)
            .MuteAudio()
            .AddButton(new AppNotificationButton("Dismiss").AddArgument(ActionKey, Dismiss))
            .SetScenario(AppNotificationBuilder.IsUrgentScenarioSupported()
                ? AppNotificationScenario.Urgent
                : AppNotificationScenario.Alarm);
        AppNotificationManager.Default.Show(builder.BuildNotification());
    }

    public void ShowInfo(string title, string body, bool withNotificationSettingsButton = false)
    {
        var builder = new AppNotificationBuilder().AddText(title).AddText(body);
        if (withNotificationSettingsButton)
            builder.AddButton(new AppNotificationButton("Open notification settings")
                .AddArgument(ActionKey, OpenNotificationSettings));
        AppNotificationManager.Default.Show(builder.BuildNotification());
    }

    public static void OpenWindowsNotificationSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:notifications") { UseShellExecute = true });

    public void Dispose() => AppNotificationManager.Default.Unregister();
}
