@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\eng\start-library-example.ps1" -ReferenceMode Project %*
exit /b %ERRORLEVEL%
