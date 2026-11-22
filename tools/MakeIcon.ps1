# Genera Assets\app.ico (orologio con spunta verde su fondo blu) in più risoluzioni, con immagini PNG incorporate.
# Il disegno è su una griglia 64×64; fino a 24 px i tratti sono più spessi e la spunta diventa un punto verde.
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\Assets\app.ico"),
    [string]$PreviewDir  # facoltativo: salva anche i PNG per controllarli
)

Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $size / 64.0
    $g.ScaleTransform($k, $k)
    $small = $size -le 24

    $blue = [System.Drawing.Color]::FromArgb(255, 59, 130, 246)   # #3B82F6, accento dell'app
    $green = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)   # #22C55E
    $round = [System.Drawing.Drawing2D.LineCap]::Round

    # Fondo: quadrato arrotondato
    $r = 14.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc(64 - 2 * $r, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc(64 - 2 * $r, 64 - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, 64 - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $blue), $path)

    # Quadrante
    $ring = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $(if ($small) { 7 } else { 5 })
    $g.DrawEllipse($ring, 13, 15, 34, 34)

    # Lancette
    $hands = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $(if ($small) { 6 } else { 4 })
    $hands.StartCap = $hands.EndCap = $round
    $hands.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLines($hands, [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF 30, 22), (New-Object System.Drawing.PointF 30, 32), (New-Object System.Drawing.PointF 37, 36)))

    # Spunta verde in basso a destra
    if ($small) {
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $green), 33, 33, 26, 26)
    }
    else {
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $blue), 33.5, 33.5, 25, 25)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $green), 35, 35, 22, 22)
        $check = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 3.5
        $check.StartCap = $check.EndCap = $round
        $check.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawLines($check, [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF 41, 46), (New-Object System.Drawing.PointF 45, 50), (New-Object System.Drawing.PointF 51, 42)))
    }
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($PreviewDir) { $bmp.Save((Join-Path $PreviewDir "icona-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    return , $ms.ToArray()
}

if ($PreviewDir) { New-Item -ItemType Directory -Force $PreviewDir | Out-Null }
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
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
