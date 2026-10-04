#Requires -Version 7.4
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
dotnet run --project (Join-Path $workspace 'tools/BdoTimers.SpeechPack') -c Release -- `
    (Join-Path $workspace 'src/BdoTimers.App/Voice/kokoro') (Join-Path $workspace 'src/BdoTimers.App/Voice/speech')
