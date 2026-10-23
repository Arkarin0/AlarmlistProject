# Alarmlist.MSBuild.SDK

An MSBuild SDK that compiles `.almx` source files into one XML alarmlist per selected culture.
It supports `dotnet msbuild` with .NET SDK 8 or later and Visual Studio MSBuild
with .NET Framework 4.7.2 or later. The package includes the compiler, task
assemblies, and their runtime dependencies.

## Use the SDK

### Generated definitions and compatibility

Milestone eleven uses analysis contract 7, analyzer contract 5 and library format 3.
Rebuild old analyzers and `.almlib` artifacts with the matching toolset. Libraries
carry a semantic definition fingerprint; a different field layout, type, default,
required flag, bound or enum choice is incompatible. Presentation-only changes do
not invalidate libraries. The Visual Studio extension requires the full tooling
fingerprint to match and retains XML editing when the SDK is incompatible.

Analysis exposes immutable `FieldDefinitions`, `SchemaFingerprint` and
`ToolingFingerprint`. Each ordinary field reports its type, absent/valid/deferred/
invalid state, canonical value, primitive typed value and whether a definition
default supplied it. Enum values cross tool boundaries as tokens, never generated
enum types. Analyzer inputs expose the same descriptors and semantic fingerprint;
rules should tolerate missing or retyped ordinary fields.

Version-2 definitions may also declare `SimpleCollection` (ordered scalar Items)
and `ComplexCollection` (ordered Items containing scalar fields). Collections
inherit entries first, append local entries and honor `<Clear />`; flat records
are inherited whole. `$(item:FieldName)` refers to another field in the same
record. Counts apply to effective entries in concrete applications. Defaults
remain literal and never create items or rewrite source. Include `<TestProcedure />`
in the definition to retain procedures; omitting it disables them. Version 1
retains its implicit procedure capability.

Analysis and analyzer inputs expose `CollectionDefinitions`, `HasTestProcedure`
and per-alarm `Collections` with local/effective/excluded entries, typed scalar
states, declaration origins and the Clear directive that excluded an entry.
Libraries preserve this source and its navigation locations. Collection-enabled
VSIX generation remains guarded until the designer iteration is complete.

Definition changes are repository build inputs, not per-project configuration.
The compiler supports typed expressions; designer fields, lists, completion and
previews use the same metadata. See the [definition authoring guide](../../../eng/tools/AlarmDefinitionGenerator/README.md)
for supported types, defaults, rebuilding and fork acceptance, and
[milestone eleven](../../VisualStudio/MILESTONE-11.md) for validation evidence.

### Procedure groups and format transition

Procedure sections are ordered named groups. Source entries retain Step, Hint,
Warning, Note and Clear; a group may have an optional expression-capable Title:

```xml
<TestProcedure>
  <Group Name="Verification">
    <Title>$(loc:VerificationTitle)</Title>
    <Step>Check $(AlarmlistAlarmCode)</Step>
  </Group>
</TestProcedure>
```

Group identities use ASCII letters, digits, underscores, dots and hyphens, starting
with a letter or underscore. They compare ordinally ignoring case. A referenced
alarm supplies group order and spelling; matching local groups append entries
with the existing Clear behavior. New local groups follow inherited groups.
An absent title inherits and then falls back to the ID. An explicitly empty title
stops inheritance and displays the ID. Titles use the consuming alarm's expression
context. Empty groups retain their headings in compiled output and previews.

This is a breaking transition: replace old Instructions/Reset elements with
`<Group Name="Instructions">` / `<Group Name="Reset">`. Legacy elements are
rejected. Compiled XML uses Group/Name/Title and TestProcedureItem children.
The compiler's TestProcedure exposes Groups; detached alarm analysis exposes
ProcedureSections with title provenance. Analysis contract 7 requires the matching
extension; external analyzers must be rebuilt for analyzer contract 5. No migration
tool or compatibility aliases are supplied. See the
[milestone-eight contract](../../VisualStudio/MILESTONE-8.md).

Milestone nine advances the detached analysis contract to 5 and the analyzer
contract to 3. Captured library inputs use `CompilationContext.WithLibraries`;
`AlarmCompiler.CreateLibrary` produces validated in-memory artifacts. Library alarm
origins expose ownership, and analysis includes dependency identities, portable
definitions and original-to-generated source span maps. Hosts supply portable
relative source/resource/lookup names when creating libraries, or provide the
`portablePaths` mapping to keep physical captured paths for owning-project
EditorConfig/analyzer validation while exporting portable names. Standalone SDK
library Build and project-reference consumption are available; Pack and saved-project graph
integration remain under implementation in
[Milestone nine](../../VisualStudio/MILESTONE-9.md).

The designer's **Add alarm** command resolves creation defaults from saved
EditorConfig files for the destination ALMX file's physical path, including linked
files. For example:

```ini
[*.almx]
alarmlist_procedure.default_groups = Instructions, Reset, Verification
alarmlist_procedure.group.instructions.title = $(loc:InstructionsTitle)
alarmlist_procedure.group.verification.title = Verification
```

Missing or `unset` lists create Instructions and Reset; an explicit empty list
creates no groups. IDs are trimmed, ordered and unique ignoring case. Invalid
lists block creation with an actionable message. Title keys ignore case; missing
or `unset` omits Title, while an empty value writes an explicit empty Title.
Titles are inserted as raw text with XML escaping, not evaluated during creation.
Saved configuration changes affect subsequent creation only. Existing or copied
alarms keep their groups, and builds do not inject defaults. Static file/project
templates contain explicit Instructions/Reset declarations. Detached analysis
exposes the resolved settings and any creation error on each source's
`ProcedureCreation` property; the compiler never discovers configuration files.

For published releases, enable nuget.org in your NuGet sources, then create
`Plant.almproj`. A project-specific `NuGet.Config` is not necessary when that
source is already enabled. Unpublished development packages require a local
feed; repositories with package source mapping must allow this SDK from the
selected feed.

```xml
<Project Sdk="Alarmlist.MSBuild.SDK/1.0.0" />
```

Replace `1.0.0` with the version you built or published. Alternatively, omit
the version in the project and pin it in `global.json`:

```json
{
  "msbuild-sdks": {
    "Alarmlist.MSBuild.SDK": "1.0.0"
  }
}
```

Place your ALMX files beside the project or in subdirectories, then run:

```powershell
dotnet msbuild Plant.almproj /restore /t:Build
dotnet msbuild Plant.almproj /t:Clean
dotnet msbuild Plant.almproj /t:Rebuild /p:Configuration=Release
```

The default output is `bin/Debug/keyword/Plant.Alarmlist.xml`. `Restore` is a no-op
after SDK resolution; ALMX projects do not restore managed package references.
The SDK does not require a `TargetFramework`: it chooses `tasks/net8.0` for
.NET MSBuild and `tasks/net472` for desktop MSBuild based on the host runtime.

## Visual Studio integration

The [Alarmlist VSIX](../../VisualStudio/README.md) adds a CPS project type and
project/item templates for Visual Studio 2022 17.9+ and 2026. Released templates
pin a published SDK version, resolved through NuGet like command-line builds.
See the [SDK-first release workflow](../../VisualStudio/README.md#sdk-first-release).
The SDK supplies the `Alarmlist`
capability, file schemas, and project properties under `tools/Rules/`.

Configurations default to `Debug|AnyCPU` and `Release|AnyCPU`. A consumer can
replace these defaults by declaring its own `ProjectConfiguration` items with
`Configuration` and `Platform` metadata. The `.almproj.user` file is imported
for CPS user settings such as Show All Files and the hidden, configuration-independent
`AlarmlistPreviewColumnLayout`. The Preview window writes column visibility/order/width
through CPS to this user file; these preferences do not change source or compiler
output. Design-time builds do not compile
or write output. Normal IDE builds always invoke MSBuild to reflect removed or
conditional inputs. ALMX files open in the XML editor in this first milestone.

## Customize a project

By default, `**/*.almx` files are compiled, excluding output, intermediate,
`bin`, `obj`, and hidden directories. Use `Compile Remove` to omit a file,
or disable default items and list your inputs explicitly:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK/1.0.0">
  <PropertyGroup>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <OutputPath>output/</OutputPath>
    <CompiledAlarmlistFileName>Plant.xml</CompiledAlarmlistFileName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="alarms/**/*.almx" />
    <Compile Include="optional/*.almx" Condition="'$(IncludeOptional)' == 'true'" />
  </ItemGroup>
</Project>
```

| Property | Default |
| --- | --- |
| `Configuration` | `Debug` |
| `AssemblyName` | Project filename without its extension |
| `BaseOutputPath` | `bin/` |
| `OutputPath` | `$(BaseOutputPath)$(Configuration)/` |
| `CompiledAlarmlistFileName` | `$(AssemblyName).Alarmlist.xml` |
| `EnableDefaultCompileItems` | `true` |
| `EnableDefaultAlarmlistResourceItems` | `true` |
| `EnableDefaultAlarmlistLookupItems` | `true` |
| `AlarmlistLookupSchema` | Packaged `tools/Schemas/AlarmlistLookup.xsd` (evaluated absolute path) |
| `AlarmlistCultures` | `keyword` (semicolon-separated build list) |
| `AlarmlistCulture` | `keyword` (independent shared preview selection) |
| `AlarmlistXliffSourceCulture` | `en` (export source language) |
| `AlarmlistXliffTargetCultures` | Required for export; semicolon-separated real cultures |
| `AlarmlistXliffOutputPath` | `$(MSBuildProjectDirectory)/xliff` |
| `AlarmlistXliffOverwrite` | `false`; use a new directory for a new translation job |
| `AlarmlistTasksAssembly` | Task assembly for the current MSBuild runtime |
| `NoWarn` | Empty; comma/semicolon-separated configurable diagnostic IDs to suppress |
| `TreatWarningsAsErrors` | `false` |
| `WarningsAsErrors` | Empty; IDs to promote from warning to error |
| `WarningsNotAsErrors` | Empty; IDs exempt from blanket warning promotion |

`@(CompiledAlarmlistFiles)` exposes absolute output paths with normalized `Culture` metadata. Build and GetTargetPath return these items; GetTargetPath needs no prior build and writes no output. Projects can use
`BeforeTargets="Build"` or `AfterTargets="Build"` for custom targets;
`Directory.Build.props` and `Directory.Build.targets` are also imported.
Each build recompiles the selected inputs so changes to conditional items and
removed files are reflected. Compiler diagnostics fail the build. Clean removes
the configured and tracked compiled outputs and preserves unrelated files.

Restore uses the installed MSBuild toolset's NuGet targets and supports
`PackageReference` dependency restore. Consumer projects still need no
`TargetFramework`: data packages use the `native,Version=v0.0` restore identity by
default. Restore writes NuGet assets under `MSBuildProjectExtensionsPath` without
compiling alarms. Visual Studio's Project > Add Reference and Manage NuGet Packages
commands can edit project/package references; save the project to persist edits.
The matching extension displays direct packages and selected transitive dependencies
under Solution Explorer's Dependencies / Packages node. It obtains graph metadata
from this SDK using the same framework/version selection as library consumption;
failed restore leaves direct nodes visible as unresolved. Browsing does not build
or pack the project.
The matching extension groups selected build assets, registered Alarmlist analyzer
DLLs, alarm libraries and declared documentation beneath each package. Include a
`readme` in the nuspec or ship `.md`, `.txt` or `.html` files under `docs/` or
`documentation/` to expose documentation. These entries open as read-only captured
text (HTML is not rendered); analyzer entries show identity/path information.
Project and package references consume library artifacts; see
[milestone nine](../../VisualStudio/MILESTONE-9.md) for acceptance evidence and limitations.

To build a standalone library, set `OutputType` to `Library`. `PackageId` defaults
to the project name and `PackageVersion` to `1.0.0`. Versions use NuGet normalization
(for example, `1.0` becomes `1.0.0`). Build writes one
`$(OutputPath)/$(AlarmlistLibraryFileName)` artifact; the filename defaults to
`$(PackageId).almlib`. `GetTargetPath` discovers this path without building, and
Clean/Rebuild use the same ownership tracking as application outputs. The default
`OutputType=Application` retains culture-specific XML output. Library validation
permits missing consumer-supplied values but rejects structural/reference errors.
Exported input names are project-relative; linked inputs should specify portable
`Link` metadata.

The Visual Studio **Alarmlist Library** project template starts with an abstract
template and a concrete derived alarm. The Alarmlist project property page exposes
Output type, Library filename, Package ID and Package version. Package identity
settings are shared across configurations; output settings can vary by configuration.
For an executable example with two consumers, transitive references and
parameter/resource/lookup overrides, see the
[library playground](../../../example/Libraries/README.md).

Add `<ProjectReference Include="../Library/Library.almproj" />` to consume another
project whose `OutputType` is `Library`. CLI builds build references first and
capture their transitive artifacts; an application can contain only references.
`BuildProjectReferences=false` and IDE-scheduled builds consume existing artifacts
without rebuilding dependencies. Missing or invalid artifacts fail the consuming
build and preserve its previous outputs. Configuration assignments, including
`SetConfiguration`/`SetPlatform` reference metadata, select dependency outputs.
CLI Clean/Rebuild traverse references when reference building is enabled.
`GetTargetPath` returns only the current project's outputs without building;
`GetAlarmlistReferenceClosure` discovers transitive library paths and identities
without compiling. Project output paths need no prior build; package paths require
a valid restore and existing artifacts in the selected package folders.

Add `<PackageReference Include="Plant.Alarms" Version="1.0.0" />` to consume a
library package. Build with `dotnet msbuild Plant.almproj /restore`; subsequent
builds can reuse restored assets. Each library package contains one `.almlib`
under `alarmlist/`, with dependency packages carrying their own artifacts. The SDK
uses the selected graph in `project.assets.json`, including mixed project/package
references, rather than searching the package cache. Library dependency identities
must match the versions selected by NuGet. Missing artifacts, restore errors and
conflicting identities fail before installing new compiled outputs. Removing a
reference and restoring removes its imports even when package bytes remain cached.
A warm cache supports restore/build against an empty local feed.

Run `dotnet msbuild Library.almproj /restore /t:Pack` to create a library package.
Pack builds and validates the library first, then writes
`$(PackageOutputPath)/<PackageId>.<normalized PackageVersion>.nupkg`;
`PackageOutputPath` defaults to `OutputPath`. `Authors` and `Description` supply
package metadata, with library-identity defaults when omitted. The package contains
one `alarmlist/<PackageId>.almlib`; direct library references become exact NuGet
dependencies, including project references. Dependencies are not embedded.
`IsPackable=false` prevents packing that project and prevents consumers from
packing a library graph that requires it. Pack rejects applications and output/input
collisions; validation or packaging failures preserve an existing package.
`@(AlarmlistPackages)` returns the installed package path. Pack currently requires
Build (`NoBuild=true` is rejected), and does not publish packages. Clean owns the
compiled artifacts, not packages prepared for distribution.

Reference errors ALM0001-ALM0004 include the source file and declaration line/column
when read from ALMX files. Missing, self, and circular references point to the
`ReferenceName` element; duplicate names point to `FullyQualifiedName`. A failed
compilation leaves any previous compiled output unchanged. Live analysis of
unsaved ALMX editor buffers is separate from saved RESX resource capture.

## Diagnostic configuration and analyzers

Milestone six adds saved `.editorconfig` settings for configurable diagnostics.
Configuration follows each physical ALMX path, including linked files, through
parent directories until `root = true`. Nearer files and later matching sections
win; `unset` removes an inherited option. Unsaved configuration drafts are ignored.

```ini
root = true

[*.almx]
alarmlist_diagnostic.SAMPLE001.severity = warning
alarmlist_sample.code_prefix = AL-

[production/**.almx]
alarmlist_diagnostic.SAMPLE001.severity = error
```

Severity values are `error`, `warning`, `suggestion` (information), `silent`
(available to analysis without a build log or Error List row), `none` (suppressed),
and `default`. Unknown IDs have no effect; invalid severity values use the rule
default. The primary diagnostic location selects its configuration; related
locations do not. Location-free or relative-path diagnostics use project controls
only. Keys/IDs are case-insensitive; globs and custom values preserve case.
Inline `#` and `;` remain part of values. `.globalconfig` is not supported.
A section header must start with `[` and end with `]`. Malformed header-only
lines do not change the active section or end the preamble used for `root`.

The order is rule default, file severity, `NoWarn`, then warning promotion.
`WarningsAsErrors` overrides `WarningsNotAsErrors`; the latter exempts only
`TreatWarningsAsErrors`, not a configured error. Mandatory compiler errors
ALM0001–ALM0025 cannot be suppressed or demoted. Any effective error prevents all
culture outputs and their ownership manifest from being replaced.

External analyzers are explicit `AlarmlistAnalyzer` items. A NuGet analyzer package
can register these through its ordinary `build` targets, so consumers need only a
direct `PackageReference` and restore. Use `PrivateAssets="all"` for project-local
tooling; analyzer configuration does not propagate through alarm libraries.
The sample package includes both runtime assemblies and selects the correct one
automatically. There is no DLL glob, cache scan or discovery of ordinary Roslyn
analyzers. See the [sample analyzer](../../Analyzers/Alarmlist.SampleAnalyzer/README.md)
for package consumption and authoring. Direct DLL registration also remains supported.
Repeated registration of the same normalized DLL path is captured once, including
when a package and an explicit item select it. Distinct analyzer implementations
still require unique diagnostic IDs; duplicate or invalid declarations report ALM0025.
For a multi-targeted analyzer, select the assembly matching the build host:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningsNotAsErrors>SAMPLE001</WarningsNotAsErrors>
</PropertyGroup>
<ItemGroup>
  <AlarmlistAnalyzer Include="analyzers/net472/Company.Rules.dll"
                    Condition="'$(MSBuildRuntimeType)' != 'Core'" />
  <AlarmlistAnalyzer Include="analyzers/net8.0/Company.Rules.dll"
                    Condition="'$(MSBuildRuntimeType)' == 'Core'" />
</ItemGroup>
```

Visual Studio live analysis uses the evaluated net472-compatible selection.
Keep private managed dependency DLLs beside the analyzer. The host captures those
saved bytes once per build batch and watches them in the IDE; it does not load a
colocated `Alarmlist.Core.dll` in place of the selected SDK. Assemblies load from
captured bytes without locking the originals. A load/contract/execution failure
reports mandatory ALM0025; missing or unreadable inputs also fail the build.

Analyzers are trusted in-process code with cooperative cancellation, not a security
sandbox or a hard timeout. They run once per successfully bound/evaluated culture;
they do not run against an invalid compiler model. Instances must support repeated
calls, avoid retaining inputs, and stop promptly on cancellation. Disposable
instances are released before output installation. If disposal throws, the build
fails without installing outputs and live analysis reports an incomplete result;
the loader still attempts to dispose every other instance and load context. A
.NET 8 unloading-callback failure is also reported after the remaining cleanup;
the interrupted unload is retried once and loader-owned references are released.
.NET 8 uses collectible load
contexts. .NET Framework retains loaded assemblies until its AppDomain exits;
conflicting/replaced DLL bytes require a fresh domain or desktop MSBuild process
(`/nodeReuse:false`). The IDE recreates its SDK domain automatically when analyzer
inputs change. A reused desktop node fails visibly instead of running old code.

## XLIFF translation export

Export is explicit and reads saved RESX files, including when an editor has
unsaved changes. It does not compile alarms or save editor buffers. Run it from
a terminal; invoking Build/Save All in Visual Studio may save documents first.

```powershell
dotnet msbuild Plant.almproj /t:ExportAlarmlistXliff /p:AlarmlistXliffTargetCultures=de
dotnet msbuild Plant.almproj /t:ExportAlarmlistXliff /p:AlarmlistXliffTargetCultures=de%3Bfr /p:AlarmlistXliffOutputPath=translation-job-2
```

The second command uses MSBuild's escaped semicolon for multiple cultures.
Configure AlarmlistXliffSourceCulture in the project (also exposed on the project
property page), or override it for a command with `/p:AlarmlistXliffSourceCulture=en-US`.
Build/preview selections and their fallback rules are unaffected.

Each language pair produces `Alarmlist.<source>.<target>.xlf` and an adjacent
`.xlf.baseline.xml`. Send only the XLIFF to the translator; retain the baseline
unchanged for conflict checking on import. Existing exchange files are protected
unless AlarmlistXliffOverwrite is explicitly true. All pairs validate before any
file is replaced, with rollback for ordinary write failures. Build and Clean do
not create or remove translation exchanges. Export evaluates GetTargetPath to
protect compiled destinations, without building them. `@(AlarmlistXliffFiles)`
returns paths with SourceCulture, Culture and BaselineFile metadata.

The [exchange profile](../../VisualStudio/MILESTONE-5-DESIGN.md#xliff-export-profile-version-one)
uses XLIFF 2.0, exact culture entries, stable case-sensitive keys, comments and
protected parameter/lookup expressions and named placeholders such as `{value}`.
Every key needs a nonempty source translation
in the selected source culture. Export does not fill missing target entries from
parents. Existing nonempty targets must contain the same protected-expression and placeholder
occurrences as their source; reordering is allowed. Missing and explicitly empty
targets remain distinct in the baseline.

Trados Studio, memoQ and Phrase TMS are the intended tools; vendor round trips
remain unverified until recorded in the milestone. Import is described below.

## XLIFF translation import

Import uses saved RESX and the unchanged local baseline from export. Keep the
baseline beside the returned XLIFF with the original filename. Run a dry run
from a terminal, review its per-key old/new values, then explicitly apply:

```powershell
dotnet msbuild Plant.almproj /t:ImportAlarmlistXliff /p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf
dotnet msbuild Plant.almproj /t:ImportAlarmlistXliff /p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf /p:AlarmlistXliffApply=true
```

`AlarmlistXliffApply` defaults to false. Select several files with the usual MSBuild
escaped semicolon (`%3B`), or use explicit items. One exchange per target culture
is allowed in each batch. Apply always performs a fresh preflight; a prior dry run
does not authorize overwriting intervening edits. Build and Clean never import.
Design-time builds do not import, and applying from an IDE build is rejected to
avoid its automatic pre-build saves.

```xml
<ItemGroup>
  <AlarmlistXliffImport Include="returned/Alarmlist.en.de.xlf"
                      BaselineFile="xliff/Alarmlist.en.de.xlf.baseline.xml"
                      TargetResource="Translations.de.resx" />
</ItemGroup>
```

`BaselineFile` defaults to the XLIFF path plus `.baseline.xml`. `TargetResource`
is optional and only selects where previously absent keys are added. It must
already be an evaluated `AlarmlistResource` of the exact target culture. Existing
keys stay in their current resource file. If several target resources exist,
select a destination explicitly; if none exists, add a RESX first. No path from
inside the exchange or baseline can select a write destination.

Any changed source, conflicting saved target/comment, malformed or unsupported
profile, duplicate/unknown key, damaged placeholder or input collision rejects
the whole batch. Missing XLIFF targets leave entries unchanged; an explicit empty
target supplies an empty value. Culture fallback is never used during exchange.
Named placeholders and expression occurrences may reorder but cannot disappear,
duplicate or change. Reimporting already applied values is a no-op. RESX comments,
unrelated entries, headers and schema are preserved; translator notes remain
informational. Changed files are written as UTF-8 without a BOM; XML quote/entity
spelling and line endings can normalize. Untouched files retain their bytes.

Before apply, **save or discard pending changes and close every affected RESX
editor in Visual Studio**, including XML views. Dry runs can run while editors
are open. Native resource cells can hold unfinished text before their document
is marked dirty, so the guard conservatively refuses all open destination editors.
It queries running Visual Studio instances in the current Windows session through
read-only automation; it never activates, saves or closes them. An inaccessible
or unresponsive instance blocks apply. Reopen the editor afterward; the designer
refreshes from the saved imported resources. Other editors must also be closed
by the caller; no claim is made to inspect non-Visual-Studio editors.

Inputs are rechecked during installation. Ordinary failures roll back installed
files; this is not a crash-atomic filesystem transaction. If a newly installed
file is independently edited before rollback, preserve that newer edit and retain
the old backup with a reported recovery path. Cancellation writes no partial batch
when normal rollback succeeds. Compiled outputs are unchanged until a later Build.

The target returns `@(AlarmlistXliffChanges)`, with key identity and `ResourceFile`,
`Culture`, `PreviousPresent`, `PreviousValue` and `Value` metadata.
`@(AlarmlistImportedResources)` contains files actually updated by apply.
Compiler hosts can call `XliffImport.Preview` with captured `ResxTextSnapshot` and
`XliffImportInput` values to obtain immutable changes, conflicts and replacement
text. Any conflict suppresses every writable file; the compiler performs no I/O.
Vendor interoperability remains unverified until actual tool round trips pass.

## Reusable instruction templates and HMI lookups

Use named arguments to reuse a sentence across procedure steps:

```xml
<Step>$(loc:Instruction.SetProperty, path=$(path:Service.Vakuum), property=$(property:Service.Vakuum#Ein), value=$(meta:Service.Vakuum#Ein#Default))</Step>
```

The RESX entry `Instruction.SetProperty` contains
`In "{path}", set property "{property}" to "{value}".` Translations may reorder
placeholders. All names must match their supplied arguments; repeated names are
allowed. Literal arguments use quotes (`value="001"`); parameter arguments use an
explicit expression (`value=$(Setpoint)`). Inserted text is never expanded again.
Use `{{` and `}}` for literal braces in formatted templates. Plain `$(loc:Key)`
keeps its existing behavior; named-argument calls enable formatting.

External tools generate lookup XML; Alarmlist does not scan the HMI codebase:

```xml
<Lookup xmlns="urn:alarmlist:lookup:1">
  <Path Key="Service.Vakuum">
    <Segment LabelKey="Hmi.Service" />
    <Segment LabelKey="Hmi.Vacuum" />
    <Property Key="Ein" LabelKey="Hmi.On">
      <Metadata Name="Default" Value="060" />
      <Metadata Name="Maximum" Value="100" />
    </Property>
  </Path>
</Lookup>
```

Save it as `Hmi.almlookup`. The SDK includes `**/*.almlookup` by default, excluding
output/intermediate and hidden directories. Use `AlarmlistLookup Remove/Update`
or set `EnableDefaultAlarmlistLookupItems=false` and declare explicit items.
Linked files and conditions work as for other source inputs:

```xml
<ItemGroup>
  <AlarmlistLookup Include="../Shared/Hmi.almlookup" Link="Shared/Hmi.almlookup" />
</ItemGroup>
```

The included XSD is at `$(AlarmlistLookupSchema)`; its schema namespace is
`urn:alarmlist:lookup:1`. See the
[full format and escaping specification](../../VisualStudio/HMI-LOOKUPS.md).
Each path segment and member uses its LabelKey in the same RESX catalogue and
selected culture as the sentence. Parent-culture lookup is allowed; unrelated or
default-language fallback is not. Paths join their translated labels with ` -> `.
Properties and buttons use `$(property:Path#Member)` and `$(button:Path#Member)`.
Explicit `$(meta:Path#Member#Name)` inserts stored metadata verbatim, including
leading zeros and empty values. Metadata has no executable or numeric semantics:
there is no bounds validation or automatic default substitution.

Keyword output retains whole localization and lookup tokens literally, including
argument bindings, while validating referenced catalogue identities. Missing
paths/members/metadata fail compilation (`ALM0021`); malformed catalogue XML is
`ALM0019`, duplicate paths are `ALM0020`, and template argument mismatches are
`ALM0022`. Every culture validates before any output is replaced. Build captures
saved lookup contents once per batch; Clean preserves lookup and RESX source files.

The persistent [North/South example](../../../example/README.md#try-reusable-instructions-and-hmi-lookups)
contains reusable Set/Press instructions, generated-style code identities,
individually translated labels, project-scoped arguments and an explicit metadata
reference. Visual Studio completion offers named argument slots and catalogue
expressions from saved files. It neither applies metadata defaults automatically
nor publishes unsaved catalogue edits.

### General named lists

The same `.almlookup` inputs support arbitrary named lists. For example:

```xml
<Lookup xmlns="urn:alarmlist:lookup:1">
  <List Key="Wires">
    <Entry Key="EC120W1" Value="=EC-120W1" DescriptionKey="Wire.Outlet">
      <Metadata Name="DrawingPage" Value="023" />
    </Entry>
  </List>
  <Template Key="Instruction.DisconnectWire">
    <Argument Name="wire" List="Wires" />
  </Template>
</Lookup>
```

Use `$(list:Wires#EC120W1)` directly or as a named localization argument. Values
are literal text, preserving punctuation, empty values and leading zeros. Optional
DescriptionKey translates suggestion guidance through the selected culture;
Description is a mutually exclusive literal alternative. Missing descriptions do
not invalidate identifiers. Metadata remains literal selection guidance.

Lists merge across files. Duplicate entry identities report ALM0023; ambiguous
or unknown explicit references fail with ALM0021. Duplicate argument bindings and
unknown bound lists report ALM0024. Bindings are editor hints only: literal and
parameter arguments remain allowed. RESX owns the template's placeholder names.
The XSD and [complete format specification](../../VisualStudio/HMI-LOOKUPS.md)
describe these rules. No built-in DeviceTag field, relationship validation, or
multi-selection formatting is introduced.

Analysis contract 7 adds read-only LookupLists and ArgumentBindings. Entries expose
their literal Value, Expression, Location, ambiguity, optional DescriptionTranslation,
and metadata; bindings expose TemplateKey, ArgumentName, ListKey and availability.
Hosts use these detached compiler answers and revalidate saved inputs for completion.
See the [electrical sample](../../../example/README.md#try-supplied-lists-and-electrical-instructions)
for wire/connection and PLC-symbol instructions in keyword, English and German.

## Property expressions and templates

Expressions are always enabled in Name, Code, Category, Description and procedure
Step/Hint/Warning/Note text. `$(Name)` inserts a property; `$$` emits a literal
dollar, so `$$(Name)` remains literal. Inserted custom values are not expanded
again. FullyQualifiedName and ReferenceName remain literal identifiers.

Declare project parameters with evaluated items (MSBuild escaping rules apply):

```xml
<ItemGroup>
  <AlarmlistProperty Include="par1" Value="001" />
  <AlarmlistProperty Include="unassigned" />
  <AlarmlistProperty Include="empty" Value="" />
  <AlarmlistProperty Include="literalToken" Value="%24(par1)" />
</ItemGroup>
```

ALMX file and alarm scopes use the same declaration shape:

```xml
<Alarmlist>
  <Properties><Property Name="par1" Value="001" /></Properties>
  <Alarm abstract="true">
    <FullyQualifiedName>Template</FullyQualifiedName>
    <Properties><Property Name="par1" /></Properties>
    <Code>$(par1)</Code>
    <Description>Inspect $(AlarmlistAlarmCode)</Description>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Child</FullyQualifiedName>
    <ReferenceName>Template</ReferenceName>
  </Alarm>
</Alarmlist>
```

Only Child is emitted, with Code `001` and Description `Inspect 001`. References
inherit raw content and parameter declarations; base-alarm assignments and base-file
values do not flow to descendants. Value lookup uses the current alarm, its own
file, then the project. Missing Value falls through; `Value=""` stops fallback.
Unused unassigned parameters are allowed. Names are case-insensitive identifiers
(`A-Z`, `a-z`, `_`, followed by those characters or digits); `Alarmlist` is reserved.
Duplicates in one scope are errors. Abstract is local and defaults to false.

Well-known project values are `AlarmlistProjectName`, `AlarmlistProjectFile`,
`AlarmlistProjectFullPath`, `AlarmlistProjectDirectory`, `AlarmlistProjectExtension`,
`AlarmlistConfiguration`, and `AlarmlistPlatform`. File equivalents use
`AlarmlistThisFileName`, `AlarmlistThisFile`, `AlarmlistThisFileFullPath`,
`AlarmlistThisFileDirectory`, and `AlarmlistThisFileExtension`. Alarm properties use
`AlarmlistAlarmFullyQualifiedName`, `AlarmlistAlarmReferenceName`, `AlarmlistAlarmName`,
`AlarmlistAlarmCode`, `AlarmlistAlarmCategory`, and `AlarmlistAlarmDescription`.
Names omit the extension, File includes it, Extension includes the dot, and Directory
has no trailing separator except for roots. Inherited expressions use the consuming
alarm and file. Scalar alarm properties read evaluated values; cycles are errors.

The SDK passes captured project path, Configuration, Platform and AlarmlistProperty
items to the task. Direct compiler callers use `CompilationContext` overloads of
Compile, CompileFiles or CompileSnapshots; missing context diagnoses only on use.
No environment variables or ambient project properties are imported. ALM0005-ALM0012
cover invalid abstract/expression syntax, unknown or unassigned values, unavailable
context, scalar cycles, and invalid/duplicate declarations. Failed compilation
preserves previous output. Source serialization retains raw expressions.

## Localization and multiple outputs

Use `$(loc:Inspection)` in an ALMX text field and define `Inspection` in a saved
string-only RESX resource. For example, `Translations.en.resx` may contain:

```xml
<root>
  <data name="Inspection" xml:space="preserve">
    <value>Inspect $(par1)</value>
    <comment>Shown to the operator.</comment>
  </data>
</root>
```

The consuming alarm's parameter scope supplies `par1`; with `par1=001`, the result
is `Inspect 001`. Keys are case-sensitive, with grammar
`[A-Za-z_][A-Za-z0-9_.-]*`. The `loc:` prefix is case-sensitive. Nested localization
inside translations is initially unsupported. Escapes and substituted parameter
values remain literal; replacement text is never rescanned.

```xml
<PropertyGroup>
  <AlarmlistCultures>de-DE;en;keyword</AlarmlistCultures>
</PropertyGroup>
<ItemGroup>
  <AlarmlistResource Remove="obsolete/**/*.resx" />
  <AlarmlistResource Include="../shared/Operator.resx" Culture="en"
                     Link="Translations/Operator.en.resx" />
  <AlarmlistResource Update="My.Catalogue.resx" Culture="" />
</ItemGroup>
```

Default resource inclusion is `**/*.resx`, using the same output/intermediate and
hidden-directory exclusions as ALMX. Explicit Include, Remove, Update, linked
files, and conditions are supported. Disable the glob with
`EnableDefaultAlarmlistResourceItems=false`. Explicit `Culture` metadata wins,
including an explicitly empty value for an optional unqualified key catalogue.
Otherwise the filename's final dotted suffix is the culture: `Labels.de-DE.resx`
is German/Germany and `Labels.resx` is unqualified. Invalid dotted suffixes are
errors. Multiple resources per culture are allowed, but duplicate keys are errors.
String values and comments are supported; object/file-reference entries and DTDs
are rejected without deserializing or loading external content.

Culture lookup follows the requested culture and its .NET parents, stopping before
invariant: `de-DE` can use `de`. Empty values count as missing; whitespace values
are nonempty. There is no unqualified/default, unrelated-language, or keyword
fallback. A nonempty translation that evaluates to empty does not restart lookup.
Keyword mode displays the complete `$(loc:Inspection)` token, even for undeclared
keys. Resource structure and duplicate keys are still validated in keyword mode.

Missing, empty or whitespace-only AlarmlistCultures selects keyword. Other lists
trim and normalize entries, rejecting unsupported cultures, duplicates, and empty
entries within a list. Output paths are
`<OutputPath>/<culture>/<CompiledAlarmlistFileName>`, including `keyword`.
The filename must be a basename. `AlarmlistCulture` selects the independent
IDE preview selection; it does not alter the build list. The designer's Preview
language selector persists this shared property through CPS and uses saved RESX
contents. Resource drafts stay private until saved.

The task captures saved ALMX and RESX inputs once per batch, evaluates and serializes
all cultures, then stages the outputs. Validation/compilation failures leave every
previous output unchanged. Ordinary install failures and cancellation roll back
installed files; restoration failures are reported with retained backup paths.
This does not guarantee atomicity across process termination or power loss.
A project/configuration/platform ownership manifest under IntermediateOutputPath
tracks exact output and temporary paths. Successful builds retire outputs removed
from the selection or moved by output settings. Clean removes configured/tracked
files, without scanning or deleting culture folders. Unrelated files survive.
Rebuild explicitly runs Clean first, so a failed Rebuild cannot preserve files that
Clean already removed. ALM0013-ALM0018 report culture, resource, duplicate-key,
missing-translation, nested-localization and invalid-key errors.

Build and future localization consumers use saved RESX contents only. Pending
resource-editor drafts are never read or saved by Alarmlist. The native editor may
autosave a committed cell; that saved value is then eligible for capture.

## Compiler analysis API

The compiler in each SDK task-runtime directory exposes
`AlarmCompiler.AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot>)` for host adapters.
It returns a detached, read-only `Alarmlist.Analysis.AnalysisResult` with contract
version 5, input versions, local/effective fields, defining alarms and locations,
reference chains, procedure origins, Clear exclusions, and located diagnostics.
It shares the compiler's resolution rules and never reads or writes snapshot paths.

The additive overload `AnalyzeSnapshots(snapshots, includeCompiledXml: true)` also
returns `AnalysisResult.CompiledXml` from those same bound inputs through the
normal XML output writer. It is null when not requested or compilation fails.
The original overload keeps its analysis-only behavior and returns contract 7.
`AnalyzeLibrarySnapshots(snapshots, context, cancellationToken)` analyzes library
definitions with the same deferred-value rules as library validation. Missing
consumer values remain unavailable; syntax, structure and reference errors remain
diagnostics. This mode never returns compiled XML. Application analysis and
compilation remain strict about missing values.
`AnalyzeSnapshots(snapshots, context, includeCompiledXml)` captures the same
CompilationContext as compilation. EffectiveExpression and procedure RawText retain
source tokens; EffectiveValue and surviving procedure Text carry evaluated results.
Check IsValueAvailable and HasEvaluatedValues independently of IsResolved: an abstract
alarm or a field-dependency error can have a valid reference chain. IsAbstract marks
templates. Alarm and source Properties contain visible custom and well-known values,
local declaration/assignment presence, and separate declaration/value origins.
Contract 7 retains selected `Culture`, captured `Resources` (path, version and input
culture), raw `ResourceEntries` with comments/locations/validity/ambiguity, and
compiler-resolved `Translations` for catalogue keys. Requested and supplying
cultures are separate. Translation `IsAvailable` means lookup succeeded, not that
its property expressions can evaluate in every alarm. Keyword entries expose the
full literal token without inventing a resource origin. Invalid selections expose
null Culture alongside the culture diagnostic.

Each alarm's `LocalizationUses` records actual evaluated uses: the translation,
ALMX usage location, evaluated Value and IsValueAvailable. Abstract or excluded
content does not invent uses. A missing or invalid expression can retain lookup
provenance while its evaluated value is unavailable. Results are detached and
read-only; changing resource/culture inputs cannot mutate earlier results.
The bridge requires contract 7 and copies this data into host-owned serializable
objects. Resource filename inference is delegated to the SDK's
`LocalizationCulture.InferResourceCulture`; explicit empty culture stays distinct.
CPS saved-resource capture and designer localization follow in iterations six/seven.

`XmlAlarmlistOutputWriter.WriteToString(AlarmList)` is the in-memory output sink;
its text matches file output, including the UTF-8 declaration and `AlarmList`
root. Neither API saves source files or changes existing build output.

Check each alarm's `IsResolved` before using effective fields or procedure lists.
Invalid or ambiguous reference chains retain local data but have no effective
projection; unrelated valid alarms remain available. Malformed XML still throws
`XmlException`. Result-local ordinals distinguish duplicate or unnamed alarms;
they are not persistent identities. Editor commands must validate source versions
and use source locations rather than treating procedure indexes as XML child indexes.

The SDK exposes `AlarmlistAnalysisCompilerAssembly` during project evaluation as
the absolute path to its `tasks/net472/Alarmlist.Core.dll`. It always selects the
desktop compiler for the in-process Visual Studio host, regardless of the build
runtime or an `AlarmlistTasksAssembly` override. Querying it requires no Build
target and writes no compiled output. Treat it as an SDK-provided location;
an absent property means the SDK does not provide this bridge integration.

The extension now has an isolated AppDomain bridge for contract 7 and a CPS
project service that captures this property and ordered Compile inputs from the
same evaluation, then analyzes current buffer/disk snapshots. The designer uses
these results for reference selection, inherited scalar/procedure values, Clear
exclusions, section copies, live designer/Error List diagnostics, and resolved
previews. The bridge requires context-aware analysis and compiled XML preview;
older SDK contracts report explicit update guidance. No alternate compiler is
loaded. Release the matching SDK before distributing a VSIX using these capabilities.

## Build and test this repository

From the repository root:

```powershell
dotnet build AlarmlistProject.slnx
dotnet test AlarmlistProject.slnx
```

The SDK package is generated during Build at
`artifacts/packages/<Configuration>/NonShipping/Alarmlist.MSBuild.SDK.<Version>.nupkg`.
To consume it locally, add that directory as a NuGet package source. When using
a `NuGet.Config` with package source mapping, map `Alarmlist.MSBuild.SDK` to
the local source as well.

The compiler, tasks, and tests target `net472;net8.0`. Running the `net472`
tests requires Windows and .NET Framework. The dedicated `AlarmList.MSBuild.SDK.Tests` project resolves
the generated package from an isolated local feed and cache, so they do not
use a previously installed SDK package.

The package tests follow [Arcade's SDK test structure](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.Tests):
checked-in sample projects, an xUnit collection fixture, isolated test apps,
and retained MSBuild logs. See the [test project guide](../Alarmlist.SDK.Tests/README.md).

To run the package tests with desktop MSBuild, set the executable path:

```powershell
$env:ALARMLIST_TEST_MSBUILD_PATH = '<Visual Studio installation>/MSBuild/Current/Bin/MSBuild.exe'
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj --framework net472
Remove-Item Env:ALARMLIST_TEST_MSBUILD_PATH
```

The package layout follows the [MSBuild custom task packaging guidance](https://learn.microsoft.com/en-us/visualstudio/msbuild/tutorial-custom-task-code-generation).
