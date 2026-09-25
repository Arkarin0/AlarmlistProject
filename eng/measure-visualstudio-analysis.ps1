# Measure the packaged net472 bridge and real designer BAML outside Visual Studio.
[CmdletBinding()]
param(
  [ValidateSet('Release')]
  [string]$Configuration = 'Release',
  [ValidateSet('Dry', 'Short', 'Full')]
  [string]$Job = 'Short',
  [string]$Filter = 'Category=Performance&Host=Isolated'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$runDirectory = Join-Path $repoRoot "artifacts/log/$Configuration/AnalysisPerformance/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$previousEnabled = $env:ALARMLIST_RUN_PERFORMANCE_TESTS
$previousOutput = $env:ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY
$previousJob = $env:ALARMLIST_PERFORMANCE_JOB
Push-Location $repoRoot
try {
  $env:ALARMLIST_RUN_PERFORMANCE_TESTS = '1'
  $env:ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY = $runDirectory
  $env:ALARMLIST_PERFORMANCE_JOB = $Job
  git rev-parse HEAD | Set-Content (Join-Path $runDirectory 'commit.txt')
  git status --short | Set-Content (Join-Path $runDirectory 'working-tree.txt')
  dotnet --info | Set-Content (Join-Path $runDirectory 'dotnet-info.txt')
  Write-Output "Performance results: $runDirectory"
  dotnet test src/VisualStudio/Alarmlist.VisualStudio.PerformanceTests/Alarmlist.VisualStudio.PerformanceTests.csproj `
    --configuration $Configuration --filter $Filter --results-directory $runDirectory `
    --logger trx -- RunConfiguration.TargetPlatform=x64
  if ($LASTEXITCODE -ne 0) { throw "Performance run failed. See $runDirectory" }
  Get-ChildItem -LiteralPath $runDirectory -Recurse -Filter '*-report-github.md' | ForEach-Object {
    Get-Content -LiteralPath $_.FullName -Encoding UTF8
  }
} finally {
  $env:ALARMLIST_RUN_PERFORMANCE_TESTS = $previousEnabled
  $env:ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY = $previousOutput
  $env:ALARMLIST_PERFORMANCE_JOB = $previousJob
  Pop-Location
}
