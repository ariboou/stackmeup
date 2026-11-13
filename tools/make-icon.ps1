param([string]$OutDir = (Join-Path $PSScriptRoot '..\src\assets'))
# StackMeUp ikonu: turuncu, parlak yuvarlak kare üstünde üst üste duran üç yarı saydam katman (izometrik).
# Çalıştırma: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function C([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
function P([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Poly($pts) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddPolygon([System.Drawing.PointF[]]$pts)
    return $p
}

# Köşeleri yuvarlatmak için: şekli doldur + aynı renkte kalın, yuvarlak birleşimli kenar çiz
function FillRound($g, $path, $brush, [float]$round) {
    $g.FillPath($brush, $path)
    $pen = New-Object System.Drawing.Pen $brush, $round
    $pen.LineJoin = 'Round'
    $g.DrawPath($pen, $path)
}

# Tek katman: üst yüz (eşkenar dörtgen) + kalınlık (sol ve sağ ön yüzler)
function Draw-Layer($g, [float]$cy, $topA, $topB, $left, $right, $edge, $shadowAlpha) {
    $cx = 128; $hw = 72; $hh = 39; $t = 13; $round = 14

    # Alttaki katmana düşen yumuşak gölge
    for ($i = 3; $i -ge 1; $i--) {
        $off = 6 + $i * 3
        $sh = Poly @((P ($cx - $hw) ($cy + $off)), (P $cx ($cy - $hh + $off)), (P ($cx + $hw) ($cy + $off)), (P $cx ($cy + $hh + $off)))
        FillRound $g $sh (New-Object System.Drawing.SolidBrush (C ([int]($shadowAlpha / $i)) 120 30 0)) ($round + $i * 6)
    }

    # Kalınlık: sol ön ve sağ ön yüz
    $lf = Poly @((P ($cx - $hw) $cy), (P $cx ($cy + $hh)), (P $cx ($cy + $hh + $t)), (P ($cx - $hw) ($cy + $t)))
    $rf = Poly @((P $cx ($cy + $hh)), (P ($cx + $hw) $cy), (P ($cx + $hw) ($cy + $t)), (P $cx ($cy + $hh + $t)))
    FillRound $g $lf (New-Object System.Drawing.SolidBrush $left) $round
    FillRound $g $rf (New-Object System.Drawing.SolidBrush $right) $round

    # Üst yüz: soldan sağa açılan cam gradyanı
    $top = Poly @((P ($cx - $hw) $cy), (P $cx ($cy - $hh)), (P ($cx + $hw) $cy), (P $cx ($cy + $hh)))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush((P ($cx - $hw) ($cy - $hh)), (P ($cx + $hw) ($cy + $hh)), $topA, $topB)
    FillRound $g $top $grad $round

    # İnce parlak kenar (cam hissi)
    $pen = New-Object System.Drawing.Pen $edge, 2.2
    $pen.LineJoin = 'Round'
    $hl = Poly @((P ($cx - $hw - 5) $cy), (P $cx ($cy - $hh - 5)), (P ($cx + $hw + 5) $cy), (P $cx ($cy + $hh + 5)))
    $g.DrawPath($pen, $hl)
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.CompositingQuality = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)

    # Arka plan: sıcak turuncu gradyan
    $bg = RoundRect 8 8 240 240 56
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush((P 20 8), (P 236 248), (C 255 255 178 64), (C 255 245 84 18))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @((C 255 255 182 72), (C 255 252 116 28), (C 255 206 58 8))
    $blend.Positions = @(0.0, 0.5, 1.0)
    $grad.InterpolationColors = $blend
    $g.FillPath($grad, $bg)

    # Üstte yumuşak parlama
    $g.SetClip($bg)
    $gloss = New-Object System.Drawing.Drawing2D.GraphicsPath
    $gloss.AddEllipse(-40, -150, 336, 290)
    $glossBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush((P 0 0), (P 0 140), (C 70 255 255 255), (C 0 255 255 255))
    $g.FillPath($glossBrush, $gloss)
    $g.ResetClip()

    # İnce iç çerçeve
    $rim = New-Object System.Drawing.Pen (C 70 255 255 255), 3
    $g.DrawPath($rim, (RoundRect 10 10 236 236 54))

    # Katmanlar: alttan üste, yukarı çıktıkça daha açık ve beyaz
    Draw-Layer $g 166 (C 238 255 198 140) (C 238 255 146 74) (C 248 226 92 26) (C 248 196 66 12) (C 170 255 236 214) 95
    Draw-Layer $g 128 (C 244 255 228 192) (C 244 255 178 112) (C 250 242 132 58) (C 250 216 100 34) (C 200 255 246 232) 100
    Draw-Layer $g 90  (C 252 255 255 255) (C 252 255 224 188) (C 252 255 200 146) (C 252 246 164 102) (C 235 255 255 255) 105

    $g.Dispose()
    return $bmp
}

# ICO: 256 px PNG olarak, küçük boyutlar 32-bit DIB olarak
function Get-DibBytes($bmp) {
    $size = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint32]40); $bw.Write([int32]$size); $bw.Write([int32]($size * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]0)
    $bw.Write([uint32]0); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([uint32]0); $bw.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $bw.Write((New-Object byte[] ($maskStride * $size)))
    $bw.Flush()
    return , $ms.ToArray()
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = @()
foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    if ($s -eq 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $images += , $ms.ToArray()
        $bmp.Save((Join-Path $OutDir 'StackMeUp-256.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        $images += , (Get-DibBytes $bmp)
    }
    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write([byte[]]$img, 0, $img.Length) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $OutDir 'StackMeUp.ico'), $out.ToArray())
"ok: $((Get-Item (Join-Path $OutDir 'StackMeUp.ico')).Length) bayt"
