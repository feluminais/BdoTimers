#Requires -Version 7.4
param([switch]$Quick)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
dotnet build (Join-Path $workspace 'src/BdoTimers.App') -p:NuGetAudit=false
$buildDirectory = Join-Path $workspace 'src/BdoTimers.App/bin/Debug'
$runName = 'performance/' + [Guid]::NewGuid().ToString('N') + '/app'
$stage = [IO.Path]::GetFullPath((Join-Path $buildDirectory $runName))
if (-not $stage.StartsWith([IO.Path]::GetFullPath($buildDirectory) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Performance staging must stay within the dev build directory.'
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
# A separate copy keeps measurements away from the developer's saved timers and speech cache.
Get-ChildItem -LiteralPath $buildDirectory | Where-Object { $_.Name -notin 'Data', 'performance' } |
    Copy-Item -Destination $stage -Recurse -Force
$outputDirectory = Join-Path $workspace 'obj/performance'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$report = Join-Path $outputDirectory ("{0:yyyyMMdd-HHmmss}-{1}.txt" -f (Get-Date), $(if ($Quick) { 'quick' } else { 'full' }))
$arguments = @('--performance-report', ('"{0}"' -f $report))
if ($Quick) { $arguments += @('--settle-seconds', '2', '--skip-idle-unload') }
$process = Start-Process -FilePath (Join-Path $stage 'BdoTimers.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(300000)) {
    # This handle refers only to the process this script created under bin/Debug/performance.
    $process.Kill()
    throw 'Performance measurement timed out.'
}
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report)) {
    throw "Performance measurement failed. See $stage/Data/logs."
}
Get-Content -LiteralPath $report
Write-Host "Report: $report"
