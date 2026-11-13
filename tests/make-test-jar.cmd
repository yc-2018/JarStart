@echo off
setlocal
chcp 65001 >nul
rem ===== 生成一个用于自测的测试 jar =====
rem 用本机 JDK 编译，会持续打印日志，方便验证启动器

set JAVAC=javac
set JAREXE=jar
if not "%JAVA_HOME%"=="" (
  if exist "%JAVA_HOME%\bin\javac.exe" set JAVAC=%JAVA_HOME%\bin\javac.exe
  if exist "%JAVA_HOME%\bin\jar.exe" set JAREXE=%JAVA_HOME%\bin\jar.exe
)

set OUT=%~dp0
set SRC=%OUT%Hello.java
if not exist "%SRC%" (
  echo [错误] 找不到 %SRC%
  exit /b 1
)

if not exist "%OUT%classes" mkdir "%OUT%classes"

echo 正在编译 …
"%JAVAC%" -d "%OUT%classes" "%SRC%" || exit /b 1
"%JAREXE%" cfe "%OUT%hello.jar" Hello -C "%OUT%classes" . || exit /b 1

echo.
echo [完成] 已生成 %OUT%hello.jar
endlocal
