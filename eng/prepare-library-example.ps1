# Prepare only the reusable-library example with the SDK selected by the built VSIX.
[CmdletBinding()]
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path $PSScriptRoot -Parent
$exampleRoot = Join-Path $repoRoot 'example/Libraries'
$vsixPath = Join-Path $repoRoot "artifacts/bin/Alarmlist.VisualStudio/$Configuration/net472/Alarmlist.VisualStudio.vsix"
$archive = [IO.Compression.ZipFile]::OpenRead($vsixPath)
try {
  $templates = @($archive.Entries | Where-Object { $_.FullName.EndsWith('/AlarmlistLibrary.almproj') })
  if ($templates.Count -ne 1) { throw 'Expected exactly one library project template.' }
  $reader = [IO.StreamReader]::new($templates[0].Open())
  try { [xml]$template = $reader.ReadToEnd() } finally { $reader.Dispose() }
  if ($template.Project.Sdk -notmatch '^Alarmlist\.MSBuild\.SDK/([^/]+)$') { throw 'The library template must pin the built SDK.' }
  $sdkVersion = $Matches[1]
} finally { $archive.Dispose() }
$packagePath = Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.$sdkVersion.nupkg"
$packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$cachePath = ".packages/$packageHash"
@{ 'msbuild-sdks' = @{ 'Alarmlist.MSBuild.SDK' = $sdkVersion } } |
  ConvertTo-Json | Set-Content (Join-Path $exampleRoot 'global.json') -Encoding UTF8
@"
<configuration>
  <config><clear /><add key="globalPackagesFolder" value="$cachePath" /></config>
  <packageSources><clear /><add key="sdk" value="../../artifacts/packages/$Configuration/NonShipping" /><add key="libraries" value=".feed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping><clear /><packageSource key="sdk"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource><packageSource key="libraries"><package pattern="Example.*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $exampleRoot 'NuGet.Config') -Encoding UTF8
New-Item -ItemType Directory -Path (Join-Path $exampleRoot '.feed') -Force | Out-Null
$previousPackages = $env:NUGET_PACKAGES
Push-Location $exampleRoot
try {
  $env:NUGET_PACKAGES = Join-Path $exampleRoot $cachePath
  foreach ($name in @('LineA', 'LineB')) {
    & dotnet msbuild "$name/$name.almproj" /t:Restore /nr:false /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Library example restore failed for $name." }
  }
} finally {
  $env:NUGET_PACKAGES = $previousPackages
  Pop-Location
}
Write-Output "Prepared library example with Alarmlist.MSBuild.SDK/$sdkVersion. Open example/Libraries/Libraries.sln."
