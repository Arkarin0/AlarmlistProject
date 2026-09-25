# Measure the packaged net472 bridge and real designer BAML outside Visual Studio.
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$runDirectory = Join-Path $repoRoot "artifacts/log/$Configuration/AnalysisPerformance/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$previousEnabled = $env:ALARMLIST_RUN_PERFORMANCE_TESTS
$previousOutput = $env:ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY
Push-Location $repoRoot
try {
  $env:ALARMLIST_RUN_PERFORMANCE_TESTS = '1'
  $env:ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY = $runDirectory
  git rev-parse HEAD | Set-Content (Join-Path $runDirectory 'commit.txt')
  git status --short | Set-Content (Join-Path $runDirectory 'working-tree.txt')
  dotnet --info | Set-Content (Join-Path $runDirectory 'dotnet-info.txt')
  Write-Output "Performance results: $runDirectory"
  dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj `
    --configuration $Configuration --filter 'Category=Performance' --results-directory $runDirectory `
    --logger trx -- RunConfiguration.TargetPlatform=x64
  if ($LASTEXITCODE -ne 0) { throw "Performance run failed. See $runDirectory" }
  Get-ChildItem -LiteralPath $runDirectory -Filter '*.csv' | ForEach-Object {
    Write-Output $_.Name
    Import-Csv -LiteralPath $_.FullName | Group-Object metric | ForEach-Object {
      $values = @($_.Group | ForEach-Object { [double]::Parse($_.value, [Globalization.CultureInfo]::InvariantCulture) } | Sort-Object)
      [pscustomobject]@{ Metric = $_.Name; Median = $values[[int][Math]::Floor($values.Count / 2)]; Max = $values[-1]; Unit = $_.Group[0].unit }
    } | Format-Table -AutoSize
  }
} finally {
  $env:ALARMLIST_RUN_PERFORMANCE_TESTS = $previousEnabled
  $env:ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY = $previousOutput
  Pop-Location
}
