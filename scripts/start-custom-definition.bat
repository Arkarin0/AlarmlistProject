@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\eng\start-custom-definition.ps1" %*
exit /b %ERRORLEVEL%
