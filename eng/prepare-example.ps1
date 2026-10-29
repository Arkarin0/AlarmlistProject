# Prepare the checked-in example to consume the SDK matching the built VSIX.
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Debug',
  [string]$MSBuildPath,
  [string]$ExampleRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-PackageContentHash([string]$Path) {
  $stream = [IO.File]::OpenRead($Path)
  $hasher = [Security.Cryptography.SHA256]::Create()
  try { return [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
  finally { $hasher.Dispose(); $stream.Dispose() }
}
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$ExampleRoot) { $ExampleRoot = Join-Path $repoRoot 'example' }
$exampleRoot = (Resolve-Path -LiteralPath $ExampleRoot).Path
$vsixPath = Join-Path $repoRoot "artifacts/bin/Alarmlist.VisualStudio/$Configuration/net472/Alarmlist.VisualStudio.vsix"
if (!(Test-Path -LiteralPath $vsixPath)) {
  throw 'Build the extension first, or run scripts/start-vs2022.bat or scripts/start-vs2026.bat.'
}

$archive = [IO.Compression.ZipFile]::OpenRead($vsixPath)
try {
  $templates = @($archive.Entries | Where-Object { $_.FullName.StartsWith('ProjectTemplates/') -and $_.FullName.EndsWith('/Alarmlist.almproj') })
  if ($templates.Count -ne 1) { throw 'Expected exactly one Alarmlist application template in the built VSIX.' }
  $reader = [IO.StreamReader]::new($templates[0].Open())
  try { [xml]$template = $reader.ReadToEnd() } finally { $reader.Dispose() }
  if ($template.Project.Sdk -notmatch '^Alarmlist\.MSBuild\.SDK/([^/]+)$') {
    throw 'The built template must pin an Alarmlist SDK version.'
  }
  $sdkVersion = $Matches[1]
} finally { $archive.Dispose() }

$packagePath = Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.$sdkVersion.nupkg"
if (!(Test-Path -LiteralPath $packagePath)) { throw "Build the matching SDK package first: $packagePath" }

# Build the opt-in sample for both task runtimes before entering the consumer's
# isolated NuGet environment. Keep generated DLLs out of persistent source files.
Push-Location $repoRoot
try {
  & dotnet build 'src/Analyzers/Alarmlist.SampleAnalyzer/Alarmlist.SampleAnalyzer.csproj' --configuration $Configuration --nologo --verbosity minimal
  if ($LASTEXITCODE -ne 0) { throw 'Example analyzer preparation failed.' }
  & dotnet build 'src/Exporters/Alarmlist.Exporters.Zenon/Alarmlist.Exporters.Zenon.csproj' --configuration $Configuration --nologo --verbosity minimal
  if ($LASTEXITCODE -ne 0) { throw 'Example zenon exporter preparation failed.' }
} finally { Pop-Location }
$exporterPackagePath = Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.Exporters.Zenon.$sdkVersion.nupkg"
if (!(Test-Path -LiteralPath $exporterPackagePath)) { throw "The matching zenon exporter package is missing: $exporterPackagePath" }
foreach ($framework in @('net472', 'net8.0')) {
  $analyzerSource = Join-Path $repoRoot "artifacts/bin/Alarmlist.SampleAnalyzer/$Configuration/$framework/Alarmlist.SampleAnalyzer.dll"
  if (!(Test-Path -LiteralPath $analyzerSource)) { throw "Build the sample analyzer first: $analyzerSource" }
  $analyzerDirectory = Join-Path $exampleRoot ".analyzers/$framework"
  New-Item -ItemType Directory -Path $analyzerDirectory -Force | Out-Null
  Copy-Item -LiteralPath $analyzerSource -Destination (Join-Path $analyzerDirectory 'Alarmlist.SampleAnalyzer.dll') -Force
}
# Development builds can change package contents without changing the version.
# A content-specific cache avoids stale compiler assemblies without deleting caches.
$packageHash = Get-PackageContentHash $packagePath
$exporterHash = Get-PackageContentHash $exporterPackagePath
$cacheHasher = [Security.Cryptography.SHA256]::Create()
try {
  $cacheHash = [BitConverter]::ToString($cacheHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes("${packageHash}:${exporterHash}"))).Replace('-', '').ToLowerInvariant()
} finally { $cacheHasher.Dispose() }
$cacheRelativePath = ".packages/$cacheHash"
"<Project><PropertyGroup><ExampleZenonPackageVersion>$sdkVersion</ExampleZenonPackageVersion></PropertyGroup></Project>" |
  Set-Content (Join-Path $exampleRoot 'Example.local.props') -Encoding UTF8
$escapedFeed = [Security.SecurityElement]::Escape((Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping"))
@{ 'msbuild-sdks' = @{ 'Alarmlist.MSBuild.SDK' = $sdkVersion } } |
  ConvertTo-Json | Set-Content (Join-Path $exampleRoot 'global.json') -Encoding UTF8
@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <clear />
    <add key="globalPackagesFolder" value="$cacheRelativePath" />
  </config>
  <packageSources>
    <clear />
    <add key="local" value="$escapedFeed" />
  </packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /><package pattern="Alarmlist.Exporters.Zenon" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $exampleRoot 'NuGet.Config') -Encoding UTF8

$previousPackages = $env:NUGET_PACKAGES
Push-Location $exampleRoot
try {
  $env:NUGET_PACKAGES = Join-Path $exampleRoot $cacheRelativePath
  foreach ($name in @('North', 'South', 'Zenon')) {
    # Restore evaluates the SDK without compiling or overwriting playground output.
    $buildArguments = @("$name/$name.almproj", '/t:Restore', '/nr:false', '/v:minimal')
    if ($MSBuildPath) {
      & $MSBuildPath @buildArguments
    } else {
      & dotnet msbuild @buildArguments
    }
    if ($LASTEXITCODE -ne 0) { throw "Example SDK preparation failed for $name." }
  }
} finally {
  $env:NUGET_PACKAGES = $previousPackages
  Pop-Location
}
Write-Output "Example solution: $(Join-Path $exampleRoot 'Example.sln')"
Write-Output "Prepared Alarmlist.MSBuild.SDK/$sdkVersion, the zenon exporter and the sample analyzer for both runtimes from the $Configuration build. Example source files are preserved."
