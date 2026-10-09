# Prepare the checked-in example to consume the SDK matching the built VSIX.
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Debug',
  [string]$MSBuildPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path $PSScriptRoot -Parent
$exampleRoot = Join-Path $repoRoot 'example'
$vsixPath = Join-Path $repoRoot "artifacts/bin/Alarmlist.VisualStudio/$Configuration/net472/Alarmlist.VisualStudio.vsix"
if (!(Test-Path -LiteralPath $vsixPath)) {
  throw 'Build the extension first, or run scripts/start-vs2022.bat or scripts/start-vs2026.bat.'
}

$archive = [IO.Compression.ZipFile]::OpenRead($vsixPath)
try {
  $templates = @($archive.Entries | Where-Object { $_.FullName.StartsWith('ProjectTemplates/') -and $_.FullName.EndsWith('.almproj') })
  if ($templates.Count -ne 1) { throw 'Expected exactly one ALMX project template in the built VSIX.' }
  $reader = [IO.StreamReader]::new($templates[0].Open())
  try { [xml]$template = $reader.ReadToEnd() } finally { $reader.Dispose() }
  if ($template.Project.Sdk -notmatch '^Alarmlist\.MSBuild\.SDK/([^/]+)$') {
    throw 'The built template must pin an Alarmlist SDK version.'
  }
  $sdkVersion = $Matches[1]
} finally { $archive.Dispose() }

$packagePath = Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.$sdkVersion.nupkg"
if (!(Test-Path -LiteralPath $packagePath)) { throw "Build the matching SDK package first: $packagePath" }
# Development builds can change package contents without changing the version.
# A content-specific cache avoids stale compiler assemblies without deleting caches.
$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$cacheRelativePath = ".packages/$packageHash"
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
    <add key="local" value="../artifacts/packages/$Configuration/NonShipping" />
  </packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $exampleRoot 'NuGet.Config') -Encoding UTF8

$previousPackages = $env:NUGET_PACKAGES
Push-Location $exampleRoot
try {
  $env:NUGET_PACKAGES = Join-Path $exampleRoot $cacheRelativePath
  foreach ($name in @('North', 'South')) {
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
Write-Output "Prepared Alarmlist.MSBuild.SDK/$sdkVersion from the $Configuration build. Example source files are preserved."
