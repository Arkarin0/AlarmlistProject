# Run with Windows PowerShell 5.1 -STA after deploying to the AlarmlistExp instance.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$VisualStudioPath,
  [string]$Configuration = 'Debug',
  [string]$RootSuffix = 'AlarmlistExp',
  [switch]$KeepOpen,
  [switch]$MeasureAnalysis = $true,
  [ValidateRange(1, 100)]
  [int]$MeasurementIterations = 20,
  [ValidateRange(1, 10000)]
  [int]$MeasurementAlarmCount = 1000,
  [string]$ResultFile,
  [string]$CancellationFile,
  [string]$OwnedProcessFile
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA' -or [IntPtr]::Size -ne 8) {
  throw 'Run this script with x64 powershell.exe -STA (Windows PowerShell 5.1).'
}
if ($MeasureAnalysis) { Get-Command Get-FileHash -ErrorAction Stop | Out-Null }
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($ResultFile -and (Test-Path -LiteralPath $ResultFile)) { throw 'ResultFile must identify a new run; refusing to reuse an existing report.' }
$runDirectory = Join-Path $repoRoot "artifacts/log/$Configuration/VisualStudioSmoke/$([Guid]::NewGuid().ToString('N'))"
$projectDirectory = Join-Path $runDirectory 'Plant'
New-Item -ItemType Directory -Path $projectDirectory -Force | Out-Null
Copy-Item (Join-Path $repoRoot 'src/SDK/Alarmlist.SDK.Tests/testassets/boilerplate/Directory.Build.*') $runDirectory
# This retained consumer lives inside the checkout, whose source mapping otherwise
# routes every package to nuget.org. Give the isolated feed an exact SDK mapping.
@'
<configuration>
  <config><add key="globalPackagesFolder" value=".packages" /></config>
  <packageSources><clear /><add key="local" value="feed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping>
    <packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource>
  </packageSourceMapping>
</configuration>
'@ | Set-Content (Join-Path $runDirectory 'NuGet.Config')
New-Item -ItemType Directory -Path (Join-Path $runDirectory 'feed') | Out-Null
Copy-Item (Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.*.nupkg") (Join-Path $runDirectory 'feed')
Write-Output "Live measurement files and logs: $runDirectory"
# Opening a solution enters the IDE directly. A bare devenv launch may remain on
# the Start window and never publish its DTE automation object.
$solutionPath = Join-Path $runDirectory 'Smoke.sln'
@'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Global
EndGlobal
'@ | Set-Content $solutionPath

# Bind only to the process started below, never to an existing user IDE session.
$interopAssembly = Join-Path $VisualStudioPath 'Common7/IDE/PublicAssemblies/Microsoft.VisualStudio.Interop.dll'
[Reflection.Assembly]::LoadFrom($interopAssembly) | Out-Null
Add-Type -ReferencedAssemblies $interopAssembly -Path (Join-Path $repoRoot 'src/shared/VisualStudioTesting/VisualStudioAutomation.cs')


function Wait-For($Action, [string]$Description, [int]$PollMilliseconds = 500) {
  $deadline = [DateTime]::UtcNow.AddSeconds(120)
  do {
    if ($CancellationFile -and (Test-Path -LiteralPath $CancellationFile)) { throw 'Live test cancellation requested.' }
    try {
      $result = & $Action
      if ($result) { return $result }
    } catch [Runtime.InteropServices.COMException] {
      if ($_.Exception.HResult -notin @(-2147418111, -2147417846)) { throw }
    }
    Start-Sleep -Milliseconds $PollMilliseconds
  } while ([DateTime]::UtcNow -lt $deadline)
  throw "Timed out: $Description"
}

function Assert-AlarmCount([int]$Expected) {
  [xml]$compiled = Get-Content (Join-Path $projectDirectory 'bin/Debug/Plant.Alarmlist.xml')
  if (@($compiled.AlarmList.Alarm).Count -ne $Expected) {
    throw "Expected $Expected compiled alarms."
  }
}

function Read-ClrCounter([string]$Instance, [string]$Name) {
  $counter = [Diagnostics.PerformanceCounter]::new('.NET CLR Memory', $Name, $Instance, $true)
  try { return $counter.RawValue } finally { $counter.Dispose() }
}

function Measure-LiveAnalysis {
  # This exercises the running IDE through its supported automation/SDK APIs.
  # It measures buffer replacement to Error List publication, not keystroke paint.
  $measurementPath = Join-Path $projectDirectory 'Measurement.almx'
  $sources = [Text.StringBuilder]::new('<Alarmlist><Properties><Property Name="Unit" Value="001" /></Properties>')
  for ($index = 0; $index -lt $MeasurementAlarmCount; $index++) {
    [void]$sources.Append("<Alarm><FullyQualifiedName>Measurement.$index</FullyQualifiedName><Name>Alarm $index</Name><Code>`$(Unit)</Code><Description>Inspect `$(AlarmlistAlarmCode)</Description></Alarm>")
  }
  [void]$sources.Append('</Alarmlist>')
  [IO.File]::WriteAllText($measurementPath, $sources.ToString())
  Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::FindItem($dte, $null, 'Measurement.almx', '') } 'measurement input discovery' | Out-Null
  git -C $repoRoot rev-parse HEAD | Set-Content (Join-Path $runDirectory 'measurement-commit.txt')
  git -C $repoRoot status --short | Set-Content (Join-Path $runDirectory 'measurement-working-tree.txt')
  [pscustomobject]@{
    HostVersion = (Get-Item (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe')).VersionInfo.FileVersion
    Configuration = $Configuration; RootSuffix = $RootSuffix; ProcessId = $ide.Id
    AdditionalAlarms = $MeasurementAlarmCount; Iterations = $MeasurementIterations
    Workload = 'Scoped file properties and alarm-field dependencies'
    DiagnosticPollMilliseconds = 50; ForcedIdeCollection = $false
    CollectAutomationClientBeforeMemorySample = $true
    StartedUtc = [DateTime]::UtcNow.ToString('O')
  } | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'measurement-environment.json')
  $latencies = [Collections.Generic.List[object]]::new()
  $memory = [Collections.Generic.List[object]]::new()
  $sourceHash = (Get-FileHash -LiteralPath $livePath -Algorithm SHA256).Hash
  $measurementHash = (Get-FileHash -LiteralPath $measurementPath -Algorithm SHA256).Hash
  $outputHash = (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash
  $dte.Documents.CloseAll(2)
  $clrInstance = $null
  try {
    $category = [Diagnostics.PerformanceCounterCategory]::new('.NET CLR Memory')
    foreach ($instance in $category.GetInstanceNames()) {
      if ((Read-ClrCounter $instance 'Process ID') -eq $ide.Id) { $clrInstance = $instance; break }
    }
  } catch { Write-Warning "CLR memory counters are unavailable: $_" }
  Write-Output "CLR memory counter instance: $clrInstance (blank means unavailable)"

  foreach ($mode in @('XmlBuffer', 'DesignerReopen')) {
    # The first two samples warm the editor, SDK, and process; retain them but
    # exclude them from the summary. Each error has a unique reference so an old
    # row or the immediate stale-result clear cannot satisfy publication.
    for ($sample = -1; $sample -le $MeasurementIterations; $sample++) {
      $reference = "Missing$mode$sample"
      $text = $liveText.Replace('MissingLive', $reference)
      $message = "Alarm 'Live' references missing alarm '$reference'."
      if ($mode -eq 'DesignerReopen') {
        [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::OpenMeasurementDesigner($dte, $livePath)
      } elseif ($sample -eq -1) {
        Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::ReplaceXmlBuffer($dte, $livePath, $liveText) } 'measurement XML buffer opening' | Out-Null
      }
      $timer = [Diagnostics.Stopwatch]::StartNew()
      if (![Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::ReplaceOpenBuffer($dte, $livePath, $text)) {
        throw 'Measurement buffer was not available for editing.'
      }
      Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::HasErrorDescription($dte, $message) } 'measured live diagnostic publication' 50 | Out-Null
      $timer.Stop()
      $latencies.Add([pscustomobject]@{ Mode = $mode; Sample = $sample; Warmup = ($sample -le 0); Milliseconds = $timer.Elapsed.TotalMilliseconds })
      $latencies | Select-Object Mode, Sample, Warmup, @{ Name = 'Milliseconds'; Expression = {
        $_.Milliseconds.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
      } } | Export-Csv (Join-Path $runDirectory 'analysis-latency.csv') -NoTypeInformation
      if ($mode -eq 'DesignerReopen') {
        $dte.Documents.Item($livePath).Close(2)
        Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::HasErrorDescription($dte, $liveMessage) } 'measurement close restores disk analysis' 50 | Out-Null
        if ($dte.Documents.Count -ne 0) { throw 'A measurement document remained open after close.' }
        # Release temporary COM wrappers in this PowerShell driver before reading
        # the other process. Never force collection inside the IDE being measured.
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
        [GC]::Collect()
        $ide.Refresh()
        $managedBytes = $null
        $gen2Collections = $null
        if ($clrInstance) {
          try {
            if ((Read-ClrCounter $clrInstance 'Process ID') -ne $ide.Id) { throw 'CLR counter instance changed process.' }
            $managedBytes = Read-ClrCounter $clrInstance '# Bytes in all Heaps'
            $gen2Collections = Read-ClrCounter $clrInstance '# Gen 2 Collections'
          } catch {
            Write-Warning "Further CLR memory samples are unavailable: $_"
            $clrInstance = $null
          }
        }
        $memory.Add([pscustomobject]@{
          Sample = $sample; Warmup = ($sample -le 0); PrivateBytes = $ide.PrivateMemorySize64
          WorkingSetBytes = $ide.WorkingSet64; Handles = $ide.HandleCount; OpenDocuments = $dte.Documents.Count
          CapturedUtc = [DateTime]::UtcNow.ToString('O')
          ManagedHeapBytes = $managedBytes; Gen2Collections = $gen2Collections
        })
        $memory | Export-Csv (Join-Path $runDirectory 'designer-close-memory.csv') -NoTypeInformation
      }
      Write-Output ("Measured {0} sample {1}: {2:N1} ms" -f $mode, $sample, $timer.Elapsed.TotalMilliseconds)
    }
    if ($mode -eq 'XmlBuffer') {
      $dte.Documents.Item($livePath).Close(2)
      Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::HasErrorDescription($dte, $liveMessage) } 'XML measurement close restores disk analysis' | Out-Null
    }
  }
  if ((Get-FileHash -LiteralPath $livePath -Algorithm SHA256).Hash -ne $sourceHash -or
      (Get-FileHash -LiteralPath $measurementPath -Algorithm SHA256).Hash -ne $measurementHash -or
      (Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash -ne $outputHash) {
    throw 'Analysis measurements changed source or compiled output on disk.'
  }
  $script:measurementSourcesPreserved = $true
  Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::FindItem($dte, $null, 'Measurement.almx', 'delete') } 'measurement input cleanup' | Out-Null
  foreach ($group in ($latencies | Where-Object { !$_.Warmup } | Group-Object Mode)) {
    $values = @($group.Group.Milliseconds | Sort-Object)
    $middle = [int][Math]::Floor($values.Count / 2)
    $median = if ($values.Count % 2) { $values[$middle] } else { ($values[$middle - 1] + $values[$middle]) / 2 }
    Write-Output ("{0}: median {1:N1} ms, max {2:N1} ms ({3} warm samples)" -f $group.Name, $median, $values[-1], $values.Count)
  }
  Write-Output 'Measurements retained; no timing thresholds or forced collection were applied to the IDE.'
}

$ide = $null
$dte = $null
$retainInstance = $false
$completedSuccessfully = $false
$measurementSourcesPreserved = $false
$failureMessage = ''
[Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::RegisterMessageFilter()
try {
  $ide = Start-Process -FilePath (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe') `
    -ArgumentList @(('"' + $solutionPath + '"'), '/RootSuffix', $RootSuffix, '/Log', ('"' + (Join-Path $runDirectory 'ActivityLog.xml') + '"')) `
    -WindowStyle Hidden -PassThru
  if ($OwnedProcessFile) {
    $ownership = [xml]'<OwnedIde />'
    $ownership.DocumentElement.SetAttribute('ProcessId', [string]$ide.Id)
    $ownership.DocumentElement.SetAttribute('DriverProcessId', [string]$PID)
    $ownership.DocumentElement.SetAttribute('StartUtcTicks', [string]$ide.StartTime.ToUniversalTime().Ticks)
    $ownership.DocumentElement.SetAttribute('Executable', [IO.Path]::GetFullPath((Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe')))
    $ownership.DocumentElement.SetAttribute('Solution', $solutionPath)
    $ownership.Save([IO.Path]::GetFullPath($OwnedProcessFile))
  }
  $dte = Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::Find($ide.Id) } 'Visual Studio automation startup'
  # The ROT entry may be published before the IDE accepts automation calls.
  Wait-For {
    $dte.SuppressUI = $true
    $dte.MainWindow.Visible = $false
    $dte.Solution.IsOpen
  } 'empty solution loading' | Out-Null
  $projectTemplate = [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::GetTemplate($dte, $false)
  $itemTemplate = [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::GetTemplate($dte, $true)
  if (!(Test-Path $projectTemplate) -or !(Test-Path $itemTemplate)) { throw 'Installed Alarmlist templates were not found.' }
  $dte.Solution.AddFromTemplate($projectTemplate, $projectDirectory, 'Plant', $false) | Out-Null
  $project = Wait-For { if ($dte.Solution.Projects.Count -eq 1) { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::GetProject($dte) } } 'project creation'
  $dte.Solution.SaveAs((Join-Path $runDirectory 'Smoke.sln'))
  $dte.Solution.SolutionBuild.Build($true)
  if ($dte.Solution.SolutionBuild.LastBuildInfo -ne 0) { throw 'The new project did not build.' }
  Assert-AlarmCount 1
  Write-Output 'Created a project from the installed template and built its starter alarm.'
  $outputFile = Join-Path $projectDirectory 'bin/Debug/Plant.Alarmlist.xml'
  $livePath = Join-Path $projectDirectory 'Live.almx'
  $liveText = "<Alarmlist>`r`n  <Alarm><FullyQualifiedName>Live</FullyQualifiedName>`r`n    <ReferenceName>MissingLive</ReferenceName></Alarm>`r`n</Alarmlist>"
  $liveMessage = "Alarm 'Live' references missing alarm 'MissingLive'."
  [IO.File]::WriteAllText($livePath, $liveText)
  Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::FindItem($dte, $null, 'Live.almx', '') } 'live input discovery' | Out-Null
  Write-Output 'Discovered the live diagnostic input.'
  Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::HasErrorDescription($dte, $liveMessage) } 'live diagnostic before Build' | Out-Null
  if ($MeasureAnalysis) { Measure-LiveAnalysis }
  Wait-For { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::FindItem($dte, $null, 'Live.almx', 'delete') } 'live input deletion' | Out-Null
  Wait-For { ![Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::HasErrorDescription($dte, $liveMessage) } 'removed input clears live diagnostic' | Out-Null
  Write-Output 'Live measurement fixture cleanup passed.'

  if ($KeepOpen) {
    $dte.ItemOperations.OpenFile((Join-Path $projectDirectory 'Alarms.almx')) | Out-Null
    $dte.SuppressUI = $false
    $dte.MainWindow.Visible = $true
    $retainInstance = $true
    Write-Output "Experimental instance retained for UI inspection. Process: $($ide.Id)"
  }
  $completedSuccessfully = $true
} catch {
  $failureMessage = $_.Exception.ToString()
  if ($dte -and $KeepOpen) {
    $dte.SuppressUI = $false
    $dte.MainWindow.Visible = $true
    $retainInstance = $true
    Write-Output "Failed experimental instance retained for inspection. Process: $($ide.Id)"
  }
  if ($dte) { try { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::DumpBuildOutput($dte) } catch { Write-Warning $_ } }
  Write-Output $_.Exception.ToString()
  throw
} finally {
  if ($dte -and !$retainInstance) {
    try { [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::CloseSolution($dte, $false); $dte.Quit() } catch { Write-Warning $_ }
  }
  if ($ide -and !$retainInstance -and !$ide.WaitForExit(10000)) { Stop-Process -Id $ide.Id }
  [Alarmlist.VisualStudio.Testing.VisualStudioAutomation]::RestoreMessageFilter()
  if ($ResultFile) {
    $cleanupCompleted = !$retainInstance -and (!$ide -or $ide.WaitForExit(10000))
    $report = [xml]'<LiveVisualStudioRun />'
    $root = $report.DocumentElement
    $hostFile = (Get-Item (Join-Path $VisualStudioPath 'Common7/IDE/devenv.exe')).VersionInfo
    $values = @{
      SchemaVersion = '1'; Succeeded = $completedSuccessfully; CleanupCompleted = $cleanupCompleted
      DriverBitness = [IntPtr]::Size * 8
      RunDirectory = $runDirectory; Configuration = $Configuration; RootSuffix = $RootSuffix
      HostVersion = '{0}.{1}.{2}.{3}' -f $hostFile.FileMajorPart, $hostFile.FileMinorPart, $hostFile.FileBuildPart, $hostFile.FilePrivatePart
      Measurements = $MeasureAnalysis.IsPresent; Iterations = $MeasurementIterations
      AdditionalAlarms = $MeasurementAlarmCount; SourcesPreserved = $measurementSourcesPreserved
      Failure = $failureMessage
    }
    foreach ($name in $values.Keys) { $root.SetAttribute($name, [string]$values[$name]) }
    foreach ($table in @(@('Latencies', 'analysis-latency.csv'), @('Memory', 'designer-close-memory.csv'))) {
      $csv = Join-Path $runDirectory $table[1]
      $element = $report.CreateElement($table[0])
      [void]$root.AppendChild($element)
      if (Test-Path -LiteralPath $csv) {
        foreach ($sample in (Import-Csv -LiteralPath $csv)) {
          $row = $report.CreateElement('Sample')
          foreach ($property in $sample.PSObject.Properties) { $row.SetAttribute($property.Name, [string]$property.Value) }
          [void]$element.AppendChild($row)
        }
      }
    }
    $report.Save([IO.Path]::GetFullPath($ResultFile))
  }
}
