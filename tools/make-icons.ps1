#Requires -Version 5.1
<#
    从 logo/512x512.png 生成全部应用图标与品牌位素材。

    设计源是整幅品牌图：纯黑底 + 发光像素字标「Ready」，512x512 且完全不透明。
    这里**不裁切**任何像素——只做整幅等比缩放，再用作品自带的黑底把方形画布补满。
    字标在源图里占画布宽 66.6%（墨迹 341x135，约 2.53:1），这是「不裁切」下的上限；
    想让它更大就必须切掉黑边，那是另一件事，不在本脚本的范围内。

    各产物的差别只有两点：画布尺寸，和源图绘制比例 Inset。
    - Inset = 1.000：整幅铺满画布，一个像素都不碰（启动屏、界面品牌位、ICO 全帧）。
    - Inset < 1.000：为 Android 自适应图标准备。字标四角到画布中心的距离是
      0.3596x边长，而各厂商遮罩（圆形/方圆/圆角方）保证可见的区域是半径
      0.3335x边长的圆；把整幅缩到 0.927 之后四角恰好落在这个圆上，所以无论
      哪种遮罩都不会切到字标，空出来的面积由同色黑补满，画面与原图一致。
      这个比例是算出来的，不是拍的；见下面的安全区断言。

    Android 的前景层必须透明底，否则单色层会变成一整块黑饼。所以那个产物会把
    黑底反预乘成 alpha：a = max(R,G,B)，颜色再除以 a 还原。黑底在合成时精确
    复现，辉光一个像素都不丢（源图最外圈最大通道值实测为 0）。

    用法：pwsh -File tools/make-icons.ps1
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# 逐像素的活都放在编译代码里：1024x1024 走 PowerShell 循环要几十秒，C# 是毫秒级。
#
# Add-Type 在 PowerShell 7 上要显式引用 System.Drawing：它的默认引用集里没有这个程序集，
# 只写 Add-Type -AssemblyName System.Drawing 只对 PowerShell 自己的脚本层有效，对这里
# 现编的 C# 不生效（否则报 CS1069 找不到 Bitmap）。5.1 的默认引用集足够，直接给程序集名。
$drawingRefs = @()
foreach ($assemblyName in @('System.Drawing.Common', 'System.Drawing.Primitives')) {
    $located = $null
    try { $located = ([System.Reflection.Assembly]::Load($assemblyName)).Location } catch { $located = $null }
    if ($located -and (Test-Path $located) -and ($drawingRefs -notcontains $located)) {
        $drawingRefs += $located
    }
}

$toolSource = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ReadyIconTool
{
    // 把「黑底上叠加的发光」还原成透明底：alpha = 最大通道，颜色除以 alpha。
    public static Bitmap KeyAgainstBlack(Bitmap source)
    {
        Rectangle rect = new Rectangle(0, 0, source.Width, source.Height);
        Bitmap target = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        BitmapData src = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapData dst = target.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int count = src.Width * src.Height;
            byte[] buffer = new byte[count * 4];
            byte[] output = new byte[count * 4];
            Marshal.Copy(src.Scan0, buffer, 0, buffer.Length);
            for (int i = 0; i < count; i++)
            {
                int o = i * 4;
                int b = buffer[o], g = buffer[o + 1], r = buffer[o + 2];
                int a = Math.Max(r, Math.Max(g, b));
                if (a <= 2) continue;                       // 纯黑：彻底留空
                output[o] = (byte)Math.Min(255, b * 255 / a);
                output[o + 1] = (byte)Math.Min(255, g * 255 / a);
                output[o + 2] = (byte)Math.Min(255, r * 255 / a);
                output[o + 3] = (byte)a;
            }
            Marshal.Copy(output, 0, dst.Scan0, output.Length);
        }
        finally
        {
            source.UnlockBits(src);
            target.UnlockBits(dst);
        }
        return target;
    }

    // 只测量墨迹外框，不改任何像素。返回 {minX, minY, maxX, maxY}。
    public static int[] MeasureInk(Bitmap bitmap, int threshold)
    {
        Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int w = data.Width;
        int h = data.Height;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        try
        {
            byte[] buffer = new byte[w * h * 4];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4;
                    // 用「有效亮度」= max(R,G,B) x alpha / 255。抠黑后的图里，几乎透明
                    // 的边缘像素颜色被反预乘拉得很亮，只看颜色会把透明区当成墨迹；乘上
                    // alpha 才是眼睛真正看到的强度。不透明图 alpha=255，式子退化成原式。
                    int value = Math.Max(buffer[o + 2], Math.Max(buffer[o + 1], buffer[o]));
                    value = value * buffer[o + 3] / 255;
                    if (value <= threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        return new int[] { minX, minY, maxX, maxY };
    }
}
'@

if ($PSVersionTable.PSVersion.Major -ge 6) {
    Add-Type -TypeDefinition $toolSource -ReferencedAssemblies $drawingRefs
}
else {
    Add-Type -TypeDefinition $toolSource -ReferencedAssemblies 'System.Drawing'
}

# Android 自适应图标的可见半径：各厂商遮罩（圆、方圆、圆角方）都保证圆内可见，
# 这个圆半径 = 画布边长的 0.3335（72dp / 108dp / 2）。比例写死在这里是有意的：
# 它是平台契约，不是我们的美学选择。
$SafeRadius = 0.3335

$repoRoot = Split-Path -Parent $PSScriptRoot
$srcPng = Join-Path $repoRoot 'logo/512x512.png'
if (-not (Test-Path $srcPng)) { throw "找不到源图：$srcPng" }

# ---------------------------------------------------------------------------
# 1. 把整幅源图铺进方形画布（唯一允许的变换：等比缩放 + 居中 + 补底色）
# ---------------------------------------------------------------------------
function New-Tile {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Size,
        [double]$Inset,
        [System.Drawing.Color]$Background
    )

    # 「不裁切」在这里落成断言：Inset 大于 1 意味着源图的边角要伸出画布，
    # 那就是裁切。宁可让脚本失败，也不要悄悄产出被切过的图标。
    if ($Inset -le 0 -or $Inset -gt 1) {
        throw "Inset 必须落在 (0,1]，实际是 $Inset。大于 1 就要切掉源图的边角，违背「不裁切」。"
    }

    $canvas = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear($Background)

        $target = [int][Math]::Round($Size * $Inset)
        $target = [Math]::Max(1, [Math]::Min($Size, $target))
        $offset = ($Size - $target) / 2.0

        $destination = New-Object System.Drawing.RectangleF([single]$offset, [single]$offset, [single]$target, [single]$target)
        $whole = New-Object System.Drawing.RectangleF(0, 0, [single]$Source.Width, [single]$Source.Height)
        $graphics.DrawImage($Source, $destination, $whole, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }
    return $canvas
}

# ---------------------------------------------------------------------------
# 2. 安全区：算出「整幅缩到多少，字标四角才刚好压在遮罩圆上」
# ---------------------------------------------------------------------------
function Measure-SafeInset {
    param([System.Drawing.Bitmap]$Source)

    $ink = [ReadyIconTool]::MeasureInk($Source, 24)
    if ($ink[2] -lt 0) { throw '源图里找不到任何墨迹，无法计算安全区。' }

    $center = $Source.Width / 2.0
    # 像素 x 占据 [x, x+1)，中心是 x+0.5；用中心算才是真正的几何距离。
    $dx = [Math]::Max([Math]::Abs($ink[0] + 0.5 - $center), [Math]::Abs($ink[2] + 0.5 - $center))
    $dy = [Math]::Max([Math]::Abs($ink[1] + 0.5 - $center), [Math]::Abs($ink[3] + 0.5 - $center))
    $corner = [Math]::Sqrt($dx * $dx + $dy * $dy) / $Source.Width

    $inset = 1.0
    if ($corner -gt $SafeRadius) {
        # 留 0.5% 余量：画布像素数是整数，目标尺寸要取整，取整方向可能把字标推出
        # 安全圆万分之几（实测 0.9274 时算出 0.3336，比 0.3335 大一点）。这点余量
        # 人眼看不出来，但能让断言变成真的成立，而不是「差不多」。
        $inset = $SafeRadius / $corner * 0.995
    }

    [pscustomobject]@{
        Ink      = $ink
        Corner   = $corner
        Inset    = [Math]::Round($inset, 4)
    }
}

# ---------------------------------------------------------------------------
# 3. 手写 ICO 容器（System.Drawing.Icon.Save 只能存单帧）
# ---------------------------------------------------------------------------
function Write-Ico {
    param([string]$Path, [int[]]$Sizes, [System.Drawing.Bitmap]$Source)

    $frames = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($size in $Sizes) {
        # 每一帧都是整幅原图铺满：不裁、不放大超出画布，所以 9 帧的字标占比一致。
        $tile = New-Tile -Source $Source -Size $size -Inset 1.0 -Background ([System.Drawing.Color]::Black)
        $stream = New-Object System.IO.MemoryStream
        $tile.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames.Add($stream.ToArray())
        $stream.Dispose()
        $tile.Dispose()
    }

    $out = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($out)
    $writer.Write([uint16]0)              # reserved
    $writer.Write([uint16]1)              # type = icon
    $writer.Write([uint16]$Sizes.Count)   # count

    $offset = 6 + 16 * $Sizes.Count
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $size = $Sizes[$i]
        $dim = if ($size -ge 256) { 0 } else { $size }   # 0 表示 256
        $writer.Write([byte]$dim)         # width
        $writer.Write([byte]$dim)         # height
        $writer.Write([byte]0)            # color palette
        $writer.Write([byte]0)            # reserved
        $writer.Write([uint16]1)          # color planes
        $writer.Write([uint16]32)         # bits per pixel
        $writer.Write([uint32]$frames[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
    $writer.Flush()

    [System.IO.File]::WriteAllBytes($Path, $out.ToArray())
    $writer.Dispose()
    $out.Dispose()
}

# ---------------------------------------------------------------------------
# 4. 生成
# ---------------------------------------------------------------------------
$source = [System.Drawing.Bitmap]::FromFile($srcPng)
try {
    $safe = Measure-SafeInset -Source $source
    $inkWidth = $safe.Ink[2] - $safe.Ink[0] + 1
    $inkHeight = $safe.Ink[3] - $safe.Ink[1] + 1

    "源图 logo/512x512.png：$($source.Width)x$($source.Height)"
    "字标墨迹：x=$($safe.Ink[0]) y=$($safe.Ink[1]) w=$inkWidth h=$inkHeight（占画布宽 {0:N1}%）" -f ($inkWidth * 100.0 / $source.Width)
    "字标四角半径：{0:N4}x边长（遮罩保证可见半径 $SafeRadius）→ Android 前景层绘制比例 {1:N4}" -f $safe.Corner, $safe.Inset

    # 抠黑只用于 Android 前景层：那一层必须透明，否则单色层会变成实心黑块。
    $keyed = [ReadyIconTool]::KeyAgainstBlack($source)

    $targets = @(
        @{ Path = 'src/Ready/Resources/AppIcon/appicon.png'; Size = 1024; Inset = $safe.Inset; Keyed = $true  },
        @{ Path = 'src/Ready/Resources/Splash/splash.png';    Size = 512;  Inset = 1.0;         Keyed = $false },
        @{ Path = 'src/Ready/wwwroot/brand/appicon.png';      Size = 256;  Inset = 1.0;         Keyed = $false }
    )

    foreach ($target in $targets) {
        $full = Join-Path $repoRoot $target.Path
        $directory = Split-Path -Parent $full
        if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }

        if ($target.Keyed) {
            $paint = $keyed
            $background = [System.Drawing.Color]::Transparent
        }
        else {
            $paint = $source
            $background = [System.Drawing.Color]::Black
        }

        $tile = New-Tile -Source $paint -Size $target.Size -Inset $target.Inset -Background $background
        try {
            # 自检：墨迹必须完整落在画布内，任何一边贴到 0 或 边长-1 都说明被切了。
            $check = [ReadyIconTool]::MeasureInk($tile, 24)
            $last = $target.Size - 1
            if ($check[0] -le 0 -or $check[1] -le 0 -or $check[2] -ge $last -or $check[3] -ge $last) {
                throw "$($target.Path) 的墨迹边界贴到了画布边缘（x=$($check[0])..$($check[2])，y=$($check[1])..$($check[3])），说明字标被切了。"
            }
            $tile.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
            "已生成 {0}  ({1}x{1}, {2:N0} 字节)  字标占宽 {3:P1}" -f $target.Path, $target.Size, (Get-Item $full).Length, (($check[2] - $check[0] + 1) / $target.Size)
        }
        finally {
            $tile.Dispose()
        }
    }
}
finally {
    if ($null -ne $keyed) { $keyed.Dispose() }
    $source.Dispose()
}

$icoPath = Join-Path $repoRoot 'src/Ready.WinForms/Assets/appicon.ico'
$icoSource = [System.Drawing.Bitmap]::FromFile($srcPng)
$icoSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
try {
    Write-Ico -Path $icoPath -Sizes $icoSizes -Source $icoSource
}
finally {
    $icoSource.Dispose()
}
"已生成 src/Ready.WinForms/Assets/appicon.ico  ({0:N0} 字节, {1} 帧：{2})" -f (Get-Item $icoPath).Length, $icoSizes.Count, ($icoSizes -join '/')

# ---------------------------------------------------------------------------
# 5. 自检：LoadIcon() 走的是 new Icon(path)，必须确认这个 ICO 能被 System.Drawing 读出来
# ---------------------------------------------------------------------------
$check = New-Object System.Drawing.Icon($icoPath)
"ICO 自检：默认取到 $($check.Width)x$($check.Height)"
$check.Dispose()
'完成。'
