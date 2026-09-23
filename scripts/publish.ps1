$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet test tests/BdoTimers.Core.Tests -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
dotnet publish src/BdoTimers.App -c Release -r win-x64 --self-contained true -o publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Write-Host "Published to $(Resolve-Path publish)"
