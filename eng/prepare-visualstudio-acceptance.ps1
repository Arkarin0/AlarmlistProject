# Create an isolated, repeatable linked-project fixture for interactive IDE checks.
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')]
  [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repoRoot = Split-Path $PSScriptRoot -Parent
$vsixPath = Join-Path $repoRoot "artifacts/bin/Alarmlist.VisualStudio/$Configuration/net472/Alarmlist.VisualStudio.vsix"
$archive = [IO.Compression.ZipFile]::OpenRead($vsixPath)
try {
  $templates = @($archive.Entries | Where-Object { $_.FullName.StartsWith('ProjectTemplates/') -and $_.FullName.EndsWith('.almproj') })
  if ($templates.Count -ne 1) { throw 'Expected exactly one ALMX project template in the built VSIX.' }
  $reader = [IO.StreamReader]::new($templates[0].Open())
  try { [xml]$template = $reader.ReadToEnd() } finally { $reader.Dispose() }
  $sdk = $template.Project.Sdk
  if ($sdk -notmatch '^Alarmlist\.MSBuild\.SDK/([^/]+)$') { throw 'The template must pin an Alarmlist SDK version.' }
  $packagePath = Join-Path $repoRoot "artifacts/packages/$Configuration/NonShipping/Alarmlist.MSBuild.SDK.$($Matches[1]).nupkg"
  if (!(Test-Path -LiteralPath $packagePath)) { throw "Build the matching SDK package first: $packagePath" }
} finally { $archive.Dispose() }

$runDirectory = Join-Path $repoRoot "artifacts/log/$Configuration/VisualStudioAcceptance/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $runDirectory | Out-Null
Copy-Item (Join-Path $repoRoot 'src/SDK/Alarmlist.SDK.Tests/testassets/boilerplate/Directory.Build.*') $runDirectory
New-Item -ItemType Directory -Path (Join-Path $runDirectory 'feed'), (Join-Path $runDirectory 'Shared') | Out-Null
Copy-Item -LiteralPath $packagePath -Destination (Join-Path $runDirectory 'feed')
@'
<configuration>
  <config><add key="globalPackagesFolder" value=".packages" /></config>
  <packageSources><clear /><add key="local" value="feed" /></packageSources>
  <disabledPackageSources><clear /></disabledPackageSources>
  <packageSourceMapping><clear /><packageSource key="local"><package pattern="Alarmlist.MSBuild.SDK" /></packageSource></packageSourceMapping>
</configuration>
'@ | Set-Content (Join-Path $runDirectory 'NuGet.Config') -Encoding UTF8
@'
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Shared.Child</FullyQualifiedName>
    <ReferenceName>Base</ReferenceName>
    <Name />
    <Code>LOCAL</Code>
    <TestProcedure>
      <Instructions><Note>Shared local note</Note></Instructions>
      <Reset><Step>Excluded local reset</Step><Clear /><Step>Shared reset only</Step></Reset>
    </TestProcedure>
  </Alarm>
</Alarmlist>
'@ | Set-Content (Join-Path $runDirectory 'Shared/Child.almx') -Encoding UTF8
@'
<Alarmlist>
  <Properties><Property Name="Unit" Value="Shared" /></Properties>
  <Alarm>
    <FullyQualifiedName>Property.Child</FullyQualifiedName><ReferenceName>Property.Template</ReferenceName>
    <Properties><Property Name="par1" /></Properties>
  </Alarm>
  <Alarm abstract="true">
    <FullyQualifiedName>Property.Template</FullyQualifiedName>
    <Properties><Property Name="par1" Value="ignored template value" /></Properties>
    <Name>$(Region) $(par1)</Name><Code>$(par1)</Code>
    <Description>Inspect $(AlarmlistAlarmCode) - $(Unit)</Description>
    <TestProcedure><Instructions><Step>Check $(par1) $$(literal)</Step></Instructions></TestProcedure>
  </Alarm>
</Alarmlist>
'@ | Set-Content (Join-Path $runDirectory 'Shared/Properties.almx') -Encoding UTF8

$projects = @()
$configurations = @()
foreach ($name in @('North', 'South')) {
  $directory = Join-Path $runDirectory $name
  New-Item -ItemType Directory -Path $directory | Out-Null
  $guid = [Guid]::NewGuid().ToString('B').ToUpperInvariant()
  @"
<Project Sdk="$sdk">
  <PropertyGroup Label="Globals"><ProjectGuid>$guid</ProjectGuid></PropertyGroup>
  <ItemGroup><Compile Include="../Shared/Child.almx"><Link>Child.almx</Link></Compile></ItemGroup>
  <ItemGroup>
    <Compile Include="../Shared/Properties.almx"><Link>Properties.almx</Link></Compile>
    <AlarmlistProperty Include="Region" Value="$name" />
    <AlarmlistProperty Include="par1" Value="001" Condition="'`$(Configuration)' != 'Release'" />
    <AlarmlistProperty Include="par1" Value="101" Condition="'`$(Configuration)' == 'Release'" />
  </ItemGroup>
</Project>
"@ | Set-Content (Join-Path $directory "$name.almproj") -Encoding UTF8
  @"
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Base</FullyQualifiedName>
    <Name>$name inherited name</Name>
    <Code>LOCAL</Code>
    <Category>$name category</Category>
    <Description>$name description &amp; Unicode: &#937;</Description>
    <TestProcedure>
      <Instructions><Step>$name inspect</Step><Warning>$name caution</Warning></Instructions>
      <Reset><Step>$name excluded reset</Step></Reset>
    </TestProcedure>
  </Alarm>
</Alarmlist>
"@ | Set-Content (Join-Path $directory 'Base.almx') -Encoding UTF8
  $projects += "Project(`"{117E1E3B-8DC0-44BF-A627-4E41F84721D8}`") = `"$name`", `"$name\$name.almproj`", `"$guid`"`r`nEndProject"
  foreach ($build in @('Debug', 'Release')) {
    $configurations += "    $guid.$build|Any CPU.ActiveCfg = $build|Any CPU"
    $configurations += "    $guid.$build|Any CPU.Build.0 = $build|Any CPU"
  }
}
@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
$($projects -join "`r`n")
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Release|Any CPU = Release|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
$($configurations -join "`r`n")
  EndGlobalSection
EndGlobal
"@ | Set-Content (Join-Path $runDirectory 'Acceptance.sln') -Encoding UTF8

$previousPackages = $env:NUGET_PACKAGES
Push-Location $runDirectory
try {
  $env:NUGET_PACKAGES = Join-Path $runDirectory '.packages'
  foreach ($name in @('North', 'South')) {
    & dotnet msbuild "$name/$name.almproj" /restore /t:Build /nr:false /v:minimal "/bl:$name.binlog"
    if ($LASTEXITCODE -ne 0) { throw "Acceptance fixture build failed: $runDirectory" }
    [xml]$compiled = Get-Content "$name/bin/Debug/keyword/$name.Alarmlist.xml" -Encoding UTF8
    $child = @($compiled.AlarmList.Alarm | Where-Object { $_.FullyQualifiedName -eq 'Shared.Child' })
    if ($child.Count -ne 1 -or $child[0].Name -ne "$name inherited name" -or $child[0].Code -ne 'LOCAL' -or
      $child[0].TestProcedure.Reset.InnerText -ne 'Shared reset only' -or
      $child[0].TestProcedure.Instructions.InnerText -ne "$name inspect$name caution" + 'Shared local note') {
      throw "Unexpected compiled fixture values for $name."
    }
    $propertyChild = @($compiled.AlarmList.Alarm | Where-Object { $_.FullyQualifiedName -eq 'Property.Child' })
    if ($propertyChild.Count -ne 1 -or $propertyChild[0].Name -ne "$name 001" -or $propertyChild[0].Code -ne '001' -or
      $propertyChild[0].Description -ne 'Inspect 001 - Shared' -or
      @($compiled.AlarmList.Alarm | Where-Object { $_.FullyQualifiedName -eq 'Property.Template' }).Count -ne 0) {
      throw "Unexpected scoped-property fixture values for $name."
    }
  }
  Get-FileHash Shared/Child.almx, Shared/Properties.almx, North/Base.almx, South/Base.almx, North/North.almproj, South/South.almproj, North/bin/Debug/keyword/North.Alarmlist.xml, South/bin/Debug/keyword/South.Alarmlist.xml |
    Select-Object @{Name='File'; Expression={ $_.Path.Substring($runDirectory.Length + 1) }}, Hash |
    ConvertTo-Json | Set-Content baseline-hashes.json -Encoding UTF8
  git -C $repoRoot rev-parse HEAD | Set-Content commit.txt
  git -C $repoRoot status --short | Set-Content working-tree.txt
  Get-FileHash -LiteralPath $vsixPath, $packagePath | Select-Object Path, Hash | ConvertTo-Json | Set-Content package-hashes.json
} finally {
  $env:NUGET_PACKAGES = $previousPackages
  Pop-Location
}
Write-Output "Acceptance solution: $(Join-Path $runDirectory 'Acceptance.sln')"
Write-Output 'Open with the deployed experimental instance. Follow src/VisualStudio/ACCEPTANCE.md; preparation alone does not validate IDE behavior.'
