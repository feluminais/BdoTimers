#Requires -Version 7.4
param(
    [Parameter(Mandatory)][string]$FilePath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{40}$')][string]$CertificateThumbprint,
    [string]$SignToolPath,
    [uri]$TimestampServer = 'https://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
try {
    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) { throw "Missing signing input: $FilePath" }
    if ($TimestampServer.Scheme -ne 'https') { throw 'Timestamp server must use HTTPS.' }
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
    if (-not $certificate.HasPrivateKey -or $certificate.Issuer -eq $certificate.Subject -or -not $certificate.Verify()) {
        throw 'Signing requires an unexpired trusted certificate with its private key.'
    }
    if (-not ($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq '1.3.6.1.5.5.7.3.3' })) {
        throw 'The selected certificate is not valid for code signing.'
    }
    if (-not $SignToolPath) {
        $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if ($command) { $SignToolPath = $command.Source }
        else {
            $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
            $SignToolPath = Get-ChildItem -LiteralPath $sdk -Directory -ErrorAction SilentlyContinue |
                Sort-Object Name -Descending | ForEach-Object { Join-Path $_.FullName 'x64/signtool.exe' } |
                Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
        }
    }
    if (-not $SignToolPath -or -not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) {
        throw 'SignTool was not found. Install the Windows SDK signing tools or provide -SignToolPath.'
    }
    & $SignToolPath sign /sha1 $CertificateThumbprint /s My /fd SHA256 /tr $TimestampServer.AbsoluteUri /td SHA256 $FilePath
    & $SignToolPath verify /pa /all $FilePath
    if ((Get-AuthenticodeSignature -LiteralPath $FilePath).Status -ne 'Valid') {
        throw "Signature verification failed: $FilePath"
    }
}
catch { Write-Error $_; exit 1 }
