#Requires -Version 7.4
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $workspace 'tests/BdoTimers.App.Tests'
if (-not $NoBuild) { dotnet build $project -c $Configuration }

# Create the test process on a separate desktop before .NET initializes COM and WPF.
# The desktop is never displayed, so test windows and focus cannot disturb the active desktop.
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public static class BdoTimersWindowsTests
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct StartupInfo
    {
        public int cb;
        public string reserved, desktop, title;
        public int x, y, width, height, charsX, charsY, fill, flags;
        public short show, reservedCount;
        public IntPtr reservedBytes, stdin, stdout, stderr;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInfo { public IntPtr process, thread; public uint processId, threadId; }

    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr mode, uint flags, uint access, IntPtr security);
    [DllImport("user32.dll")]
    static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
        bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll")]
    static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    public static int Run(string dotnet, string command, string directory, string log)
    {
        var name = "BdoTimers.Tests." + Guid.NewGuid().ToString("N");
        var desktop = CreateDesktop(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var process = new ProcessInfo();
        try
        {
            using (var output = new FileStream(log, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
            using (var input = new FileStream("NUL", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var stdout = output.SafeFileHandle.DangerousGetHandle();
                var stdin = input.SafeFileHandle.DangerousGetHandle();
                if (!SetHandleInformation(stdout, 1, 1) || !SetHandleInformation(stdin, 1, 1))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), desktop = name,
                    flags = 0x100, stdin = stdin, stdout = stdout, stderr = stdout };
                if (!CreateProcess(dotnet, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, true,
                    0x08000000, IntPtr.Zero, directory, ref startup, out process))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                uint waited = 0x102;
                for (var attempt = 0; attempt < 5 && waited == 0x102; attempt++)
                    waited = WaitForSingleObject(process.process, 60000);
                if (waited == 0x102)
                {
                    using (var ownProcess = Process.GetProcessById((int)process.processId)) ownProcess.Kill(true);
                    throw new TimeoutException("Windows UI tests exceeded five minutes.");
                }
                if (waited != 0 || !GetExitCodeProcess(process.process, out var code))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return (int)code;
            }
        }
        finally
        {
            if (process.thread != IntPtr.Zero) CloseHandle(process.thread);
            if (process.process != IntPtr.Zero) CloseHandle(process.process);
            CloseDesktop(desktop);
        }
    }
}
'@
$resultDirectory = Join-Path $workspace 'TestResults'
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$log = Join-Path $resultDirectory ('wpf-' + [Guid]::NewGuid().ToString('N') + '.log')
$dotnet = (Get-Command dotnet).Source
$command = '"{0}" test "{1}" --no-build --no-restore -c {2} --logger "trx;LogFileName=wpf.trx" --results-directory "{3}"' -f $dotnet, $project, $Configuration, $resultDirectory
$result = [BdoTimersWindowsTests]::Run($dotnet, $command, $workspace, $log)
Get-Content -LiteralPath $log
if ($result -ne 0) { throw "Windows UI tests failed (exit $result). See $log." }
