# 生成 README 用的界面截图（浅色 / 深色），窗口在屏幕外，不影响你的桌面
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'JarStart.exe'
$shots = Join-Path $root 'shots'

Add-Type -Path (Join-Path $root 'tools\IsolatedUi.dll')
Add-Type @"
using System; using System.Runtime.InteropServices;
public class DpiShot {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
}
"@
try { [void][DpiShot]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }

$dir = Join-Path $env:TEMP 'jarstart-shot'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Path $dir | Out-Null

function New-Cfg($theme) {
    $cfg = @"
{
  "Version":"1",
  "Items":[{"Id":"a","Name":"fw-server-sj.20250520-SNAPSHOT","JarPath":"D:\\myData\\JarStart\\tests\\hello.jar","Group":"生产环境","JavaPath":"","JvmArgs":"-Xms512m -Xmx1g","ProgramArgs":"--spring.profiles.active=dev","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"","AutoStart":false},
           {"Id":"b","Name":"wms-server","JarPath":"D:\\myData\\JarStart\\tests\\hello.jar","Group":"生产环境","JavaPath":"","JvmArgs":"","ProgramArgs":"","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"8081","AutoStart":false},
           {"Id":"c","Name":"task-scheduler","JarPath":"D:\\myData\\JarStart\\tests\\hello.jar","Group":"测试环境","JavaPath":"","JvmArgs":"","ProgramArgs":"","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"","AutoStart":false}],
  "Groups":["生产环境","测试环境"],
  "Theme":"$theme","Accent":"","LogFontFamily":"Cascadia Mono","LogFontSize":9.5,"AutoScroll":true,
  "DefaultJavaPath":"","MaxLogLines":3000,"StopAllOnExit":true,"MinimizeToTray":true,"TrayHintShown":true,
  "LastSelectedId":"a","WinX":-2600,"WinY":-1200,"WinW":1180,"WinH":720
}
"@
    [System.IO.File]::WriteAllText((Join-Path $dir 'config.json'), $cfg, (New-Object System.Text.UTF8Encoding($false)))
}

function Find-Window($ui) {
    foreach ($w in $ui.TopWindows()) {
        if ($w.Title -like '*JarStart*' -and $w.Width -gt 200 -and $w.Height -gt 200 `
            -and $w.ClassName -like 'WindowsForms10.Window.20008*') { return $w }
    }
    return $null
}

function Capture-Theme($theme, $outName) {
    New-Cfg $theme
    [System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $dir, 'Process')
    [System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', '1', 'Process')
    [System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', '1', 'Process')
    [System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', '1', 'Process')

    $ui = New-Object IsolatedUi.IsolatedUi($exe, '')
    Start-Sleep -Seconds 4
    $main = Find-Window $ui
    if (-not $main) { $ui.Dispose(); throw "窗口没起来" }

    # 启动第一个 jar，让日志有内容、状态变成运行中
    foreach ($c in $ui.Children($main.Handle)) {
        if ($c.Title -eq '启动') {
            $ui.Click($c.Handle, [int]($c.Width / 2), [int]($c.Height / 2))
            break
        }
    }
    Start-Sleep -Seconds 5

    # 离屏窗口有时还没重绘，先截一次触发重绘，再截一次拿内容
    $out = Join-Path $shots $outName
    $ui.Capture($main.Handle, $out)
    Start-Sleep -Milliseconds 600
    $ui.Capture($main.Handle, $out)
    Write-Output "已生成 $outName"
    $ui.Dispose()
    Start-Sleep -Milliseconds 800
}

Capture-Theme 'light' '30-light.png'
Capture-Theme 'dark'  '31-dark.png'

[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', $null, 'Process')
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host '[完成]' -ForegroundColor Green
