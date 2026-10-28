# JSON export format

Export compiler-resolved alarms as structured JSON for downstream tools, preserving
types, collection order and the distinction between absent and deferred values.

## When to use it

Use JSON when another application needs resolved alarm data without reading ALMX
expressions or reproducing inheritance and localization rules.

## Requirements and availability

Use the matching SDK with the built-in JSON provider. No provider package or DLL
registration is required. JSON is supported by both net472 desktop and net8.0
.NET MSBuild task hosts, including rebuilt scalar/collection definitions.

## How it works

Select JSON alone or alongside XML and external providers. Each selected culture
produces its own document. All selected outputs share the build transaction: a
failure in another culture or provider preserves the previous complete batch.

JSON format version 1 is UTF-8 without a byte-order mark. It uses compiler-owned
canonical values and preserves declaration/alarm/item/group order. It does not
serialize source paths, expression syntax trees or generated CLR enum types.
Concrete cultures contain resolved translations; keyword mode preserves the
compiler's ALMX tokens and reports deferred states where applicable.

## Configuration or syntax

Set `AlarmlistExportTypes` to `JSON` for JSON-only output, or `XML;JSON` for both.
The default JSON filename replaces the extension of CompiledAlarmlistFileName:
Plant.Alarmlist.xml becomes Plant.Alarmlist.json. An AlarmlistExportProvider item
with Include="JSON" can override OutputFileName. Version 1 has no Option_ settings;
unknown provider options fail the build.

### Document structure

| Member | Meaning |
| --- | --- |
| `formatVersion` | Number `1`; independent of provider contract and library format versions |
| `culture` | Normalized selected compilation culture, including `keyword` |
| `definitionFingerprint` | Semantic fingerprint of the alarm definition |
| `hasTestProcedure` | Whether the compiled definition supports procedures |
| `fieldDefinitions` | Ordered field descriptors: name, type, label, required and enum token values |
| `collectionDefinitions` | Ordered descriptors: name, kind, label and field descriptors |
| `alarms` | Ordered resolved concrete alarms; abstract templates do not become entries |

Each alarm has fullyQualifiedName, fields, collections and procedureGroups.
Fields are arrays of records with name, state, isDefault, canonicalText and value.
Using name values rather than object member names preserves custom field names
such as `__type` without serializer metadata collisions.

| State | Value and canonicalText |
| --- | --- |
| `valid` | Compiler-provided typed value and canonical text; empty strings remain empty |
| `absent` | Both are null; distinct from an explicit empty string |
| `deferred` | Available keyword-mode expression text remains in canonicalText; value is a string for string fields and null when a typed value awaits a concrete culture |

Int64, Decimal and Double values are JSON numbers; Boolean values are JSON booleans.
Strings and enums are JSON strings, with enum values retaining stored tokens.
Date, Time and Timestamp are strings in the compiler's canonical formats, such as
2024-02-29, 12:34:56.12 and 2024-02-29T12:34:56.12+02:00. CanonicalText is retained
for precision-preserving consumers; an importing application's numeric precision
may be narrower than the compiler's Int64 or Decimal range.

Each collection has name and items. Each item has a fields array, including the
single field named Item for a simple collection. Effective inherited entries and
duplicates retain compiler order; Clear directives are reflected in the resolved
items rather than exported as operations. Empty collections have empty arrays.

Each procedure group has name, title and entries. Entries have kind (Instruction,
Hint, Warning or Note) and text. Disabled procedure capability is indicated by
hasTestProcedure=false with empty procedureGroups arrays. Resource fallback and
parameter substitution have already occurred in the compiler.

## Minimal example

With the SDK available from the configured feed, use Plant.almproj:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK">
  <PropertyGroup>
    <AlarmlistExportTypes>JSON</AlarmlistExportTypes>
  </PropertyGroup>
</Project>
```

Create Alarms.almx beside it:

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Plant.Ready</FullyQualifiedName>
    <Name>Ready</Name>
    <Code>001</Code>
  </Alarm>
</Alarmlist>
```

Run `dotnet msbuild Plant.almproj /restore /t:Build`.

### Expected result

bin/Debug/keyword/Plant.Alarmlist.json contains Plant.Ready. The Code field record
is a string even though it contains digits:

```json
{"name":"Code","state":"valid","isDefault":false,"canonicalText":"001","value":"001"}
```

XML is not produced by this selection. Select XML;JSON to produce both formats.
GetTargetPath and Clean use the recorded JSON artifact paths.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Expected XML is missing | Explicit JSON selection replaces the default | Select XML;JSON when both are needed. |
| A typed value is null with state deferred | Keyword mode has not evaluated localization-dependent typed expressions | Select a concrete culture, or preserve canonicalText for keyword-aware processing. |
| A large number changes after import | The consumer uses a narrower numeric representation | Read canonicalText with a precision-capable parser. |
| An Option_ setting fails | Version 1 has no JSON provider options | Remove the option; use OutputFileName for a filename override. |

## Related features and tutorials

See [export providers](export-providers.md), [project builds](project-builds.md),
[localization](localization.md), and [build your first alarmlist](../tutorials/build-your-first-alarmlist.md).
