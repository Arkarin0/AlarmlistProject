[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$runRoot = Join-Path $repoRoot "artifacts/log/Debug/AlarmDefinitionGeneration/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $runRoot | Out-Null
$compiler = Join-Path $repoRoot 'src/Compiler/Alarmlist.Core.csproj'
$generator = Join-Path $repoRoot 'eng/tools/AlarmDefinitionGenerator/AlarmDefinitionGenerator.csproj'
$generated = Join-Path $repoRoot 'artifacts/obj/Alarmlist.Core/Debug/net8.0/Generated/AlarmDefinition.g.cs'

function Invoke-CheckedDotNet([string]$name, [string[]]$arguments) {
  $log = Join-Path $runRoot "$name.log"
  & dotnet @arguments *> $log
  if ($LASTEXITCODE -ne 0) { throw "$name failed. See $log" }
}

Push-Location $repoRoot
try {
  Invoke-CheckedDotNet 'clean-compiler' @('clean', $compiler, '--verbosity', 'quiet', '-m:1', '-nr:false')
  Invoke-CheckedDotNet 'clean-generator' @('clean', $generator, '--verbosity', 'quiet', '-m:1', '-nr:false')
  if (Test-Path -LiteralPath $generated) { throw 'Clean did not remove generated compilation inputs.' }
  Invoke-CheckedDotNet 'fresh-design-time' @('msbuild', $compiler, '-t:Compile', '-p:TargetFramework=net8.0', '-p:DesignTimeBuild=true', '-p:BuildProjectReferences=false', '-p:SkipCompilerExecution=true', '-verbosity:quiet', '-m:1', '-nr:false')
  if (!(Test-Path -LiteralPath $generated)) { throw 'A fresh design-time compile did not generate its inputs.' }
  Invoke-CheckedDotNet 'parallel-build' @('build', $compiler, '--verbosity', 'quiet', '-m:4', '-nr:false')
  $initialHash = (Get-FileHash -LiteralPath $generated -Algorithm SHA256).Hash
  $initialWrite = (Get-Item -LiteralPath $generated).LastWriteTimeUtc
  Invoke-CheckedDotNet 'no-op-build' @('build', $compiler, '--verbosity', 'quiet', '-m:4', '-nr:false')
  if ((Get-FileHash -LiteralPath $generated -Algorithm SHA256).Hash -ne $initialHash -or
      (Get-Item -LiteralPath $generated).LastWriteTimeUtc -ne $initialWrite) {
    throw 'An unchanged build rewrote generated source.'
  }
  $invalidDefinition = Join-Path $runRoot 'invalid.xml'
  Set-Content -LiteralPath $invalidDefinition -Value '<AlarmDefinition Version="2" />' -Encoding utf8
  $failureLog = Join-Path $runRoot 'invalid-definition.log'
  & dotnet build $compiler --framework net8.0 "-p:AlarmlistDefinitionFile=$invalidDefinition" --verbosity quiet -m:1 -nr:false *> $failureLog
  if ($LASTEXITCODE -eq 0 -or !(Select-String -LiteralPath $failureLog -SimpleMatch 'ALMGEN001' -Quiet)) {
    throw 'An invalid definition did not fail compilation with a generator diagnostic.'
  }
  if ((Get-FileHash -LiteralPath $generated -Algorithm SHA256).Hash -ne $initialHash) {
    throw 'Failed generation replaced the prior complete output.'
  }
  Invoke-CheckedDotNet 'recovered-build' @('build', $compiler, '--verbosity', 'quiet', '-m:4', '-nr:false')
  $fixture = Join-Path $runRoot 'GeneratedTypes'
  New-Item -ItemType Directory -Path $fixture | Out-Null
  $typeDefinition = Join-Path $fixture 'types.xml'
  Set-Content -LiteralPath $typeDefinition -Encoding utf8 -Value @'
<AlarmDefinition Version="1">
  <Field Name="Message" Type="String" Label="Quoted &quot;label&quot;" />
  <Field Name="Count" Type="Int64" />
  <Field Name="Price" Type="Decimal" />
  <Field Name="Ratio" Type="Double" />
  <Field Name="Enabled" Type="Boolean" />
  <Field Name="Priority" Type="Enum"><Value Name="Low"/><Value Name="High"/></Field>
  <Field Name="Day" Type="Date" />
  <Field Name="Clock" Type="Time" />
  <Field Name="Instant" Type="Timestamp" />
</AlarmDefinition>
'@
  $toolAssembly = Join-Path $repoRoot 'artifacts/bin/AlarmDefinitionGenerator/Debug/net8.0/AlarmDefinitionGenerator.dll'
  Invoke-CheckedDotNet 'generate-all-types' @($toolAssembly, $typeDefinition, (Join-Path $fixture 'Generated.cs'), 'compiler')
  Invoke-CheckedDotNet 'generate-host-types' @($toolAssembly, $typeDefinition, (Join-Path $fixture 'Host.cs'), 'host')
  Set-Content -LiteralPath (Join-Path $fixture 'Stubs.cs') -Encoding utf8 -Value @'
namespace Alarmlist.Compiler { public sealed class TestProcedure { } }
namespace Alarmlist.Syntax
{
    public partial class AlarmSyntaxNode
    {
        private string GetEffectiveFieldValue(string name) => null;
        private void SetLocalFieldValue(string name, string value) { }
    }
}
'@
  $fixtureProject = Join-Path $fixture 'GeneratedTypes.csproj'
  Set-Content -LiteralPath $fixtureProject -Encoding utf8 -Value '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>net472;net8.0</TargetFrameworks><IsPackable>false</IsPackable></PropertyGroup></Project>'
  Invoke-CheckedDotNet 'compile-all-generated-types' @('build', $fixtureProject, '--verbosity', 'quiet', '-m:1', '-nr:false')
  Write-Output "Generation lifecycle checks passed. Evidence: $runRoot"
} finally {
  Pop-Location
}
