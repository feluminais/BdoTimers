#Requires -Version 7.4
# Fetches the Kokoro v1.0 voice model the app speaks with (int8, Apache 2.0, via sherpa-onnx) and keeps only the English
# parts in src/BdoTimers.App/Voice/kokoro, which git ignores. publish.ps1 runs it; it does nothing when that model is
# already there.
# -Archive uses an already downloaded kokoro-int8-multi-lang-v1_0.tar.bz2 instead of downloading it (132 MB).
# -Destination puts the model in another folder.
param([string]$Archive, [string]$Destination = "$PSScriptRoot/../src/BdoTimers.App/Voice/kokoro")
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
# The progress display slows Invoke-WebRequest down many times over.
$ProgressPreference = 'SilentlyContinue'

$url = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-multi-lang-v1_0.tar.bz2'
$sha256 = '4C3052ABAA60943A341F193888CF6ABD68787DAE6AB8AE5C925A706CAA247E4E'
# Names the archive the model came from. Only a complete model has it, so a folder without it (an interrupted run) or
# with another hash (a new model) is fetched again.
$stamp = 'archive.sha256'
$Destination = [IO.Path]::GetFullPath($Destination, $PWD.Path).TrimEnd('\', '/')
if ((Get-Content (Join-Path $Destination $stamp) -ErrorAction Ignore) -eq $sha256) { Write-Host 'Voice model already present'; return }

# Assembled beside the destination, so the finished model moves into place with one rename on the same drive.
$staging = "$Destination.partial"
$download = Join-Path ([IO.Path]::GetTempPath()) "bdotimers-voice-$PID.tar.bz2"
try {
    if (-not $Archive) {
        Write-Host "Downloading $url"
        Invoke-WebRequest $url -OutFile $download
        $Archive = $download
    }
    if ((Get-FileHash $Archive -Algorithm SHA256).Hash -ne $sha256) { throw "Checksum mismatch: $Archive" }

    Remove-Item $staging -Recurse -Force -ErrorAction Ignore
    New-Item -ItemType Directory $staging | Out-Null
    # The word lists (lexicon-*.txt) are left out: this model reads English through espeak-ng, never the lists.
    # espeak-ng turns English text into phonemes; its English data is about 1 MB of the 18 MB, the rest being other
    # languages' dictionaries.
    $espeak = 'intonations', 'phondata', 'phondata-manifest', 'phonindex', 'phontab', 'en_dict', 'lang', 'voices'
    [string[]]$parts = @('model.int8.onnx', 'voices.bin', 'tokens.txt', 'LICENSE') + $espeak.ForEach({ "espeak-ng-data/$_" })
    # Windows' own tar: a Git or MSYS tar earlier in PATH would read "C:" as a remote host.
    & (Join-Path $env:SystemRoot 'System32\tar.exe') -xjf $Archive -C $staging --strip-components=1 $parts.ForEach({ "kokoro-int8-multi-lang-v1_0/$_" })
    Set-Content (Join-Path $staging $stamp) $sha256 -NoNewline

    if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
    Move-Item $staging $Destination
    Write-Host "Voice model ready in $Destination"
}
finally {
    Remove-Item $download, $staging -Recurse -Force -ErrorAction Ignore
}
