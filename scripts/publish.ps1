$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test tests/BdoTimers.Core.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
# The voice model isn't in git; the setup ships it, so fetch it first if this checkout lacks it.
& "$PSScriptRoot/get-voice.ps1"

$stage = 'obj/publish/app'
foreach ($dir in 'obj/publish', 'installer/Msi/bin', 'installer/Bundle/bin') {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}
# Empty publish/ rather than delete it: an Explorer window showing the folder would block deleting the folder itself.
New-Item -ItemType Directory -Force publish | Out-Null
Get-ChildItem publish | Remove-Item -Recurse -Force

# App -> MSI (the package) -> setup window -> bundle (the setup.exe users run)
dotnet publish src/BdoTimers.App -c Release -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
dotnet build installer/Msi/BdoTimers.Installer.wixproj -c Release "-p:AppDir=$(Resolve-Path $stage)"
if ($LASTEXITCODE -ne 0) { throw 'MSI build failed' }
dotnet build installer/SetupUi -c Release
if ($LASTEXITCODE -ne 0) { throw 'Setup window build failed' }
$msi = Get-ChildItem installer/Msi/bin/Release -Recurse -Filter *.msi | Select-Object -First 1
dotnet build installer/Bundle/BdoTimers.Bundle.wixproj -c Release "-p:MsiPath=$($msi.FullName)"
if ($LASTEXITCODE -ne 0) { throw 'Bundle build failed' }

Get-ChildItem installer/Bundle/bin/Release -Recurse -Filter *.exe | Copy-Item -Destination publish/
Write-Host "Setup: $(Resolve-Path publish/*.exe)"
