# 用真实 jar 做端到端验证：状态是不是"启动中"→"运行中"，端口能不能显示出来
# 用法: powershell -ExecutionPolicy Bypass -File tools\uitest-real.ps1
# 环境变量 JARREAL_JAR / JARREAL_JAVA 可以指定要测的 jar 和 java
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'JarStart.exe'
$dll = Join-Path $root 'tools\IsolatedUi.dll'
$shots = Join-Path $root 'shots'

$jar = $env:JARREAL_JAR
if (-not $jar) { $jar = 'D:\wms2\fx-framework-master\fw-server\target\fw-server-sj.20250520-SNAPSHOT.jar' }
$java = $env:JARREAL_JAVA
if (-not $java) { $java = 'D:\green\jdks\jdk-17.0.8' }

Add-Type -Path $dll
Add-Type @"
using System; using System.Runtime.InteropServices;
public class DpiAwareness {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
}
"@
try { [void][DpiAwareness]::SetProcessDpiAwarenessContext([IntPtr](-4)) } catch { }

$script:failed = 0
function Check($ok, $name, $detail) {
    if ($ok) { Write-Output "  [OK] $name" }
    else { $script:failed++; Write-Output "  [FAIL] $name $(if($detail){'-> '+$detail})" }
}

Write-Output "== 真实 jar 端到端验证 =="
Write-Output "jar  : $jar"
Write-Output "java : $java"
if (-not (Test-Path $jar)) { Write-Output '[跳过] jar 不存在'; exit 0 }

$dir = Join-Path $env:TEMP 'jarstart-realtest'
if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
New-Item -ItemType Directory -Path $dir | Out-Null
$cfg = @"
{
  "Version":"1",
  "Items":[{"Id":"jar-fw","Name":"fw","JarPath":"$($jar -replace '\\','\\\\')","Group":"生产环境","JavaPath":"$($java -replace '\\','\\\\')","JvmArgs":"-Dfile.encoding=UTF-8","ProgramArgs":"","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"","AutoStart":false}],
  "Groups":["生产环境"],
  "Theme":"light","Accent":"","LogFontFamily":"Cascadia Mono","LogFontSize":9.5,"AutoScroll":true,
  "DefaultJavaPath":"","MaxLogLines":3000,"StopAllOnExit":true,"MinimizeToTray":true,"TrayHintShown":true,
  "LastSelectedId":"jar-fw","WinX":-2600,"WinY":-1200,"WinW":1180,"WinH":720
}
"@
[System.IO.File]::WriteAllText((Join-Path $dir 'config.json'), $cfg, (New-Object System.Text.UTF8Encoding($false)))
[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $dir, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', '1', 'Process')

function Find-Window($ui) {
    foreach ($w in $ui.TopWindows()) {
        if ($w.Title -like '*JarStart*' -and $w.Width -gt 200 -and $w.Height -gt 200 `
            -and $w.ClassName -like 'WindowsForms10.Window.20008*') { return $w }
    }
    return $null
}
function Find-Child($ui, $parent, $title) {
    foreach ($c in $ui.Children($parent)) { if ($c.Title -like $title) { return $c } }
    return $null
}

$ui = New-Object IsolatedUi.IsolatedUi($exe, '')
Start-Sleep -Seconds 4
$main = Find-Window $ui
Check ($null -ne $main) '主窗口已创建' $ui.LastError
if (-not $main) { $ui.Dispose(); exit 1 }

$btn = Find-Child $ui $main.Handle '启动'
Check ($null -ne $btn) '找到"启动"按钮' ''
if ($btn) {
    $ui.Click($btn.Handle, [int]($btn.Width / 2), [int]($btn.Height / 2))

    # 刚起来的一瞬间，应该是"启动中"而不是"运行中"
    Start-Sleep -Seconds 4
    $st = Find-Child $ui $main.Handle '启动中*'
    Check ($null -ne $st) '启动后立刻显示"启动中"' ''
    if ($st) { Write-Output "        状态: $($st.Title)" }
    $ui.Capture($main.Handle, (Join-Path $shots '90-real-starting.png'))

    # 等它真的起来（fw 大概要一分半）
    $ready = $null
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 5
        $st = Find-Child $ui $main.Handle '运行中*'
        if ($st) { $ready = $st; break }
    }
    Check ($null -ne $ready) '等到状态变成"运行中"' ''
    if ($ready) { Write-Output "        状态: $($ready.Title)" }
    $ui.Capture($main.Handle, (Join-Path $shots '91-real-running.png'))
}
$ui.Dispose()

[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', $null, 'Process')
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue

Write-Output ""
if ($script:failed -eq 0) { Write-Output "真实 jar 验证通过 ✅" } else { Write-Output "失败 $script:failed 项 ❌"; exit 1 }
