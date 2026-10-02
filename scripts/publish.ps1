#Requires -Version 7.4
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if ((git rev-parse --is-shallow-repository) -eq 'true') {
        throw 'Publishing needs full Git history. Run git fetch --unshallow first.'
    }
    # 1.0.<number of commits>: rebuilding a commit keeps its version, and each later commit gets a higher one.
    $version = "1.0.$(git rev-list --count HEAD)"
    dotnet test tests/BdoTimers.Core.Tests -c Release
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
    dotnet publish src/BdoTimers.App -c Release -o $stage "-p:Version=$version"
    dotnet build installer/Msi/BdoTimers.Installer.wixproj -c Release "-p:Version=$version" "-p:AppDir=$(Resolve-Path $stage)"
    dotnet build installer/SetupUi -c Release "-p:Version=$version"
    $msi = Get-ChildItem installer/Msi/bin/Release -Recurse -Filter *.msi | Select-Object -First 1
    dotnet build installer/Bundle/BdoTimers.Bundle.wixproj -c Release "-p:Version=$version" "-p:MsiPath=$($msi.FullName)"

    Get-ChildItem installer/Bundle/bin/Release -Recurse -Filter *.exe | Copy-Item -Destination publish/
    Write-Host "Setup: $(Resolve-Path publish/*.exe)"
}
finally {
    Pop-Location
}
