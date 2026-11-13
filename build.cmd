@echo off
setlocal
chcp 65001 >nul
rem ===== JarStart 一键编译 =====
rem 只依赖 Windows 自带的 .NET Framework 编译器，无需安装任何 SDK
rem 编译器仅支持 C# 5，源码请勿使用字符串内插 / nameof / 表达式体成员等新语法

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [错误] 找不到 C# 编译器 csc.exe
  exit /b 1
)

echo 正在编译 主程序 …
"%CSC%" -nologo -codepage:65001 -target:winexe -optimize+ -platform:anycpu ^
  -main:JarStart.Program ^
  -out:"%~dp0JarStart.exe" ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:System.Web.Extensions.dll ^
  "%~dp0src\*.cs"

if errorlevel 1 (
  echo.
  echo [失败] 主程序编译未通过
  exit /b 1
)

echo 正在编译 自检程序 …
"%CSC%" -nologo -codepage:65001 -target:exe -optimize+ -platform:anycpu ^
  -main:JarStart.SelfTest ^
  -out:"%~dp0tests\SelfTest.exe" ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  -r:System.Web.Extensions.dll ^
  "%~dp0src\*.cs"

if errorlevel 1 (
  echo.
  echo [失败] 自检程序编译未通过
  exit /b 1
)

echo 正在编译 界面测试工具 …
"%CSC%" -nologo -codepage:65001 -target:library -optimize+ ^
  -out:"%~dp0tools\IsolatedUi.dll" ^
  -r:System.dll -r:System.Core.dll -r:System.Drawing.dll ^
  "%~dp0tools\IsolatedUi.cs"

if errorlevel 1 (
  echo.
  echo [失败] 界面测试工具编译未通过
  exit /b 1
)

echo.
echo [完成] 已生成 JarStart.exe / tests\SelfTest.exe / tools\IsolatedUi.dll
endlocal
