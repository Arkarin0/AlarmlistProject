# Export resolved alarms to XML, JSON and zenon keyword XML

Build three output formats from one alarm and verify that a failed export keeps
the previous successful files.

## Prerequisites and starting files

Use .NET MSBuild and matching Alarmlist SDK/exporter packages available from your
configured NuGet feed. The examples use version 1.0.0. If your matching toolset has
another version, use that version for both packages. Feed source mapping must
allow `Alarmlist.MSBuild.SDK` and `Alarmlist.Exporters.Zenon`.

Create a new empty directory for this exercise. Run all following commands there.
If it is inside the repository, add `Directory.Build.props` and
`Directory.Build.targets`, each containing `<Project />`, to isolate it from the
repository's own build configuration. Keep the SDK/feed configuration available
to this directory.

## Steps

### 1. Build XML and JSON together

Create `Plant.almproj`:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK/1.0.0">
  <PropertyGroup>
    <OutputType>Exports</OutputType>
    <AlarmlistExportTypes>XML;JSON</AlarmlistExportTypes>
  </PropertyGroup>
</Project>
```

Create `Alarms.almx`:

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Plant.Position</FullyQualifiedName>
    <Properties><Property Name="Position" Value="1" /></Properties>
    <Name>$(loc:YouPlacedAtPositionKeyword) $(Position) $(loc:atLocationkeyword)</Name>
    <Code>001</Code>
  </Alarm>
</Alarmlist>
```

```powershell
dotnet msbuild Plant.almproj /restore /t:Build
```

Expected result: `bin/Debug/keyword/Plant.Alarmlist.xml` and
`Plant.Alarmlist.json` exist. The default keyword culture preserves localization
expressions. JSON records Code as the string `001`.

### 2. Add the zenon provider

Add this item group inside `Plant.almproj`:

```xml
<ItemGroup>
  <PackageReference Include="Alarmlist.Exporters.Zenon"
                    Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
```

Change `AlarmlistExportTypes` to `XML;JSON;Zenon`, then rebuild:

```powershell
dotnet msbuild Plant.almproj /restore /t:Build
```

Expected result: `Plant.Zenon.xml` appears alongside the other formats, containing:

```xml
<Name>@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@</Name>
```

This is the existing alarm XML shape with zenon keyword text. Configure the
corresponding language-table keys separately; this file is not a complete zenon
alarm/variable import file.

### 3. Verify failure preserves all outputs

Capture hashes, then temporarily append `@` to the Name expression in
`Alarms.almx` and build:

```powershell
$previousExports = Get-ChildItem bin/Debug/keyword -File | Get-FileHash
dotnet msbuild Plant.almproj /t:Build
Get-ChildItem bin/Debug/keyword -File | Get-FileHash
```

Expected result: the build fails with a literal-delimiter error. Every output
hash matches the corresponding entry in `$previousExports`. Restore the original
Name before continuing.

### 4. Switch formats and clean owned files

Change `AlarmlistExportTypes` to `JSON` and build:

```powershell
dotnet msbuild Plant.almproj /t:Build
dotnet msbuild Plant.almproj /t:Clean
```

Expected result: Build leaves only the JSON artifact from this project. Clean
removes it. Source files remain. A subsequent Build recreates JSON.

## Verify the result

The original source still contains ALMX expressions and Code `001`. Export
selection determines produced files without changing `TargetFramework`. XML and
JSON retain keyword-mode values; zenon renders localization keys as `@Key@` and
keeps the evaluated `1` literal. Failed export does not replace any successful
artifact or its ownership manifest.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Package restore fails | Missing version or feed/source mapping | Make the matching SDK and exporter packages available to this directory. |
| Zenon provider is not registered | Package not restored | Run the step-2 command with /restore. |
| Substitution or adjacent-key error | Text exceeds plain keyword projection | Use separate plain key references and nonempty literal separators. |
| Only XML is produced | Export list was omitted | Save the explicit AlarmlistExportTypes selection. |

## Cleanup

After Clean, remove only the exercise directory you created when no longer needed.

## Next steps and related features

Read [export providers](../features/export-providers.md),
[JSON format](../features/json-export-format.md),
[zenon keyword XML](../features/zenon-keyword-export.md), and
[localization](../features/localization.md). Provider authors can start with the
[contract notes](../../src/Compiler/Output/README.md).
