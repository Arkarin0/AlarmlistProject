[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ForkRoot)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$fork = [IO.Path]::GetFullPath($ForkRoot)
$allowed = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts/col13')) + [IO.Path]::DirectorySeparatorChar
if (!$fork.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Select an isolated collection acceptance fork under artifacts/col13.' }
$definition = Join-Path $fork 'src/Compiler/Definition/AlarmDefinition.xml'
$palette = Join-Path $fork 'src/Compiler/Definition/Preview/Palette.xaml'
$generated = Join-Path $fork 'artifacts/obj/Alarmlist.VisualStudio/Debug/net472/Generated/AlarmDefinition.g.cs'
$staged = Join-Path $fork 'artifacts/obj/Alarmlist.VisualStudio/Debug/net472/Generated/Preview/CollectionTemplates/Preview/Palette.xaml'
$definitionBytes = [IO.File]::ReadAllBytes($definition)
$paletteBytes = [IO.File]::ReadAllBytes($palette)
$logs = Join-Path $fork 'preview-lifecycle'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
function Build([string]$name, [bool]$success = $true) {
  $previous = $ErrorActionPreference
  try {
    $ErrorActionPreference = 'Continue'
    & dotnet build (Join-Path $fork 'src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj') -v:quiet -m:1 -nr:false "-p:RestorePackagesPath=$(Join-Path $repo '.packages')" *> (Join-Path $logs "$name.log")
    $result = $LASTEXITCODE
  } finally { $ErrorActionPreference = $previous }
  if (($result -eq 0) -ne $success) { throw "Unexpected $name build result: $result. See $logs" }
}
try {
  Build 'baseline'
  $timestamp = [IO.File]::GetLastWriteTimeUtc($staged)
  $codeTimestamp = [IO.File]::GetLastWriteTimeUtc($generated)
  $originalCode = [IO.File]::ReadAllText($generated)
  Build 'no-op'
  if ([IO.File]::GetLastWriteTimeUtc($staged) -ne $timestamp -or [IO.File]::GetLastWriteTimeUtc($generated) -ne $codeTimestamp) { throw 'No-op build rewrote preview inputs.' }
  [IO.File]::AppendAllText($palette, "`r`n<!-- lifecycle change -->")
  Build 'dependency-change'
  $changedCode = [IO.File]::ReadAllText($generated)
  if ($changedCode -eq $originalCode) { throw 'Template dependency change did not change tooling metadata.' }
  [xml]$invalid = [IO.File]::ReadAllText($definition)
  $invalid.SelectSingleNode('//ComplexCollection/Preview').SetAttribute('TemplateKey', 'MissingTemplate')
  $invalid.Save($definition)
  Build 'missing-key' $false
  if ([IO.File]::ReadAllText($generated) -ne $changedCode) { throw 'Failed generation replaced the last valid metadata.' }
  [IO.File]::WriteAllBytes($definition, $definitionBytes)
  [IO.File]::WriteAllText($palette, '<ResourceDictionary')
  Build 'invalid-xaml' $false
  # Exact file in the validated private fork; never delete a source-tree asset.
  Remove-Item -LiteralPath $palette
  Build 'missing-dependency' $false
  [IO.File]::WriteAllBytes($palette, $paletteBytes)
  [xml]$without = [IO.File]::ReadAllText($definition)
  $preview = $without.SelectSingleNode('//ComplexCollection/Preview')
  [void]$preview.ParentNode.RemoveChild($preview)
  $without.Save($definition)
  Build 'removed-template'
  if (Test-Path -LiteralPath $staged) { throw 'Removed template left stale staged resources.' }
  $resources = [Resources.ResourceReader]::new((Join-Path $fork 'artifacts/obj/Alarmlist.VisualStudio/Debug/net472/Alarmlist.VisualStudio.g.resources'))
  try {
    foreach ($entry in $resources.GetEnumerator()) {
      if ([string]$entry.Key -like 'collectiontemplates/*') { throw 'Removed template remains in compiled resources.' }
    }
  } finally { $resources.Dispose() }
} finally {
  [IO.File]::WriteAllBytes($definition, $definitionBytes)
  [IO.File]::WriteAllBytes($palette, $paletteBytes)
  Build 'restored'
}
Write-Output "Template lifecycle checks passed; original private fork inputs restored. Evidence: $logs"
