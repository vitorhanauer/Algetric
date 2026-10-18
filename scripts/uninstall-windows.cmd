@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1" %*
set "result=%errorlevel%"
if not "%result%"=="0" pause
exit /b %result%
