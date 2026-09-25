# Fetches the Kokoro v1.0 voice model the app speaks with (int8, Apache 2.0, via sherpa-onnx) and keeps only the English
# parts in src/BdoTimers.App/Voice/kokoro, which git ignores. publish.ps1 runs it when the model is missing.
# -Archive uses an already downloaded kokoro-int8-multi-lang-v1_0.tar.bz2 instead of downloading it (132 MB).
param([string]$Archive)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$url = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-multi-lang-v1_0.tar.bz2'
$sha256 = '4C3052ABAA60943A341F193888CF6ABD68787DAE6AB8AE5C925A706CAA247E4E'
$dest = 'src/BdoTimers.App/Voice/kokoro'
if (Test-Path "$dest/model.int8.onnx") { Write-Host 'Voice model already present'; return }

$work = Join-Path ([IO.Path]::GetTempPath()) "bdotimers-voice-$PID"
New-Item -ItemType Directory -Force $work | Out-Null
try {
    if (-not $Archive) {
        $Archive = Join-Path $work 'kokoro.tar.bz2'
        Write-Host "Downloading $url"
        Invoke-WebRequest $url -OutFile $Archive
    }
    if ((Get-FileHash $Archive -Algorithm SHA256).Hash -ne $sha256) { throw "Checksum mismatch: $Archive" }
    # Windows' own tar: a Git or MSYS tar earlier in PATH would read "C:" as a remote host.
    & (Join-Path $env:SystemRoot 'System32\tar.exe') -xjf $Archive -C $work
    if ($LASTEXITCODE -ne 0) { throw 'Extracting the voice model failed' }
    $src = Join-Path $work 'kokoro-int8-multi-lang-v1_0'

    New-Item -ItemType Directory -Force "$dest/espeak-ng-data" | Out-Null
    # The word lists (lexicon-*.txt) are left out: this model reads English through espeak-ng, never the lists.
    foreach ($file in 'model.int8.onnx', 'voices.bin', 'tokens.txt', 'LICENSE') {
        Copy-Item (Join-Path $src $file) $dest
    }
    # espeak-ng turns English text into phonemes; its English data is about 1 MB of the 18 MB, the rest being other
    # languages' dictionaries.
    foreach ($item in 'intonations', 'phondata', 'phondata-manifest', 'phonindex', 'phontab', 'en_dict', 'lang', 'voices') {
        Copy-Item (Join-Path $src "espeak-ng-data/$item") "$dest/espeak-ng-data/" -Recurse
    }
    Write-Host "Voice model ready in $dest"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
