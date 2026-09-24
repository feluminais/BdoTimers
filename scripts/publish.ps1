$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test tests/BdoTimers.Core.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

$stage = 'obj/publish/app'
foreach ($dir in 'obj/publish', 'installer/Msi/bin', 'installer/Bundle/bin', 'publish') {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}

# App -> MSI (the package) -> setup window -> bundle (the setup.exe users run)
dotnet publish src/BdoTimers.App -c Release -r win-x64 --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
dotnet build installer/Msi/BdoTimers.Installer.wixproj -c Release "-p:AppDir=$(Resolve-Path $stage)"
if ($LASTEXITCODE -ne 0) { throw 'MSI build failed' }
dotnet build installer/SetupUi -c Release
if ($LASTEXITCODE -ne 0) { throw 'Setup window build failed' }
$msi = Get-ChildItem installer/Msi/bin/Release -Recurse -Filter *.msi | Select-Object -First 1
dotnet build installer/Bundle/BdoTimers.Bundle.wixproj -c Release "-p:MsiPath=$($msi.FullName)"
if ($LASTEXITCODE -ne 0) { throw 'Bundle build failed' }

New-Item -ItemType Directory publish | Out-Null
Get-ChildItem installer/Bundle/bin/Release -Recurse -Filter *.exe | Copy-Item -Destination publish/
Write-Host "Setup: $(Resolve-Path publish/*.exe)"
