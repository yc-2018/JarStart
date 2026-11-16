# 从 assets/app.svg 生成多尺寸 assets/app.ico
# 用系统自带 Edge 的 headless 模式栅格化（不用装任何东西），ICO 也在 PowerShell 里拼
#
# 用法: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
param(
    [string]$Svg = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\app.svg'),
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\app.ico'),
    [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
)

$ErrorActionPreference = 'Stop'

# —— 找浏览器 ——
$browser = $null
$cands = @(
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
)
foreach ($c in $cands) { if (Test-Path $c) { $browser = $c; break } }
if (-not $browser) { Write-Host '[失败] 没找到 Edge 或 Chrome' -ForegroundColor Red; exit 1 }
Write-Host "用浏览器: $browser" -ForegroundColor Cyan

$tmp = Join-Path $env:TEMP ("jarstart-icon-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp -Force | Out-Null

try {
    $svgText = Get-Content $Svg -Raw -Encoding UTF8
    # 只取 <svg> 元素本身，去掉 XML 声明和 DOCTYPE
    $m = [regex]::Match($svgText, '<svg[\s\S]*</svg>')
    if (-not $m.Success) { Write-Host '[失败] SVG 里找不到 <svg> 元素' -ForegroundColor Red; exit 1 }
    $inner = $m.Value

    $entries = @()
    foreach ($s in $Sizes) {
        $html = Join-Path $tmp "icon-$s.html"
        @"
<!doctype html><html><head><meta charset="utf-8"><style>
html,body{margin:0;padding:0;background:transparent;overflow:hidden}
svg{display:block;width:${s}px;height:${s}px}
</style></head><body>$inner</body></html>
"@ | Set-Content -Path $html -Encoding UTF8

        $png = Join-Path $tmp "icon-$s.png"
        $uri = 'file:///' + ($html -replace '\\', '/')
        $err = Join-Path $tmp "edge-$s.log"
        # 用独立 profile，别碰你自己的 Edge；stderr 重定向到文件，避免被当成错误
        $args = @(
            '--headless=new', '--disable-gpu', '--hide-scrollbars',
            '--default-background-color=00000000',
            '--no-first-run', '--no-default-browser-check', '--disable-extensions',
            "--user-data-dir=$tmp\profile",
            "--window-size=$s,$s",
            "--screenshot=$png",
            $uri
        )
        $proc = Start-Process -FilePath $browser -ArgumentList $args -Wait -PassThru -NoNewWindow `
            -RedirectStandardError $err -RedirectStandardOutput (Join-Path $tmp "edge-$s.out")

        # 等文件出现（headless 是异步落盘的）
        for ($i = 0; $i -lt 40 -and -not (Test-Path $png); $i++) { Start-Sleep -Milliseconds 150 }
        if (-not (Test-Path $png)) { throw "生成 $s x $s 的 PNG 失败" }

        $bytes = [System.IO.File]::ReadAllBytes($png)
        # 确认是 PNG
        if ($bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50) { throw "$s 的截图不是 PNG" }
        $entries += ,@{ Size = $s; Data = $bytes }
        Write-Host ("  {0}x{0}  {1} 字节" -f $s, $bytes.Length)
    }

    # —— 拼 ICO（目录项 + PNG 数据，Vista+ 支持 PNG 压缩的 ICO）——
    $count = $entries.Count
    $header = New-Object byte[] 6
    [BitConverter]::GetBytes([uint16]0).CopyTo($header, 0)
    [BitConverter]::GetBytes([uint16]1).CopyTo($header, 2)
    [BitConverter]::GetBytes([uint16]$count).CopyTo($header, 4)

    $dir = New-Object byte[] ($count * 16)
    $offset = 6 + $count * 16
    for ($i = 0; $i -lt $count; $i++) {
        $e = $entries[$i]
        $o = $i * 16
        $dir[$o] = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
        $dir[$o + 1] = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
        $dir[$o + 2] = 0
        $dir[$o + 3] = 0
        [BitConverter]::GetBytes([uint16]1).CopyTo($dir, $o + 4)
        [BitConverter]::GetBytes([uint16]32).CopyTo($dir, $o + 6)
        [BitConverter]::GetBytes([uint32]$e.Data.Length).CopyTo($dir, $o + 8)
        [BitConverter]::GetBytes([uint32]$offset).CopyTo($dir, $o + 12)
        $offset += $e.Data.Length
    }

    $ms = New-Object System.IO.MemoryStream
    $ms.Write($header, 0, $header.Length)
    $ms.Write($dir, 0, $dir.Length)
    foreach ($e in $entries) { $ms.Write($e.Data, 0, $e.Data.Length) }
    [System.IO.File]::WriteAllBytes($Out, $ms.ToArray())
    $ms.Dispose()

    Write-Host ''
    Write-Host ('[完成] ' + $Out + '  (' + (Get-Item $Out).Length + ' 字节, ' + $count + ' 个尺寸)') -ForegroundColor Green
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
