[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$runRoot = Join-Path $repoRoot "artifacts/log/Debug/AlarmCollections/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $runRoot | Out-Null
function Invoke-Checked([string]$name, [string[]]$arguments) {
    & dotnet @arguments *> (Join-Path $runRoot "$name.log")
    if ($LASTEXITCODE -ne 0) { throw "$name failed. See $runRoot" }
}
Push-Location $repoRoot
try {
    $generatorDirectory = Join-Path $runRoot 'generator'
    $runtimeDirectory = Join-Path $runRoot 'runtime'
    New-Item -ItemType Directory -Path $generatorDirectory, $runtimeDirectory | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'eng/testassets/AlarmCollections/Preview') -Destination (Join-Path $runtimeDirectory 'Preview') -Recurse
    $generatorProject = Join-Path $generatorDirectory 'CollectionFixtureGenerator.csproj'
    Set-Content -LiteralPath $generatorProject -Encoding utf8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFrameworks /><TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <Compile Include="$repoRoot/eng/testassets/AlarmCollections/GenerateFixture.cs" />
    <ProjectReference Include="$repoRoot/eng/tools/AlarmDefinitionGenerator/AlarmDefinitionGenerator.csproj" />
  </ItemGroup>
</Project>
"@
    Invoke-Checked 'build-generator' @('build', $generatorProject, '--verbosity', 'quiet', '-m:1', '-nr:false')
    $runtimeProject = Join-Path $runtimeDirectory 'CollectionRuntimeChecks.csproj'
    Set-Content -LiteralPath $runtimeProject -Encoding utf8 -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFrameworks>net472;net8.0</TargetFrameworks><OutputType>Exe</OutputType><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <Compile Include="$repoRoot/src/Compiler/**/*.cs" />
    <Compile Include="$repoRoot/src/shared/Configuration/EditorConfigPreamble.cs" />
    <Compile Include="$repoRoot/src/shared/Configuration/ResourceFileName.cs" />
    <Compile Include="$repoRoot/eng/testassets/AlarmCollections/Program.cs" />
    <Compile Include="$repoRoot/eng/testassets/Exports/ExportJsonChecks.cs" />
    <Compile Include="$repoRoot/src/Exporters/Alarmlist.Exporters.Zenon/ZenonExportProvider.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisSnapshot.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisCollections.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisExplanation.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisContractReader.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisContractReader.References.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisReferences.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisProperty.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisProcedureCreation.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisLocalization.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisLookup.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisLookupList.cs" />
    <Compile Include="$repoRoot/src/VisualStudio/Alarmlist.VisualStudio/Analysis/AnalysisLibrary.cs" />
    <EmbeddedResource Include="$repoRoot/src/Compiler/Lookups/AlarmlistLookup.xsd" LogicalName="Alarmlist.Lookups.AlarmlistLookup.xsd" />
  </ItemGroup>
</Project>
"@
    foreach ($procedure in @('disabled', 'enabled', 'minimum')) {
        [xml]$definition = Get-Content -LiteralPath (Join-Path $repoRoot 'eng/testassets/AlarmCollections/Definition.xml') -Raw
        if ($procedure -eq 'enabled') { [void]$definition.DocumentElement.AppendChild($definition.CreateElement('TestProcedure')) }
        if ($procedure -eq 'minimum') { $definition.AlarmDefinition.SimpleCollection | Where-Object Name -eq 'Tags' | ForEach-Object { $_.SetAttribute('MinCount', '1') } }
        $definitionPath = Join-Path $runtimeDirectory 'Definition.xml'
        $definition.Save($definitionPath)
        Invoke-Checked "$procedure-generate" @((Join-Path $repoRoot 'artifacts/bin/CollectionFixtureGenerator/Debug/net8.0/CollectionFixtureGenerator.dll'), $definitionPath, (Join-Path $runtimeDirectory 'Generated.cs'))
        Invoke-Checked "$procedure-build" @('build', $runtimeProject, '--verbosity', 'quiet', '-m:1', '-nr:false')
        Invoke-Checked "$procedure-net8" @((Join-Path $repoRoot 'artifacts/bin/CollectionRuntimeChecks/Debug/net8.0/CollectionRuntimeChecks.dll'))
        & (Join-Path $repoRoot 'artifacts/bin/CollectionRuntimeChecks/Debug/net472/CollectionRuntimeChecks.exe') *> (Join-Path $runRoot "$procedure-net472.log")
        if ($LASTEXITCODE -ne 0) { throw "$procedure net472 checks failed. See $runRoot" }
        if ($procedure -ne 'minimum') {
            $coreHashes = @(Select-String -LiteralPath (Join-Path $runRoot "$procedure-net8.log") -Pattern '^JSON_SHA256:' | ForEach-Object Line)
            $desktopHashes = @(Select-String -LiteralPath (Join-Path $runRoot "$procedure-net472.log") -Pattern '^JSON_SHA256:' | ForEach-Object Line)
            if ($coreHashes.Count -eq 0 -or ($coreHashes -join ';') -ne ($desktopHashes -join ';')) { throw "JSON output differs between task frameworks: $procedure" }
        }

    }
    Write-Output "Collection runtime checks passed on both frameworks with procedures enabled and disabled. Evidence: $runRoot"
} finally { Pop-Location }
