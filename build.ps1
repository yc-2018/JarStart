# JarStart 一键编译
# 只依赖 Windows 自带的 .NET Framework 编译器，无需安装任何 SDK
# 编译器仅支持 C# 5，源码请勿使用字符串内插 / nameof / 表达式体成员等新语法
#
# 注意：本文件请保存为 "带 BOM 的 UTF-8"，Windows PowerShell 5.1 靠 BOM 识别中文编码。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root

try {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    if (-not (Test-Path $csc)) {
        Write-Host '[失败] 找不到 c# 编译器 csc.exe' -ForegroundColor Red
        exit 1
    }

    $manifest = Join-Path $root 'src\app.manifest'
    $srcFiles = @(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName })

    function Invoke-Csc {
        param(
            [string]$Output,
            [string]$Target,
            [string]$MainType,
            [string[]]$Sources,
            [string[]]$ExtraRefs
        )
        $cscArgs = @('-nologo', '-codepage:65001', "-target:$Target", '-optimize+', '-platform:anycpu', "-out:$Output")
        if ($MainType) { $cscArgs += "-main:$MainType" }
        if (Test-Path $manifest) { $cscArgs += "-win32manifest:$manifest" }
        $cscArgs += @('-r:System.dll', '-r:System.Core.dll', '-r:System.Drawing.dll',
                      '-r:System.Windows.Forms.dll', '-r:System.Web.Extensions.dll')
        if ($ExtraRefs) { $cscArgs += $ExtraRefs }
        $cscArgs += $Sources
        & $csc @cscArgs
        if ($LASTEXITCODE -ne 0) { throw "编译失败：$Output" }
    }

    Write-Host '正在编译 主程序 ...' -ForegroundColor Cyan
    Invoke-Csc -Output (Join-Path $root 'JarStart.exe') -Target 'winexe' `
                -MainType 'JarStart.Program' -Sources $srcFiles -ExtraRefs @()

    Write-Host '正在编译 自检程序 ...' -ForegroundColor Cyan
    Invoke-Csc -Output (Join-Path $root 'tests\SelfTest.exe') -Target 'exe' `
                -MainType 'JarStart.SelfTest' -Sources $srcFiles -ExtraRefs @()

    Write-Host '正在编译 界面测试工具 ...' -ForegroundColor Cyan
    $toolFiles = @(Get-ChildItem (Join-Path $root 'tools') -Filter *.cs | ForEach-Object { $_.FullName })
    Invoke-Csc -Output (Join-Path $root 'tools\IsolatedUi.dll') -Target 'library' `
                -MainType '' -Sources $toolFiles -ExtraRefs @()

    Write-Host ''
    Write-Host '[完成] 已生成 JarStart.exe / tests\SelfTest.exe / tools\IsolatedUi.dll' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host ('[失败] ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
