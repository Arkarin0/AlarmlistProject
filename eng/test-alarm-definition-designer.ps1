[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$VisualStudio2022Path,
  [Parameter(Mandatory = $true)][string]$VisualStudio2026Path,
  [switch]$IncludePreview,
  [switch]$FullWorkflow
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$fork = Join-Path $repo ('artifacts/fork11/' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $fork | Out-Null
# Copy source files, including this iteration's new files, but never local outputs.
$files = @(& git -C $repo ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository sources.' }
foreach ($file in $files) {
  $source = Join-Path $repo $file
  if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
  $target = [IO.Path]::GetFullPath((Join-Path $fork $file))
  if (!$target.StartsWith($fork + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escaped fixture.' }
  New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
  Copy-Item -LiteralPath $source -Destination $target
}
& git -C $fork init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize isolated fork.' }
# The only source customization: remove Name/Category, retype Code, change presentation
# and add all supported scalar shapes. Defaults remain compiler-owned.
Set-Content -LiteralPath (Join-Path $fork 'src/Compiler/Definition/AlarmDefinition.xml') -Encoding utf8 -Value @'
<AlarmDefinition Version="1">
  <Field Name="Description" Type="String" Multiline="true" />
  <Field Name="Code" Type="Int64" Required="true" Default="7" Minimum="0" Maximum="10" Label="Alarm number" />
  <Field Name="Price" Type="Decimal" />
  <Field Name="Ratio" Type="Double" PreviewVisible="false" />
  <Field Name="Enabled" Type="Boolean" />
  <Field Name="Priority" Type="Enum"><Value Name="Low" Label="Low priority"/><Value Name="High" Label="High priority"/></Field>
  <Field Name="Day" Type="Date" />
  <Field Name="Clock" Type="Time" />
  <Field Name="Instant" Type="Timestamp" />
  <Field Name="Source" Type="String" Label="Source field" Default="Ordinary source value" />
  <Field Name="Identity" Type="String" Label="Identity field" Default="Ordinary identity value" />
  <Field Name="TestMethod" Type="String" Label="Method field" Default="Ordinary method value" />
</AlarmDefinition>
'@
$project = Join-Path $fork 'src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj'
& dotnet build $project --verbosity quiet -m:1 -nr:false "-p:RestorePackagesPath=$(Join-Path $repo '.packages')" *> (Join-Path $fork 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Alternate build failed: $fork/build.log" }
if ($FullWorkflow) {
  $IncludePreview = $true
  & (Join-Path $PSScriptRoot 'test-alarm-definition-fork-consumers.ps1') -ForkRoot $fork -DesktopMSBuildPath (Join-Path $VisualStudio2022Path 'MSBuild/Current/Bin/MSBuild.exe')
}
$settings = @{
  ALARMLIST_RUN_UI_VS_TESTS = '1'
  ALARMLIST_VS_ROOT_SUFFIX = 'AlarmlistExp'
  ALARMLIST_VS2022_PATH = $VisualStudio2022Path
  ALARMLIST_VS2026_PATH = $VisualStudio2026Path
}
$previous = @{}
foreach ($name in $settings.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
  foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $settings[$name]) }
  foreach ($version in @('2022', '2026')) {
    $results = Join-Path $fork "results-$version"
    $areas = if ($IncludePreview) { '(Area=MetadataFields|Area=MetadataPreview)' } else { 'Area=MetadataFields' }
    & dotnet test $project --no-build --no-restore --filter "Host=VisualStudio&VSVersion=$version&$areas" --logger trx --results-directory $results *> (Join-Path $fork "native-$version.log")
    if ($LASTEXITCODE -ne 0) { throw "Alternate native test failed: $fork/native-$version.log" }
    $reports = @(Get-ChildItem -LiteralPath $results -Filter '*.trx')
    if ($reports.Count -ne 1) { throw 'Missing native report.' }
    [xml]$report = Get-Content -LiteralPath $reports[0].FullName -Raw
    $counts = $report.TestRun.ResultSummary.Counters
    $expected = if ($IncludePreview) { 2 } else { 1 }
    if ([int]$counts.total -ne $expected -or [int]$counts.passed -ne $expected) { throw 'Alternate native scenarios did not pass.' }
  }
} finally {
  foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
  # Each run owns/closes its IDE. Re-deploy and verify the working checkout's
  # standard extension on both hosts, including after a failed alternate test.
  $restoreErrors = @()
  foreach ($hostVersion in @('2022', '2026')) {
    $installation = if ($hostVersion -eq '2022') { $VisualStudio2022Path } else { $VisualStudio2026Path }
    try {
      & (Join-Path $repo 'eng/test-visualstudio.ps1') -VisualStudioPath $installation -Filter 'Area=MetadataFields' *> (Join-Path $fork "restore-$hostVersion.log")
    } catch { $restoreErrors += "VS${hostVersion}: $_" }
  }
  if ($restoreErrors.Count -ne 0) { throw "Standard deployment restoration failed: $($restoreErrors -join '; '). See $fork." }
}
Write-Output "Alternate designer checks and standard deployment restoration passed: $fork"
