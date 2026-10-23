[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$DesktopMSBuildPath)
$ErrorActionPreference = 'Stop'
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
    [xml]$collections = Get-Content -LiteralPath (Join-Path $repo 'eng/testassets/AlarmCollections/Definition.xml') -Raw
    foreach ($collection in $collections.DocumentElement.ChildNodes | Where-Object { $_.LocalName -in @('SimpleCollection', 'ComplexCollection') }) {
        [void]$definition.DocumentElement.AppendChild($definition.ImportNode($collection, $true))
    }
    if ($procedure) { [void]$definition.DocumentElement.AppendChild($definition.CreateElement('TestProcedure')) }
    $definition.Save((Join-Path $fork 'src/Compiler/Definition/AlarmDefinition.xml'))
    & dotnet build (Join-Path $fork 'src/SDK/Alarmlist.SDK/AlarmList.MSBuild.SDK.csproj') --verbosity quiet -m:1 -nr:false "-p:RestorePackagesPath=$(Join-Path $repo '.packages')" *> (Join-Path $fork 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Collection SDK build failed: $fork/build.log" }
    & (Join-Path $PSScriptRoot 'test-alarm-definition-fork-consumers.ps1') -ForkRoot $fork -DesktopMSBuildPath $DesktopMSBuildPath -Collections
    Write-Output "Collection package acceptance passed; TestProcedure=$procedure; fork=$fork"
}
