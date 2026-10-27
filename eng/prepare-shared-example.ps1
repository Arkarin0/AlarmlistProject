# Prepare the native Shared Project example without replacing source/project files.
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
  [string]$MSBuildPath,
  [string]$ExampleRoot
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path $PSScriptRoot -Parent
if (!$ExampleRoot) { $ExampleRoot = Join-Path $repoRoot 'example/SharedProjects' }
$ExampleRoot = (Resolve-Path -LiteralPath $ExampleRoot).Path
$vsixPath = Join-Path $repoRoot "artifacts/bin/Alarmlist.VisualStudio/$Configuration/net472/Alarmlist.VisualStudio.vsix"
$archive = [IO.Compression.ZipFile]::OpenRead($vsixPath)
try {
  $templates = @($archive.Entries | Where-Object { $_.FullName.StartsWith('ProjectTemplates/') -and $_.FullName.EndsWith('/Alarmlist.almproj') })
  if ($templates.Count -ne 1) { throw 'Expected exactly one application template in the built VSIX.' }
  $reader = [IO.StreamReader]::new($templates[0].Open())
  try { [xml]$template = $reader.ReadToEnd() } finally { $reader.Dispose() }
  if ($template.Project.Sdk -notmatch '^Alarmlist\.MSBuild\.SDK/([^/]+)$') { throw 'The built template must pin its SDK.' }
  $sdkVersion = $Matches[1]
} finally { $archive.Dispose() }
$feed = Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping"
$package = Join-Path $feed "Alarmlist.MSBuild.SDK.$sdkVersion.nupkg"
$cache = '.packages/' + (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
@{ 'msbuild-sdks' = @{ 'Alarmlist.MSBuild.SDK' = $sdkVersion } } |
  ConvertTo-Json | Set-Content (Join-Path $ExampleRoot 'global.json') -Encoding UTF8
$escapedFeed = [Security.SecurityElement]::Escape($feed)
@"
<configuration>
  <config><clear /><add key="globalPackagesFolder" value="$cache" /></config>
  <packageSources><clear /><add key="sdk" value="$escapedFeed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping><clear /><packageSource key="sdk"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $ExampleRoot 'NuGet.Config') -Encoding UTF8
$previousPackages = $env:NUGET_PACKAGES
Push-Location $ExampleRoot
try {
  $env:NUGET_PACKAGES = Join-Path $ExampleRoot $cache
  foreach ($name in @('North', 'South')) {
    $arguments = @("$name/$name.almproj", '/t:Restore', "/p:Configuration=$Configuration", '/nr:false', '/v:minimal')
    if ($MSBuildPath) { & $MSBuildPath @arguments } else { & dotnet msbuild @arguments }
    if ($LASTEXITCODE -ne 0) { throw "Shared example SDK preparation failed for $name." }
  }
} finally { $env:NUGET_PACKAGES = $previousPackages; Pop-Location }
Write-Output "Prepared Alarmlist.MSBuild.SDK/$sdkVersion. Source/project files were preserved. Open $ExampleRoot/SharedProjects.sln."
