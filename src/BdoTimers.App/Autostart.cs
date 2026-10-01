using Microsoft.Win32;

namespace BdoTimers.App;

/// <summary>
/// Start with Windows, as a value in the user's Run key. The setup window links this file to remove the value on
/// uninstall, since the package doesn't own it.
/// </summary>
public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "BdoTimers";

#if NET // The setup window (.NET Framework, no Environment.ProcessPath) only removes the value.
    /// <summary>No-op in Debug builds so a bin\Debug exe never lands in the Run key.</summary>
    public static void Apply(bool enabled)
    {
#if !DEBUG
        if (!enabled)
        {
            Remove();
            return;
        }
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
#endif
    }
#endif

    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
