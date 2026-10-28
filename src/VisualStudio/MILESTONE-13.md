# Milestone 13 — Export providers

## Outcome and confirmed scope

Authorized on 2026-10-28. Build resolved alarms through selectable XML, JSON and
external export providers. Supply a packaged zenon keyword-XML provider. Providers
may return multiple text or binary artifacts per culture. Support declared NuGet
packages and explicit DLL references. Publish all cultures/providers as one batch;
failure preserves the previous complete outputs and manifest.

Application projects use OutputType=Exports and AlarmlistExportTypes=XML;JSON;Zenon.
Application remains a compatibility alias; absent export selection defaults to XML.
OutputType=Library retains its existing artifact and rejects export selections.
TargetFramework retains its runtime meaning. Existing XML bytes/names and public
writer APIs remain compatible. Exporters never execute during design-time analysis
or inside the VS extension. Provider assemblies execute as trusted build code.

Exclude CSV, printable documents, full zenon import files, IDE export dialogs and
other backlog proposals. The zenon provider keeps the existing alarm XML shape,
rendering compiler-owned localization segments as @Key@ and preserving literals
and evaluated parameter values. Do not reconstruct segments from resolved strings.
Ambiguous literal @ or unsupported adjacent keyword text fails explicitly.

## Design direction

Use versioned immutable detached resolved data, including typed scalars, generated
metadata, collections, procedures, culture and literal/localization text segments.
Providers return relative artifact names and bytes; only the host publishes files.
Validate provider IDs, compatibility, configuration, paths and collisions before
publication. Built-in IDs XML/JSON and sample Zenon are selected case-insensitively.
Load only explicitly declared external entry types. Choose package assets for the
executing task host framework, not the consumer project's TargetFramework.

Keep output infrastructure in compiler Output, build lifecycle in MSBuild and
consumer configuration/package assets in SDK. Consulted Roslyn's portable compiler
and Arcade's SDK layout: retain the existing compiler/build boundary and root SDK
integration; no Roslyn runtime dependency or upstream build-policy copy is needed.

References:
- https://github.com/dotnet/roslyn/tree/main/src/Compilers/Core/Portable
- https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk
- https://docs.copadata.com/?articleId=33174&lang=en&product=supervisor&ver=1500

## Iterations and acceptance

Each iteration depends on the preceding iteration. Commit its implementation,
tests, documentation and actual evidence before starting the next. Keep planned
acceptance separate from results. Native/WPF fixtures run sequentially in owned
profiles; never close user IDEs.

1. **Plan and configuration contract.** Record scope, exclusions, sequence and
   property decisions; link from instructions/backlog. Review diff and links;
   no build needed. Complete when the durable plan is committed.
2. **Artifact primitives.** Add immutable binary/text artifact and segmented-text
   primitives under Output; introduce the provider contract version in iteration 3. Test defensive copies, Unicode,
   ordering and invalid inputs on net472/net8.0. No build activation yet.
3. **Resolved provider contract.** Add immutable detached document/metadata model,
   provider request/result interface and XML adapter. Preserve default XML bytes.
   Test typed fields, collections, procedures and alternate definitions on both
   frameworks. Include contract/definition compatibility validation.
4. **Compiler text segments.** Carry compiler-owned literal/localization segments
   through fields, property dependencies, collections, procedure titles/steps and
   formatted lookups without changing existing expression semantics. Validate
   inherited child context, translated arguments, keyword/concrete cultures and
   absent/default values. Do not claim zenon support until this gate passes.
5. **Provider selection/loading.** Wire Exports/Application/Library and export IDs
   through task and SDK. Add explicit DLL and declared NuGet provider loading,
   dependencies and host-framework selection. Validate duplicate/missing IDs,
   incompatible providers and design-time nonexecution in packaged consumers.
6. **Publication/lifecycle.** Extend staging/rollback to multiple binary artifacts,
   manifests, stale output removal, Clean and GetTargetPath. Reject escaping paths,
   input collisions, duplicate outputs and symlink escapes. Test cancellation,
   exceptions, failed installation and preservation of unrelated files on both
   frameworks. XML-only consumers remain compatible.
7. **JSON.** Ship deterministic versioned JSON with culture, alarm identity,
   generated field names/typed values, collections and procedures. Distinguish
   absent from empty; preserve enum tokens and compiler ordering. Validate package
   consumers selecting JSON only and XML+JSON; document the format.
8. **Zenon sample.** Package the external keyword-XML provider for both task hosts,
   consuming compiler segments. Test mixed keywords/literals, parameters, generated
   fields and rejected delimiters; demonstrate NuGet and DLL configuration. Binary
   multi-artifact acceptance uses a test provider, not a vendor format claim.
9. **Integrated acceptance.** Compiler/task/SDK affected full regressions on both
   frameworks, desktop MSBuild on both IDE hosts, shared/library/package consumers,
   custom definition forks and output switching/Clean/Rebuild. Native project
   loading/build and XML hints accept new settings without executing exporters
   in analysis. Add feature/tutorial/provider-authoring documentation. Record any
   unavailable essential check as a blocker, not success. Runtime zenon import
   certification remains outside scope.

## Evidence and status

Planning: clean dev/milestone-13; inspected existing writer, generated scalar and
collection values, compilation evaluator, task output plan, transaction, SDK
targets and pinned Arcade integration. Existing enum-based writer selection is
XML-only; existing transaction stages text, requiring binary support later.

No product validation or implementation acceptance is claimed yet.

Iteration 1: plan/backlog/instruction links reviewed; git diff --check passed.
Tests not run (documentation only).

### Iteration 2 evidence — complete

- Added ExportArtifact with defensive byte copies and strict BOM-free UTF-8 text
  creation, plus immutable ordered ExportText/ExportTextSegment primitives.
  Filesystem validation deliberately remains the publication host responsibility.
- Literal text resembling expressions or zenon delimiters stays literal. These
  primitives do not yet capture compiler evaluation boundaries or activate exports.
- Output tests: 5 passed per framework (net472/net8.0), no failures/skips:
  dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj --filter FullyQualifiedName~Alarmlist.Output.Tests --no-restore -v quiet
- Full compiler regression: 675 net472 and 677 net8.0 passed, no failures/skips:
  dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj --no-build --no-restore -v quiet
- Existing CA1416 warning in shared TestHelper remains. No task/SDK or IDE behavior
  changed; downstream package/native acceptance belongs to its later iterations.
  Reviewed diff and git diff --check. Iteration 3 is next; provider compatibility,
  detached resolved document and XML adaptation are not implemented yet.
