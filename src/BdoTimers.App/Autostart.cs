using Microsoft.Win32;

namespace BdoTimers.App;

public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "BdoTimers";

    /// <summary>No-op in Debug builds so a bin\Debug exe never lands in the Run key.</summary>
    public static void Apply(bool enabled)
    {
#if !DEBUG
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --minimized");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
#endif
    }
}
