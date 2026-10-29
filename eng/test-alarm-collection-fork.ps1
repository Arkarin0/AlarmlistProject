[CmdletBinding()]
param(
    [string]$DesktopMSBuildPath,
    [switch]$Designer,
    [switch]$SkipConsumers,
    [switch]$XmlCompletion,
    [switch]$Exports,
    [string]$VisualStudio2022Path,
    [string]$VisualStudio2026Path,
    [ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')]
    [string]$RootSuffix = 'AlarmlistDefinitionExp'
)
$ErrorActionPreference = 'Stop'
if (!$SkipConsumers -and !$DesktopMSBuildPath) { throw 'DesktopMSBuildPath is required for consumer acceptance.' }
if ($SkipConsumers -and !$Designer) { throw 'Select Designer when skipping consumer acceptance.' }
if ($XmlCompletion -and !$Designer) { throw 'Select Designer for XML completion acceptance.' }
if ($Designer -and (!$VisualStudio2022Path -or !$VisualStudio2026Path)) { throw 'Both IDE paths are required for designer acceptance.' }
$repo = Split-Path $PSScriptRoot -Parent
$files = @(& git -C $repo ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository sources.' }
foreach ($procedure in @($true, $false)) {
    $fork = Join-Path $repo ('artifacts/col13/' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $fork | Out-Null
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
    [xml]$definition = Get-Content -LiteralPath (Join-Path $repo 'example/CustomDefinition/AlarmDefinition.xml') -Raw
    $definition.DocumentElement.SetAttribute('Version', '2')
    foreach ($member in @($definition.DocumentElement.ChildNodes | Where-Object { $_.LocalName -in @('SimpleCollection', 'ComplexCollection', 'TestProcedure') })) {
        [void]$definition.DocumentElement.RemoveChild($member)
    }
    [xml]$collections = Get-Content -LiteralPath (Join-Path $repo 'eng/testassets/AlarmCollections/Definition.xml') -Raw
    foreach ($collection in $collections.DocumentElement.ChildNodes | Where-Object { $_.LocalName -in @('SimpleCollection', 'ComplexCollection') }) {
        [void]$definition.DocumentElement.AppendChild($definition.ImportNode($collection, $true))
    }
    if ($procedure) { [void]$definition.DocumentElement.AppendChild($definition.CreateElement('TestProcedure')) }
    $definition.Save((Join-Path $fork 'src/Compiler/Definition/AlarmDefinition.xml'))
    Copy-Item -LiteralPath (Join-Path $repo 'eng/testassets/AlarmCollections/Preview') -Destination (Join-Path $fork 'src/Compiler/Definition/Preview') -Recurse
    $project = if ($Designer) { Join-Path $fork 'src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj' }
        else { Join-Path $fork 'src/SDK/Alarmlist.SDK/AlarmList.MSBuild.SDK.csproj' }
    & dotnet build $project --verbosity quiet -m:1 -nr:false "-p:RestorePackagesPath=$(Join-Path $repo '.packages')" *> (Join-Path $fork 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Collection SDK build failed: $fork/build.log" }
    if ($Exports) {
        & dotnet build (Join-Path $fork 'src/Exporters/Alarmlist.Exporters.Zenon/Alarmlist.Exporters.Zenon.csproj') --verbosity quiet -m:1 -nr:false "-p:RestorePackagesPath=$(Join-Path $repo '.packages')" *> (Join-Path $fork 'exporter-build.log')
        if ($LASTEXITCODE -ne 0) { throw "Collection exporter build failed: $fork/exporter-build.log" }
    }
    if (!$SkipConsumers) {
        & (Join-Path $PSScriptRoot 'test-alarm-definition-fork-consumers.ps1') -ForkRoot $fork -DesktopMSBuildPath $DesktopMSBuildPath -Collections -Exports:$Exports
    }
    if ($Designer) {
        $settings = @{
            ALARMLIST_RUN_UI_VS_TESTS = '1'
            ALARMLIST_TEST_COLLECTION_DEFINITION = '1'
            ALARMLIST_TEST_PROCEDURE_ENABLED = $(if ($procedure) { '1' } else { '0' })
            ALARMLIST_VS_ROOT_SUFFIX = $RootSuffix
            ALARMLIST_VS2022_PATH = $VisualStudio2022Path
            ALARMLIST_VS2026_PATH = $VisualStudio2026Path
        }
        $previous = @{}
        foreach ($name in $settings.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
        # Only move the fixture's private template copy. Native rendering must
        # use packaged BAML, without the original definition-relative files.
        $templateSource = [IO.Path]::GetFullPath((Join-Path $fork 'src/Compiler/Definition/Preview'))
        $templateArchive = [IO.Path]::GetFullPath((Join-Path $fork 'unavailable-preview-sources'))
        foreach ($path in @($templateSource, $templateArchive)) {
            if (!$path.StartsWith($fork + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Template fixture path escaped fork.' }
        }
        Move-Item -LiteralPath $templateSource -Destination $templateArchive
        try {
            foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $settings[$name]) }
            foreach ($version in @('2022', '2026')) {
                $results = Join-Path $fork "results-$version"
                $area = if ($XmlCompletion) { '(Area=XmlCompletion|Area=XmlDefinitionCompletion)' } else { 'Area=MetadataCollections' }
                & dotnet test $project --no-build --no-restore --filter "Host=VisualStudio&VSVersion=$version&$area" --logger trx --results-directory $results *> (Join-Path $fork "native-$version.log")
                if ($LASTEXITCODE -ne 0) { throw "Collection designer failed: $fork/native-$version.log" }
                $reports = @(Get-ChildItem -LiteralPath $results -Filter '*.trx')
                if ($reports.Count -ne 1) { throw 'Missing collection designer report.' }
                [xml]$report = Get-Content -LiteralPath $reports[0].FullName -Raw
                $expected = if ($XmlCompletion) { 5 } else { 1 }
                $counts = $report.TestRun.ResultSummary.Counters
                if ([int]$counts.passed -ne $expected -or [int]$counts.total -ne $expected -or [int]$counts.executed -ne $expected) { throw 'Selected native cases did not all pass.' }
            }
        } finally {
            foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
            Move-Item -LiteralPath $templateArchive -Destination $templateSource
        }
    }
    Write-Output "Requested collection acceptance passed; TestProcedure=$procedure; fork=$fork"
}
