using System.IO;
using Microsoft.Win32;

namespace BdoTimers.SetupUi;

/// <summary>
/// The Windows notification registration BDO Timers makes on first run. Windows App SDK identifies unpackaged apps by
/// their exe path, so a copy that was uninstalled or moved would otherwise stay listed in Windows' notification settings.
/// </summary>
internal static class NotificationRegistration
{
    /// <summary>Removes the registration for <paramref name="exePath"/>, if there is one.</summary>
    public static void Remove(string exePath)
    {
        using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
        using var aumids = classes?.OpenSubKey("AppUserModelId", writable: true);
        if (classes is null || aumids is null) return;

        foreach (var aumid in aumids.GetSubKeyNames())
        {
            string? activator;
            using (var key = aumids.OpenSubKey(aumid)) activator = key?.GetValue("CustomActivator") as string;
            if (activator is null || !LaunchesExe(classes, activator, exePath)) continue;

            // The app also keeps a key named after its path that points at this registration.
            foreach (var other in aumids.GetSubKeyNames())
            {
                string? target;
                using (var key = aumids.OpenSubKey(other)) target = key?.GetValue("NotificationGUID") as string;
                if (string.Equals(target, aumid, StringComparison.OrdinalIgnoreCase)) aumids.DeleteSubKeyTree(other, false);
            }
            aumids.DeleteSubKeyTree(aumid, false);
            classes.DeleteSubKeyTree($@"CLSID\{activator}", false);
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\{aumid}", false);
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
