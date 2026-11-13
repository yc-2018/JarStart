# 生成一个用于自测的测试 jar（会持续打印日志，方便验证启动器）
# 用本机 JDK 编译；CI 里也用这个脚本
#
# 注意：本文件请保存为 "带 BOM 的 UTF-8"。

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$here = Split-Path -Parent $root
$tests = Join-Path $here 'tests'

$javac = 'javac'
$jarExe = 'jar'
if ($env:JAVA_HOME) {
    $j = Join-Path $env:JAVA_HOME 'bin\javac.exe'
    if (Test-Path $j) { $javac = $j }
    $k = Join-Path $env:JAVA_HOME 'bin\jar.exe'
    if (Test-Path $k) { $jarExe = $k }
}

$src = Join-Path $tests 'Hello.java'
if (-not (Test-Path $src)) {
    Write-Host '[失败] 找不到 Hello.java' -ForegroundColor Red
    exit 1
}

$classes = Join-Path $tests 'classes'
if (-not (Test-Path $classes)) { New-Item -ItemType Directory -Path $classes | Out-Null }

Write-Host '正在编译测试 jar ...' -ForegroundColor Cyan
& $javac -d $classes $src
if ($LASTEXITCODE -ne 0) { Write-Host '[失败] javac 失败' -ForegroundColor Red; exit 1 }

$out = Join-Path $tests 'hello.jar'
& $jarExe cfe $out Hello -C $classes .
if ($LASTEXITCODE -ne 0) { Write-Host '[失败] jar 打包失败' -ForegroundColor Red; exit 1 }

Write-Host ''
Write-Host ('[完成] 已生成 ' + $out) -ForegroundColor Green
