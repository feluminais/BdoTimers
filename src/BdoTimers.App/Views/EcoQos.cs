using System.ComponentModel;
using System.Runtime.InteropServices;
using BdoTimers.Core.Diagnostics;

namespace BdoTimers.App.Views;

/// <summary>
/// Windows' EcoQoS for the whole process while the main window is out of sight: its threads lean to efficient cores and
/// lower clocks, leaving the game more room. Unlike efficiency mode it leaves the priority alone, so alerts aren't
/// delayed (see <see cref="TrayIcon"/>). UI thread only.
/// </summary>
static partial class EcoQos
{
    const int ProcessPowerThrottling = 4;
    const uint CurrentVersion = 1;
    const uint ExecutionSpeed = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    struct PowerThrottlingState
    {
        public uint Version, ControlMask, StateMask;
    }

    static bool? _on;

    /// <summary>On asks for EcoQoS; off hands the choice back to Windows. A failure is logged and changes nothing.</summary>
    public static void Set(bool on)
    {
        if (_on == on) return;
        var mask = on ? ExecutionSpeed : 0;
        var state = new PowerThrottlingState { Version = CurrentVersion, ControlMask = mask, StateMask = mask };
        if (SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>()))
            _on = on;
        else
            Log.Error($"Couldn't turn EcoQoS {(on ? "on" : "off")}", new Win32Exception(Marshal.GetLastPInvokeError()));
    }

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(IntPtr process, int informationClass, ref PowerThrottlingState information, uint size);
}
