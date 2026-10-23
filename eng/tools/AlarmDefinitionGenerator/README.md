# Alarm definition generator

This build tool validates the repository's versioned XML alarm definition and
generates compiler model members and detached Visual Studio field metadata.
It runs on net8.0; generated code supports the compiler's net472/net8.0 and the
extension's net472 targets. It adds no runtime compiler filesystem access.

The authoritative input is `src/Compiler/Definition/AlarmDefinition.xml`; its
schema is `AlarmDefinition.xsd` beside it. Ordinary field names use portable ASCII
C#/XML identifiers. Names are also expression suffixes and must not collide with
reserved members. Types and metadata are documented in
[milestone 11](../../../src/VisualStudio/MILESTONE-11.md).

Shared `eng/targets/AlarmDefinitionGeneration.targets` builds the tool and runs it
before CoreCompile, including fresh design-time builds. Generated files live in
each consumer's intermediate directory. Normal Clean removes those files. Atomic
installation and unchanged-content detection preserve complete outputs and avoid
rewriting unchanged source. A validation error fails the calling build; the last
complete generated file is not a fallback for a failed build.

Run focused checks from the repository root:

```powershell
dotnet test eng/tools/AlarmDefinitionGenerator.UnitTests/AlarmDefinitionGenerator.UnitTests.csproj
./eng/test-alarm-definition-generation.ps1
```

The lifecycle check exercises Clean, a fresh design-time compile, parallel and
no-op builds, invalid-definition failure and recovery. It also builds an isolated
alternate-definition compiler on both frameworks and checks typed values,
expressions, defaults, bounds, inheritance, keyword deferral and canonical output.
Evidence remains under
`artifacts/log/Debug/AlarmDefinitionGeneration/`. It does not edit tracked source.

The CLI accepts `<definition.xml> <output.cs> <compiler|host>`. An
`AlarmlistDefinitionFile` MSBuild override is available for isolated fork fixtures;
it is a repository build input, not an `.almproj` consumer option. The semantic
fingerprint excludes presentation metadata; the tooling fingerprint includes it.

The template mode accepts `<definition.xml> <output.almx> template <input.almx>`.
VSIX packaging uses it to omit removed/retyped sample prose and order retained
fields by the definition. It never materializes defaults or invents required
values; missing required values without defaults are reported by the compiler.

Compiler fields are metadata-driven, with raw syntax
access through `GetLocalFieldValue`/`SetLocalFieldValue` and resolved values through
`Alarm.GetField`. Generated accessors expose nullable typed values; deferred typed
values remain null and retain their token text in the generic field result.
ALM0029 reports mandatory scalar/required/bounds errors. String output retains its
text; typed output is invariant and canonical. Defaults are literal and are never
written into authored source.

Analysis 7, analyzer 5 and library format 3 carry matching definition metadata
and fingerprints. Rebuild matching tools after definition changes; this is not
runtime project customization. The field designer, lists, completion and previews
use metadata. The milestone document records completed acceptance and remaining gates.

## Authoring a fork definition

Edit the definition, then rebuild the compiler, SDK and VSIX together. Ordinary
fields can be added, removed, reordered or retyped; the XML declaration order is
also the default designer/output/preview order. Do not edit generated C# files.
Existing source values must satisfy any changed type or bound. Update consumer
sources, rebuild referenced libraries and rebuild analyzers against the matching
compiler before using the new toolset. Keep fork packages in a distinct feed or
use distinct package versions so a cache cannot select older tools with the same
package identity.

From the repository root, build the production extension project:

```powershell
dotnet build src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj
```

Its project dependencies build the matching SDK and both compiler/task runtimes.
Use the resulting SDK package and VSIX together. The standard solution includes
tests written for the standard fields. The fork workflow below exercises a
representative alternate definition; adapt consumer fixtures for your fork's own
fields rather than treating the baseline tests as schema-agnostic.

For example, retain desired existing fields and add:

```xml
<Field Name="Severity" Type="Enum" Required="true" Default="Low" Label="Severity">
  <Value Name="Low" Label="Low priority" />
  <Value Name="High" Label="High priority" />
</Field>
<Field Name="Limit" Type="Decimal" Minimum="0" Maximum="100" Help="Allowed percentage" />
<Field Name="ObservedAt" Type="Timestamp" PreviewWidth="220" />
```

Place these inside `<AlarmDefinition Version="1">`. Enum tokens are case-sensitive;
labels are presentation only. Field names must be unique ignoring case. Identity,
reference, parameters, procedure structure, generated API members and the
`Alarmlist` prefix are reserved; the generator reports unsupported names.

| Type | Accepted literal examples and behavior |
| --- | --- |
| String | Exact text, including whitespace. `Multiline="true"` is available. |
| Int64 | Signed integer, such as `-42` or `+0042`; canonical output removes the plus and leading zeros. |
| Decimal | Invariant decimal, such as `12.500`; canonical output is `12.5`. No exponent or grouping separators. |
| Double | Finite floating-point number, such as `1e3`; NaN and infinities are rejected. |
| Boolean | `true`, `false`, `1`, `0`; canonical output uses `true`/`false`. |
| Enum | One declared `Value` token; sort order follows declaration order. |
| Date | `yyyy-MM-dd`, such as `2024-02-29`. |
| Time | `HH:mm:ss` with optional 1–7 fractional digits, such as `12:34:56.12`. |
| Timestamp | Date and time separated by `T`, optional fractional seconds, and required `Z` or explicit offset such as `+02:00`. |

Non-string values trim surrounding whitespace before validation, but whitespace
alone is invalid. Defaults are literals, never expressions. An absent or authored
empty value can inherit; a default applies only when no authored inherited value
is available. An expression that evaluates to empty does not trigger a default.
Optional absent typed fields are omitted from compiled XML; absent strings retain
the standard empty-element output. Numeric bounds are inclusive and use the same
invariant literal grammar as the field type.

Every ordinary field accepts expressions in ALMX. Validation follows evaluation;
keyword-mode localization tokens retain their text with deferred typed validation.
Concrete-culture builds validate resolved values. Boolean/enum designer helpers
offer explicit literal choices while preserving expression editing and drafts.
Focusing a field never writes defaults or inherited values into source.

Optional metadata includes `Label`, `Help`, `Multiline` (String only),
`PreviewVisible` (default true), and `PreviewWidth` (40–2000, default 180).
New preview columns use these defaults; surviving saved preferences retain their
order, width and visibility. Use Reset columns to restore definition defaults.

`eng/test-alarm-definition-designer.ps1` accepts `-VisualStudio2022Path` and
`-VisualStudio2026Path`. It copies repository sources into `artifacts/fork11/`,
changes only the definition, builds the alternate compiler/SDK/VSIX and exercises
native field editing on both hosts. Add `-IncludePreview` for dynamic columns,
typed sorting, completion, filtering and clipboard checks, including a definition
without Name/Category and ordinary fields named Source, Identity and TestMethod.
It retains logs and restores the standard
experimental deployment afterward. Run it sequentially with other live IDE tests.

For the complete fork acceptance workflow, use `-FullWorkflow` (which also enables
preview checks):

```powershell
./eng/test-alarm-definition-designer.ps1 -VisualStudio2022Path '<VS2022 installation>' -VisualStudio2026Path '<VS2026 installation>' -FullWorkflow
```

The isolated copy changes only `src/Compiler/Definition/AlarmDefinition.xml` in
product source. It builds matching production tools, then tests the resulting SDK
package with private feeds and caches. Consumers use both ProjectReference and
PackageReference libraries. Their raw expressions remain deferred in the library,
then resolve to canonical scalar values in the application. A custom analyzer,
compiled against the packaged compiler on each framework, verifies metadata,
typed values and default provenance. Both .NET and desktop MSBuild execute it;
both runtimes verify output and the packaged analysis API. Invalid scalar and
analyzer failures must preserve prior output. Native field/preview tests run on
both hosts, followed by standard experimental deployment restoration.

Logs remain under `artifacts/fork11/<run>/`; consumer evidence is under
`consumer-checks/<run>/`. To diagnose only consumer checks against an existing
isolated fork package, run `eng/test-alarm-definition-fork-consumers.ps1` with
`-ForkRoot` and `-DesktopMSBuildPath`. Each run creates a new private workspace.
The fixture's baseline-specific tests are not the arbitrary-schema contract:
fork acceptance builds production tools and runs generic metadata-based checks.
The normal solution regression suites still validate the repository's standard
definition separately.

## Version-2 collection foundations (staged)

The parser and generated metadata recognize version-2 definitions containing
`Field`, `SimpleCollection`, `ComplexCollection` and one optional `TestProcedure`.
Version 1 keeps its implicit procedure capability and rejects the new declarations.
Version 2 requires an explicit `<TestProcedure />` to retain that capability.

A simple collection declares a scalar `Type` and the existing scalar constraints /
choices. A complex collection contains one or more `Field` declarations; field
names are scoped to that record. Both declare `Name`, optional `Label`/`Help`,
`MinCount` (default zero) and optional `MaxCount` (unbounded when omitted). Counts
are nonnegative Int32 values and must form a valid range. Nested collections are
rejected. Collection/ordinary field names share the alarm namespace and follow
existing reserved-name rules. The generated simple-item descriptor is named Item.

The compiler and SDK accept collection and procedure-disabled definitions. Host
generation remains guarded until the collection designer is ready; a fork cannot
yet build a collection-enabled VSIX. The standard checked-in definition stays at
version 1 during this stage. Internal host fixtures test detached metadata before
that guard is removed.

Generated metadata adds `AlarmCollectionDescriptor`, `AlarmDefinition.Collections`
and `AlarmDefinition.HasTestProcedure`. Semantic fingerprints include collection
shape, item constraints, counts and procedure availability; labels/help remain
presentation-only. The standard version-1 semantic fingerprint is preserved, while
the generator-3 tooling fingerprint requires rebuilding matching SDK and extension
assets. Use the milestone plan for the remaining end-to-end acceptance gates.

The compiler parses and writes local collection containers,
resolves inherited-first entries and Clear, and evaluates item scalar expressions.
Flat record fields can refer to siblings using `$(item:FieldName)`; references are
case-insensitive and cycles are errors. Item expressions retain the consuming
alarm's parameter, localization and lookup scope. Literal defaults never create
items or appear in source serialization. Effective counts are validated only for
concrete application alarms; ALM0030 is mandatory. Resolved items are exposed by
`Alarm.Collections` / `GetCollection(name)` and contain `AlarmFieldValue` results.

Run `eng/test-alarm-collections.ps1` for isolated runtime fixtures on net472 and
net8.0, including procedure-enabled/disabled definitions and minimum cardinality.
The test-only fixture generator accesses internal host generation; it does not
remove the production host guard or modify the standard definition. Analysis 7
and analyzer 5 expose collection descriptors and local/effective/excluded entries
with typed fields, item origins and Clear exclusions. Library format 3 preserves
raw expressions and per-item/field navigation locations. Older contracts require
a rebuild even if their scalar definition fingerprint matches.

Run `eng/test-alarm-collection-fork.ps1 -DesktopMSBuildPath '<MSBuild.exe>'` for
isolated compiler/SDK forks with procedures enabled and disabled. It builds real
packages, checks project and NuGet consumers on both task hosts and verifies
collection-aware analyzers and prior-output preservation after failures. It does
not deploy an IDE extension. Native collection acceptance remains a later gate.
