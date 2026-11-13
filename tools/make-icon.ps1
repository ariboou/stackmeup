param(
    # layers | fan | bars | wallet
    [string]$Style = 'layers',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\src\assets'),
    # Yalnızca 256 px önizleme PNG'si üretir (ICO'ya dokunmaz)
    [string]$PreviewPath
)
# StackMeUp ikonu: turuncu, parlak yuvarlak kare üstünde seçilen tarzda çizim.
# Örnek: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 -Style fan
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function C([int]$a, [int]$r, [int]$g, [int]$b) { [System.Drawing.Color]::FromArgb($a, $r, $g, $b) }
function P([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }
function SB($color) { New-Object System.Drawing.SolidBrush $color }

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

# Yumuşak gölge: aynı şekli birkaç kez, artan kaymayla ve azalan opaklıkla çiz
function SoftShadow($g, $path, [int]$alpha, [float]$dy) {
    for ($i = 3; $i -ge 1; $i--) {
        $st = $g.Save()
        $g.TranslateTransform(0, $dy + $i * 2)
        $pen = New-Object System.Drawing.Pen (C ([int]($alpha / $i)) 110 30 0), ($i * 6)
        $pen.LineJoin = 'Round'
        $g.FillPath((SB (C ([int]($alpha / $i)) 110 30 0)), $path)
        $g.DrawPath($pen, $path)
        $g.Restore($st)
    }
}

function Arrow($g, $color, [float]$width, $points) {
    $pen = New-Object System.Drawing.Pen $color, $width
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]$points)
}

# ---------------------------------------------------------------- zemin
function Draw-Background($g) {
    $bg = RoundRect 8 8 240 240 56
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush((P 20 8), (P 236 248), (C 255 255 178 64), (C 255 245 84 18))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @((C 255 255 182 72), (C 255 252 116 28), (C 255 206 58 8))
    $blend.Positions = @(0.0, 0.5, 1.0)
    $grad.InterpolationColors = $blend
    $g.FillPath($grad, $bg)

    $g.SetClip($bg)
    $gloss = New-Object System.Drawing.Drawing2D.GraphicsPath
    $gloss.AddEllipse(-40, -150, 336, 290)
    $g.FillPath((New-Object System.Drawing.Drawing2D.LinearGradientBrush((P 0 0), (P 0 140), (C 70 255 255 255), (C 0 255 255 255))), $gloss)
    $g.ResetClip()

    $g.DrawPath((New-Object System.Drawing.Pen (C 70 255 255 255), 3), (RoundRect 10 10 236 236 54))
}

# ---------------------------------------------------------------- 1) cam katmanlar
function Draw-Layer($g, [float]$cy, $topA, $topB, $left, $right, $edge, $shadowAlpha) {
    $cx = 128; $hw = 72; $hh = 39; $t = 13; $round = 14
    for ($i = 3; $i -ge 1; $i--) {
        $off = 6 + $i * 3
        $sh = Poly @((P ($cx - $hw) ($cy + $off)), (P $cx ($cy - $hh + $off)), (P ($cx + $hw) ($cy + $off)), (P $cx ($cy + $hh + $off)))
        FillRound $g $sh (SB (C ([int]($shadowAlpha / $i)) 120 30 0)) ($round + $i * 6)
    }
    $lf = Poly @((P ($cx - $hw) $cy), (P $cx ($cy + $hh)), (P $cx ($cy + $hh + $t)), (P ($cx - $hw) ($cy + $t)))
    $rf = Poly @((P $cx ($cy + $hh)), (P ($cx + $hw) $cy), (P ($cx + $hw) ($cy + $t)), (P $cx ($cy + $hh + $t)))
    FillRound $g $lf (SB $left) $round
    FillRound $g $rf (SB $right) $round
    $top = Poly @((P ($cx - $hw) $cy), (P $cx ($cy - $hh)), (P ($cx + $hw) $cy), (P $cx ($cy + $hh)))
    FillRound $g $top (New-Object System.Drawing.Drawing2D.LinearGradientBrush((P ($cx - $hw) ($cy - $hh)), (P ($cx + $hw) ($cy + $hh)), $topA, $topB)) $round
    $pen = New-Object System.Drawing.Pen $edge, 2.2
    $pen.LineJoin = 'Round'
    $g.DrawPath($pen, (Poly @((P ($cx - $hw - 5) $cy), (P $cx ($cy - $hh - 5)), (P ($cx + $hw + 5) $cy), (P $cx ($cy + $hh + 5)))))
}

function Draw-Layers($g) {
    Draw-Layer $g 166 (C 238 255 198 140) (C 238 255 146 74) (C 248 226 92 26) (C 248 196 66 12) (C 170 255 236 214) 95
    Draw-Layer $g 128 (C 244 255 228 192) (C 244 255 178 112) (C 250 242 132 58) (C 250 216 100 34) (C 200 255 246 232) 100
    Draw-Layer $g 90  (C 252 255 255 255) (C 252 255 224 188) (C 252 255 200 146) (C 252 246 164 102) (C 235 255 255 255) 105
}

# ---------------------------------------------------------------- 2) yelpaze kartlar + ok
function Draw-Card($g, [float]$angle, [float]$dx, $fill) {
    $st = $g.Save()
    $g.TranslateTransform(128 + $dx, 150)
    $g.RotateTransform($angle)
    $card = RoundRect -58 -78 116 136 20
    SoftShadow $g $card 70 4
    $g.FillPath((SB $fill), $card)
    $g.DrawPath((New-Object System.Drawing.Pen (C 120 255 255 255), 2), $card)
    $g.Restore($st)
}

function Draw-Fan($g) {
    Draw-Card $g -17 -18 (C 150 255 236 220)
    Draw-Card $g 13 18 (C 200 255 244 232)
    Draw-Card $g 0 0 (C 255 255 255 255)
    $orange = C 255 248 112 26
    Arrow $g $orange 18 @((P 100 154), (P 128 126), (P 156 154))
    Arrow $g $orange 18 @((P 128 130), (P 128 184))
}

# ---------------------------------------------------------------- 3) basamak: ok + daralan çubuklar
function Draw-Bars($g) {
    $white = C 255 255 255 255
    $bars = @(
        @{ y = 104; w = 112; a = 255 },
        @{ y = 142; w = 148; a = 225 },
        @{ y = 180; w = 184; a = 190 }
    )
    foreach ($b in $bars) {
        $r = RoundRect (128 - $b.w / 2) $b.y $b.w 28 14
        SoftShadow $g $r 55 3
        $g.FillPath((SB (C $b.a 255 255 255)), $r)
    }
    Arrow $g $white 17 @((P 100 84), (P 128 56), (P 156 84))
}

# ---------------------------------------------------------------- 3b) ters basamak: üstte "Up" oku, altta aşağı açılan yığın
# Çubuklar üstten aşağı daralır ve silikleşir: uygulamadaki gibi aşağı "düşen" bir liste hissi.
function Draw-Drop($g, [int]$count) {
    if ($count -eq 4) {
        $arrowY = 46; $bars = @(
            @{ y = 88;  w = 184; a = 255 },
            @{ y = 126; w = 150; a = 225 },
            @{ y = 162; w = 116; a = 180 },
            @{ y = 196; w = 82;  a = 125 })
        $h = 26
    } else {
        $arrowY = 52; $bars = @(
            @{ y = 98;  w = 184; a = 255 },
            @{ y = 142; w = 142; a = 215 },
            @{ y = 184; w = 100; a = 160 })
        $h = 30
    }
    foreach ($b in $bars) {
        $r = RoundRect (128 - $b.w / 2) $b.y $b.w $h ($h / 2)
        SoftShadow $g $r ([int](60 * $b.a / 255)) 3
        # Cam dokunuşu: üstten beyaz, alta doğru hafif şeftali
        $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush((P 0 $b.y), (P 0 ($b.y + $h)), (C $b.a 255 255 255), (C $b.a 255 230 210))
        $g.FillPath($fill, $r)
        # Üst kenarda ince parlak çizgi
        $g.SetClip($r)
        $g.DrawLine((New-Object System.Drawing.Pen (C ([int]($b.a * 0.9)) 255 255 255), 2), (128 - $b.w / 2 + $h / 2), ($b.y + 1.5), (128 + $b.w / 2 - $h / 2), ($b.y + 1.5))
        $g.ResetClip()
    }
    $arrow = C 255 255 255 255
    $st = $g.Save()
    $g.TranslateTransform(0, 2)
    Arrow $g (C 60 110 30 0) 17 @((P 100 ($arrowY + 28)), (P 128 $arrowY), (P 156 ($arrowY + 28)))
    $g.Restore($st)
    Arrow $g $arrow 17 @((P 100 ($arrowY + 28)), (P 128 $arrowY), (P 156 ($arrowY + 28)))
}

# ---------------------------------------------------------------- 4) cüzdan kartları
function Draw-Wallet($g) {
    $cards = @(
        @{ y = 50;  fill = (C 150 255 226 200) },
        @{ y = 82;  fill = (C 205 255 238 222) },
        @{ y = 114; fill = (C 255 255 255 255) }
    )
    foreach ($c in $cards) {
        $r = RoundRect 44 $c.y 168 104 20
        SoftShadow $g $r 75 4
        $g.FillPath((SB $c.fill), $r)
        $g.DrawPath((New-Object System.Drawing.Pen (C 110 255 255 255), 2), $r)
    }
    # Öndeki kartın içinde küçük "dosya" çizgileri
    $g.FillPath((SB (C 255 248 112 26)), (RoundRect 66 140 54 14 7))
    $g.FillPath((SB (C 150 248 140 70)), (RoundRect 66 166 112 11 5.5))
    $g.FillPath((SB (C 100 248 140 70)), (RoundRect 66 186 84 11 5.5))
}

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.CompositingQuality = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    Draw-Background $g
    switch ($Style) {
        'fan'    { Draw-Fan $g }
        'bars'   { Draw-Bars $g }
        'drop'   { Draw-Drop $g 3 }
        'drop4'  { Draw-Drop $g 4 }
        'wallet' { Draw-Wallet $g }
        default  { Draw-Layers $g }
    }
    $g.Dispose()
    return $bmp
}

if ($PreviewPath) {
    $bmp = Draw-Icon 256
    $bmp.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return
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
"ok ($Style): $((Get-Item (Join-Path $OutDir 'StackMeUp.ico')).Length) bayt"
