# 界面自动化自检：
#   - 程序窗口开到屏幕外（配置里的 WinX 用负数），你完全看不到它
#   - 全程用 PostMessage 发消息，不移动真实鼠标、不抢焦点
# 用法: powershell -ExecutionPolicy Bypass -File tools\uitest.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'JarStart.exe'
$dll = Join-Path $root 'tools\IsolatedUi.dll'
$shots = Join-Path $root 'shots'

Add-Type -Path $dll

$script:failed = 0
function Check($ok, $name, $detail) {
    if ($ok) { Write-Output "  [OK] $name" }
    else { $script:failed++; Write-Output "  [FAIL] $name $(if($detail){'-> '+$detail})" }
}

# 窗口开到屏幕外，避免打扰用户
function New-TestConfig($dir) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir | Out-Null
    $cfg = @'
{
  "Version":"1",
  "Items":[{"Id":"jar-fw","Name":"fw-server-sj.20250520-SNAPSHOT","JarPath":"D:\\myData\\JarStart\\tests\\hello.jar","Group":"生产环境","JavaPath":"","JvmArgs":"-Xms512m","ProgramArgs":"","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"","AutoStart":false}],
  "Groups":["生产环境"],
  "Theme":"light","Accent":"","LogFontFamily":"Cascadia Mono","LogFontSize":9.5,"AutoScroll":true,
  "DefaultJavaPath":"","MaxLogLines":3000,"StopAllOnExit":true,"MinimizeToTray":true,"TrayHintShown":true,
  "LastSelectedId":"jar-fw","WinX":-2600,"WinY":-1200,"WinW":1100,"WinH":700
}
'@
    [System.IO.File]::WriteAllText((Join-Path $dir 'config.json'), $cfg, (New-Object System.Text.UTF8Encoding($false)))
}

function Find-Window($ui, $pattern) {
    foreach ($w in $ui.TopWindows()) { if ($w.Title -like $pattern) { return $w } }
    return $null
}

function Find-Child($ui, $parent, $title) {
    foreach ($c in $ui.Children($parent)) { if ($c.Title -like $title) { return $c } }
    return $null
}

Write-Output "== 界面自检（窗口在屏幕外，全程用消息驱动，不动你的鼠标） =="

$dir = Join-Path $env:TEMP 'jarstart-uitest'
New-TestConfig $dir
[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $dir, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', $null, 'Process')

$ui = New-Object IsolatedUi.IsolatedUi($exe, '')
Start-Sleep -Seconds 4
$main = Find-Window $ui '*JarStart*'
Check ($null -ne $main) '主窗口已创建' $ui.LastError
if ($main) {
    Write-Output "        窗口位置 $($main.X),$($main.Y) 大小 $($main.Width)x$($main.Height)"
    $ui.Capture($main.Handle, (Join-Path $shots '80-isolated.png'))

    # 点"启动"
    $btn = Find-Child $ui $main.Handle '启动'
    Check ($null -ne $btn) '找到"启动"按钮' ''
    if ($btn) {
        $ui.Click($btn.Handle, [int]($btn.Width / 2), [int]($btn.Height / 2))
        Start-Sleep -Seconds 3
        $status = Find-Child $ui $main.Handle '运行中*'
        Check ($null -ne $status) 'jar 启动后状态变为运行中' ''
        if ($status) { Write-Output "        状态文字: $($status.Title)" }
        $ui.Capture($main.Handle, (Join-Path $shots '81-isolated-run.png'))
    }

    # 点标题栏右上角的关闭按钮（自绘图标没有文字，按位置找最靠右的那个）
    $close = $null
    foreach ($c in $ui.Children($main.Handle)) {
        if ($c.Y -lt ($main.Y + 90) -and $c.Width -gt 20 -and $c.Width -lt 90 -and $c.Height -lt 90) {
            if ($null -eq $close -or $c.X -gt $close.X) { $close = $c }
        }
    }
    Check ($null -ne $close) '找到标题栏关闭按钮' ''
    if ($close) {
        $ui.Click($close.Handle, [int]($close.Width / 2), [int]($close.Height / 2))
        Start-Sleep -Seconds 2
        $after = Find-Window $ui '*JarStart*'
        Check ($null -ne $after -and -not $after.Visible) '点关闭后窗口隐藏（缩到托盘）' ''
        Check ($null -ne (Get-Process -Id $ui.ProcessId -ErrorAction SilentlyContinue)) '程序仍在后台运行' ''
    }
}
$ui.Dispose()

# ————————————————————— 场景二：托盘菜单的"显示/退出" —————————————————————
Start-Sleep -Seconds 1
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', '1', 'Process')
$ui2 = New-Object IsolatedUi.IsolatedUi($exe, '')
Start-Sleep -Seconds 5

# 让程序把托盘菜单弹出来（自定义消息通道）
$main2 = Find-Window $ui2 '*JarStart*'
if ($main2) { $ui2.Message($main2.Handle, 0x8001) }
Start-Sleep -Seconds 1

$menu = $null
foreach ($w in $ui2.TopWindows()) {
    if ($w.ClassName -like '*Window.8*' -and $w.Visible) { $menu = $w }
}
if (-not $menu) {
    Write-Output "        进程窗口列表:"
    foreach ($w in $ui2.TopWindows()) { Write-Output "          $($w.Title) | $($w.ClassName) | vis=$($w.Visible)" }
}
Check ($null -ne $menu) '托盘菜单已弹出' ''
if ($menu) {
    # End + Enter = 最后一项"退出"
    $ui2.Key($menu.Handle, 0x23)   # End
    $ui2.Key($menu.Handle, 0x0D)   # Enter
    Start-Sleep -Seconds 3
    $alive = $null -ne (Get-Process -Id $ui2.ProcessId -ErrorAction SilentlyContinue)
    Check (-not $alive) '托盘菜单"退出"能真正退出程序' ''
}
$ui2.Dispose()

[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', $null, 'Process')
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue

Write-Output ""
if ($script:failed -eq 0) { Write-Output "界面自检全部通过 ✅" } else { Write-Output "失败 $script:failed 项 ❌"; exit 1 }
