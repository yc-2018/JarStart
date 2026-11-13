@echo off
rem Wrapper: all logic lives in build.ps1 (kept ASCII-only so it works on any locale)
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %errorlevel%
