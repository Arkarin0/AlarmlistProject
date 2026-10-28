# Export providers

Select the files an alarm project produces, including external export formats
supplied by a NuGet package or an explicitly registered DLL.

## When to use it

Use exports to feed resolved alarm data into another tool or to build several
output formats together from the same sources and selected cultures.

## Requirements and availability

Use the matching SDK containing export-provider support. Providers are trusted
build code and must match export contract version 1 and the executing MSBuild
runtime. .NET MSBuild uses net8.0 assets; desktop MSBuild uses net472 assets.
Consumer TargetFramework does not select an exporter or its runtime.

## How it works

OutputType=Exports builds concrete alarms. Application is a compatibility alias;
Library produces a reusable library artifact and rejects an explicit export list.
An empty or absent AlarmlistExportTypes defaults to XML. An explicit list replaces
that default, so selecting only an external provider produces only its artifacts.
IDs are case-insensitive; duplicates and unknown providers fail the build.
The built-in JSON provider supplies [versioned structured JSON](json-export-format.md).
The external [zenon provider](zenon-keyword-export.md) supplies keyword XML.

Each selected provider runs for each configured culture and returns at least one
artifact. Artifacts can be binary and can use nested paths below that culture's
output directory. No output is published until compilation and all providers
succeed. Ordinary installation failures roll back the batch. Previous files and
the ownership manifest remain after failed compilation or export.

GetTargetPath returns the last successful recorded export files, with Culture and
ExportProvider metadata, without loading provider code. Before the first export,
external artifact paths are unknown; XML retains its predictable default path.
Clean removes recorded files without loading providers, including after provider
removal. Unrelated files remain. Build removes previously owned artifacts that
are no longer produced. Output paths must not follow symbolic links or junctions.

## Configuration or syntax

Set AlarmlistExportTypes to a semicolon-separated list. XML and JSON are built in. External
providers register AlarmlistExportProvider items, either from a package's ordinary
build assets or directly in the project. Package registration does not select a
provider automatically. Packages select their asset by MSBuildRuntimeType and
must not activate through buildTransitive.

Registration metadata is AssemblyPath, TypeName, Dependencies, OutputFileName,
and Option_<name>. AssemblyPath identifies the DLL; TypeName is its exact public
entry type. Dependencies lists explicitly required DLLs, relative to the provider
DLL's directory unless absolute. Do not supply Alarmlist.Core.dll: the build host
owns that contract. Option_ metadata passes provider-defined option values.
OutputFileName overrides the suggested filename; providers still define their
returned artifact names. XML registrations can change OutputFileName without
specifying an assembly or entry type.

## Minimal example

With the SDK available from your configured feed, use this Plant.almproj beside
an existing valid Alarms.almx:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK">
  <PropertyGroup>
    <OutputType>Exports</OutputType>
    <AlarmlistExportTypes>XML</AlarmlistExportTypes>
  </PropertyGroup>
  <ItemGroup>
    <AlarmlistExportProvider Include="XML">
      <OutputFileName>resolved.xml</OutputFileName>
    </AlarmlistExportProvider>
  </ItemGroup>
</Project>
```

Run `dotnet msbuild Plant.almproj /restore /t:Build`.

### Expected result

The build writes bin/Debug/keyword/resolved.xml. With no export-specific filename
override, the existing CompiledAlarmlistFileName setting still names XML output.
Sources and expressions remain unchanged.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Provider is not registered | Missing package build assets or DLL declaration | Restore the declared package or add its explicit registration. |
| Contract/definition mismatch | Provider was built against incompatible tooling | Rebuild the provider for the matching contract and required definition. |
| Build fails on a later culture | Compilation or export failed | Correct the reported error and rebuild; previous outputs remain. |
| Desktop assembly conflict | A loaded DLL changed inside a reused MSBuild process | Restart desktop MSBuild with node reuse disabled. |
| Artifact path rejected | Absolute, escaping, duplicate, input-colliding or linked path | Return distinct relative artifact paths below the culture output directory. |

## Related features and tutorials

See [project builds](project-builds.md), [localization](localization.md), and
[build your first alarmlist](../tutorials/build-your-first-alarmlist.md).
Provider authors can read the [contract notes](../../src/Compiler/Output/README.md).
