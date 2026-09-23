@echo off
setlocal
rem Run from any directory. --deploy-only builds and installs without opening the IDE.
set "AlarmlistVsWhere=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%AlarmlistVsWhere%" (
  echo ERROR: Visual Studio Installer's vswhere.exe was not found.
  exit /b 1
)

set "AlarmlistVsPath="
set "AlarmlistVsInstance="
pushd "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"
if errorlevel 1 exit /b 1
for /f "usebackq delims=" %%I in (`vswhere.exe -latest -products * -version "[17.9,18.0)" -requires Microsoft.Component.MSBuild -property installationPath`) do set "AlarmlistVsPath=%%I"
for /f "usebackq delims=" %%I in (`vswhere.exe -latest -products * -version "[17.9,18.0)" -requires Microsoft.Component.MSBuild -property instanceId`) do set "AlarmlistVsInstance=%%I"
popd
if not defined AlarmlistVsPath (
  echo ERROR: Visual Studio 2022 with MSBuild was not found.
  exit /b 1
)
if not defined AlarmlistVsInstance (
  echo ERROR: Could not identify the Visual Studio 2022 installation.
  exit /b 1
)

pushd "%~dp0.."
if errorlevel 1 exit /b 1
echo Building and deploying Alarmlist to Visual Studio 2022 ^(AlarmlistExp^)...
"%AlarmlistVsPath%\MSBuild\Current\Bin\MSBuild.exe" "src\VisualStudio\Alarmlist.VisualStudio\Alarmlist.VisualStudio.csproj" /restore /p:Configuration=Debug /p:DeployExtension=true /p:DeployTargetInstanceId=%AlarmlistVsInstance% /p:VSSDKTargetPlatformRegRootSuffix=AlarmlistExp /v:minimal /nr:false
if errorlevel 1 (
  popd
  echo ERROR: Extension deployment failed. Close the AlarmlistExp instance before retrying.
  exit /b 1
)

echo SDK package feed: %CD%\artifacts\packages\Debug\NonShipping
if /i "%~1"=="--deploy-only" (
  popd
  exit /b 0
)
echo Starting Visual Studio 2022 with the Alarmlist extension...
start "" "%AlarmlistVsPath%\Common7\IDE\devenv.exe" /RootSuffix AlarmlistExp
set "AlarmlistLaunchResult=%ERRORLEVEL%"
popd
exit /b %AlarmlistLaunchResult%
