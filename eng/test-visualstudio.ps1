# Functional IDE scenarios are owned by xUnit. Deploy the selected profile first.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$VisualStudioPath,
  [string]$Configuration = 'Debug',
  [string]$RootSuffix = 'AlarmlistExp',
  [string]$Filter,
  [switch]$MeasureAnalysis,
  [ValidateRange(1, 100)]
  [int]$MeasurementIterations = 20,
  [ValidateRange(1, 10000)]
  [int]$MeasurementAlarmCount = 1000,
  [switch]$KeepOpen,
  [string]$ResultFile,
  [string]$CancellationFile,
  [string]$OwnedProcessFile
)

$ErrorActionPreference = 'Stop'
# Preserve the old measurement command while keeping the functional suite in C#.
if ($MeasureAnalysis) {
  if ($Filter) { throw '-Filter applies only to functional xUnit tests.' }
  $measurementArguments = @{}
  foreach ($key in $PSBoundParameters.Keys) {
    if ($key -ne 'Filter') { $measurementArguments[$key] = $PSBoundParameters[$key] }
  }
  & (Join-Path $PSScriptRoot 'measure-visualstudio-live.ps1') @measurementArguments
  return
}
if ($KeepOpen -or $ResultFile -or $CancellationFile -or $OwnedProcessFile) {
  throw 'Functional xUnit tests own and close their IDEs and report through TRX. These driver options require -MeasureAnalysis. Use the acceptance fixture for interactive inspection.'
}
$executable = Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe'
if (!(Test-Path -LiteralPath $executable)) { throw "Visual Studio was not found: $executable" }
$major = (Get-Item -LiteralPath $executable).VersionInfo.FileMajorPart
$version = switch ($major) { 17 { '2022' } 18 { '2026' } default { throw "Unsupported Visual Studio major version: $major" } }
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj'
$results = Join-Path $repoRoot "artifacts/log/$Configuration/VisualStudioUI/TestResults/$([Guid]::NewGuid().ToString('N'))"
$settings = @{
  ALARMLIST_RUN_UI_VS_TESTS = '1'
  ALARMLIST_VS_ROOT_SUFFIX = $RootSuffix
}
$settings["ALARMLIST_VS${version}_PATH"] = [IO.Path]::GetFullPath($VisualStudioPath)
$previous = @{}
foreach ($name in $settings.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
  foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $settings[$name]) }
  $selection = "Host=VisualStudio&VSVersion=$version"
  if ($Filter) { $selection = "($selection)&($Filter)" }
  & dotnet test $project --configuration $Configuration --filter $selection --logger trx --results-directory $results
  if ($LASTEXITCODE -ne 0) { throw "UI integration tests failed with exit code $LASTEXITCODE. Results: $results" }
  $reports = @(Get-ChildItem -LiteralPath $results -Filter '*.trx')
  if ($reports.Count -ne 1) { throw "Expected one xUnit TRX report in $results." }
  [xml]$report = Get-Content -LiteralPath $reports[0].FullName -Raw
  $counts = $report.TestRun.ResultSummary.Counters
  if ([int]$counts.executed -eq 0 -or [int]$counts.executed -ne [int]$counts.total -or [int]$counts.failed -ne 0) {
    throw "The selected UI cases did not all execute successfully. Check -Filter and the report in $results."
  }
} finally {
  foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
