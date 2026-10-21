[CmdletBinding()]
param(
  [Parameter(Mandatory)][ValidateSet('Project', 'Package')][string]$ReferenceMode,
  [ValidateSet('2022', '2026')][string]$VisualStudioVersion = '2026',
  [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$range = if ($VisualStudioVersion -eq '2022') { '[17.0,18.0)' } else { '[18.0,19.0)' }
$instances = @((& $vswhere -latest -products '*' -version $range -requires Microsoft.Component.MSBuild -format json | Out-String | ConvertFrom-Json))
if ($LASTEXITCODE -ne 0 -or $instances.Count -ne 1) { throw "Visual Studio $VisualStudioVersion with MSBuild was not found." }
$instance = $instances[0]
$msbuild = Join-Path $instance.installationPath 'MSBuild/Current/Bin/MSBuild.exe'
Push-Location $repoRoot
try {
  & $msbuild 'src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj' /restore /p:Configuration=Debug /p:DeployExtension=true "/p:DeployTargetInstanceId=$($instance.instanceId)" /p:VSSDKTargetPlatformRegRootSuffix=AlarmlistExp /v:minimal /nr:false
  if ($LASTEXITCODE -ne 0) { throw 'Extension deployment failed. Close the AlarmlistExp instance before retrying.' }
  & "$PSScriptRoot/prepare-library-example.ps1" -ReferenceMode $ReferenceMode
  if (!$PrepareOnly) {
    $previousPackages = $env:NUGET_PACKAGES
    try {
      $env:NUGET_PACKAGES = $null
      Start-Process -FilePath (Join-Path $instance.installationPath 'Common7/IDE/devenv.exe') -ArgumentList @('/RootSuffix', 'AlarmlistExp', ('"' + (Join-Path $repoRoot 'example/Libraries/Libraries.sln') + '"'))
    } finally { $env:NUGET_PACKAGES = $previousPackages }
  }
} finally { Pop-Location }
