#Requires -Version 7.4
param(
    [string]$CertificateThumbprint = $env:BDOTIMERS_SIGNING_THUMBPRINT,
    [switch]$AllowUnsigned
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $workspace
try {
    if ($CertificateThumbprint -and $AllowUnsigned) { throw 'Choose signed output or -AllowUnsigned.' }
    if (-not $CertificateThumbprint -and -not $AllowUnsigned) {
        throw 'A trusted signing identity is required. Set BDOTIMERS_SIGNING_THUMBPRINT or use -AllowUnsigned for a local test build.'
    }
    if ($CertificateThumbprint) {
        if ($CertificateThumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'Invalid certificate thumbprint.' }
        $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
        if (-not $certificate.HasPrivateKey -or -not $certificate.Verify() -or $certificate.Issuer -eq $certificate.Subject) {
            throw 'A trusted signing certificate with its private key is required.'
        }
    }
    if ((git rev-parse --is-shallow-repository) -eq 'true') {
        throw 'Publishing needs full Git history. Run git fetch --unshallow first.'
    }
    # 1.0.<number of commits>: rebuilding a commit keeps its version, and each later commit gets a higher one.
    $version = "1.0.$(git rev-list --count HEAD)"
    dotnet test tests/BdoTimers.Core.Tests -c Release
    & "$PSScriptRoot/test-windows.ps1" -Configuration Release
    # The voice model isn't in git; the setup ships it, so fetch it first if this checkout lacks it.
    & "$PSScriptRoot/get-voice.ps1"

    $stage = 'obj/publish/app'
    foreach ($dir in 'obj/publish', 'installer/Msi/bin', 'installer/Bundle/bin') {
        $target = [IO.Path]::GetFullPath((Join-Path $workspace $dir))
        if (-not $target.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Publish cleanup must stay within the workspace.'
        }
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    }
    # Empty publish/ rather than delete it: an Explorer window showing the folder would block deleting the folder itself.
    New-Item -ItemType Directory -Force publish | Out-Null
    $publishDirectory = [IO.Path]::GetFullPath((Join-Path $workspace 'publish'))
    foreach ($item in Get-ChildItem -LiteralPath $publishDirectory) {
        if (-not $item.FullName.StartsWith($publishDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Publish cleanup must stay within publish/.'
        }
        Remove-Item -LiteralPath $item.FullName -Recurse -Force
    }

    # App -> MSI (the package) -> setup window -> bundle (the setup.exe users run)
    dotnet publish src/BdoTimers.App -c Release -o $stage "-p:Version=$version"
    if ($CertificateThumbprint) {
        foreach ($name in 'BdoTimers.exe', 'BdoTimers.dll', 'BdoTimers.Core.dll') {
            & "$PSScriptRoot/sign.ps1" -FilePath (Join-Path $stage $name) -CertificateThumbprint $CertificateThumbprint
        }
    }
    dotnet build installer/Msi/BdoTimers.Installer.wixproj -c Release "-p:Version=$version" "-p:AppDir=$(Resolve-Path $stage)"
    dotnet build installer/SetupUi -c Release "-p:Version=$version"
    $msi = Get-ChildItem installer/Msi/bin/Release -Recurse -Filter *.msi | Select-Object -First 1
    $signing = @()
    if ($CertificateThumbprint) {
        & "$PSScriptRoot/sign.ps1" -FilePath $msi.FullName -CertificateThumbprint $CertificateThumbprint
        & "$PSScriptRoot/sign.ps1" -FilePath 'installer/SetupUi/bin/Release/net48/win-x64/SetupUi.exe' -CertificateThumbprint $CertificateThumbprint
        $signing = @('-p:SignOutput=true', "-p:CertificateThumbprint=$CertificateThumbprint")
    }
    dotnet build installer/Bundle/BdoTimers.Bundle.wixproj -c Release "-p:Version=$version" "-p:MsiPath=$($msi.FullName)" @signing

    Get-ChildItem installer/Bundle/bin/Release -Recurse -Filter *.exe | Copy-Item -Destination publish/
    Get-ChildItem publish -Filter *.exe | ForEach-Object {
        $signature = Get-AuthenticodeSignature -LiteralPath $_.FullName
        if ($CertificateThumbprint -and $signature.Status -ne 'Valid') { throw 'Refusing to publish an unverified signature.' }
        $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $($_.Name)" | Set-Content -LiteralPath ($_.FullName + '.sha256')
    }
    if ($AllowUnsigned) { Write-Warning 'This setup is unsigned and intended for local testing.' }
    Write-Host "Setup: $(Resolve-Path publish/*.exe)"
}
finally {
    Pop-Location
}
