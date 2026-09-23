# Build candidates separately from publishing. Never upload packages from a build.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')]
  [string]$Version,
  [ValidateSet('Prepare', 'PublishSdk', 'VerifyPublished')]
  [string]$Stage = 'Prepare'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path $PSScriptRoot -Parent
$candidateDirectory = Join-Path $repoRoot "artifacts/release-candidates/$Version"
$packageName = "Alarmlist.MSBuild.SDK.$Version.nupkg"
$vsixName = 'Alarmlist.VisualStudio.vsix'

function Read-ZipEntry($Archive, [string]$Name) {
  $entry = $Archive.GetEntry($Name)
  if (!$entry) { throw "Missing package entry: $Name" }
  $reader = [IO.StreamReader]::new($entry.Open())
  try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Read-Template([string]$VsixPath) {
  $archive = [IO.Compression.ZipFile]::OpenRead($VsixPath)
  try {
    $projectEntries = @($archive.Entries | Where-Object {
      $_.FullName.StartsWith('ProjectTemplates/') -and $_.FullName.EndsWith('.almproj')
    })
    if ($projectEntries.Count -ne 1) { throw 'Expected exactly one project template.' }
    $projectEntry = $projectEntries[0]
    $projectText = Read-ZipEntry $archive $projectEntry.FullName
    [xml]$projectXml = $projectText
    if ($projectXml.Project.Sdk -ne "Alarmlist.MSBuild.SDK/$Version") {
      throw 'The VSIX template does not pin the candidate SDK version.'
    }
    [xml]$manifest = Read-ZipEntry $archive 'extension.vsixmanifest'
    if ($manifest.PackageManifest.Metadata.Identity.Version -ne $Version) {
      throw 'Update source.extension.vsixmanifest Identity Version to match the release version.'
    }
    $directory = $projectEntry.FullName.Substring(0, $projectEntry.FullName.LastIndexOf('/') + 1)
    return @{
      Project = $projectText
      Alarms = (Read-ZipEntry $archive ($directory + 'Alarms.almx')).Replace('$safeprojectname$', 'Plant')
    }
  } finally { $archive.Dispose() }
}

Push-Location $repoRoot
try {
  if ($Stage -eq 'Prepare') {
    if (Test-Path -LiteralPath $candidateDirectory) {
      throw "Candidate already exists: $candidateDirectory. Use a new version or explicitly archive the old candidate first."
    }
    [xml]$sourceManifest = Get-Content 'src/VisualStudio/Alarmlist.VisualStudio/source.extension.vsixmanifest'
    if ($sourceManifest.PackageManifest.Metadata.Identity.Version -ne $Version) {
      throw 'Update source.extension.vsixmanifest Identity Version to match the release version.'
    }
    & dotnet test AlarmlistProject.slnx --configuration Release "/p:PackageVersion=$Version" "/p:Version=$Version" /p:DeployExtension=false --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Release build or tests failed; no candidate was staged.' }
    $packagePath = Join-Path $repoRoot "artifacts/packages/Release/NonShipping/$packageName"
    $vsixPath = Join-Path $repoRoot "artifacts/bin/Alarmlist.VisualStudio/Release/net472/$vsixName"
    $null = Read-Template $vsixPath
    if (!(Test-Path -LiteralPath $packagePath)) { throw "Missing SDK package: $packagePath" }
    New-Item -ItemType Directory -Path $candidateDirectory | Out-Null
    Copy-Item -LiteralPath $packagePath, $vsixPath -Destination $candidateDirectory
    Get-FileHash (Join-Path $candidateDirectory $packageName), (Join-Path $candidateDirectory $vsixName) -Algorithm SHA256 |
      Select-Object @{Name='File'; Expression={ Split-Path $_.Path -Leaf }}, Hash |
      ConvertTo-Json | Set-Content (Join-Path $candidateDirectory 'checksums.json') -Encoding UTF8
    Write-Output "Validated candidate: $candidateDirectory"
    Write-Output 'Publish its SDK package to NuGet.org, then run this script with -Stage VerifyPublished before distributing the VSIX.'
    return
  }

  $checksums = Get-Content (Join-Path $candidateDirectory 'checksums.json') -Raw | ConvertFrom-Json
  foreach ($fileName in @($packageName, $vsixName)) {
    $expected = @($checksums | Where-Object { $_.File -eq $fileName })
    if ($expected.Count -ne 1 -or (Get-FileHash (Join-Path $candidateDirectory $fileName) -Algorithm SHA256).Hash -ne $expected[0].Hash) {
      throw "Candidate changed since validation: $fileName"
    }
  }
  $template = Read-Template (Join-Path $candidateDirectory $vsixName)
  if ($Stage -eq 'PublishSdk') {
    if ([string]::IsNullOrWhiteSpace($env:ALARMLIST_NUGET_API_KEY)) {
      throw 'Set ALARMLIST_NUGET_API_KEY in this process to a NuGet.org API key scoped to Alarmlist.MSBuild.SDK. Never put the key in source control.'
    }
    & dotnet nuget push (Join-Path $candidateDirectory $packageName) --source https://api.nuget.org/v3/index.json --api-key $env:ALARMLIST_NUGET_API_KEY
    if ($LASTEXITCODE -ne 0) { throw 'SDK publication failed. The VSIX has not been promoted.' }
    Write-Output 'SDK submitted. After NuGet.org validation/indexing completes, run -Stage VerifyPublished.'
    return
  }
  $runDirectory = Join-Path $repoRoot "artifacts/log/Release/PublishedSdk/$([Guid]::NewGuid().ToString('N'))"
  New-Item -ItemType Directory -Path $runDirectory | Out-Null
  Copy-Item 'src/SDK/Alarmlist.SDK.Tests/testassets/boilerplate/Directory.Build.*' $runDirectory
  # The test lives below the repository, so isolate its sources and caches from
  # repository/user configuration. No local SDK feed is available to this probe.
  @'
<configuration>
  <config><add key="globalPackagesFolder" value=".packages" /></config>
  <packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping><clear /><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
'@ | Set-Content (Join-Path $runDirectory 'NuGet.Config') -Encoding UTF8
  $template.Project | Set-Content (Join-Path $runDirectory 'Plant.almproj') -Encoding UTF8
  $template.Alarms | Set-Content (Join-Path $runDirectory 'Alarms.almx') -Encoding UTF8
  $previousPackages = $env:NUGET_PACKAGES
  $previousHttpCache = $env:NUGET_HTTP_CACHE_PATH
  try {
    $env:NUGET_PACKAGES = Join-Path $runDirectory '.packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $runDirectory '.http-cache'
    Push-Location $runDirectory
    try {
      & dotnet msbuild Plant.almproj /restore /t:Build /nr:false /v:minimal /bl:build.binlog
      if ($LASTEXITCODE -ne 0) {
        throw "The pinned SDK could not build from NuGet.org. Do not distribute the VSIX. Logs: $runDirectory"
      }
      [xml]$compiled = Get-Content 'bin/Debug/Plant.Alarmlist.xml'
      if (@($compiled.AlarmList.Alarm).Count -ne 1) { throw 'Published SDK did not compile the starter alarm.' }
      # NuGet.org adds a repository signature, so compare package payloads rather
      # than whole-archive hashes to ensure the published version is our candidate.
      $downloadedPath = Join-Path $env:NUGET_PACKAGES "alarmlist.msbuild.sdk/$Version/$($packageName.ToLowerInvariant())"
      $candidate = [IO.Compression.ZipFile]::OpenRead((Join-Path $candidateDirectory $packageName))
      try {
        $downloaded = [IO.Compression.ZipFile]::OpenRead($downloadedPath)
        try {
          $candidateEntries = @($candidate.Entries | Where-Object { $_.FullName -ne '.signature.p7s' })
          $downloadedEntries = @($downloaded.Entries | Where-Object { $_.FullName -ne '.signature.p7s' })
          if ($candidateEntries.Count -ne $downloadedEntries.Count) { throw 'Published package differs from the validated candidate.' }
          foreach ($entry in $candidateEntries) {
            $remoteEntry = $downloaded.GetEntry($entry.FullName)
            if (!$remoteEntry) { throw "Published package is missing $($entry.FullName)." }
            $left = $entry.Open()
            $right = $remoteEntry.Open()
            $hash = [Security.Cryptography.SHA256]::Create()
            try {
              if ([Convert]::ToBase64String($hash.ComputeHash($left)) -ne [Convert]::ToBase64String($hash.ComputeHash($right))) {
                throw "Published package differs from the candidate: $($entry.FullName)"
              }
            } finally { $left.Dispose(); $right.Dispose(); $hash.Dispose() }
          }
        } finally { $downloaded.Dispose() }
      } finally { $candidate.Dispose() }
    } finally { Pop-Location }
  } finally {
    $env:NUGET_PACKAGES = $previousPackages
    $env:NUGET_HTTP_CACHE_PATH = $previousHttpCache
  }
  $releaseDirectory = Join-Path $repoRoot "artifacts/releases/$Version"
  New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
  Copy-Item (Join-Path $candidateDirectory $vsixName), (Join-Path $candidateDirectory 'checksums.json') $releaseDirectory
  Write-Output "Published SDK verified. VSIX ready for distribution: $releaseDirectory"
} finally { Pop-Location }
