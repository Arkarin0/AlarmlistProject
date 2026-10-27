# Project builds and resolved XML

The Alarmlist SDK compiles `.almx` sources in an `.almproj` project into resolved
XML, with one application output per selected culture.

## When to use it

Use the SDK to build alarms from the command line or Visual Studio and to control
which source files and output languages belong to a project.

## Requirements and availability

Use a matching Alarmlist.MSBuild.SDK package available from your configured feed.
.NET MSBuild uses the net8.0 task; desktop MSBuild uses net472. Repository builds
require the tooling described in the root agent instructions. No consumer
`TargetFramework` is needed.

## How it works

Default inputs include `**/*.almx`, `**/*.resx`, and `**/*.almlookup`, excluding
generated output/intermediate and hidden directories. Build recompiles evaluated
inputs, including conditional membership changes. A failed compilation preserves
previous outputs. Clean removes owned compiled outputs, preserving source files.
Library projects produce a library artifact instead of culture-specific XML.

## Configuration or syntax

| Setting | Purpose | Default |
| --- | --- | --- |
| `AlarmlistCultures` | Semicolon-separated output cultures | `keyword` |
| `OutputPath` | Output directory | `bin/Debug/` in Debug |
| `CompiledAlarmlistFileName` | Application XML filename | Project name plus `.Alarmlist.xml` |
| `EnableDefaultCompileItems` | Include default ALMX glob | `true` |
| `EnableDefaultAlarmlistResourceItems` | Include default RESX glob | `true` |
| `EnableDefaultAlarmlistLookupItems` | Include default lookup glob | `true` |

Use `Compile Include/Remove`, `AlarmlistResource` and `AlarmlistLookup` for explicit
membership. Do not include an already globbed file a second time.

## Minimal example

Create `Plant.almproj` with `<Project Sdk="Alarmlist.MSBuild.SDK" />` in a directory
whose `global.json` pins an available SDK version (see the
[SDK guide](../../src/SDK/Alarmlist.SDK/README.md#use-the-sdk)). Beside it, create
`Alarms.almx`:

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Plant.Ready</FullyQualifiedName>
    <Name>Ready</Name>
    <Code>001</Code>
  </Alarm>
</Alarmlist>
```

Run `dotnet msbuild Plant.almproj /restore /t:Build` from that directory.

### Expected result

`bin/Debug/keyword/Plant.Alarmlist.xml` contains `Plant.Ready`, Name `Ready` and
Code `001`. Authored XML has root `Alarmlist`; resolved XML has root `AlarmList`.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| SDK cannot resolve | Version/feed unavailable | Use the matching installed or prepared SDK version and feed. |
| Removed file still appears | Another import includes it | Inspect evaluated project/import membership. |
| Output still has old values after failure | Failed builds preserve outputs | Fix reported errors and build again. |

## Related features and tutorials

See [first build](../tutorials/build-your-first-alarmlist.md),
[localization](localization.md), and [libraries](alarm-libraries.md).
