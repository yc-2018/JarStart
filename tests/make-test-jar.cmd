@echo off
rem Wrapper: all logic lives in make-test-jar.ps1
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make-test-jar.ps1" %*
exit /b %errorlevel%
