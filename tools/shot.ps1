# Usage: powershell -ExecutionPolicy Bypass -File tools\shot.ps1 -Out "shots\main.png"
# Locate the window by title or process name and capture a screenshot.
param(
    [string]$Title = "",
    [string]$ProcessName = "JarStart",
    [string]$Out = "shot.png",
    [int]$DelayMs = 0,
    [switch]$Full
)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Make this process DPI aware, otherwise window rect is scaled/virtualized.
$sigDpi = @"
using System;
using System.Runtime.InteropServices;
public class Dpi {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
Add-Type -TypeDefinition $sigDpi -Language CSharp
try { [void][Dpi]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { try { [void][Dpi]::SetProcessDPIAware() } catch {} }

if ($DelayMs -gt 0) { Start-Sleep -Milliseconds $DelayMs }

$sig = @"
using System;
using System.Runtime.InteropServices;
public class Win {
    [DllImport("user32.dll", SetLastError=true)]
    public static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
Add-Type -TypeDefinition $sig -Language CSharp

if ($Full) {
    $area = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $area.Width, $area.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($area.X, $area.Y, 0, 0, $bmp.Size)
    $outPath = [System.IO.Path]::GetFullPath($Out)
    $dir = [System.IO.Path]::GetDirectoryName($outPath)
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SAVED $outPath"
    exit 0
}

$h = [IntPtr]::Zero
if ($Title -ne "") { $h = [Win]::FindWindow($null, $Title) }
if ($h -eq [IntPtr]::Zero -and $ProcessName -ne "") {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    if ($proc) { $h = $proc.MainWindowHandle }
}
if ($h -eq [IntPtr]::Zero) { Write-Output "NOT_FOUND"; exit 1 }

# 窗口已经隐藏（缩到托盘）时不要 ShowWindow，否则会把它又弹出来
if ([Win]::IsWindowVisible($h)) {
    [void][Win]::ShowWindow($h, 5)
    [void][Win]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 400
}

$r = New-Object Win+RECT
[void][Win]::GetWindowRect($h, [ref]$r)
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top
Write-Output "RECT $($r.Left) $($r.Top) $w $hh"

$bmp = New-Object System.Drawing.Bitmap $w, $hh
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [Win]::PrintWindow($h, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)
if (-not $ok) {
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
}
$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
$outPath = [System.IO.Path]::GetFullPath($Out)
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Output "SAVED $outPath"
