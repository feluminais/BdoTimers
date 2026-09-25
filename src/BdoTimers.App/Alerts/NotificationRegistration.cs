using System.IO;
using Microsoft.Win32;

namespace BdoTimers.App.Alerts;

/// <summary>
/// How Windows knows the app for notifications: a fixed app id under HKCU\Software\Classes\AppUserModelId, which also
/// keys the user's notification settings, so they survive moving or reinstalling the app. The setup window links this
/// file to remove the registration on uninstall.
/// </summary>
public static class NotificationRegistration
{
    /// <summary>The installed app's id; debug builds register under their own.</summary>
    public const string InstalledAppId = "BdoTimers";

    const string AppIds = @"Software\Classes\AppUserModelId";
    const string Settings = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    public static void Register(string appId, string displayName, string iconPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{AppIds}\{appId}");
        key.SetValue("DisplayName", displayName);
        key.SetValue("IconUri", iconPath);
    }

    /// <summary>Removes the registration and the user's notification settings for it.</summary>
    public static void Remove(string appId)
    {
        Registry.CurrentUser.DeleteSubKeyTree($@"{AppIds}\{appId}", false);
        Registry.CurrentUser.DeleteSubKeyTree($@"{Settings}\{appId}", false);
    }

    /// <summary>
    /// Removes the registration earlier versions made through the Windows App SDK, which keyed it by exe path, so
    /// <paramref name="exePath"/> isn't listed twice in Windows' notification settings.
    /// </summary>
    public static void RemoveLegacy(string exePath)
    {
        using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
        using var aumids = classes?.OpenSubKey("AppUserModelId", writable: true);
        if (classes is null || aumids is null) return;

        foreach (var aumid in aumids.GetSubKeyNames())
        {
            string? activator;
            using (var key = aumids.OpenSubKey(aumid)) activator = key?.GetValue("CustomActivator") as string;
            if (activator is null || !LaunchesExe(classes, activator, exePath)) continue;

            // That registration also kept a key named after the exe path that points at it.
            foreach (var other in aumids.GetSubKeyNames())
            {
                string? target;
                using (var key = aumids.OpenSubKey(other)) target = key?.GetValue("NotificationGUID") as string;
                if (string.Equals(target, aumid, StringComparison.OrdinalIgnoreCase)) aumids.DeleteSubKeyTree(other, false);
            }
            aumids.DeleteSubKeyTree(aumid, false);
            classes.DeleteSubKeyTree($@"CLSID\{activator}", false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{Settings}\{aumid}", false);
            var icon = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsAppSDK", aumid + ".png");
            if (File.Exists(icon)) File.Delete(icon);
        }
    }

    static bool LaunchesExe(RegistryKey classes, string activator, string exePath)
    {
        using var server = classes.OpenSubKey($@"CLSID\{activator}\LocalServer32");
        return server?.GetValue("") is string command
               && command.StartsWith("\"" + exePath + "\"", StringComparison.OrdinalIgnoreCase);
    }
}
