# Draws the Desk Fences icon and writes a multi-size .ico (PNG entries, Windows Vista and later).
# Two glass fences: a teal one behind, a frosted one in front with a bright top-left rim and a
# 2x2 grid of app tiles. Small sizes (16-24 px) draw a single fence so the grid stays readable.
#
# Run from the repo root:
#   powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
param(
    [string]$Out = "$PSScriptRoot\..\src\DeskFences\Assets\DeskFences.ico",
    [string]$PreviewDir = ""
)

Add-Type -AssemblyName System.Drawing

$TealLight = '#5FD3E0'
$TealDark = '#1C7FA6'

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Color([int]$alpha, [string]$hex) {
    return [System.Drawing.Color]::FromArgb($alpha, [System.Drawing.ColorTranslator]::FromHtml($hex))
}

# Diagonal gradient, top-left to bottom-right.
function New-Gradient([float]$x, [float]$y, [float]$w, [float]$h, $from, $to) {
    $rect = New-Object System.Drawing.RectangleF($x, $y, $w, $h)
    return New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $from, $to, 45.0)
}

# Everything is drawn on a 256 x 256 grid and scaled down.
function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    if ($size -ge 32) {
        # Back fence: teal glass, up and to the left.
        $back = New-RoundedRect 20 30 160 136 28
        $brush = New-Gradient 20 30 160 136 (New-Color 235 $TealLight) (New-Color 235 $TealDark)
        $g.FillPath($brush, $back)
        $brush.Dispose()
        $pen = New-Object System.Drawing.Pen((New-Color 110 '#FFFFFF'), 4)
        $g.DrawPath($pen, $back)
        $pen.Dispose()

        # Front fence: frosted glass with a rim that is bright top-left and fades bottom-right.
        $fx = 76; $fy = 92; $fw = 160; $fh = 140
        $front = New-RoundedRect $fx $fy $fw $fh 28
        $brush = New-Gradient $fx $fy $fw $fh (New-Color 245 '#F4FDFF') (New-Color 230 '#A6E4F0')
        $g.FillPath($brush, $front)
        $brush.Dispose()
        $rimBrush = New-Gradient $fx $fy $fw $fh (New-Color 255 '#FFFFFF') (New-Color 70 '#FFFFFF')
        $rim = New-Object System.Drawing.Pen($rimBrush, 5)
        $g.DrawPath($rim, $front)
        $rim.Dispose()
        $rimBrush.Dispose()

        # The fence's title.
        $title = New-RoundedRect ($fx + 22) ($fy + 16) 64 12 6
        $brush = New-Object System.Drawing.SolidBrush((New-Color 200 $TealDark))
        $g.FillPath($brush, $title)
        $brush.Dispose()

        $tile = 36; $gap = 12; $rows = 2
        $tx = $fx + ($fw - (2 * $tile + $gap)) / 2
        $ty = $fy + 42
        $tileFrom = New-Color 255 '#34C3D6'; $tileTo = New-Color 255 '#14648C'
    }
    else {
        # Small sizes: one teal fence filling the icon, with a white title and one row of
        # white tiles. (A 2x2 grid of squares at 16 px looks like the Windows logo.)
        $fx = 12; $fy = 22; $fw = 232; $fh = 212
        $front = New-RoundedRect $fx $fy $fw $fh 40
        $brush = New-Gradient $fx $fy $fw $fh (New-Color 255 $TealLight) (New-Color 255 $TealDark)
        $g.FillPath($brush, $front)
        $brush.Dispose()

        $title = New-RoundedRect ($fx + 36) ($fy + 34) 110 30 15
        $brush = New-Object System.Drawing.SolidBrush((New-Color 235 '#FFFFFF'))
        $g.FillPath($brush, $title)
        $brush.Dispose()

        $tile = 76; $gap = 28; $rows = 1
        $tx = $fx + ($fw - (2 * $tile + $gap)) / 2
        $ty = $fy + 96
        $tileFrom = New-Color 255 '#FFFFFF'; $tileTo = New-Color 255 '#D2F3F8'
    }

    # App tiles inside the front fence: two per row.
    for ($row = 0; $row -lt $rows; $row++) {
        foreach ($col in 0, 1) {
            $x = $tx + $col * ($tile + $gap)
            $y = $ty + $row * ($tile + $gap)
            $tilePath = New-RoundedRect $x $y $tile $tile ($tile * 0.25)
            $brush = New-Gradient $x $y $tile $tile $tileFrom $tileTo
            $g.FillPath($brush, $tilePath)
            $brush.Dispose()
            $tilePath.Dispose()
        }
    }

    $g.Dispose()
    return $bmp
}

# The sizes Windows asks for (taskbar, Explorer views, Alt+Tab, high DPI).
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 256
$pngs = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs.Add($ms.ToArray())
    if ($PreviewDir) {
        $bmp.Save((Join-Path $PreviewDir "icon-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $bmp.Dispose()
}

# ICO file: 6-byte header, one 16-byte entry per size, then the PNG data.
$outFull = [System.IO.Path]::GetFullPath($Out)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($outFull)) | Out-Null
$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create($outFull))
$writer.Write([uint16]0)              # reserved
$writer.Write([uint16]1)              # type: icon
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }   # 0 means 256
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)            # no palette
    $writer.Write([byte]0)            # reserved
    $writer.Write([uint16]1)          # colour planes
    $writer.Write([uint16]32)         # bits per pixel
    $writer.Write([uint32]$pngs[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) {
    $writer.Write($png)
}
$writer.Close()
Write-Output "Wrote $outFull ($($sizes.Count) sizes)"
