# Genera Assets\app.ico (orologio stilizzato) in più risoluzioni, con immagini PNG incorporate.
param([string]$OutFile = (Join-Path $PSScriptRoot "..\Assets\app.ico"))

Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $blue = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $dark = [System.Drawing.Color]::FromArgb(255, 30, 41, 59)
    $m = $size * 0.04
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $blue), $m, $m, $size - 2 * $m, $size - 2 * $m)
    $i = $size * 0.15
    $g.FillEllipse([System.Drawing.Brushes]::White, $i, $i, $size - 2 * $i, $size - 2 * $i)

    $c = $size / 2
    $pen = New-Object System.Drawing.Pen $dark, ([Math]::Max(1.5, $size * 0.08))
    $pen.StartCap = $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, $c, $c, $c, $size * 0.26)
    $g.DrawLine($pen, $c, $c, $size * 0.68, $c)
    $r = $size * 0.06
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $blue), $c - $r, $c - $r, 2 * $r, 2 * $r)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) { , (New-IconPng $s) }

New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$fs = [System.IO.File]::Create($OutFile)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($k = 0; $k -lt $sizes.Count; $k++) {
    $dim = if ($sizes[$k] -ge 256) { 0 } else { $sizes[$k] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$k].Length); $w.Write([UInt32]$offset)
    $offset += $images[$k].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Dispose()
Write-Host "Icona creata: $OutFile"
