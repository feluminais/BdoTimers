# Draws the app icon (gold diamond with an hourglass on black) with WPF and writes a multi-size .ico whose
# entries are PNGs. Run from the repo root with Windows PowerShell: powershell -File scripts/make-icon.ps1
param([string]$Out = (Join-Path $PSScriptRoot '..\src\BdoTimers.App\Assets\app.ico'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

function Brush([string]$hex) { $b = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($hex)); $b.Freeze(); $b }
$black = Brush '#0B0B0C'; $gold = Brush '#B89A5E'; $light = Brush '#E8C98A'
function Geo([string]$path) { [System.Windows.Media.Geometry]::Parse($path) }

# Design space is 64x64; smaller sizes drop detail so the shape stays readable.
function Render([int]$size) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform ($size / 64.0), ($size / 64.0)))
    $dc.DrawRoundedRectangle($black, $null, (New-Object System.Windows.Rect 0, 0, 64, 64), 10, 10)
    if ($size -le 16) {
        $dc.DrawGeometry($gold, $null, (Geo 'M32,5 L59,32 L32,59 L5,32 Z'))
        $dc.DrawGeometry($black, $null, (Geo 'M25,21 L39,21 L32,32 L39,43 L25,43 L32,32 Z'))
    } elseif ($size -le 32) {
        $dc.DrawGeometry($null, (New-Object System.Windows.Media.Pen $gold, 4.5), (Geo 'M32,7 L57,32 L32,57 L7,32 Z'))
        $dc.DrawGeometry($light, $null, (Geo 'M26,21 L38,21 L32,32 L38,43 L26,43 L32,32 Z'))
    } else {
        $dc.DrawGeometry($null, (New-Object System.Windows.Media.Pen $gold, 2.5), (Geo 'M32,8 L56,32 L32,56 L8,32 Z'))
        $dc.DrawGeometry($null, (New-Object System.Windows.Media.Pen $light, 1.6), (Geo 'M32,17 L47,32 L32,47 L17,32 Z'))
        $dc.DrawGeometry($light, $null, (Geo 'M27.5,24 L36.5,24 L32,32 L36.5,40 L27.5,40 L32,32 Z'))
    }
    $dc.Pop(); $dc.Close()
    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $ms = New-Object System.IO.MemoryStream
    $encoder.Save($ms)
    , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @(); foreach ($s in $sizes) { $images += , (Render $s) }

# ICO: 6-byte header, a 16-byte directory entry per image, then the PNG payloads.
$file = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $file
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $Out)).Path + '\' + (Split-Path $Out -Leaf), $file.ToArray())
"wrote $Out ($($file.Length) bytes, sizes $($sizes -join ', '))"
