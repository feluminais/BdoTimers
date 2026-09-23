$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test tests/BdoTimers.Core.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }

$stage = 'obj/publish/app'
foreach ($dir in 'obj/publish', 'installer/bin', 'publish') { if (Test-Path $dir) { Remove-Item $dir -Recurse -Force } }
dotnet publish src/BdoTimers.App -c Release -r win-x64 --self-contained true -o $stage
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
dotnet build installer/BdoTimers.Installer.wixproj -c Release "-p:AppDir=$(Resolve-Path $stage)"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

New-Item -ItemType Directory publish | Out-Null
Copy-Item installer/bin/Release/*.msi publish/
Write-Host "Installer: $(Resolve-Path publish/*.msi)"
