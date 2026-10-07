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
for /f "usebackq delims=" %%I in (`vswhere.exe -latest -products * -version "[18.0,19.0)" -requires Microsoft.Component.MSBuild -property installationPath`) do set "AlarmlistVsPath=%%I"
for /f "usebackq delims=" %%I in (`vswhere.exe -latest -products * -version "[18.0,19.0)" -requires Microsoft.Component.MSBuild -property instanceId`) do set "AlarmlistVsInstance=%%I"
popd
if not defined AlarmlistVsPath (
  echo ERROR: Visual Studio 2026 with MSBuild was not found.
  exit /b 1
)
if not defined AlarmlistVsInstance (
  echo ERROR: Could not identify the Visual Studio 2026 installation.
  exit /b 1
)

pushd "%~dp0.."
if errorlevel 1 exit /b 1
echo Building and deploying Alarmlist to Visual Studio 2026 ^(AlarmlistExp^)...
"%AlarmlistVsPath%\MSBuild\Current\Bin\MSBuild.exe" "src\VisualStudio\Alarmlist.VisualStudio\Alarmlist.VisualStudio.csproj" /restore /p:Configuration=Debug /p:DeployExtension=true /p:DeployTargetInstanceId=%AlarmlistVsInstance% /p:VSSDKTargetPlatformRegRootSuffix=AlarmlistExp /v:minimal /nr:false
if errorlevel 1 (
  popd
  echo ERROR: Extension deployment failed. Close the AlarmlistExp instance before retrying.
  exit /b 1
)

echo Preparing the example solution with the matching SDK...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "eng\prepare-example.ps1" -MSBuildPath "%AlarmlistVsPath%\MSBuild\Current\Bin\MSBuild.exe"
if errorlevel 1 (
  popd
  echo ERROR: Example preparation failed.
  exit /b 1
)
rem Let the example's NuGet.Config select its isolated cache in the child IDE.
set "NUGET_PACKAGES="
if /i "%~1"=="--deploy-only" (
  popd
  exit /b 0
)
echo Starting Visual Studio 2026 with the Alarmlist extension...
start "" "%AlarmlistVsPath%\Common7\IDE\devenv.exe" /RootSuffix AlarmlistExp "%CD%\example\Example.sln"
set "AlarmlistLaunchResult=%ERRORLEVEL%"
popd
exit /b %AlarmlistLaunchResult%
