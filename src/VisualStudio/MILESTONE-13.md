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

### Iteration 3 evidence — complete

- Added version-1 IAlarmlistExportProvider, ExportRequest and immutable detached
  ExportDocument with generated field/collection metadata, typed values, ordered
  procedure groups and concrete culture. Captures mutable lists/options; rejects
  failed compilations, invalid/deferred scalar states and incompatible contract
  or definition fingerprints with rebuild guidance. Enum values are detached
  canonical tokens, not generated enum objects.
- Added metadata-driven XML adapter. Legacy public APIs are unchanged. Tests
  compare actual file bytes (including BOM/escaping/newlines) and verify cancellation,
  compatibility, snapshot independence, generated typed scalars and collections.
- Added Output/README.md documenting the internal authoring contract and pending
  segment capture/SDK activation. No provider is loaded by builds or the IDE yet.
- Updated isolated generation/collection fixture source membership to include the
  existing ResourceFileName helper required by LocalizationCulture. Initial
  collection compile exposed that stale fixture membership; fixed run passes.
- Output tests: 8 per framework passed. Full compiler: 678 net472, 680 net8.0
  passed, no failures/skips. Downstream task tests: 99 per framework passed.
  Commands: dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj
  --no-restore --no-build -v minimal; dotnet test
  src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj
  --no-restore -v quiet -m:1 -nr:false.
- eng/test-alarm-collections.ps1 passes both frameworks with procedures disabled,
  enabled and minimum cardinality: AlarmCollections/5b21855d5c3741f4bdf859a75a2f5f57.
- eng/test-alarm-definition-generation.ps1 passes full lifecycle and alternate
  typed/required runtime cases on both frameworks:
  AlarmDefinitionGeneration/d6da312f6cc44410bbd3240d964c43fd. A separate scalar-only
  diagnostic run also passed, but does not substitute for that full fixture.
- Initial restricted-process runs encountered silent nested build failure, failed
  Clean assertions and test-host connection timeouts. Full successful compiler
  and generation reruns outside that environment supersede those failed attempts.
  Existing shared TestHelper CA1416 warning remains. git diff --check passed.
- No remaining scoped iteration-3 blocker. SDK/provider activation, compiler text
  boundaries and native/package acceptance remain in iterations 4–9.

### Iteration 4 evidence — complete

- Expression evaluation now constructs immutable resolved text and ordered literal/
  localization segments together. Scalar fields, inherited child-context values,
  field dependencies, collections, sibling references, procedure titles and steps
  retain compiler-owned boundaries. Defaults, escaped expressions and parameter
  assignments stay literal; no resolved-string scanning or compiler file I/O.
- Concrete localization segments expose their translated Expansion and mark
  formatted/expression-dependent translations RequiresSubstitution. Lookup labels
  expose their resource keys; paths retain separators. Metadata/list values stay
  literal even in the keyword-preserving representation, while ResolvedText keeps
  the existing ALMX token in keyword mode. Existing diagnostics and output text
  remain unchanged.
- ExportScalar now exposes Text and IsDeferred. The previous iteration incorrectly
  rejected available keyword-mode typed results solely because of Deferred state;
  these are now retained with their raw canonical token and nullable typed Value.
  Truly unavailable/invalid values still fail export snapshot construction.
- Procedure item equality/hash remain based on existing Kind/Text semantics; export
  metadata must not alter resolved item equality. The existing snapshot equality
  regression caught this during implementation and passes after the correction.
- New tests cover inherited child assignments, parent-culture fallback, field and
  procedure dependencies, escapes/literal token-looking values, formatted lookup
  expansion, absent/empty fields and empty-title fallback. Initial new tests used
  an unsupported title attribute/resource constructor and were corrected to the
  existing Title element and captured-resource constructor; no syntax was added.
- Full compiler regression passes: 683 net472 / 685 net8.0, zero failures/skips:
  dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj --no-restore
  -v quiet -m:1 -nr:false. Includes the unchanged legacy output regressions.
- Generated collection fixture passes both frameworks with procedure enabled/
  disabled and minimum-count variants; new checks cover sibling key boundaries,
  keyword typed deferral, literal record defaults and nested formatted expansion:
  eng/test-alarm-collections.ps1, AlarmCollections/0408c0870b3e4a26ab969e13d04c57e3.
- Full generator lifecycle/scalar fixture passes both frameworks, including new
  literal-default export assertion: eng/test-alarm-definition-generation.ps1,
  AlarmDefinitionGeneration/6de00b7b22b346659f844d11ce1823d2.
- Downstream task suites pass on both frameworks: 99 unit and 4 integration tests
  per framework. Commands use dotnet test for each task test project with
  --no-restore -v quiet -m:1 -nr:false. Existing CA1416 TestHelper warning remains.
- Output authoring README updated; git diff --check passes. No scoped blocker.
  SDK activation, concrete exporters and packaged/native acceptance remain in
  iterations 5–9; none are claimed complete by these compiler checks.
