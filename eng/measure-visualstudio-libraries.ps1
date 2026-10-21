# Dot-sourced by measure-visualstudio-live.ps1 inside its owned IDE lifecycle.
function Measure-LiveLibraries {
  [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::CloseSolution($dte, $false)
  $script:project = $null
  $projectPath = Join-Path $projectDirectory 'Plant.almproj'
  $originalProject = [IO.File]::ReadAllBytes($projectPath)
  $outputHash = (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash
  $libraryDirectory = Join-Path $runDirectory 'Library'
  New-Item -ItemType Directory -Path $libraryDirectory | Out-Null
  $libraryPath = Join-Path $libraryDirectory 'Library.almproj'
  $librarySource = Join-Path $libraryDirectory 'Library.almx'
  $projectXml = [xml][IO.File]::ReadAllText($projectPath)
  $libraryXml = [xml]'<Project><PropertyGroup><OutputType>Library</OutputType><PackageId>Measurement.Library</PackageId></PropertyGroup></Project>'
  $libraryXml.DocumentElement.SetAttribute('Sdk', $projectXml.DocumentElement.GetAttribute('Sdk'))
  $libraryXml.Save($libraryPath)
  $items = $projectXml.CreateElement('ItemGroup')
  $reference = $projectXml.CreateElement('ProjectReference')
  $reference.SetAttribute('Include', '../Library/Library.almproj')
  [void]$items.AppendChild($reference)
  [void]$projectXml.DocumentElement.AppendChild($items)
  $projectXml.Save($projectPath)
  $sources = [Text.StringBuilder]::new('<Alarmlist><Alarm abstract="true"><FullyQualifiedName>Template</FullyQualifiedName><Properties><Property Name="RequiredValue" /></Properties><Name>$(RequiredValue)</Name></Alarm>')
  for ($index = 0; $index -lt $MeasurementAlarmCount; $index++) {
    [void]$sources.Append("<Alarm><FullyQualifiedName>Imported.$index</FullyQualifiedName><ReferenceName>Template</ReferenceName></Alarm>")
  }
  [void]$sources.Append('</Alarmlist>')
  $baseline = $sources.ToString()
  [IO.File]::WriteAllText($librarySource, $baseline)
  $preserved = @{}
  foreach ($path in @($projectPath, $libraryPath) + @(Get-ChildItem -LiteralPath $projectDirectory -Filter '*.almx' | ForEach-Object FullName)) {
    $preserved[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
  }
  git -C $repoRoot rev-parse HEAD | Set-Content (Join-Path $runDirectory 'measurement-commit.txt')
  git -C $repoRoot status --short | Set-Content (Join-Path $runDirectory 'measurement-working-tree.txt')
  [pscustomobject]@{
    HostVersion = (Get-Item (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe')).VersionInfo.FileVersion
    Configuration = $Configuration; RootSuffix = $RootSuffix; ProcessId = $ide.Id
    AdditionalAlarms = $MeasurementAlarmCount; Iterations = $MeasurementIterations
    LibraryWorkload = $true; DiagnosticPollMilliseconds = 50; ForcedIdeCollection = $false
    Workload = 'Saved external library to unique diagnostic; consumer removal from solution to diagnostic removal'
    StartedUtc = [DateTime]::UtcNow.ToString('O')
  } | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'measurement-environment.json')
  $latencies = [Collections.Generic.List[object]]::new()
  $memory = [Collections.Generic.List[object]]::new()
  try {
    [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::OpenSolution($dte, $solutionPath)
    # Template creation opens its starter source and solution reopening can
    # restore that view. This workload deliberately measures saved inputs only.
    $dte.Documents.CloseAll(2)
    $baselineMessage = "Alarm 'Imported.0' uses parameter 'RequiredValue' without an available assignment."
    Wait-For { Test-LiveDiagnostic $baselineMessage } 'initial imported consumer diagnostic' 50 | Out-Null
    for ($sample = -1; $sample -le $MeasurementIterations; $sample++) {
      $parameter = 'RequiredValue' + ($sample + 2)
      $message = "Alarm 'Imported.0' uses parameter '$parameter' without an available assignment."
      $changed = $baseline.Replace('RequiredValue', $parameter)
      $timer = [Diagnostics.Stopwatch]::StartNew()
      [IO.File]::WriteAllText($librarySource, $changed)
      Wait-For { Test-LiveDiagnostic $message } 'saved library diagnostic publication' 50 | Out-Null
      $timer.Stop()
      $latencies.Add([pscustomobject]@{ Mode = 'SavedLibrary'; Sample = $sample; Warmup = ($sample -le 0); Milliseconds = $timer.Elapsed.TotalMilliseconds })
      if ($dte.Solution.Projects.Count -ne 1 -or $dte.Documents.Count -ne 0) {
        throw "Unexpected measurement state: $($dte.Solution.Projects.Count) projects, $($dte.Documents.Count) documents."
      }
      $timer.Restart()
      [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::RemoveMeasurementProject($dte)
      Wait-For { $dte.Solution.Projects.Count -eq 0 -and !(Test-LiveDiagnostic $message) } 'consumer unload clears diagnostics' 50 | Out-Null
      $timer.Stop()
      $latencies.Add([pscustomobject]@{ Mode = 'ProjectUnload'; Sample = $sample; Warmup = ($sample -le 0); Milliseconds = $timer.Elapsed.TotalMilliseconds })
      $latencies | Select-Object Mode, Sample, Warmup, @{ Name = 'Milliseconds'; Expression = {
        $_.Milliseconds.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
      } } | Export-Csv (Join-Path $runDirectory 'analysis-latency.csv') -NoTypeInformation
      # Collect only the external automation driver, never the measured IDE.
      [GC]::Collect()
      [GC]::WaitForPendingFinalizers()
      [GC]::Collect()
      $ide.Refresh()
      $memory.Add([pscustomobject]@{
        Sample = $sample; Warmup = ($sample -le 0); PrivateBytes = $ide.PrivateMemorySize64
        WorkingSetBytes = $ide.WorkingSet64; Handles = $ide.HandleCount
        OpenDocuments = $dte.Documents.Count; LoadedProjects = $dte.Solution.Projects.Count
        CapturedUtc = [DateTime]::UtcNow.ToString('O'); ManagedHeapBytes = $null; Gen2Collections = $null
      })
      $memory | Export-Csv (Join-Path $runDirectory 'project-unload-memory.csv') -NoTypeInformation
      [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::AddMeasurementProject($dte, $projectPath)
      Wait-For { Test-LiveDiagnostic $message } 'reloaded consumer analyzes saved library' 50 | Out-Null
      Write-Output "Measured saved library refresh and consumer unload sample $sample."
    }
    foreach ($path in $preserved.Keys) {
      if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $preserved[$path]) { throw "Measurement changed preserved input: $path" }
    }
    if ((Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash -ne $outputHash -or
        @(Get-ChildItem -LiteralPath $libraryDirectory -Filter '*.almlib' -Recurse).Count -ne 0) { throw 'Analysis changed build outputs.' }
    [IO.File]::WriteAllText($librarySource, $baseline)
    Wait-For { Test-LiveDiagnostic $baselineMessage } 'restored library baseline' 50 | Out-Null
    $script:measurementSourcesPreserved = $true
  } finally {
    [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::CloseSolution($dte, $false)
    [IO.File]::WriteAllBytes($projectPath, $originalProject)
    [IO.File]::WriteAllText($librarySource, $baseline)
  }
  Write-Output 'Raw saved-library and consumer-unload samples retained; no timing thresholds or forced IDE collection.'
}
