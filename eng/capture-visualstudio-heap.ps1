# Optional diagnostic capture, separate from latency/throughput measurements.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateRange(1, [int]::MaxValue)]
  [int]$ProcessId,
  [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$process = Get-Process -Id $ProcessId -ErrorAction Stop
try {
  if ($process.ProcessName -notin @('devenv', 'powershell') -and !$process.ProcessName.StartsWith('testhost')) {
    throw 'Select the PID of your experimental IDE, test host, or smoke driver.'
  }
  $started = $process.StartTime.ToUniversalTime().Ticks.ToString([Globalization.CultureInfo]::InvariantCulture)
} finally { $process.Dispose() }
if (!$OutputDirectory) {
  $OutputDirectory = Join-Path $repoRoot "artifacts/log/Release/HeapProfiles/$([Guid]::NewGuid().ToString('N'))"
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$outputPath = Join-Path $OutputDirectory "heap-$([Guid]::NewGuid().ToString('N')).json"
Push-Location $repoRoot
try {
  git rev-parse HEAD | Set-Content (Join-Path $OutputDirectory 'commit.txt')
  git status --short | Set-Content (Join-Path $OutputDirectory 'working-tree.txt')
  dotnet --info | Set-Content (Join-Path $OutputDirectory 'dotnet-info.txt')
  dotnet run --project eng/tools/VisualStudioHeapProbe/VisualStudioHeapProbe.csproj -c Release -- $ProcessId $started $outputPath
  if ($LASTEXITCODE -ne 0) { throw "Heap capture failed. See $OutputDirectory" }
} finally { Pop-Location }
