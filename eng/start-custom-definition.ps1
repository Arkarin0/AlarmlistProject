[CmdletBinding()]
param(
  [ValidateSet('2022', '2026')][string]$VisualStudioVersion = '2026',
  [string]$DefinitionFile,
  [string]$SampleFile,
  [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = Split-Path $PSScriptRoot -Parent
if (!$DefinitionFile) { $DefinitionFile = Join-Path $repo 'example/CustomDefinition/AlarmDefinition.xml' }
$definition = (Resolve-Path -LiteralPath $DefinitionFile).ProviderPath
if (!(Test-Path -LiteralPath $definition -PathType Leaf)) { throw 'DefinitionFile must be an XML file.' }
if (!$SampleFile) { $SampleFile = Join-Path $repo 'example/CustomDefinition/Alarms.almx' }
$sample = (Resolve-Path -LiteralPath $SampleFile).ProviderPath
if (!(Test-Path -LiteralPath $sample -PathType Leaf)) { throw 'SampleFile must be an XML alarm source file.' }
$profile = 'AlarmlistDefinitionExp'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio Installer (vswhere) was not found.' }
$range = if ($VisualStudioVersion -eq '2022') { '[17.9,18.0)' } else { '[18.0,19.0)' }
$instances = @((& $vswhere -latest -products '*' -version $range -requires Microsoft.Component.MSBuild -format json | Out-String | ConvertFrom-Json))
if ($LASTEXITCODE -ne 0 -or $instances.Count -ne 1) { throw "Visual Studio $VisualStudioVersion with MSBuild was not found." }
$instance = $instances[0]
$msbuild = Join-Path $instance.installationPath 'MSBuild/Current/Bin/MSBuild.exe'
$devenv = Join-Path $instance.installationPath 'Common7/IDE/devenv.exe'
# Do not replace tools beneath an existing playground, or close an IDE owned by the user.
$running = @(Get-CimInstance Win32_Process -Filter "Name = 'devenv.exe'" | Where-Object {
  $_.ExecutablePath -eq $devenv -and ($_.CommandLine -match $profile -or !$_.CommandLine)
})
if ($running.Count -ne 0) { throw "Close the Visual Studio $VisualStudioVersion $profile playground before rebuilding." }

# Keep this prefix short: native acceptance fixtures add their own private NuGet
# paths, and desktop SDK resolution still encounters the Windows path limit.
$fork = Join-Path $repo ('artifacts/def11/' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $fork | Out-Null
Write-Output "Preparing custom-definition tools in $fork"
$files = @(& git -C $repo ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository sources.' }
foreach ($file in $files) {
  $source = Join-Path $repo $file
  if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
  $target = [IO.Path]::GetFullPath((Join-Path $fork $file))
  if (!$target.StartsWith($fork + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escaped playground.' }
  New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
  Copy-Item -LiteralPath $source -Destination $target
}
& git -C $fork init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize isolated source copy.' }
Copy-Item -LiteralPath $definition -Destination (Join-Path $fork 'src/Compiler/Definition/AlarmDefinition.xml') -Force
Copy-Item -LiteralPath $definition -Destination (Join-Path $fork 'example/CustomDefinition/AlarmDefinition.xml') -Force

function Invoke-Checked([string]$Name, [string]$Executable, [string[]]$Arguments) {
  Write-Output "$Name..."
  & $Executable @Arguments *> (Join-Path $fork "$Name.log")
  if ($LASTEXITCODE -ne 0) { throw "$Name failed. See $fork/$Name.log. No playground was opened." }
}

$previousPackages = $env:NUGET_PACKAGES
Push-Location $fork
try {
  # Repository dependency packages may be reused; custom SDK consumers never share a cache.
  $env:NUGET_PACKAGES = Join-Path $repo '.packages'
  $extension = Join-Path $fork 'src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj'
  Invoke-Checked 'build-tools' 'dotnet' @('build', $extension, '-v:minimal', '-m:1', '-nr:false', "-p:RestorePackagesPath=$env:NUGET_PACKAGES")
  $packages = @(Get-ChildItem (Join-Path $fork 'artifacts/packages/Debug/NonShipping') -Filter 'Alarmlist.MSBuild.SDK.*.nupkg')
  if ($packages.Count -ne 1) { throw 'Expected exactly one freshly built matching SDK package.' }
  $work = Join-Path $fork 'playground'
  New-Item -ItemType Directory -Path $work | Out-Null
  Copy-Item -Path (Join-Path $fork 'example/CustomDefinition/*') -Destination $work
  Copy-Item -LiteralPath $sample -Destination (Join-Path $work 'Alarms.almx') -Force
  $feed = Join-Path $work 'feed'
  New-Item -ItemType Directory -Path $feed | Out-Null
  Copy-Item -LiteralPath $packages[0].FullName -Destination $feed
  $archive = [IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
  try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('Alarmlist.MSBuild.SDK.nuspec').Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $sdkVersion = [string]$spec.package.metadata.version
  } finally { $archive.Dispose() }
  @{ 'msbuild-sdks' = @{ 'Alarmlist.MSBuild.SDK' = $sdkVersion } } | ConvertTo-Json | Set-Content (Join-Path $work 'global.json') -Encoding UTF8
  Set-Content (Join-Path $work 'NuGet.Config') -Encoding UTF8 -Value @'
<configuration>
  <config><clear /><add key="globalPackagesFolder" value="packages" /></config>
  <packageSources><clear /><add key="playground" value="feed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping><clear /><packageSource key="playground"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource></packageSourceMapping>
</configuration>
'@
  $env:NUGET_PACKAGES = Join-Path $work 'packages'
  Push-Location $work
  try {
    Invoke-Checked 'build-sample-core' 'dotnet' @('msbuild', 'Playground.almproj', '-restore', '-t:Build', '-v:minimal', '-nr:false')
    Invoke-Checked 'build-sample-desktop' $msbuild @('Playground.almproj', '-restore', '-t:Rebuild', '-v:minimal', '-nr:false')
  } finally { Pop-Location }
  $env:NUGET_PACKAGES = Join-Path $repo '.packages'
  Invoke-Checked 'deploy-tools' $msbuild @($extension, '/p:Configuration=Debug', '/p:DeployExtension=true', "/p:DeployTargetInstanceId=$($instance.instanceId)", "/p:VSSDKTargetPlatformRegRootSuffix=$profile", "/p:RestorePackagesPath=$env:NUGET_PACKAGES", '/v:minimal', '/nr:false', '/m:1')
  $solution = Join-Path $work 'Playground.sln'
  @{ ForkRoot = $fork; Solution = $solution; VisualStudioVersion = $VisualStudioVersion; RootSuffix = $profile; SdkVersion = $sdkVersion } |
    ConvertTo-Json | Set-Content (Join-Path $fork 'playground.json') -Encoding UTF8
  Write-Output "Ready: $solution"
  Write-Output "Edit $definition and rerun to rebuild. Existing playground runs are preserved."
  if (!$PrepareOnly) {
    $env:NUGET_PACKAGES = $null
    Start-Process -FilePath $devenv -ArgumentList @('/RootSuffix', $profile, ('"' + $solution + '"'))
  }
} finally {
  Pop-Location
  $env:NUGET_PACKAGES = $previousPackages
}
