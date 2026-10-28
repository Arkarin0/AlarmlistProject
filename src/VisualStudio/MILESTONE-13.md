# Milestone 13 — Export providers

Status: complete. All nine planned iterations and their scoped acceptance gates
have passed. Vendor runtime import certification is excluded.

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
5. **Provider selection/loading.** Deliver 5a (captured provider loader and
   configuration validation) as an independently tested foundation. Deliver 5b
   (task/SDK activation and packaged consumers) together with iteration 6's
   publication boundary: multi-file providers cannot safely run through the old
   text-only transaction or predicted single-file output discovery.
   Wire Exports/Application/Library and export IDs
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

At initial planning, no product validation or implementation acceptance was
claimed. Completed iteration evidence follows.

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

### Iteration 5a evidence — complete; iteration 5b remains

- Extracted existing analyzer captured-byte loading into shared internal
  Extensibility/CapturedAssemblyScope. Analyzer behavior and lifecycle are retained;
  providers reuse its .NET 8 collectible contexts and desktop identity/conflict
  guard instead of introducing a separate binding policy.
- Added captured ExportAssemblyImage/ExportProviderReference and provider loader
  for explicitly declared public entry types. Validates declared/runtime IDs,
  contract version and duplicate/reserved IDs. Disposes partial instances/scopes
  on failure. Tests load a test-only provider from absent source paths and verify
  multiple/binary artifacts, precise rejection failures, recovery, defensive
  images and collectible-context selection; no process-memory inference.
- Added build-only ExportConfiguration for ID selection, registration metadata,
  explicitly declared dependency capture, host-contract protection, options and
  output-name validation. Only selected external code is captured. Exports is
  accepted as the Application alias by the existing output planner. Empty/absent
  selection defaults XML; Library rejects a nonempty explicit selection.
- Test authoring exposed reserved MSBuild FileName metadata. Use OutputFileName
  for provider output overrides; the provider request's FileName API is unchanged.
- Adjusted remaining plan dependencies: 5a is an isolated foundation commit;
  5b activation/package gates share iteration 6's transactional artifact boundary.
  Existing SDK builds remain XML-only until that boundary. NuGet distribution,
  dependency asset selection, and design-time nonexecution still require packaged
  consumer evidence; no claim that full iteration 5 is complete.
- Full compiler tests: 689 net472 / 691 net8.0 pass, no failures/skips. Includes
  existing analyzer disposal/unloading and repeated collectible-load tests.
  Task unit tests: 106 per framework pass. Task integration: 4 per framework pass.
  Commands: dotnet test for each compiler/task test project with --no-restore
  -v quiet -m:1 -nr:false. Existing CA1416 TestHelper warning remains.
- Output authoring notes and git diff --check reviewed. No 5a scoped blocker.

### Iterations 5b and 6 evidence — complete

- Activated OutputType=Exports (SDK default) and Application compatibility with
  AlarmlistExportTypes and explicit AlarmlistExportProvider registrations. XML
  remains the default; selected external providers can supplement or replace it.
  Library exports reject an explicit list and never load providers. JSON is
  reserved but not yet available until iteration 7; selecting it fails before
  publication rather than silently falling back to XML.
- Captures saved provider/dependency bytes and protects every declared provider
  path. Compiles all cultures first, invokes each selected provider with detached
  data, checks compatibility, validates all returned artifacts, and disposes
  providers before staging. Null/empty artifact collections and failures are
  build errors; cancellation/disposal failure cannot publish a partial batch.
- OutputTransaction stages exact binary bytes while preserving BOM-free existing
  XML build output and XLIFF text/expected-byte behavior. Preflights/revalidates
  link-free paths, stages the whole batch, restores ordinary installation or
  cancellation failures, and retains backups on failed restoration. Unit checks
  observe an installed binary before triggering recovery.
- Added export manifest version 3 with actual provider/culture paths, nested
  artifact support and bounded retained temporary ownership. Versions 1/2 still
  read; libraries retain version 2. GetTargetPath reads the last successful batch
  without loading providers; before a first export only XML has a predicted path.
  Clean removes owned files even after provider removal, preserving unrelated
  outputs. Output switching removes stale owned artifacts. No directory globbing
  or recursive output deletion was introduced.
- Full SDK regression initially found Library-to-Exports temporary-owner scope
  loss: deletion backups for retired library files are outside culture folders.
  Corrected scopes now retain their captured original file/root bounds; an added
  regression checks transition round-trip and rejects a forged escaping owner.
  Initial 93/90-pass runs with one failure each are superseded by final acceptance.
- Packaged verification exporter compiles both runtime assemblies against the
  extracted SDK contract, not a compiler project reference. Tests prove DLL and
  ordinary NuGet build-asset registration, a declared dependency, actual runtime
  choice, nested binary/sidecar files, later-culture failure preservation, invalid
  DLL failure/recovery, missing-package selection failure, output switching,
  XML-name overrides and GetTargetPath/Clean/design-time nonexecution. Constructor
  instrumentation is test-only. Package selection path assertions normalize
  ordinary build/../tools paths; the initial unnormalized assertion was not a pass.
- SDK project-reference globals and saved IDE dependency evaluation both remove
  consumer export selection. Exports retains ordinary application analysis.
  General.xaml offers Exports plus Application compatibility and export selection;
  embedded XML hints follow the same SDK rule values. No exporter loads in the
  in-process analysis path. Native functional acceptance remains iteration 9.
- Added the export feature guide and updated project-build, SDK, authoring/index,
  coverage and repository instructions. Reviewed local links, XML assets and diff;
  git diff --check passes. Existing CA1416 TestHelper warning remains.

Final scoped acceptance:

| Gate | Result |
| --- | --- |
| Full packaged SDK regression | 94 net472 / 91 net8.0 passed, zero failures/skips; Iteration5b6/SDKFull TRX records. |
| Task unit suite | 124 per framework passed, zero failures/skips; Iteration5b6/TaskUnit. Focused strengthened rollback rerun: 2 per framework passed. |
| Task build integration | 4 per framework passed, zero failures/skips. |
| VS2022 desktop MSBuild packaged export/reference cases | 4 passed net472, zero failures/skips; Iteration5b6/Desktop17. |
| VS2026 desktop MSBuild packaged export/reference cases | 4 passed net472, zero failures/skips; Iteration5b6/Desktop18. |
| IDE rule/XML hints and saved project/input tests | 119 passed net472, zero failures/skips. |

Commands used dotnet test with --no-restore -v quiet -m:1 -nr:false for SDK and
both task projects; SDK acceptance used --logger trx --results-directory
artifacts/log/Debug/Milestone13/Iteration5b6/SDKFull. Desktop repeats selected
ALARMLIST_TEST_MSBUILD_PATH for each installed host, --framework net472 --no-build
and filter FullyQualifiedName~ExportProviderTests|FullyQualifiedName~InvalidReferenceGraphPreservesConsumerOutput;
the previous environment value was restored. IDE filter combined RuleTests,
CatalogXmlCompletionTests, SavedProjectGraphTests and ProjectAnalysisInputTests.

No remaining scoped 5b/6 blocker. Iterations 7 (JSON), 8 (zenon) and 9 (integrated
native/fork/documentation acceptance) remain authorized and incomplete.

### Iteration 7 evidence — complete

- Added built-in metadata-driven JSON provider, formatVersion 1, producing exact
  BOM-free UTF-8 artifacts without source I/O or expression re-evaluation. Uses
  the platform JsonReaderWriterFactory writer on both frameworks; no serializer
  package dependency/version changes. Reference:
  https://learn.microsoft.com/en-us/dotnet/api/system.runtime.serialization.json.jsonreaderwriterfactory
- Wire format contains culture, semantic definition fingerprint, capability and
  ordered descriptors/alarms/fields/collections/procedures. Field names are values
  in ordered records rather than dynamic serializer members, preserving names
  such as __type. Scalar records carry valid/absent/deferred state, default flag,
  canonical text and typed JSON value. Numeric canonical digits are written
  directly; temporal strings and enum tokens are never reformatted or reparsed.
  Keyword-mode typed deferral remains distinct from absence and explicit empty.
- SDK now selects JSON alone or alongside XML/external providers. Defaults and XML
  bytes remain unchanged. JSON format 1 rejects unknown Option_ metadata before
  publication. OutputFileName uses the existing shared request configuration.
- Core tests cover UTF-8/no BOM, quotes/slashes/control characters/Unicode, null
  versus empty, all procedure kinds, culture fallback, keyword states, stable
  ordering/bytes under three current cultures, empty lists, cancellation and
  unsupported options. net8 tests also parse bytes with System.Text.Json.
- Generated scalar fixture adds a valid __type field and checks Int64 maximum,
  Decimal maximum, large Double exponent, booleans, enum tokens, canonical dates/
  times/timestamps, literal defaults and numeric keyword deferral. Collection
  fixture checks simple/record shapes, empty collections, inherited duplicate
  ordering and procedure capability. Both scripts compare actual JSON SHA-256
  vectors across net472/net8.0, rather than inferring parity from matching counts.
- Packaged tests compile/build JSON-only and XML+JSON in keyword/en, inspect
  resolved fields and titles, keep numeric-looking strings unchanged, verify
  deterministic rebuilds, reject unsupported options, preserve all artifacts and
  manifest on later-culture failure, retain Application compatibility and clean
  both formats. The existing DLL/NuGet provider case is included in focused runs.
- Added the versioned JSON feature/format reference, linked from the index and
  export guide; updated contract/instruction/backlog/coverage status. Reviewed
  local paths/links/placeholders, example syntax and git diff --check.

Scoped acceptance:

| Gate | Result |
| --- | --- |
| Full compiler suite | 692 net472 / 694 net8.0 passed, zero failures/skips; Iteration7/Compiler TRX records. Final focused JSON rerun: 3 per framework passed. |
| Task suites | 124 unit and 4 build integration tests per framework passed, zero failures/skips. |
| Packaged JSON + external provider cases | 2 per test framework passed, zero failures/skips; Iteration7/SDKFocused. |
| VS2022 / VS2026 desktop packaged JSON | 1 per host passed net472, zero failures/skips; Iteration7/Desktop17 and Desktop18. |
| Scalar generation/lifecycle/runtime | Both frameworks pass, including exact JSON byte-vector parity; AlarmDefinitionGeneration/af432bc6a51647f89bc68ce1d2a1aa8a. |
| Collection generation/runtime | Both frameworks pass for enabled/disabled procedures and minimum counts, with JSON byte-vector parity; AlarmCollections/2f79f57967ae4b7f831f0d7ec7322c17. |

Commands used dotnet test on compiler/task projects with --no-restore -v quiet
-m:1 -nr:false; SDK focused filter was FullyQualifiedName~JsonExportTests|FullyQualifiedName~ExportProviderTests,
with --logger trx and results under artifacts/log/Debug/Milestone13/Iteration7.
Desktop repeats use --framework net472 --no-build and each installed MSBuild.exe;
ALARMLIST_TEST_MSBUILD_PATH is restored afterward. Generator commands are
eng/test-alarm-definition-generation.ps1 and eng/test-alarm-collections.ps1.
The existing CA1416 TestHelper warning remains. Prior full SDK evidence remains
in 5b/6; integrated final regressions repeat under iteration 9 after zenon.

No remaining scoped iteration-7 blocker. Zenon and final native/fork acceptance
remain authorized and incomplete; the milestone goal is not complete.

### Iteration 8 evidence — complete

- Added external Alarmlist.Exporters.Zenon package with explicit provider ID Zenon,
  ordinary build assets selecting net472/net8.0 by MSBuildRuntimeType, and a
  configurable filename defaulting to $(MSBuildProjectName).Zenon.xml. Package
  includes both DLLs, build registration and README; tests reject compiler copies,
  lib assets, buildTransitive activation and package dependencies.
- Shared XML provider now accepts detached scalar/text projections while retaining
  its parameterless constructor and legacy serialization. Zenon reuses that shape
  and XML escaping. Key segments become @Key@; literals/defaults and concrete typed
  canonical values are preserved. No source scanning/evaluation or provider I/O.
- Rejects literal @, adjacent keys without a nonempty literal separator,
  RequiresSubstitution keys and unsupported provider options before publication.
  This is keyword XML, not a full vendor import file or language-table exporter.
  COPA-DATA language-switching reference reviewed:
  https://download.copadata.com/fileadmin/user_upload/Downloads/Dokumentation/820SP0/ENGLISH/Manual/Language_switch.pdf
- Tests cover exact user example in keyword/en, child-context inheritance,
  procedure field references, escaped/parameter token-looking literals, lookup
  labels/metadata, cancellation and rejected representations. A .NET 8 test retains
  artifacts plus the disposed loader and proves its collectible context dies by
  WeakReference; it does not infer unloading from process memory.
- Packaged consumers verify private NuGet and direct DLL registration, no automatic
  selection, actual host asset path, XML+JSON+Zenon coexistence, default/override
  names, retired artifact cleanup and previous complete bytes/manifest after
  delimiter and later-culture substitution failure. GetTargetPath, design-time
  Build and Clean succeed after the selected DLL is replaced with invalid bytes.
- Generated scalar fixture covers canonical numeric values and token-looking
  defaults; collection fixture covers deferred typed keys and sibling references,
  plus enabled/disabled procedure capability. Both frameworks pass.
- Added user feature guide, packaged configuration examples, authoring notes,
  index/coverage/backlog/instruction updates. Reviewed links, paths and diff.
  Initial unit build lacked the test's direct compiler reference (the package
  deliberately uses PrivateAssets); corrected. Initial package override test used
  a directory where OutputFileName accepts only filenames; corrected to keywords.xml.
  Neither failed attempt is counted as acceptance.

Scoped acceptance:

| Gate | Result |
| --- | --- |
| Full compiler regression, including legacy XML byte parity | 692 net472 / 694 net8.0 passed, no failures/skips. |
| Zenon unit tests | 9 net472 / 10 net8.0 passed, no failures/skips; extra .NET 8 collectible lifetime test. |
| Packaged XML/JSON/external/zenon cases | 3 per framework passed, no failures/skips; Iteration8/SDKFinal. |
| Desktop VS2022 / VS2026 MSBuild zenon | 1 per host passed net472, no failures/skips; Iteration8/Desktop17 and Desktop18. |
| Task suites | 124 unit and 4 integration tests per framework passed, no failures/skips. |
| Generated scalar/lifecycle | Both runtimes pass; AlarmDefinitionGeneration/f1e6baa238fb48ab9b454438d227bfaa. |
| Generated collections | Both runtimes and procedure capabilities pass; AlarmCollections/057173ad8ebe452da50440546769a9f6. |

Commands used dotnet test on compiler, zenon, SDK and task projects with
--no-restore -v quiet -m:1 -nr:false --logger trx; results are under
artifacts/log/Debug/Milestone13/Iteration8. SDK focused filter combines
ZenonExportTests|JsonExportTests|ExportProviderTests. Desktop repeats use
--framework net472 --no-build with ALARMLIST_TEST_MSBUILD_PATH selected from each
installed IDE and restored afterward. Generator scripts are
eng/test-alarm-definition-generation.ps1 and eng/test-alarm-collections.ps1.
The existing shared TestHelper CA1416 warning remains. git diff --check passes.

No scoped iteration-8 blocker. Iteration 9's full integrated regressions,
native/fork acceptance and guided export tutorial remain authorized and incomplete.

### Iteration 9 evidence — complete

- Added native Area=ExportBuild on both IDE hosts. Loads an Exports project whose
  selected provider DLL contains invalid bytes and verifies resolved Alarm Summary
  data before any Build. A valid matching zenon DLL then builds alongside XML/JSON.
  Reopening with an invalid DLL keeps analysis usable; a failed Build preserves
  outputs. Selecting JSON succeeds without loading the unselected invalid provider,
  removes old XML/zenon files, and supports Clean and Build.
- Installed XML catalogue acceptance adds Exports, Application and
  AlarmlistExportTypes candidates, including insertion/Escape/Undo checks. VS2022
  passed both export/catalogue cases. VS2026 passed ExportBuild and installed
  catalogue acceptance. Both hosts closed their owned test IDEs; no user IDE was
  closed.
- Added -Exports to collection-fork/consumer fixtures. Builds a matching external
  zenon package and verifies XML/JSON/zenon through both ProjectReference and NuGet
  library consumption. Verification compiles against extracted packaged compiler
  DLLs, never compiler project references. Checks retyped numeric Code, canonical
  scalars, removed fields, metadata fingerprint, collection sibling values and
  procedure capability. Zenon equals ordinary XML for these literal-only sources.
  Analyzer/scalar/cardinality failures preserve all export and manifest hashes.
  JSON-only switching removes old files; Clean/Rebuild round-trips all formats.
- The first fork consumer run exposed PowerShell scalar unrolling of a one-element
  command-prefix array. Corrected to an explicit string array. Successful consumer
  repeats and enabled/disabled forks supersede that fixture failure.
- Guided export tutorial uses actual XML/JSON/zenon examples and checks hashes
  after delimiter failure, format switching and Clean. Executed its XML code blocks
  in an isolated local-feed directory; every expected result passed. Extended
  provider-authoring notes cover runtime packaging, detachment, dependencies,
  cancellation/disposal and trusted-code boundaries. Local links/placeholders and
  git diff --check reviewed.
- Separate isolated shared-project check builds North with XML+JSON and South with
  JSON only, inspects en/de-DE consumer-specific values, verifies source hashes,
  no shared-container outputs, and South Clean preserving North output. Initial
  setup inherited repository feed mappings; clearing those in its private config
  fixed setup. No checked-in playground sources/configuration were changed.

Passing acceptance evidence:

| Gate | Evidence |
| --- | --- |
| VS2022 export build + installed XML hints | 2 passed, no failures/skips; VisualStudioUI/TestResults/307fb511a2b84c38bb7ea479ec965f71. |
| VS2026 export build | Passed; VisualStudioUI/TestResults/c927de5f343c465f9dc0ed3d5b6b4010. |
| VS2026 installed XML hints | 1 passed, no failures/skips; VisualStudioUI/TestResults/09df5551db504e579977fc2ee53d1cc6. |
| Procedure-enabled fork, VS2022 desktop + .NET MSBuild | col13/27db5d96/consumer-checks/cbcd6de4. |
| Procedure-enabled fork, VS2026 desktop + .NET MSBuild | col13/67ce9429/consumer-checks/6ba1a61f. |
| Procedure-disabled fork, VS2026 desktop + .NET MSBuild | col13/f4a3c3ea/consumer-checks/f8859e32. |
| Procedure-disabled fork, VS2022 desktop + .NET MSBuild | col13/f4a3c3ea/consumer-checks/e2f0eda6. |
| Actual tutorial examples | Milestone13/Iteration9/Tutorial/4ad9bfa6b7f548ea89ddccc20a284848. |
| Shared export selection and cultures | Milestone13/Iteration9/SharedExports/72da47359f8a48a6896a5d710f45798d. |
| Full VSIX/package integration | 14 per framework passed, no failures/skips; Iteration9/ExtensionPackage. |
| Full SDK regression | 96 net472 / 93 net8.0 passed, no failures/skips; Iteration9/SDKFull. |
| Full IDE unit regression | 832 net472 passed, no failures/skips; Iteration9/ExtensionUnitRepeat. |

Compiler/task/zenon production code is unchanged since iteration 8's full
dual-framework acceptance, including legacy XML byte parity and generated
scalar/collection runtime fixtures; that evidence remains applicable.

Commands: eng/test-visualstudio.ps1 with each installed IDE path and
-Filter 'Area=ExportBuild|Area=XmlCatalogCompletion'; VS2026 catalogue acceptance uses
-Filter 'Area=XmlCatalogCompletion'. Collection acceptance uses
eng/test-alarm-collection-fork.ps1 -Exports -DesktopMSBuildPath <host>, and
eng/test-alarm-definition-fork-consumers.ps1 -Collections -Exports -ForkRoot <fork>
for the second desktop host. Full SDK/VSIX/unit suites use dotnet test on their
projects with --no-restore -v quiet --logger trx --results-directory under
artifacts/log/Debug/Milestone13/Iteration9; SDK/package builds use -m:1 -nr:false.
The accepted unit run uses --no-build. Native and WPF suites ran
sequentially, separately from SDK XLIFF tests. Environment overrides were restored.

Reviewed focused diffs, changed documentation links and git diff --check. The
existing shared TestHelper CA1416 warning remains. No essential gate is unavailable
and no scoped implementation blocker remains. This completes Milestone 13;
full zenon import/language-table generation, vendor runtime certification and
unrelated historical milestone release gates remain outside this milestone.
