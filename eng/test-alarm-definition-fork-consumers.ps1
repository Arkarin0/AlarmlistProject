[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$ForkRoot,
  [Parameter(Mandatory = $true)][string]$DesktopMSBuildPath,
  [switch]$Collections,
  [switch]$Exports
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$work = Join-Path $ForkRoot ('consumer-checks/' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $work | Out-Null
$feed = Join-Path $work 'feed'
New-Item -ItemType Directory -Path $feed | Out-Null
$packages = @(Get-ChildItem (Join-Path $ForkRoot 'artifacts/packages/Debug/NonShipping') -Filter 'Alarmlist.MSBuild.SDK.*.nupkg')
if ($packages.Count -ne 1) { throw 'Expected one freshly built fork SDK package.' }
Copy-Item -LiteralPath $packages[0].FullName -Destination $feed
if ($Exports) {
  $exporterPackages = @(Get-ChildItem (Join-Path $ForkRoot 'artifacts/packages/Debug/NonShipping') -Filter 'Alarmlist.Exporters.Zenon.*.nupkg')
  if ($exporterPackages.Count -ne 1) { throw 'Expected one freshly built fork zenon package.' }
  Copy-Item -LiteralPath $exporterPackages[0].FullName -Destination $feed
}
$sdk = Join-Path $work 'sdk'
[IO.Compression.ZipFile]::ExtractToDirectory($packages[0].FullName, $sdk)
[xml]$spec = Get-Content (Join-Path $sdk 'Alarmlist.MSBuild.SDK.nuspec') -Raw
$version = $spec.package.metadata.version
Set-Content (Join-Path $work 'global.json') ('{"msbuild-sdks":{"Alarmlist.MSBuild.SDK":"' + $version + '"}}')
# Isolate consumer build policy, resolver cache and assets from both repository copies.
Set-Content (Join-Path $work 'Directory.Build.props') '<Project />'
Set-Content (Join-Path $work 'Directory.Build.targets') '<Project />'
Set-Content (Join-Path $work 'Directory.Packages.props') '<Project />'
$escapedFeed = [Security.SecurityElement]::Escape($feed)
Set-Content (Join-Path $work 'NuGet.config') "<configuration><packageSources><clear/><add key=`"fork`" value=`"$escapedFeed`"/><add key=`"nuget`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources><packageSourceMapping><clear/><packageSource key=`"fork`"><package pattern=`"Alarmlist.MSBuild.SDK`"/><package pattern=`"Fork.*`"/></packageSource><packageSource key=`"nuget`"><package pattern=`"*`"/></packageSource></packageSourceMapping></configuration>"
if ($Exports) {
  [xml]$configuration = Get-Content (Join-Path $work 'NuGet.config') -Raw
  $mapping = $configuration.configuration.packageSourceMapping.packageSource | Where-Object { $_.key -eq 'fork' }
  $pattern = $configuration.CreateElement('package'); $pattern.SetAttribute('pattern', 'Alarmlist.Exporters.Zenon')
  [void]$mapping.AppendChild($pattern)
  $configuration.Save((Join-Path $work 'NuGet.config'))
}
Copy-Item -Path (Join-Path $PSScriptRoot 'testassets/AlarmDefinitionFork/*') -Destination $work
$previousCache = $env:NUGET_PACKAGES
$env:NUGET_PACKAGES = Join-Path $work 'packages'
function Invoke-Checked([string]$Name, [string]$Executable, [string[]]$Arguments) {
  & $Executable @Arguments *> (Join-Path $work "$Name.log")
  if ($LASTEXITCODE -ne 0) { throw "$Name failed: $work/$Name.log" }
}
try {
  Push-Location $work
  try {
    Invoke-Checked 'verifier-build' 'dotnet' @('build', 'Verify.csproj', "-p:ForkSdkPath=$sdk", '-v:quiet', '-m:1', '-nr:false')
    foreach ($framework in @('net472', 'net8.0')) {
      Invoke-Checked "analyzer-build-$framework" 'dotnet' @('build', 'Verify.csproj', '-f', $framework, "-p:ForkSdkPath=$sdk", '-p:OutputType=Library', '-p:AssemblyName=ForkAnalyzer', "-p:OutputPath=analyzers/$framework/", '-v:quiet', '-m:1', '-nr:false')
    }
    New-Item -ItemType Directory -Path 'Library', 'ProjectConsumer', 'PackageConsumer' | Out-Null
    Set-Content 'Library/Library.almproj' '<Project Sdk="Alarmlist.MSBuild.SDK"><PropertyGroup><OutputType>Library</OutputType><PackageId>Fork.Library</PackageId><PackageVersion>1.0.0</PackageVersion></PropertyGroup></Project>'
    $source = @'
<Alarmlist><Alarm><FullyQualifiedName>Library</FullyQualifiedName>
<Code>$(ConsumerCode)</Code><Description>$(ConsumerText)</Description><Price>12.500</Price>
<Ratio>1e3</Ratio><Enabled>1</Enabled><Priority>High</Priority><Day>2024-02-29</Day>
<Clock>12:34:56.1200</Clock><Instant>2024-02-29T12:34:56.1200+02:00</Instant>
</Alarm></Alarmlist>
'@
    if ($Collections) {
      $source = $source.Replace('</Alarm>', '<Tags><Item>base</Item></Tags><Numbers><Item>$(ConsumerCode)</Item></Numbers><Checks><Item><Count>$(ConsumerCode)</Count><Copy>$(item:Count)</Copy></Item></Checks></Alarm>')
    }
    Set-Content 'Library/Library.almx' $source
    foreach ($kind in @('Project', 'Package')) {
      $reference = if ($kind -eq 'Project') { '<ProjectReference Include="../Library/Library.almproj" />' } else { '<PackageReference Include="Fork.Library" Version="1.0.0" />' }
      $project = @'
<Project Sdk="Alarmlist.MSBuild.SDK">
  <ItemGroup>
    REFERENCE
    <AlarmlistProperty Include="ConsumerCode" Value="+008" />
    <AlarmlistProperty Include="ConsumerText" Value="Consumer text" />
    <AlarmlistAnalyzer Include="../analyzers/net8.0/ForkAnalyzer.dll" Condition="'$(MSBuildRuntimeType)' == 'Core'" />
    <AlarmlistAnalyzer Include="../analyzers/net472/ForkAnalyzer.dll" Condition="'$(MSBuildRuntimeType)' != 'Core'" />
  </ItemGroup>
</Project>
'@
      $project = $project.Replace('REFERENCE', $reference)
      if ($Exports) {
        $project = $project.Replace('</Project>', '<PropertyGroup><OutputType>Exports</OutputType><AlarmlistExportTypes>XML;JSON;Zenon</AlarmlistExportTypes></PropertyGroup><ItemGroup><PackageReference Include="Alarmlist.Exporters.Zenon" Version="' + $version + '" PrivateAssets="all" /></ItemGroup></Project>')
      }
      Set-Content "$($kind)Consumer/Consumer.almproj" $project
      Set-Content "$($kind)Consumer/Local.almx" '<Alarmlist><Alarm><FullyQualifiedName>Local</FullyQualifiedName></Alarm></Alarmlist>'
    }
    Invoke-Checked 'library-pack' 'dotnet' @('msbuild', 'Library/Library.almproj', '-restore', '-t:Pack', "-p:PackageOutputPath=$feed", '-v:minimal', '-nr:false')
    [xml]$library = Get-Content 'Library/bin/Debug/Fork.Library.almlib' -Raw
    if ($library.DocumentElement.FormatVersion -ne '3' -or !$library.DocumentElement.SchemaFingerprint) { throw 'Library contract/fingerprint missing.' }
    if (!(Get-Content 'Library/bin/Debug/Fork.Library.almlib' -Raw).Contains('$(ConsumerCode)')) { throw 'Library lost its deferred expression.' }
    foreach ($hostKind in @('Core', 'Desktop')) {
      foreach ($kind in @('Project', 'Package')) {
        $name = "$hostKind-$kind"
        $arguments = @("$($kind)Consumer/Consumer.almproj", '-restore', '-t:Build', '-v:minimal', '-nr:false')
        if ($hostKind -eq 'Core') { Invoke-Checked $name 'dotnet' (@('msbuild') + $arguments) }
        else { Invoke-Checked $name $DesktopMSBuildPath $arguments }
        if (!(Get-Content "$name.log" -Raw).Contains('FORK001')) { throw "Analyzer did not execute: $name" }
        $output = "$($kind)Consumer/bin/Debug/keyword/Consumer.Alarmlist.xml"
        $verificationArguments = @($output, 'Library/bin/Debug/Fork.Library.almlib')
        if ($Exports) { $verificationArguments += 'exports' }
        Invoke-Checked "$name-verify-net8" 'dotnet' (@('bin/Debug/net8.0/Verify.dll') + $verificationArguments)
        Invoke-Checked "$name-verify-net472" (Join-Path $work 'bin/Debug/net472/Verify.exe') $verificationArguments
      }
    }
    $output = 'PackageConsumer/bin/Debug/keyword/Consumer.Alarmlist.xml'
    $previousOutput = (Get-FileHash $output).Hash
    $previousArtifacts = @{}
    if ($Exports) {
      foreach ($file in @(Get-ChildItem 'PackageConsumer/bin' -File -Recurse) + @(Get-ChildItem 'PackageConsumer/obj' -Filter '*.outputs.xml' -File -Recurse)) {
        $previousArtifacts[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash
      }
    }
    $consumer = Get-Content 'PackageConsumer/Consumer.almproj' -Raw
    foreach ($case in @(@{ Value = '9'; Diagnostic = 'FORK002' }, @{ Value = '11'; Diagnostic = 'ALM0029' })) {
      Set-Content 'PackageConsumer/Consumer.almproj' $consumer.Replace('+008', $case.Value)
      & dotnet msbuild 'PackageConsumer/Consumer.almproj' -t:Build -v:minimal -nr:false *> "$($case.Diagnostic).log"
      if ($LASTEXITCODE -eq 0 -or !(Get-Content "$($case.Diagnostic).log" -Raw).Contains($case.Diagnostic)) { throw "Expected failure missing: $($case.Diagnostic)" }
      if ((Get-FileHash $output).Hash -ne $previousOutput) { throw 'Failed consumer build replaced its prior output.' }
      foreach ($path in $previousArtifacts.Keys) {
        if ((Get-FileHash -LiteralPath $path).Hash -ne $previousArtifacts[$path]) { throw 'Failed build replaced an export/manifest.' }
      }
    }
    Set-Content 'PackageConsumer/Consumer.almproj' $consumer
    if ($Collections) {
      $local = Get-Content 'PackageConsumer/Local.almx' -Raw
      foreach ($hostKind in @('Core', 'Desktop')) {
        foreach ($case in @(@{ Xml = '<Tags><Item>a</Item><Item>b</Item><Item>c</Item><Item>d</Item></Tags>'; Diagnostic = 'ALM0030' },
                           @{ Xml = '<Checks><Item><Count>bad</Count></Item></Checks>'; Diagnostic = 'ALM0029' })) {
          Set-Content 'PackageConsumer/Local.almx' $local.Replace('</Alarm>', $case.Xml + '</Alarm>')
          $log = "collection-$hostKind-$($case.Diagnostic).log"
          $arguments = @('PackageConsumer/Consumer.almproj', '-t:Build', '-v:minimal', '-nr:false')
          if ($hostKind -eq 'Core') { & dotnet msbuild @arguments *> $log } else { & $DesktopMSBuildPath @arguments *> $log }
          if ($LASTEXITCODE -eq 0 -or !(Get-Content $log -Raw).Contains($case.Diagnostic)) { throw "Expected collection failure missing: $log" }
          if ((Get-FileHash $output).Hash -ne $previousOutput) { throw 'Failed collection build replaced prior output.' }
          foreach ($path in $previousArtifacts.Keys) {
            if ((Get-FileHash -LiteralPath $path).Hash -ne $previousArtifacts[$path]) { throw 'Failed collection build replaced an export/manifest.' }
          }
        }
      }
      Set-Content 'PackageConsumer/Local.almx' $local
    }
    if ($Exports) {
      foreach ($hostKind in @('Core', 'Desktop')) {
        $executable = if ($hostKind -eq 'Core') { 'dotnet' } else { $DesktopMSBuildPath }
        [string[]]$prefix = @()
        if ($hostKind -eq 'Core') { $prefix = @('msbuild') }
        Set-Content 'PackageConsumer/Consumer.almproj' $consumer.Replace('XML;JSON;Zenon', 'JSON')
        Invoke-Checked "exports-$hostKind-switch" $executable ($prefix + @('PackageConsumer/Consumer.almproj', '-t:Build', '-v:minimal', '-nr:false'))
        if ((Test-Path $output) -or (Test-Path 'PackageConsumer/bin/Debug/keyword/Consumer.Zenon.xml')) { throw 'Switch left stale export artifacts.' }
        Invoke-Checked "exports-$hostKind-clean" $executable ($prefix + @('PackageConsumer/Consumer.almproj', '-t:Clean', '-v:minimal', '-nr:false'))
        if (Test-Path 'PackageConsumer/bin/Debug/keyword/Consumer.Alarmlist.json') { throw 'Clean left JSON output.' }
        Set-Content 'PackageConsumer/Consumer.almproj' $consumer
        Invoke-Checked "exports-$hostKind-rebuild" $executable ($prefix + @('PackageConsumer/Consumer.almproj', '-t:Rebuild', '-v:minimal', '-nr:false'))
        Invoke-Checked "exports-$hostKind-rebuild-verify" 'dotnet' @('bin/Debug/net8.0/Verify.dll', $output, 'Library/bin/Debug/Fork.Library.almlib', 'exports')
      }
    }
    if ((Get-Content 'Library/Library.almx' -Raw).Trim() -ne $source.Trim()) { throw 'Build rewrote library source.' }
  } finally { Pop-Location }
} finally { $env:NUGET_PACKAGES = $previousCache }
Write-Output "Fork project/package consumers, analyzers and packaged APIs passed: $work"
