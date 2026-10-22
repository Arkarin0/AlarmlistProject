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

Current iteration boundary: compiler fields are metadata-driven, with raw syntax
access through `GetLocalFieldValue`/`SetLocalFieldValue` and resolved values through
`Alarm.GetField`. Generated accessors expose nullable typed values; deferred typed
values remain null and retain their token text in the generic field result.
ALM0029 reports mandatory scalar/required/bounds errors. String output retains its
text; typed output is invariant and canonical. Defaults are literal and are never
written into authored source.

Analysis 6, analyzer 4 and library format 2 carry matching definition metadata
and fingerprints. Rebuild matching tools after definition changes; this is not
runtime project customization. The field designer and previews use metadata;
full fork acceptance and integrated validation remain later milestone gates.

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
