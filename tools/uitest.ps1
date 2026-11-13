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

# 这个脚本自己也要 DPI 感知：否则 GetWindowRect 返回的是虚拟坐标，
# 截图会被裁掉一半、窗口尺寸也会算错。
Add-Type @"
using System; using System.Runtime.InteropServices;
public class DpiAwareness {
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
try { [void][DpiAwareness]::SetProcessDpiAwarenessContext([IntPtr](-4)) }
catch { try { [void][DpiAwareness]::SetProcessDPIAware() } catch { } }

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
  "Items":[{"Id":"jar-fw","Name":"fw-server-sj.20250520-SNAPSHOT","JarPath":"D:\\myData\\JarStart\\tests\\hello.jar","Group":"生产环境","JavaPath":"D:\\green\\jdks\\jdk-17.0.8\\bin","JvmArgs":"-Xms512m","ProgramArgs":"","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"","AutoStart":false}],
  "Groups":["生产环境"],
  "Theme":"light","Accent":"","LogFontFamily":"Cascadia Mono","LogFontSize":9.5,"AutoScroll":true,
  "DefaultJavaPath":"","MaxLogLines":3000,"StopAllOnExit":true,"MinimizeToTray":true,"TrayHintShown":true,
  "LastSelectedId":"jar-fw","WinX":-2600,"WinY":-1200,"WinW":1100,"WinH":700
}
'@
    [System.IO.File]::WriteAllText((Join-Path $dir 'config.json'), $cfg, (New-Object System.Text.UTF8Encoding($false)))
}

function Find-Window($ui, $pattern) {
    foreach ($w in $ui.TopWindows()) {
        # 只认真正的主窗体：类名是 WinForms 主窗口、尺寸够大
        # （"GDI+ Window (JarStart.exe)" 这类辅助窗口标题里也带 JarStart，会误匹配）
        if ($w.Title -like $pattern -and $w.Width -gt 200 -and $w.Height -gt 200 `
            -and $w.ClassName -like 'WindowsForms10.Window.20008*') { return $w }
    }
    return $null
}

function Find-Dialog($ui) {
    # 对话框的窗口类名是 WindowsForms10.Window.8.*（主窗体是 .20008.*）
    foreach ($w in $ui.TopWindows()) {
        if ($w.Visible -and $w.Title -eq '设置' -and $w.ClassName -like 'WindowsForms10.Window.8*') { return $w }
    }
    return $null
}

function Find-Child($ui, $parent, $title) {
    foreach ($c in $ui.Children($parent)) { if ($c.Title -like $title) { return $c } }
    return $null
}

# 标题栏右上角那排自绘按钮（设置 / 最小化 / 关闭），从右往左排
# 注意只取标题栏那一行：日志面板的工具按钮也在窗口上方，宽度和 Y 都要区分开
function Get-TitleButtons($ui, $main) {
    $row = @()
    foreach ($c in $ui.Children($main.Handle)) {
        # 标题栏按钮紧贴窗口顶端（日志面板的工具条在下面 50px 以后，别混进来）
        $inTitleBar = ($c.Y -lt ($main.Y + 40)) -and ($c.Width -gt 30) -and ($c.Width -lt 70)
        if ($inTitleBar) { $row += $c }
    }
    return ($row | Sort-Object X -Descending)
}

Write-Output "== 界面自检（窗口在屏幕外，全程用消息驱动，不动你的鼠标） =="

$dir = Join-Path $env:TEMP 'jarstart-uitest'
New-TestConfig $dir
[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $dir, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', $null, 'Process')

# ————————————————————— 场景一：启动 jar / 点关闭缩到托盘 —————————————————————
$ui = New-Object IsolatedUi.IsolatedUi($exe, '')
Start-Sleep -Seconds 4
$main = Find-Window $ui '*JarStart*'
Check ($null -ne $main) '主窗口已创建' $ui.LastError
if ($main) {
    Write-Output "        窗口位置 $($main.X),$($main.Y) 大小 $($main.Width)x$($main.Height)"
    $ui.Capture($main.Handle, (Join-Path $shots '80-isolated.png'))

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

    $topRow = Get-TitleButtons $ui $main
    $close = $null
    if ($topRow.Count -gt 0) { $close = $topRow[0] }
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
Start-Sleep -Seconds 1

# ————————————————————— 场景二：设置里的"检测" —————————————————————
$ui2 = New-Object IsolatedUi.IsolatedUi($exe, '')
Start-Sleep -Seconds 4
$main2 = Find-Window $ui2 '*JarStart*'
Check ($null -ne $main2) '主窗口已创建（第二个实例）' $ui2.LastError
if ($main2) {
    $row = Get-TitleButtons $ui2 $main2
    if ($row.Count -ge 3) {
        $gear = $row[2]      # 标题栏右端三个自绘按钮：设置 / 最小化 / 关闭
        $ui2.Click($gear.Handle, [int]($gear.Width / 2), [int]($gear.Height / 2))
        Start-Sleep -Seconds 2
        $dlg = Find-Dialog $ui2
        Check ($null -ne $dlg) '设置对话框已打开' ''
        if (-not $dlg) { foreach ($w in $ui2.TopWindows()) { Write-Output "          [$([int]$w.X),$([int]$w.Y)] $($w.Width)x$($w.Height) vis=$($w.Visible) | $($w.ClassName) | $($w.Title)" } }
        if ($dlg) {
            $probe = Find-Child $ui2 $dlg.Handle '检测'
            Check ($null -ne $probe) '设置里有"检测"按钮' ''
            if ($probe) {
                $ui2.Click($probe.Handle, [int]($probe.Width / 2), [int]($probe.Height / 2))
                Start-Sleep -Seconds 2
                $ver = $null
                foreach ($c in $ui2.Children($dlg.Handle)) { if ($c.Title -like '*✔*') { $ver = $c } }
                Check ($null -ne $ver) '检测后能显示 java 版本号' ''
                if ($ver) { Write-Output "        版本显示: $($ver.Title)" }
                $ui2.Capture($dlg.Handle, (Join-Path $shots '82-settings.png'))
            }
        }
    }
    else { Check $false '找到标题栏的设置按钮' '' }
}
$ui2.Dispose()
Start-Sleep -Seconds 1

# ————————————————————— 场景三：托盘菜单的"退出" —————————————————————
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', '1', 'Process')
$ui3 = New-Object IsolatedUi.IsolatedUi($exe, '')
Start-Sleep -Seconds 5

$menu = $null
foreach ($w in $ui3.TopWindows()) {
    if ($w.ClassName -like '*Window.8*' -and $w.Visible) { $menu = $w }
}
Check ($null -ne $menu) '托盘菜单已弹出' ''
if ($menu) {
    $ui3.Key($menu.Handle, 0x23)   # End
    $ui3.Key($menu.Handle, 0x0D)   # Enter
    Start-Sleep -Seconds 3
    $alive = $null -ne (Get-Process -Id $ui3.ProcessId -ErrorAction SilentlyContinue)
    Check (-not $alive) '托盘菜单"退出"能真正退出程序' ''
}
$ui3.Dispose()

[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', $null, 'Process')
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue

Write-Output ""
if ($script:failed -eq 0) { Write-Output "界面自检全部通过 ✅" } else { Write-Output "失败 $script:failed 项 ❌"; exit 1 }
