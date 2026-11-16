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

    # 用一个"日志内容固定不变"的测试 jar：这样截图对比才能说明是滚动条动了
    # （如果日志一直在刷，截图变化不能证明是滚动条生效）
    $quiet = Join-Path $dir 'quiet.jar'
    $java = 'javac'
    if ($env:JAVA_HOME) {
        $j = Join-Path $env:JAVA_HOME 'bin\javac.exe'
        if (Test-Path $j) { $java = $j }
    }
    $jarExe = 'jar'
    if ($env:JAVA_HOME) {
        $k = Join-Path $env:JAVA_HOME 'bin\jar.exe'
        if (Test-Path $k) { $jarExe = $k }
    }
    $src = Join-Path $dir 'Quiet.java'
    $code = "public class Quiet { public static void main(String[] a) throws Exception {" +
            " System.out.println(""Tomcat started on port(s): 8080 (http) with context path ''""); " +
            " for (int i = 0; i < 400; i++) System.out.println(""static line "" + i + "" padding padding padding end""); " +
            " System.out.flush(); Thread.sleep(600000); } }"
    [System.IO.File]::WriteAllText($src, $code, (New-Object System.Text.UTF8Encoding($false)))
    & $java -d $dir $src 2>$null
    & $jarExe cfe $quiet Quiet -C $dir Quiet.class 2>$null

    $jarPath = Join-Path $root 'tests\hello.jar'
    if (Test-Path $quiet) { $jarPath = $quiet }

    $cfg = @"
{
  "Version":"1",
  "Items":[{"Id":"jar-fw","Name":"fw-server-sj.20250520-SNAPSHOT","JarPath":"$($jarPath -replace '\\','\\\\')","Group":"生产环境","JavaPath":"","JvmArgs":"","ProgramArgs":"","WorkDir":"","EnvVars":"","Encoding":"UTF-8","Port":"","AutoStart":false}],
  "Groups":["生产环境"],
  "Theme":"light","Accent":"","LogFontFamily":"Cascadia Mono","LogFontSize":9.5,"AutoScroll":true,
  "DefaultJavaPath":"","MaxLogLines":3000,"StopAllOnExit":true,"MinimizeToTray":true,"TrayHintShown":true,
  "LastSelectedId":"jar-fw","WinX":-2600,"WinY":-1200,"WinW":1100,"WinH":700
}
"@
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
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', '1', 'Process')
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

        # ——— 日志滚动条能不能拖 ———
        Start-Sleep -Seconds 4
        $before = Join-Path $shots '93-scroll-before.png'
        $after = Join-Path $shots '94-scroll-after.png'
        $ui.Capture($main.Handle, $before)

        # 找日志控件：客户区里点一下，取最深的子窗口
        $logHwnd = $ui.WindowAtClient($main.Handle, [int]($main.Width * 0.55), [int]($main.Height * 0.6))
        $logRect = $null
        foreach ($c in $ui.Children($main.Handle)) { if ($c.Handle -eq $logHwnd) { $logRect = $c } }
        Check ($null -ne $logRect) '找到日志视图控件' ''
        if ($logRect) {
            # 竖直滚动条在右边，滑块初始在底部；往上拖到中间
            $sb = 18
            $x = [int]($logRect.Width - $sb / 2)
            $y1 = [int]($logRect.Height - 30)
            $y2 = [int]($logRect.Height * 0.35)
            $ui.Drag($logHwnd, $x, $y1, $x, $y2)
            $ui.Capture($main.Handle, $after)
            $h1 = (Get-FileHash $before -Algorithm MD5).Hash
            $h2 = (Get-FileHash $after -Algorithm MD5).Hash
            Check ($h1 -ne $h2) '拖动竖直滚动条后画面有变化（能拖）' ''

            # ——— 能不能只框住一行里的几个字 ———
            $selX1 = 200
            $selX2 = 300
            $selY = 5
            $ui.Drag($logHwnd, $selX1, $selY, $selX2, $selY)
            Start-Sleep -Milliseconds 300
            $copy = Find-Child $ui $main.Handle '复制'
            if ($copy) {
                $ui.Click($copy.Handle, [int]($copy.Width / 2), [int]($copy.Height / 2))
                Start-Sleep -Milliseconds 500
                $clip = ''
                try { $clip = Get-Clipboard -Raw -ErrorAction SilentlyContinue } catch { }
                if ($null -eq $clip) { $clip = '' }
                $clip = $clip.Trim()
                Write-Output "        剪贴板内容: '$clip'"
                $oneLine = ($clip -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 }).Count
                Check ($clip.Length -gt 0 -and $clip.Length -lt 200) '只复制了选中的一小段（不是整份日志）' "长度 $($clip.Length)"
                Check ($oneLine -le 2) '复制的是片段（行数少）' "$oneLine 行"
            }
            else { Check $false '找到"复制"按钮' '' }
        }
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
    if ($row.Count -ge 4) {
        $gear = $row[3]      # 标题栏右端四个自绘按钮：设置 / 最小化 / 最大化 / 关闭

        # ——— 最大化 / 还原 ———
        $maxBtn = $row[1]
        $w0 = $main2.Width; $h0 = $main2.Height
        $ui2.Click($maxBtn.Handle, [int]($maxBtn.Width / 2), [int]($maxBtn.Height / 2))
        Start-Sleep -Seconds 2
        $maxed = Find-Window $ui2 '*JarStart*'
        Write-Output "        最大化后: $($maxed.X),$($maxed.Y) $($maxed.Width)x$($maxed.Height)"
        Check ($null -ne $maxed -and $maxed.Width -gt $w0) '最大化按钮能放大窗口' "$w0 x $h0 -> $($maxed.Width) x $($maxed.Height)"
        $ui2.Capture($maxed.Handle, (Join-Path $shots '96-maximized.png'))

        Start-Sleep -Seconds 1
        $ui2.Click($maxBtn.Handle, [int]($maxBtn.Width / 2), [int]($maxBtn.Height / 2))
        Start-Sleep -Seconds 2
        $restored = Find-Window $ui2 '*JarStart*'
        Write-Output "        还原后  : $($restored.X),$($restored.Y) $($restored.Width)x$($restored.Height)"
        Check ($null -ne $restored -and $restored.Width -lt $maxed.Width) '再点一次能还原窗口' "$($restored.Width) x $($restored.Height)"

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
                # 探测要拉起一个 java 进程，冷启动可能慢一点，这里轮询等结果
                $ver = $null
                for ($i = 0; $i -lt 20; $i++) {
                    Start-Sleep -Milliseconds 500
                    foreach ($c in $ui2.Children($dlg.Handle)) { if ($c.Title -like '*✔*') { $ver = $c } }
                    if ($ver) { break }
                }
                Check ($null -ne $ver) '检测后能显示 java 版本号' ''
                if ($ver) { Write-Output "        版本显示: $($ver.Title)" }
                else {
                    foreach ($c in $ui2.Children($dlg.Handle)) {
                        if ($c.Title -ne '') { Write-Output "          [$($c.Title)]" }
                    }
                }
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
for ($i = 0; $i -lt 20; $i++) {
    foreach ($w in $ui3.TopWindows()) {
        if ($w.ClassName -like '*Window.8*' -and $w.Visible) { $menu = $w }
    }
    if ($menu) { break }
    Start-Sleep -Milliseconds 500
}
Check ($null -ne $menu) '托盘菜单已弹出' ''
if ($menu) {
    # 菜单是定时反复弹出的，End 和 Enter 之间有可能被重弹把选中项重置，
    # 所以这里反复尝试，直到进程真的退出
    $exited = $false
    for ($i = 0; $i -lt 12 -and -not $exited; $i++) {
        $m = $null
        foreach ($w in $ui3.TopWindows()) {
            if ($w.ClassName -like '*Window.8*' -and $w.Visible) { $m = $w }
        }
        if (-not $m) { Start-Sleep -Milliseconds 300; continue }
        $ui3.Key($m.Handle, 0x23)   # End
        $ui3.Key($m.Handle, 0x0D)   # Enter
        Start-Sleep -Milliseconds 800
        $exited = $null -eq (Get-Process -Id $ui3.ProcessId -ErrorAction SilentlyContinue)
    }
    Check $exited '托盘菜单"退出"能真正退出程序' ''
}
$ui3.Dispose()

[System.Environment]::SetEnvironmentVariable('JARSTART_CONFIG_DIR', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', '1', 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_ALLOW_MULTI', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_TRAY', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_NO_REPOSITION', $null, 'Process')
[System.Environment]::SetEnvironmentVariable('JARSTART_SHOW_TRAY_MENU', $null, 'Process')
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue

Write-Output ""
if ($script:failed -eq 0) { Write-Output "界面自检全部通过 ✅" } else { Write-Output "失败 $script:failed 项 ❌"; exit 1 }
