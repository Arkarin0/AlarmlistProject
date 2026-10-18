# Milestone nine: reusable alarm libraries

Status: iterations 1–9 and 13–14 complete; iterations 10–12 planned for Solution Explorer dependencies; iteration 15 planned for XML keyboard editing.
The native integration foundation gate has passed on both supported hosts.

## Outcome and confirmed scope

Deliver reusable alarm libraries through ProjectReference and NuGet, including
native Visual Studio reference management, designer completion, previews and
navigation. The [design contract](MILESTONE-9-DESIGN.md) records the owner's
confirmed semantics. Preserve net472/net8.0 compiler/tasks, the net472 VS host,
compiler AppDomain isolation and framework-independent consumer projects.

Libraries preserve definitions, inheritance, abstract templates, concrete alarms,
expressions, resources, lookups and portable origins. Consumers resolve final
values. Saved referenced-project changes refresh consumer analysis without Build;
unsaved dependency edits remain private. Native Add Project Reference and NuGet
management on both VS2022 and VS2026 are required, not optional follow-ups.

Excluded: Shared Projects, XML IntelliSense, syntax generation, automatic
publication and legacy bridge compatibility adapters. Historical manual/vendor/
release gates remain separate. This milestone does not authorize publication.

## Architecture and references

Keep artifact validation/evaluation compiler-owned and input discovery/capture
host-owned. Use immutable detached inputs/results across the SDK boundary. Use
the existing Arcade repository engineering conventions and isolated package-test
fixtures, and Roslyn's separation of references, binding, diagnostics and hosts.

- [MSBuild ProjectReference protocol](https://github.com/dotnet/msbuild/blob/main/documentation/ProjectReference-Protocol.md)
- [NuGet pack/restore targets](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets)
- [NuGet VS extensibility contracts](https://learn.microsoft.com/en-us/nuget/visual-studio-extensibility/nuget-api-in-visual-studio)
- [Roslyn metadata references](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/MetadataReference/MetadataReference.cs)
- [Arcade SDK](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk)

NuGet VS integration must use supported public VS contracts, not private NuGet
client implementation assemblies in devenv. Consumer projects must not require a
TargetFramework. The native integration feasibility gate precedes compiler work;
if it fails, record evidence and revise the plan rather than dropping native UI.

## Numbered implementation iterations

Each iteration depends on its predecessor. Keep existing compiler/SDK/editor
workflows usable at boundaries, include tests/documentation with the behavior,
and commit a successful iteration before starting the next. Record exact commands,
results and limitations below; planned checks are not passing evidence.

| Iteration | Deliverable | Required validation and completion criteria |
| --- | --- | --- |
| 1 | Contract and native integration foundation: framework-independent restore inputs, CPS reference rules, native project reference and NuGet participation. | Isolated fixture restore and native Add/Remove Project Reference plus package install/update/uninstall on both supported IDE hosts. Ordinary project workflows remain usable. Native integration is an essential gate. |
| 2 | Deterministic library artifact writer/reader, captured library graph, binding/evaluation, resource precedence and deferred values; coordinated analysis 5/analyzer 3 bridge/sample/consumer update. | Compiler tests on both frameworks for round trips, scopes, provenance, malformed artifacts and graph errors; editor bridge/package-contract tests. No mismatched compiler/bridge iteration boundary. |
| 3 | SDK OutputType=Library, reference build/configuration ordering, GetTargetPath and transactional outputs, reference-only consumers. | Packaged SDK consumers on both runtimes; conditional references, solution mapping, BuildProjectReferences=false, Build/Clean/Rebuild, no-build output discovery and previous-output preservation. |
| 4 | NuGet Restore/Pack/PackageReference library workflows and package dependency mapping. | Fresh isolated feed/cache, warm offline use, transitive packages, updates/removal/conflicts and equivalence to project references. Pack rejects unrepresentable dependencies. |
| 5 | Saved referenced-project graph capture and consumer invalidation, including unloaded/out-of-solution projects. | Saved source/import/membership/configuration changes, deletion/restore and races; unsaved dependency isolation; no output writes; lifetime cleanup and current-input validation. |
| 6 | Library-aware designer completion, preview and navigation. | Origin labels, concrete imported preview rows, unavailable deferred values, local-source navigation and read-only package views; drafts, Undo, stale actions and disposal; native cases on both hosts. |
| 7 | Library project creation/properties, persistent examples and complete usage documentation. | Two consumers and a transitive dependency with template/concrete alarms, parameter/resource/lookup overrides; native and CLI workflows, templates and example tests. |
| 8 | Integrated acceptance and performance. | Full regressions, both task runtimes, package consumers and native workflows on both hosts; graph capture/save refresh/preview/unload measurements with retained raw data and correctness checks. Audit all requirements. |

## Acceptance matrix

- Compiler: abstract/concrete imports, cross-library inheritance/groups, deferred
  and final validation, explicit empty values, consumer scope, culture fallback,
  overrides/sibling conflicts, cycles/diamonds, duplicate alarms, format versions,
  deterministic artifacts and portable source identity.
- SDK: equivalent project/package output, conditions/configuration mapping,
  fresh/offline restore, unavailable dependencies, failed-output preservation,
  input-safe Clean, reference-only consumers and transitive packaging.
- Editor: source save refresh without Build; unsaved dependency exclusion;
  invalid-input stale-result suppression; update/remove/restore refresh;
  navigation, drafts, Undo, linked contexts and unload/reload.
- Native: add/remove project references and install/update/uninstall packages
  on VS2022 and VS2026, including reopen and restore recovery.
- Compatibility: both compiler/task frameworks, framework-independent consumers,
  host isolation, coordinated contracts, SDK/VSIX package content and preserved
  existing application workflows.

Run WPF tests, native IDE tests and isolated WPF measurements sequentially. No
machine-dependent performance thresholds. A skipped opt-in test is not acceptance.

## Implementation evidence

### Iteration 1 — complete

Initial inspection: clean `dev/milestone-9` at `8a98bd2`. The existing SDK has an
empty Restore target, no consumer ProjectReference pipeline, no Pack workflow and
no reference/package CPS capabilities. Current Build/GetTargetPath return final
culture-specific XML. Existing UI test/deployment infrastructure supports both
IDE hosts and isolated fixtures. This is the baseline, not a gate result.

Plan and design contract recorded. Added the SDK NuGet restore import using the
installed MSBuild toolset's targets, with `native,Version=v0.0` as its default
restore framework identity and no required consumer TargetFramework. A packaged
SDK test creates local data-only packages, restores a transitive dependency and
verifies that removing the direct reference removes the closure from assets.
These are restore probes, not implemented alarm library artifacts.

Validation so far:

- `dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
  --framework net8.0 --no-restore --filter
  'FullyQualifiedName~PackageRestoreTests|FullyQualifiedName~BuildLifecycleTests|FullyQualifiedName~TargetFrameworkTests'
  --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone9/Iteration1/RestoreTests`: 6 passed, no failures/skips.
- Same selection on net472 with ALARMLIST_TEST_MSBUILD_PATH selecting VS2022
  desktop MSBuild: 6 passed, no failures/skips; results under
  `artifacts/log/Debug/Milestone9/Iteration1/DesktopRestoreTests`. Restored the
  previous environment value after the test. Initial net472 compilation exposed
  a missing System.IO.Compression framework reference in the new test; added the
  conditional reference and reran successfully.
- Exploratory scratch restore found that NuGet rejects an Any framework identity;
  its native identity succeeds without TargetFramework. Scratch inputs/results
  remain under the ignored `artifacts/log/Debug/Milestone9/Iteration1/RestoreProbe`.
- `RuleTests` focused editor unit suite: 13 passed before the subsequent reference
  metadata additions; rerun required against the final iteration contents.
- Native VS2022 reference-manager probes exposed an unsupported-reference
  confirmation, captured in `VisualStudioUI/ProjectReferenceManagement/2022/
  bcf714a49bda489db9c3722ea8a098c7/reference-error-ui.txt`. The dialog lists the
  library but CPS's default compatibility checker does not accept this project
  type. Added an Alarmlist-scoped `IValidProjectReferenceChecker`, following the
  [.NET project system's checker](https://github.com/dotnet/project-system/blob/main/src/Microsoft.VisualStudio.ProjectSystem.Managed/ProjectSystem/References/AlwaysAllowValidProjectReferenceChecker.cs):
  reference editing is allowed; semantic compatibility belongs to the common
  build/analysis pipeline implemented in later iterations. Its precedence is 10,
  ahead of CPS's default 0, matching the upstream checker.
  A subsequent automation probe confirmed that DTE ExecuteCommand blocks while
  the modal reference manager is open; the test now checks command readiness but
  invokes the native menu to leave its test thread free to operate the dialog.
- `eng/test-visualstudio.ps1 -VisualStudioPath <VS2022> -RootSuffix AlarmlistExp
  -Filter 'Area=LibraryReferences'`: 1 passed, no failures/skips. Native reference
  manager add/save/remove passed without the unsupported-reference prompt;
  fixture and cleanup evidence: `VisualStudioUI/ProjectReferenceManagement/2022/
  d56c89498b8d40289d418471a99da6b5`; TRX run `fe072c2e82334b91ae008d540993db25`.
  This proves reference editing only, not library build/analysis or reopen behavior.
- VS2026's context Add submenu did not expose the reference command, while its
  native main Project menu did. The test now uses that menu and the stable
  `OkButton` dialog identifier. In fixture `5064b223f1bf4f188be77cfc3d8ad31e`, the
  initial add/remove/add phase completed, but the reopened phase failed; cleanup
  masked its original exception with a focus error. Original-error recording is
  fixed. This is partial evidence, not a passing VS2026/reopen gate.
- `eng/test-visualstudio.ps1 -VisualStudioPath <VS2022> -RootSuffix AlarmlistExp
  -Filter 'Area=LibraryPackages'`: 1 passed, no failures/skips. Native NuGet UI
  installed local data package 1.0.0, updated it to 2.0.0, and uninstalled it.
  Each operation saved the project and verified both PackageReference and restored
  assets, without a consumer TargetFramework. Fixture/cleanup:
  `VisualStudioUI/LibraryPackages/2022/d5d96c03bd264e6187efcecc747e8302`;
  TRX run `089510cff9d74678a6e17d5e38d6361a`. These packages are data probes,
  not `.almlib` artifacts. The initial probe omitted project Save and correctly
  failed its disk-persistence assertion despite NuGet completing the install.
- Full SDK net8.0 regression initial run: 52 passed, 3 failed. Updated the schema
  count/identity assertions for the two new rules and isolated design-time Build
  from the test helper's explicit Restore (which now legitimately creates obj
  assets). XLIFF apply failed closed while a concurrent test IDE was running;
  documented serialization of these checks in AGENTS.md. Full rerun without a
  concurrent IDE test: **55 passed**, no failures/skips; TRX
  `Milestone9/Iteration1/SDKNet8/sobon_DESKTOP-6R4P20U_2026-10-13_21_19_08_net8.0.trx`.
- Final-current `RuleTests|ProjectAnalysisCompositionTests` selection: **15 passed**
  on net472, no failures/skips. Includes a regression assertion that the reference
  checker is restricted to Alarmlist and takes precedence over the CPS fallback.
  Results: `Milestone9/Iteration1/Rules/...21_20_29_net472.trx`.
- Subsequent VS2026 diagnostics isolated two harness differences: after reopening,
  a foreground window dismissed the main Project menu (fixture
  `7a379dcbde4247389c612ea1f60f3e89`); the NuGet version dropdown exposes its full
  list in a separate UIA popup rather than combo descendants (fixture
  `366f32947f4843feb8c06d037a983441`). The test now re-expands the menu with owned-IDE
  focus checks and locates version items in the owned process's popup. Retest
  pending; neither failed run counts as passing acceptance.
- Final VS2026 `Area=LibraryReferences|Area=LibraryPackages` run: **2 passed**,
  no failures/skips. Project references add/remove/add, exact-path persistence,
  checked state after a fresh IDE reopen, and removal all passed. Native NuGet
  install/update/uninstall and saved-project/assets checks passed. TRX run
  `dcbc39d59d7148449d9abe5adb584e1d`; fixtures
  `ProjectReferenceManagement/2026/a6a063562544461bb1547d1fcdcacdca` (including
  Restart cleanup without forced termination) and
  `LibraryPackages/2026/de364d6296b441cca1a353519c7f3075` under VisualStudioUI.
- Final VS2022 matching run: **2 passed**, no failures/skips, including the same
  fresh-IDE reopen assertions. TRX run `aa4aeb501f5a46e4963dd792be7f56da`; fixtures
  `ProjectReferenceManagement/2022/610e1055ea554d3db212c95034c03b93` and
  `LibraryPackages/2022/ecf97e65561b4bd9b0cd66c79d40783d` under VisualStudioUI.

- Full SDK net472 suite with ALARMLIST_TEST_MSBUILD_PATH selecting VS2022 desktop
  MSBuild: **55 passed**, no failures/skips. Previous environment value restored.
  TRX: `Milestone9/Iteration1/SDKDesktop/...21_28_10_net472.trx`.
- Reviewed the implementation, plan links and staged diff; `git diff --cached
  --check` passed. Only iteration-1 source/tests/docs are included.

Native reference/restore feasibility has passed on both hosts. Both full SDK
runtime suites and focused schema/composition checks passed. Iteration 1 is complete.
Library artifacts, dependency build/evaluation, package-reopen/restore recovery
and integrated acceptance remain assigned to later iterations. The native gate
does not by itself establish these behaviors.

Next: iteration 2, compiler-owned deterministic artifacts, graph/scoped evaluation
and coordinated analysis-5/analyzer-3 contracts.

### Iteration 2 — in progress

Iteration 1 committed as `0db95e4`. Initial compiler inspection confirmed that
ALMX's source serializer already writes local raw values and procedure directives;
library normalization reuses it rather than the resolved output writer. Added an
internal immutable `LibrarySource` projection with platform-independent relative
names, deterministic XML formatting, and a structural-key origin table. Origins
retain original line/column spans but use the portable source name and omit local
buffer versions. Capturing bound syntax retains ReferenceName/local overrides,
not bound object references or inherited assignments.

Validation: `dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj
--no-restore --filter FullyQualifiedName~LibrarySourceTests --verbosity minimal
--logger trx --results-directory artifacts/log/Debug/Milestone9/Iteration2/SourceProjection`:
**16 passed on each framework**, no failures/skips. The existing net8.0 CA1416
warning in shared TestHelper remains. Tests cover formatting/path independence,
detachment from mutable syntax, expressions, explicit-empty versus unassigned
properties, inheritance/Clear round trips and portable field/group/entry origins.

This is an internal projection, not a public artifact API or a successful library
build. Artifact envelope/reader validation, dependency graph, resources/lookups,
scoped evaluation/deferred values, and coordinated contracts remain to implement
and validate before committing this iteration. Normalization requires prior
semantic validation; it must never sanitize invalid source into a valid library.

Added the internal version-1 artifact envelope (`urn:alarmlist:library:1`) and
immutable contents: resolved package identity/dependencies, project defaults,
ordered source scopes with origin tables, resources and lookups. XML read/write
preserves explicit empty values and leaf text whitespace while discarding comments
and inter-element formatting. The reader rejects unsupported versions/namespaces,
unknown envelope content, missing/duplicate sections, invalid embedded root names,
invalid/duplicate origin keys and spans, absolute source names and DTDs.

Added a captured-artifact graph with iterative dependency-first traversal, stable
first-encounter order, diamond deduplication, missing dependency/cycle/version/
payload conflict checks, and ancestor queries for later resource override rules.
The graph never discovers files or chooses package versions; hosts supply resolved
identities and captured payloads. A 4,096-node chain test covers nonrecursive traversal.

Current validation: the `FullyQualifiedName~Libraries` selection on both frameworks
passes **36 tests per framework**, no failures/skips; TRX files under
`artifacts/log/Debug/Milestone9/Iteration2/Artifacts` at `21_38_26`. Initial checks
found a RESX constructor argument-order mistake and an invalid namespace-mutation
fixture; both were corrected before the passing run. No iteration-2 commit yet.
Envelope/graph exceptions still need conversion into normal compiler diagnostics;
embedded payload semantic validation, resource projection/precedence, provenance
rehydration, scope-aware evaluation and contract/bridge integration remain open.

Connected internal graph binding to fresh per-library syntax with restored portable
origin spans. Imported references are restricted to their owning dependency closure;
consumer/sibling definitions cannot satisfy them. Concrete imported alarms resolve
own alarm/file assignments, consumer project values, then owning library defaults.
Consumer-local derived alarms inherit declarations/raw expressions without library
assignments. Library source filename/name/extension remain portable, physical path
properties are unavailable, and project identity remains the final consumer's.

Validation: `dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj
--no-restore --filter 'FullyQualifiedName~Libraries|FullyQualifiedName~Evaluation'
--verbosity minimal --logger trx --results-directory
artifacts/log/Debug/Milestone9/Iteration2/Scopes`: **98 passed per framework**,
no failures/skips. Full compiler regression with the filter omitted and results
directory `artifacts/log/Debug/Milestone9/Iteration2/CompilerRegression`:
**556 passed on net8.0, 554 on net472**, no failures/skips. New tests cover explicit
empty assignments at every level, dependency inheritance, global duplicate alarms,
fresh mutable syntax per compilation, portable diagnostic provenance and the scope
boundaries above. These checks validate the internal pipeline only; public library
inputs/production, resource precedence, deferred library validation and coordinated
contract/bridge integration remain incomplete. Iteration 2 remains uncommitted.

Resource catalogues now merge captured library resources by key/culture and graph
ownership before normal culture fallback. Consumers override dependency values;
ancestors override their dependencies; unresolved sibling maxima are ambiguous.
Same-owner duplicates and malformed resources remain errors even when overridden.
All entries remain available internally for provenance, with portable library
resource paths. A root graph on the merged syntax also retains resource-only
dependencies without relying on the presence of ALMX files.

Validation: the Libraries/Localization selection passed **197 tests per framework**
under `artifacts/log/Debug/Milestone9/Iteration2/Resources`. After adding two further
regressions for a common ancestor resolving sibling conflicts and malformed
overridden input, the full compiler command above, with results directory
`artifacts/log/Debug/Milestone9/Iteration2/ResourceRegression`, passed **568 tests on
net8.0 and 566 on net472**, no failures/skips (TRX at `21_44_50`/`21_44_51`).
The initial new test build had an ambiguous CompilationContext constructor call;
explicit argument names fixed it. The existing shared TestHelper CA1416 warning
remains. Lookup precedence, public library production/inputs, original RESX span
mapping, detached analysis provenance and coordinated contracts remain pending.

Added internal lookup graph composition before catalogue evaluation. Overrides
replace whole paths, individual entries within merged lists, and template/argument
bindings. Retained entries keep their portable library origin. Effective sibling
conflicts flow through normal catalogue ambiguity diagnostics; hidden same-owner
duplicates still fail. Every captured document is schema-validated before selection.
Lookup-only dependencies participate in actual compilation through the root graph.

Validation: full compiler regression passed **575 net8.0 / 573 net472** tests, no
failures/skips, under `Milestone9/Iteration2/LookupRegression` (`21_46_22`/`21_46_23`).
After adding an actual list-expression compilation test with consumer overrides,
`--filter FullyQualifiedName~LibraryLookupTests` passed **8 per framework** under
`Milestone9/Iteration2/LookupCompilation` (`21_46_40`/`21_46_41`). An initial duplicate
fixture put repeated definitions in one document and therefore reached XSD rejection
before cross-file catalogue validation; changed it to two captured files to exercise
the intended hidden-duplicate rule. No product fix was needed for that test failure.
Public library validation/production and contract integration are still pending;
the merge does not yet expose shadowed lookup provenance through detached analysis.

Added public immutable `LibraryIdentity`, `LibraryTextSnapshot` and
`LibraryReferenceSet`, attached through `CompilationContext.WithLibraries`.
Diagnostic/analyzer context copies preserve references. Compile entry points and
snapshot analysis now include the captured graph without opening artifact paths.
ALM0028 reports malformed envelopes and graph failures as mandatory compiler errors;
NoWarn cannot suppress them. Local analysis source/property indexing accounts for
prepended library files. Reference-only consumers can emit concrete library alarms,
and final compilation still rejects unassigned consumer values.

Validation: full compiler regression before the new public-input tests passed
**576 net8.0 / 574 net472**, no failures/skips, under
`Milestone9/Iteration2/CapturedInputs` (`21_47_52`/`21_47_53`). The subsequent
`--filter FullyQualifiedName~LibraryInputTests` run passed **6 per framework** under
`Milestone9/Iteration2/PublicInputs` (`21_48_28`). These cover captured input/no-I/O,
local snapshot identity/property preservation, imported concrete output, final
strictness, mandatory malformed/missing dependency diagnostics and immutable copies.
The public production/validation API remains pending, as do analysis-5/analyzer-3
and bridge integration. Existing analysis results do not yet contain complete
library/source/resource provenance; this is not a supported iteration boundary.

Added evaluator support for deferred missing parameters/context/translations/lookups,
retaining separate diagnostic evidence and unavailable evaluated values. Structural,
syntax, reference and field-cycle errors remain failures. `AlarmCompiler.CreateLibrary`
now validates captured portable source/resource/lookup names, binds dependencies,
checks keyword plus supplied resource cultures, runs the owning context's analyzers,
and only then serializes owned definitions and direct dependency identities.
`LibraryBuildResult` separates validation diagnostics from deferred diagnostics and
exposes no artifact on failure. Own file identity matches portable imported identity;
the producing identity cannot already occur in the dependency closure.

Validation: deferred evaluator selection passed **11 per framework**; initial
production round-trip selection passed **7 per framework**. After portable identity
and self-dependency regressions, full compiler tests passed **602 net8.0 / 600 net472**,
no failures/skips, under `Milestone9/Iteration2/ProductionRegression`
(`21_51_27`/`21_51_28`). Tests cover deterministic production, deferred-to-strict
consumer evaluation, invalid source rejection before normalization, validation of
provided translations, transitive artifacts without embedded dependency definitions,
and source/identity behavior. Existing TestHelper CA1416 warning remains.
Production hardening (including complete portable resource provenance and analyzer
ownership evidence), detached analysis-5/analyzer-3 data and coordinated bridge work
remain required before iteration 2 can be committed.

Added detached analysis library identities/dependencies and owned source definitions.
Alarm origins and inherited reference-chain origins now identify their library;
consumer-local origins have no library owner. Each source exposes portable identity,
file-scope properties and a structural-key mapping from original spans to generated
definition spans. This enables later host navigation without interpreting compiler
syntax or pretending normalized definition positions are original source positions.
Library resource/lookup file identities are grouped under their owning library.
File-scope well-known properties now use the same portable identity as alarm scope.

Validation: Libraries selection passed **98 per framework**; full compiler regression
after origin/reference-chain/source-map assertions passed **602 net8.0 / 600 net472**,
no failures/skips, under `Milestone9/Iteration2/AnalysisRegression` (`21_53_06`).
Resource/lookup identity assertions subsequently passed in **7 public-input tests per
framework** under `Milestone9/Iteration2/LibraryIdentityLists` (`21_53_43`/`21_53_44`),
including a library with no ALMX sources and separation from consumer-owned inputs.
Contract versions and the VS bridge have not yet transitioned; these additive
compiler changes remain in the unfinished iteration and must not be committed alone.

Extended the VS host's serializable DTOs and reflection adapter with library
identity/dependencies, definitions, original/generated span maps and owned resource/
lookup identities. Added captured library reference inputs to AnalysisContext;
saved-input copies preserve them and context matching compares root identity/order
and artifact path/version/content. The worker constructs compiler reference inputs
inside its isolated domain and copies only host-owned results back.

Validation: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
--no-restore --filter FullyQualifiedName~SdkAnalysisBridgeTests --verbosity minimal
--logger trx --results-directory artifacts/log/Debug/Milestone9/Iteration2/BridgeLibraries`:
**40 passed on net472**, no failures/skips (`21_55_47`). New coverage reads library
input, compiles a local derived alarm, unloads the compiler domain, then verifies
ownership, generated definition navigation spans and immutable result collections.
The host still has no Alarmlist.Core assembly loaded. Context matching also detects
changed payload text at an unchanged path/version. A preceding run of the existing
38 bridge tests passed before the two added tests.
Contract version changes, analyzer ownership data and package-consumer updates remain
pending; no iteration-2 commit has been made.

Advanced compiler analysis to contract 5 and analyzer inputs to contract 3, with
matching bridge validation/messages, sample/lifecycle analyzers, test fixtures and
current usage documentation. Analyzer context now includes detached dependency
library definitions; alarm origins carry ownership. Historical milestone evidence
retains its original contract versions. The package-consumer fixture now creates
a deferred library using only the restored compiler, consumes it with project
parameters and verifies detached library/alarm ownership.

Validation:
- Full compiler regression: **603 net8.0 / 601 net472 passed**, no failures/skips,
  `Milestone9/Iteration2/Contract5Compiler` (`21_57_05`/`21_57_06`). Subsequently
  extended rejection cases to cover analyzer contract 2 explicitly; rerun pending.
- Bridge suite: initial **35 passed / 6 failed** because two dynamically compiled
  analyzer fixture sources still advertised contract 2. Updated those fixtures;
  final **41 passed**, no failures/skips, `Contract5Bridge` (`21_57_54`), including
  explicit rejection of analysis contract 4 alongside earlier versions.
- Packaged compiler contract test: **1 passed per framework** with dotnet MSBuild,
  `Contract5Package` (`21_58_27`); **1 passed on net472 with VS2022 desktop MSBuild**,
  `Contract5DesktopPackage` (`21_59_09`). Restored the prior environment override.
  Restricted desktop attempts returned without test output and are not counted;
  the recorded desktop pass used an approved unrestricted execution.
Full downstream regressions, remaining production/provenance hardening and the
iteration acceptance audit remain pending. Iteration 2 is still uncommitted.

Added immutable structural XML origin maps to normalized library resources/lookups.
Artifact read/write retains original element/attribute spans, checks complete key
coverage and rejects duplicate/unknown/missing keys or invalid spans. Snapshot
readers remap locations through these captured maps; imported positions use portable
library identities and no editor buffer version. Ordinary saved snapshot behavior
is unchanged. Map handling never reads source paths or external XML entities.

Validation: full compiler suite **608 net8.0 / 606 net472 passed**, no failures/skips,
under `Milestone9/Iteration2/XmlOriginRegression` (`22_01_07`/`22_01_08`). This includes
the additional analyzer-contract-2 rejection case and new resource/list original
line-number/version/path assertions through public production and analysis. The
initial focused run found that a formatting test compared complete artifacts while
ignoring their now intentionally different original spans. It now compares normalized
definitions, asserts distinct provenance and verifies stable artifact read/write.
Resource header/schema projection and generated resource navigation data still need
review before the complete provenance requirement can be accepted.

Library production now projects validated RESX into string data, dropping standard
resheader/schema/type metadata and retaining values, comments and original spans.
Invalid non-string resources still fail before projection. Focused library tests:
**105 per framework passed**, no failures/skips, `ResourceProjection`
(`22_02_22`/`22_02_23`). Full SDK suite at this state: **55 per framework passed**
with dotnet MSBuild, no failures/skips, `SDKRegression` (`22_02_51`).

An API review identified that requiring portable input paths alone prevents physical
EditorConfig matching. Added captured `portablePaths` to CreateLibrary: validation
and owning analyzer context retain physical input/project paths, while source,
resource and lookup artifact names are mapped independently. Full compiler suite
with this mapping passed **611 net8.0 / 609 net472**, no failures/skips, under
`PhysicalSourceValidation` (`22_04_42`/`22_04_43`). A subsequent defensive dictionary
copy and test mutation during analyzer execution await the next focused rerun.
The package fixture's reflected CreateLibrary call now supplies the fifth argument;
it also needs rerun after the signature change. Updated the SDK usage documentation.

Full EditorRegression run was started after SDK completion and remains in progress;
its outcome is not yet acceptance evidence. Generated resource/lookup navigation
definitions and the remaining iteration acceptance audit are still open.

The full EditorRegression run completed: **487 passed on net472**, no failures/skips,
TRX `22_04_54`. Added detached resource/lookup definition documents and original-to-
generated span maps to analysis libraries and the serializable host DTOs/adapter.
After domain unload, the host retains document kind/culture, text and both locations;
it does not need to parse compiler XML to determine navigation positions.

Validation after these additions:
- Libraries plus AlarmAnalyzerTests: **117 per framework passed**, no failures/skips,
  `NavigationMaps` (`22_07_25`/`22_07_26`). Includes the physical-path EditorConfig
  check and mutation of the caller's portable-name dictionary during analysis,
  confirming the defensive copy preserves the originally captured mapping.
- Bridge suite: **41 passed**, no failures/skips, `DocumentBridge` (`22_07_58`),
  including resource/lookup definition data after unload and immutable collections.
  An initial build caught a missing closing parenthesis in the new reflection copy;
  corrected before the passing run.
- Updated packaged CreateLibrary signature/round trip: **1 passed per framework**
  with dotnet MSBuild, `MappedProductionPackage` (`22_08_40`).

Next iteration-2 acceptance audit: review duplicate portable input identities and
library-project assignment provenance, check production/consumption failure paths,
then run remaining MSBuild/VSIX downstream regressions and desktop package checks.
The full eight-iteration goal remains unchanged; no iteration-2 commit yet.

Acceptance follow-up (2026-10-14): verified the completed OwnershipAudit TRX
results: **614 net8.0 / 612 net472 passed**, no failures/skips (`22_10_05` /
`22_10_06`, 2026-10-13). Production and artifact reading now reject duplicate
portable input names, including empty source definitions. Property assignment
origins identify their owning library; a consumer project override retains its
consumer ownership. The corresponding OwnershipBridge run completed with
**41 net472 passed**, no failures/skips (`2026-10-13_22_10_23`), including detached
library-project default ownership after AppDomain unload.

Downstream regression commands, all using `dotnet test <project> --no-restore
--verbosity minimal --logger trx --results-directory <directory>`:
- `src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj`:
  **68 passed per framework**, no failures/skips; results in
  `Milestone9/Iteration2/MSBuildUnit` (`2026-10-14_22_35_45` / `22_35_46`).
- `src/MSBuild/AlarmList.MSBuild.IntegrationTests/AlarmList.MSBuild.IntegrationTests.csproj`:
  **4 passed per framework**, no failures/skips; `MSBuildIntegration`
  (`2026-10-14_22_36_00` / `22_36_01`).
- `src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj`:
  **12 passed per framework**, no failures/skips; `VSIXIntegration`
  (`2026-10-14_22_36_17` / `22_36_18`). These verify packaged VSIX/template
  consumers; they do not replace the later native library editor acceptance.

The existing CA1416 warning in shared TestHelper remains. A working-tree change
to `example/North/Alarms1.almx` is outside iteration 2 and must be preserved and
excluded from its commit. Its trailing whitespace is the only current tracked
`git diff --check` finding. Desktop SDK regression and final scope/diff review
remain pending; iteration 2 is not yet declared complete.

Extended the public CreateLibrary round-trip regression to inspect the consumer
analysis of a deferred parameter: its reference resolves, raw expression and
library ownership survive, the evaluated value is unavailable, and compiled XML
is suppressed until the consumer supplies the parameter. LibraryBuilderTests:
**11 passed per framework**, no failures/skips, `DeferredPublicPreview`
(`2026-10-14_22_37_50` / `22_37_51`). No production changes were needed.
The iteration-owned tracked diff passes `git diff --check` when the unrelated
example edit is excluded.

The final evaluator review found a missing validation path: imported project
parameters were checked through source-file ownership, which omitted a library
with no ALMX sources. Validation now includes every artifact in the captured
graph, retaining the existing source-owner checks. A public compile/analysis
regression supplies an invalid parameter in a source-less artifact and verifies
ALM0011, failed compilation and no preview XML. Full compiler suite after this fix:
**615 net8.0 / 613 net472 passed**, no failures/skips, `SourceLessValidation`
(`2026-10-14_22_39_31` / `22_39_32`). The already-running desktop SDK suite uses
the earlier packaged compiler; a rebuilt package-contract check must follow it.

### Iteration 2 — acceptance complete (2026-10-14)

Desktop SDK regression completed with **55 net472 passed**, no failures/skips,
`DesktopSDKRegression/2026-10-14_22_36_46`, using VS2022 MSBuild.exe through
ALARMLIST_TEST_MSBUILD_PATH (previous environment value restored). Rebuilt the
package after the source-less validation fix and reran
`--filter FullyQualifiedName~PackagedCompilerAnalyzesUnsavedSnapshotsWithOrigins`:
**1 net472 passed**, no failures/skips, `FinalDesktopContract/22_40_54`.

Iteration acceptance review:
- Deterministic artifacts, raw definitions/scopes, portable source and XML spans,
  malformed inputs and graph rejection: LibrarySourceTests, LibraryArtifactXmlTests,
  LibraryXmlOriginTests and LibraryGraphTests, in the full compiler results above.
- Concrete/abstract inheritance, parameter priority, explicit-empty assignments,
  consumer identity and resource/lookup precedence: LibraryCompilationTests,
  LibraryResourceTests and LibraryLookupTests.
- Public production, deferred values, strict final consumers and captured inputs:
  LibraryBuilderTests, LibraryDeferredValidationTests and LibraryInputTests.
- Analysis 5/analyzer 3 coordinated boundary, detached ownership/navigation and
  host isolation: compiler analyzer tests, full editor/bridge results and packaged
  analysis-contract consumers; legacy bridge contracts fail explicitly.
- Existing task, SDK and VSIX workflows: the recorded downstream regressions on
  both frameworks, including the complete desktop SDK run.

These results satisfy iteration 2's compiler and contract boundary. Library SDK
build/reference orchestration, packaging, saved project capture and native library
UI remain required iterations 3–8. The compiler's internal deferred validation
and public consumer analysis are verified here; the library-project editor mode
must be wired through the saved-project host work in iterations 5–6. No native UI,
performance or release gate is inferred from these unit/package results.

Iteration 2 committed as `93393b7` (64 files); the unrelated example edit remains
outside the commit.

### Iteration 3 — in progress

Implementation sequence within this reviewable iteration:
1. Extend the existing output plan/build/get-output/clean task boundary for
   OutputType=Library and package identity, preserving transactional installation,
   ownership manifests, cancellation, diagnostics and input collision protection.
   Capture portable names from project-relative/linked input metadata; compile
   captured library references without introducing compiler filesystem access.
2. Add SDK ProjectReference orchestration with configuration/platform propagation,
   reference dependency order and no-build output discovery. Preserve
   BuildProjectReferences=false and design-time guards; allow reference-only
   applications. Carry transitive artifact identity/path metadata to the task.
3. Validate task failure/rollback behavior and isolated packaged consumers on both
   runtimes: library-to-application and transitive projects, conditional references,
   solution mappings, Build/Clean/Rebuild, no-build discovery and missing/stale
   dependency failures. Update SDK documentation alongside the implementation.

Initial source inspection confirms BuildOutputPlan, OutputManifest and
OutputTransaction already centralize the application output lifecycle. Extend
those paths rather than introducing a separate installation/cleanup mechanism.
The current SDK Compile target rejects empty local sources and has no project
reference pipeline; both must change before iteration 3 can pass. These are
implementation findings, not completed behavior. No iteration-3 checks run yet.

Initial implementation evidence (2026-10-14): the build task now produces a
library artifact through the existing transaction and manifest lifecycle.
BuildOutputPlan/GetTargetPath/Clean agree on the culture-independent output;
project-relative or Link names are exported while validation uses physical inputs.
The SDK exposes OutputType (default Application), PackageId (project name),
PackageVersion (1.0.0) and AlarmlistLibraryFileName (`$(PackageId).almlib`).
Library projects may have no ALMX sources. Reference-only application support and
reference capture/orchestration remain pending.

The first task regression run exposed the manifest's culture-directory-only
assumption (4 failures per framework). Manifest version 2 records output type;
reading version 1 remains supported. Added preflight protected-input checks to
Clean and project-file collision protection to Build. SDK Clean supplies evaluated
source/resource/lookup/analyzer inputs. Further dependency-input protection must
be added with reference capture.

Validation:
- Full MSBuild unit suite after library manifest support: 73 passed per framework,
  `Iteration3/LibraryManifest` (`22_43_44` / `22_43_45`).
- Full MSBuild unit suite after protected Clean: **74 passed per framework**, no
  failures/skips, `Iteration3/ProtectedLibraryClean` (`22_44_05` / `22_44_06`).
  Covers deferred values, failed-build preservation, resource-only/linked inputs,
  output-type transition, no-build discovery, cleanup and protected-input refusal.
- Packaged SDK `--filter FullyQualifiedName~BuildLifecycleTests`: **2 passed per
  framework** with dotnet MSBuild, no failures/skips, `SDKLibraryLifecycle`
  (`22_44_57`). Proves default application lifecycle and standalone library
  discovery/Build/Clean/Rebuild. Desktop MSBuild validation is still required.

Iteration 3 remains incomplete and uncommitted. Next: captured dependency inputs,
ProjectReference configuration/order/GetTargetPath behavior, failure and lifecycle
tests against transitive consumers, then required broader regressions.

Captured task consumption implemented: LibraryReferences carry PackageId,
PackageVersion and Direct metadata (false marks transitive-only inputs). The task
captures artifact text once with other saved inputs, passes LibraryReferenceSet
to both library production and application compilation, and protects artifact
paths from output/manifest collisions. Applications with references can have no
local ALMX files. Regression coverage checks a consumer-supplied value, missing
artifact preservation, output collisions and a two-library chain with repeated
transitive input and exactly-once concrete output.

Full MSBuild unit suite: **77 passed per framework**, no failures/skips,
`Iteration3/TransitiveTask` (`2026-10-14_22_46_46` / `22_46_47`). Earlier direct
consumption checks passed 76 per framework in `LibraryConsumption` (`22_46_05` /
`22_46_06`). SDK targets do not yet populate these reference inputs.

Before adding reference orchestration, consulted the upstream
[ProjectReference protocol](https://github.com/dotnet/msbuild/blob/main/documentation/ProjectReference-Protocol.md)
and installed MSBuild's AssignProjectConfiguration/ResolveProjectReferences
implementation. The repository's similarly named tools/Microsoft.Common.targets
only handles extension and Directory.Build.targets imports; it does not import
the managed reference pipeline. The Alarmlist integration therefore needs explicit
configuration assignment and cross-project calls. Planned implementation uses the
toolset's AssignProjectConfiguration task, separates reference Build from no-build
closure discovery, and respects the VS scheduler and BuildProjectReferences=false.
GetTargetPath must still return only this project's outputs; a separate closure
target will provide transitive identities/paths without building. This is a scoped
adaptation for data-library artifacts and the framework-independent project type.

Project-reference pipeline implemented in Alarmlist.ProjectReferences.targets:
AssignProjectConfiguration supplies per-reference configuration/platform; CLI
builds build dependencies, while IDE-scheduled and BuildProjectReferences=false
builds discover existing paths. GetTargetPath remains local-only; the no-build
GetAlarmlistReferenceClosure target carries direct/transitive identities and paths.
Reference-only applications feed those artifacts to the captured compiler context.
CLI Clean/Rebuild traverse dependencies, with captured paths protected during the
consumer's cleanup. Native editor analysis remains outside this iteration.

Packaged SDK evidence (dotnet MSBuild on both test frameworks):
- Initial transitive build/no-build/preservation scenario: **1 passed per framework**,
  `ProjectReferencePipeline` (`2026-10-14_22_48_37` / `22_48_38`).
- Clean/Rebuild, explicit Release reference configuration and conditional removal:
  **4 passed per framework**, `ProjectReferenceLifecycle`
  (`2026-10-15_19_22_16` / `19_22_17`).
- Extended with IDE-scheduler mode and solution configuration mapping:
  **6 passed per framework**, no failures/skips, `ReferenceConfiguration`
  (`2026-10-15_19_23_11` / `19_23_12`).

The DesktopReferences run is pending; it is not yet passing evidence. Additional
scope review and full SDK/downstream regression remain required before commit.
Both changes in example/North (Alarms1.almx and North.almproj) are unrelated working
tree edits and must remain outside the iteration commit.

DesktopReferences completed: **8 net472 passed**, no failures/skips
(`2026-10-15_19_25_14`), selecting LibraryProjectReferenceTests and
BuildLifecycleTests with installed VS2022 MSBuild.exe. The previous
ALARMLIST_TEST_MSBUILD_PATH value was restored. This verifies the packaged net472
task, transitive builds, no-build/IDE scheduling, configuration mapping and cleanup
with desktop MSBuild; it is not a native UI test. Full SDK regression follows.

Full SDK regression with the current working-tree examples completed with
**61 passed / 1 failed per framework**, no skips, `SDKRegression`
(`2026-10-15_19_26_00` / `19_26_01`). The only failure is the North example analyzer
scenario: its unrelated local ProjectReference points to South, which is an
Application, and library consumption correctly refuses it. The example also has
an unrelated ALMX edit. Neither source file was changed to make the test pass.

For baseline regression, rebuilt the current SDK/test assemblies and started the
complete suite with the two North files restored from HEAD **only in generated
testassets copies**. The command backs up/restores those generated copies in a
finally block and checks that working-tree example hashes remain unchanged.
`--no-build` deliberately retains the freshly built package and controlled copies.
Results under SDKCommittedExample are pending. The new tests additionally cover
design-time/no-build closure discovery and missing/application/cyclic project
references preserving the consumer output; their acceptance is also pending.

The first controlled-copy invocation exited 1 before producing any TRX, so it is
not counted as test evidence. Its finally block restored the generated copies.
Retried the same complete run outside the sandbox; both framework runners started
and remain in progress. No source-example edits or relaxed reference semantics
were used to bypass the initial North failure.

The controlled baseline run completed: **66 passed per framework**, no
failures/skips, `SDKCommittedExample/2026-10-15_19_29_26`. Generated example copies
were restored and hashes confirmed the working-tree example sources unchanged.
This covers the full SDK suite against the committed example baseline, not the
user-edited North playground. It includes the design-time/discovery and invalid
reference graph scenarios added above.

A final collision review added referenced project paths and imported MSBuild
files to protected inputs for Build/Clean. The build task also protects SourceProject
metadata on transitive artifacts. Full task unit suite after this change:
**78 passed per framework**, no failures/skips, `ProtectedBuildInputs`
(`2026-10-15_19_30_46` / `19_30_47`). Full task integration suite: **4 passed per
framework**, no failures/skips, `MSBuildIntegration/2026-10-15_19_31_45`.
The package reference/lifecycle selection is rerunning against this final task
and SDK change. The application-reference rejection test now first supplies its
required value and asserts the explicit OutputType=Library diagnostic.

FinalReferences passed **12 tests per framework**, no failures/skips
(`2026-10-15_19_32_29`), against the updated protected-input implementation.
VSIX/template package integration passed **12 per framework**, no failures/skips,
`VSIXIntegration` (`2026-10-15_19_34_07` / `19_34_08`). The final desktop selection
is still running. Repository guidance now documents library outputs, manifest
compatibility, no-build discovery, reference scheduling and input protection.

### Iteration 3 — acceptance complete (2026-10-15)

FinalDesktopReferences passed **12 net472 tests**, no failures/skips
(`2026-10-15_19_34_38`), using the already rebuilt package with VS2022 MSBuild.exe;
the original environment setting was restored.

Acceptance review: standalone library Build/GetTargetPath/Clean/Rebuild and
transactional failure preservation are covered by LibraryBuildTests and packaged
BuildLifecycleTests. LibraryProjectReferenceTests cover the transitive/reference-
only consumer, configuration and solution mappings, conditional removal, CLI and
IDE/no-build scheduling, no-build discovery, missing inputs, application targets,
cycles and output preservation. Full task suites and the SDK baseline regression
plus final scoped package checks cover existing application workflows on both
frameworks; desktop package checks verify the alternate task runtime. VSIX package
integration remains green. Diff review excludes the two unrelated North edits.

The current edited North playground is not a passing fixture: it references an
Application as a library. Baseline acceptance uses committed example copies as
described above. No user edits were reverted or included in this iteration.
NuGet artifact Restore/Pack/consumption, saved dependency analysis, native library
editor workflows, templates/examples and performance remain iterations 4–8.

Iteration 3 committed as `dea632e` (19 files); both unrelated North edits remain
outside the commit.

### Iteration 4 — complete

Implementation sequence within this reviewable iteration:
1. Resolve restored package artifacts from NuGet's selected dependency graph into
   the same captured LibraryReferenceSet used for project references. Preserve
   direct/transitive identity, normalized versions, restore failure diagnostics,
   input protection and project/package equivalence. Do not glob arbitrary caches.
2. Add Pack for library projects with the artifact under `alarmlist/` and explicit
   package dependencies for project and package references. Validate that every
   declared dependency can be represented before creating a package; do not bundle
   dependencies, analyzers, EditorConfig or physical source paths.
3. Validate isolated fresh feeds/caches, warm offline use, transitive mixed graphs,
   package updates/removal/conflicts, failed-output preservation and equivalence to
   project references. Update usage documentation and relevant instruction contracts.

The native restore foundation from iteration 1 and project artifact closure from
iteration 3 are prerequisites already implemented. NuGet artifact resolution and
Pack are not yet implemented or validated; the full milestone remains active.

Initial package-resolution implementation (2026-10-15): added the public
NuGet.ProjectModel 7.9.0 dependency to the task project and package catalogue.
The [published package](https://www.nuget.org/packages/NuGet.ProjectModel/7.9.0)
supports net472/net8.0. Consulted NuGet's
[LockFileFormat implementation](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.ProjectModel/LockFile/LockFileFormat.cs)
instead of introducing a private JSON/restore resolver. Compiler dependencies and
the VS host remain unchanged. The SDK's existing runtime dependency packaging must
be verified with these new assemblies in subsequent package/desktop checks.

AlarmlistResolvePackageLibrariesTask selects the restored framework graph,
validates direct requested versions and restore errors, traverses only reachable
packages, and locates one declared `alarmlist/*.almlib` per library package from
the assets file's package folders/file list. It emits normalized identities and
direct/transitive metadata for the existing capture pipeline. Non-library package
aggregators can expose first-reachable libraries as roots. Missing files, malformed
paths and ambiguous artifact lists fail without publishing partial references.
No arbitrary package-cache search or compiler filesystem access was added.

Restore initially failed under the sandbox with NU1301/DNS failure; retry outside
the sandbox succeeded and the actual restored net472/net8.0 assemblies and assets
entries were verified. Both task frameworks build successfully. The first four
resolver tests failed while constructing fixtures because NuGet's default file
collections are immutable; explicitly supplied mutable fixture lists fixed setup.
Full task unit suite then passed **82 per framework**, no failures/skips,
`Iteration4/PackageResolutionFixtures` (`2026-10-15_19_40_03` / `19_40_04`). Tests
cover selected graph/normalized identities/root metadata, missing artifact,
changed requested version and escaping package paths. The existing CA1416 test
helper warning remains.

SDK targets now resolve package libraries for Build and reference-closure discovery,
including mixed project/package graphs. Package-only consumers are accepted and
Clean protects resolved package artifacts and the assets file. Library production
and no-build output discovery normalize NuGet versions consistently (`1.0` becomes
`1.0.0`); reference closures retain those output identities.

Validation on 2026-10-15:

- Full task unit suite: **83 per framework**, no failures/skips, under
  `Iteration4/NormalizedLibraryIdentity` (`19_44_34` / `19_44_35`). Includes
  build/discovery version normalization. Existing CA1416 warning remains.
- PackageRestoreTests and LibraryProjectReferenceTests: **12 per framework**,
  no failures/skips, `Iteration4/PackageConsumer` (`19_45_00`). The new fixture
  builds real Base/Owner artifacts, creates local packages with an exact transitive
  dependency, switches the consumer from ProjectReference to PackageReference,
  restores and compares final XML byte-for-byte, then builds again without restore.
- PackageRestoreTests using VS2022 desktop MSBuild: **2 net472**, no failures/skips,
  `Iteration4/DesktopPackageConsumer` (`19_45_53`). This exercises the packaged
  task's NuGet dependency loading in desktop MSBuild as well as dotnet MSBuild.
- SDK packaging initially emitted NU5118 for duplicate net8.0
  `System.Security.Cryptography.Pkcs.dll` runtime/reference entries. Preserving
  `ReferenceCopyLocalPaths.DestinationSubDirectory` in package paths fixes the
  collision and retains the Windows runtime asset alongside the portable asset.
  Rebuilt without NU5118; package-content assertions verify NuGet assemblies in
  both task directories and the Windows-specific Pkcs path.
- OfflinePackageConsumer: **3 per framework**, no failures/skips (`19_47_15` /
  `19_47_16`). Forced restore/build against an empty local feed succeeds from the
  warm cache. Removing a cached artifact fails Build and preserves previous output;
  removing the package reference and restoring leaves only the new local alarm,
  despite cached library bytes remaining.
- PackageUpdateConflict: **3 per framework**, no failures/skips (`19_48_25` /
  `19_48_26`). Real library artifacts rebuilt as version `2.0` are packaged as
  normalized `2.0.0`; updating Owner and its exact Base dependency changes the
  consuming XML. A conflicting direct Base version fails restore/build and a
  subsequent no-restore Build, preserving previous XML in both cases.
- DesktopPackageLifecycle: **4 net472**, no failures/skips (`19_49_01`), covering
  the complete package lifecycle scenarios and SDK package contents through
  VS2022 desktop MSBuild. SDK README now documents implemented package consumption
  and distinguishes project path discovery from restored package requirements.

Consulted the installed NuGet pack targets and the official
[Pack/Nuspec workflow documentation](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets).
Implemented a focused Pack target using NuGet.Packaging 7.9.0 PackageBuilder,
declared explicitly in the task project and central version catalogue. The existing
native SDK has no managed target-framework compilation/pack pipeline; its thin task
feeds the validated library output and captured reference identities to NuGet's
package writer instead of importing the managed pack target dependency chain.
Compiler artifact semantics remain compiler-owned.

Pack builds/validates first, emits one artifact under `alarmlist/`, and represents
direct library references as exact NuGet dependencies. It rejects applications,
self/conflicting identities, graphs with IsPackable=false projects and destinations
that collide with source inputs. Staging and atomic installation preserve existing
packages on validation/writer failures. PackageOutputPath, Authors, Description and
AlarmlistPackages are documented. Pack does not publish, embed dependencies or ship
analyzers/configuration. Current explicit limitation: NoBuild=true is rejected;
packages are not compiled outputs owned by Clean.

Validation on 2026-10-15:

- Task build succeeds for net472/net8.0 with zero warnings/errors.
- Initial LibraryPack: **5 per framework**, no failures/skips (`19_51_56`). The
  real consumer equivalence/offline test now creates Base/Owner packages through
  the SDK Pack target and verifies the exact Base dependency in Owner's nuspec.
- LibraryPackValidation: **6 per framework**, no failures/skips (`19_52_50` /
  `19_52_51`), including non-packable dependency rejection and prior package
  preservation. NuGet.Packaging restore succeeded.
- MixedLibraryPack: **5 per framework**, no failures/skips (`19_53_30`). A project
  consumer referencing Owner with a Base PackageReference produces the same XML
  after packing Owner and switching to an entirely package-based graph. An explicit
  source at the destination .nupkg path is preserved and Pack rejects the collision.
- DesktopLibraryPack: **8 net472**, no failures/skips (`19_54_01`), using VS2022
  desktop MSBuild for all Pack and package-consumption scenarios above.

Final iteration-4 validation and acceptance audit (2026-10-15):

- Full task suite: **83 per framework**, no failures/skips, `FinalTasks`
  (`19_55_54` / `19_55_55`); only the existing CA1416 test-helper warning.
- Task integration: **4 per framework**, no failures/skips, `TaskIntegration`
  (`19_56_28`). VSIX/template package integration: **12 per framework**, no
  failures/skips, `VSIXIntegration` (`19_56_33`).
- Full SDK suite: **73 per framework**, no failures/skips, `FullSDK` (`19_55_34`).
  As in iteration 3, only generated North example test copies temporarily used
  the committed baseline; they were restored in finally and the original user
  source hashes were verified unchanged. No persistent example source was edited.
- Added XLIFF reference discovery without Build and protected library artifact
  and assets paths. A deliberate export/library path collision is rejected while
  retaining the artifact bytes. The initial new fixture duplicated an implicitly
  included RESX and failed before export; changing Include to Update fixed setup.
  `LibraryExchangeFixed`: **1 additional test per framework**, no failures/skips
  (`19_58_52` / `19_58_53`). This is additional to the full 73-test run, not a
  claimed 74-test full run. Production code did not change between those runs.
  `DesktopLibraryExchange`: **1 net472**, no failures/skips (`19_59_29`), verifies
  the same collision with VS2022 desktop MSBuild.
- Reviewed tracked/new implementation, package layout, target wiring and docs;
  `git diff --check -- AGENTS.md Directory.Packages.props src` passes. Unrelated
  North source edits (including their pre-existing whitespace) are preserved.

Iteration 4's criteria are met: fresh isolated package restore, warm-cache offline
restore/build, transitive and mixed graphs, project/package output equivalence,
update/removal/conflict behavior, exact Pack dependency mapping, rejection of
non-packable references, and previous-output/input preservation have executable
evidence above. Documentation records Pack's required Build and package ownership.
Native UI acceptance remains covered by iteration 1's foundation evidence and the
later integrated scenarios; saved dependency analysis/designer behavior is still
iterations 5–6. No release/publication is authorized or performed.

### Iteration 5 — complete

Initial inspection after iteration-4 commit `f93894f`: ProjectAnalysisService
captures local buffers plus saved resource/lookup/configuration/analyzer inputs,
then recaptures them before publication. AnalysisContext already transports captured
library artifacts through the isolated contract-5 bridge, but CPS snapshots and
input watchers do not yet discover dependencies. No saved dependency graph is
currently connected to the live service.

Implementation sequence within this iteration:

1. Capture detached evaluated reference metadata and evaluate saved projects in
   private MSBuild collections without executing targets or reusing CPS draft XML.
2. Capture transitive saved project/package inputs, configuration/portable paths,
   imports and membership watch roots. Preserve solution/configuration assignment
   and reject missing, conflicting or cyclic graphs with recoverable state.
3. Create project library artifacts in memory through the isolated SDK worker,
   preserving deferred library validation and keeping compiler/NuGet implementation
   assemblies outside the VS host domain. Capture restored package artifacts.
4. Integrate dependency capture, immediate invalidation, cancellation, input
   recapture and navigation validation into service/watcher lifecycle. Exercise
   saved changes, drafts, unloaded projects, deletion/recovery and races.

First implementation: AnalysisProject now retains ordered ProjectReference and
PackageReference items with copied read-only evaluated metadata. Project reference
paths are absolute; package IDs remain identifiers. SavedProjectEvaluation creates
a fresh disposable ProjectCollection, reads saved project/import files, returns a
detached AnalysisProject and import paths, and unloads the collection. It does not
execute targets and does not consult open documents. This helper is not yet wired
into service capture; it is a prerequisite, not completed dependency analysis.

Validation on 2026-10-15:

- SavedEvaluation: **3 net472 tests**, no failures/skips (`20_02_34`). Proves
  private evaluation ignores unsaved changes in another loaded collection, preserves
  reference metadata, executes no InitialTargets, writes no output, recaptures
  changed imports/conditional glob membership/duplicates, rejects deleted imports,
  recovers after recreation, and honors cancellation before evaluation.
- SnapshotRegression: **22 net472 tests**, no failures/skips (`20_02_58`), covering
  existing source/resource/lookup input capture. Tests use plain project fixtures;
  SDK-resolver/native IDE behavior is not yet acceptance evidence for this helper.

Transitive discovery, in-memory library production, watch/publication integration
and remaining iteration-5 gates are pending. No iteration-5 commit yet.

SavedProjectGraph now traverses evaluated ProjectReference items using an explicit
stack, preserving encounter order and producing dependency-first saved evaluations.
It deduplicates identical path/global-property contexts, keeps differing contexts
separate, excludes ReferenceOutputAssembly=false edges, detects active-path cycles,
and honors cancellation between evaluations. Snapshots copy evaluation global
properties. Reference evaluation applies explicit SetConfiguration/SetPlatform and
AdditionalProperties and removes SDK output-identity/framework properties plus
GlobalPropertiesToRemove. All project collections are disposed after evaluation.
This traversal is not yet the complete library graph: solution configuration mapping,
OutputType/identity validation, restored packages and captured source bytes remain
to be added before service integration. In particular, no claim of native saved
dependency refresh follows from these helper tests.

`SavedGraph` passed **6 net472 tests**, no failures/skips (`2026-10-15_20_05_01`),
combining the first three evaluation tests with diamond ordering/deduplication,
cycle rejection/recovery, distinct explicit configurations, output-property removal,
excluded missing references and missing-file recovery. Fixture InitialTargets would
fail if targets were executed; no build outputs are created. Iteration 5 remains
in progress and uncommitted.

SavedProjectEvaluation now captures OutputType, package identity, assets/framework
selection and portable names for Compile/AlarmlistResource/AlarmlistLookup items.
Portable names follow the build task's project-relative/Link projection. A new
serializable CapturedLibraryProject reads only saved XML bytes, retains source
order/duplicates, captures resources/lookups and the library's own diagnostic
configuration/analyzer images, and copies the portable-name map. It rejects
Application references and missing package identity before reading inputs. This
data is intended for that library's isolated compiler; its configuration/analyzers
must not be copied into the consumer context or serialized into exported artifacts.

CapturedLibraryProject.Matches compares saved contents and context rather than
timestamps alone, preparing for pre-publication graph recapture. Raw package
versions are retained at this boundary; normalization must run in the isolated SDK
side with NuGet's version rules before artifact creation. The current worker has
shared input conversion and analyzer-loading logic embedded in AnalyzeCore; the
next step is to reuse it for CreateLibrary rather than duplicate compiler behavior
or load NuGet/compiler assemblies into the VS host.

`SavedLibraryInputs` passed **12 net472 tests**, no failures/skips
(`2026-10-15_20_07_23`): the six evaluation/graph tests plus saved input order,
explicit-empty culture, portable Link names, immutable captured sources, unchanged-
timestamp source/resource/lookup/EditorConfig changes, non-library rejection and
missing-input failures. No build outputs were produced by capture. SDK artifact
creation, package resolution, solution mapping and live graph integration remain
pending; these tests do not establish end-to-end dependency analysis.

The isolated bridge now exposes ISdkLibraryBridge.CreateLibrary. SdkAnalysisWorker
shares captured snapshot/context conversion and analyzer-loader lifetime between
Analyze and CreateLibrary, invokes the SDK's public five-argument CreateLibrary
API, and returns a detached LibraryCreationResponse with normalized identity,
artifact text, hard diagnostic messages and separate deferred diagnostic messages.
NuGet.Versioning is resolved from the selected SDK beside the compiler only in
the worker domain. Cancellation uses the existing cooperative host probe; no
compiler objects, reflection objects or SDK exceptions cross the boundary. Failed
creation returns no artifact. Structured library diagnostic locations remain a
follow-up for dependency/editor diagnostics; current messages retain ID and text.

`LibraryCreationBridge` passed **42 net472 bridge tests**, no failures/skips
(`2026-10-15_20_10_09`), including existing configuration/analyzer/isolation coverage.
The new test captures a library, corrupts the source on disk afterward, creates a
valid in-memory artifact from the captured contents, verifies deferred consumer
values and NuGet normalization, and consumes it successfully with a consumer
property. Fresh capture of the corrupted source fails without an artifact. The
returned artifact survives domain unload and contains no physical input directory;
neither Alarmlist.Core nor NuGet.Versioning enters the host domain.
`LibraryCreationCancellation` passed **1 net472 test** (`20_10_58`) after adding
the service-facing interface and pre-cancelled-call coverage. No library file is
written. The live service still does not invoke this operation; graph edges,
solution mapping, package capture, watchers and publication revalidation remain.

SavedProjectGraph now retains root edges and direct child evaluations as well as
dependency-first order. CapturedProjectLibraries connects graph capture to saved
input capture and ISdkLibraryBridge creation. Each library uses its own evaluated
compiler path in a separately disposed bridge; no fallback SDK is substituted.
Only its direct dependencies are roots in the creation context, with their complete
artifact closure available. The consumer receives direct roots plus all generated
artifacts. Shared project/configuration nodes are created once. Unavailable SDKs
and failed library creation stop capture without returning a partial graph.

`TransitiveLibraryCreation` passed **4 net472 tests**, no failures/skips
(`2026-10-15_20_12_55`). The new real packaged-SDK scenario evaluates a saved diamond,
creates Base/Left/Right artifacts in memory, consumes all three alarms with a
consumer property, verifies the two direct roots and three distinct libraries,
rejects a newly malformed saved dependency, then recaptures changed saved content
after recovery. InitialTargets are never executed and no output files/directories
are created. The other three tests retain graph order/configuration/cycle coverage.

This coordinator remains unconnected to ProjectAnalysisService. It currently
rejects PackageReference-containing graphs explicitly, pending isolated package
capture, rather than silently omitting packages. Solution mappings, full captured
graph equality, watcher membership/recovery and live publication remain required
iteration-5 work. These helper tests do not claim native IDE completion.

Extracted LibraryPackageResolver from the build task so build and isolated IDE
capture share NuGet graph/version/path rules. Its API takes captured assets text,
the assets identity, framework and requested ID/version arrays, and returns plain
package identities/paths/direct flags without MSBuild task types. The task remains
a thin file-capture/logging adapter. NuGet parser errors now retain their diagnostic
detail instead of only the generic invalid-assets message.

ISdkLibraryBridge.ResolvePackages invokes this API only in the SDK AppDomain and
copies results into serializable host-owned DTOs. The initial isolation test failed:
NuGet's parser swallowed a dependency load failure into an invalid lock-file result.
After exposing its parser diagnostics, the cause was an older Unsafe assembly
reference versus the SDK's selected Unsafe 6 image. A resolver scoped to package
resolution in the worker unifies only System.Runtime.CompilerServices.Unsafe to
the selected SDK image after version/culture/public-key checks. It is removed in
finally; the host resolver and VS assembly bindings are unchanged.

Validation on 2026-10-15:

- SharedPackageResolver: full task suite **83 per framework**, no failures/skips
  (`20_14_47`), before the additional parser-diagnostic detail.
- IsolatedPackageResolver initially **1 passed / 1 failed** (`20_15_53`);
  diagnostic repeats identified the assembly mismatch (`20_16_12`, `20_17_05`).
- IsolatedPackageResolverFixed: **2 net472**, no failures/skips (`20_17_50`),
  covering captured assets even when their path does not exist, normalized direct
  package identity, rejected version mismatch/missing artifact, detached results
  after domain unload, and absence of NuGet/AlarmList.MSBuild in the host. The
  transitive saved-project artifact scenario also remains green.
- ResolverPackageRegression: **3 per framework**, no failures/skips (`20_18_05`),
  exercising real packaged SDK restore/Pack consumption, offline use, removal,
  updates/conflicts and previous-output preservation after the shared resolver refactor.

Package capture now joins CapturedProjectLibraries: CPS/saved evaluations retain
ProjectAssetsFile and TargetFrameworkMoniker, each library resolves its own saved
package assets through its selected SDK, and package artifacts propagate through
project closures. Consumer package roots are resolved independently. Captures
retain assets text/path and artifact text for subsequent input validation. Identical
diamond inputs are collapsed; conflicting text is retained for compiler rejection.

Validation: `dotnet test
src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
--no-restore --filter 'FullyQualifiedName~PackageResolverUsesCapturedAssets|FullyQualifiedName~SavedDiamond'
--verbosity minimal --logger trx --results-directory
artifacts/log/Debug/Milestone9/Iteration5/PackageGraphCapture`: **2 net472 passed**,
no failures/skips (`20_22_41`). The package scenario now creates a valid library
artifact, consumes it through a saved intermediate project library, verifies final
consumer XML, and rejects a malformed saved package artifact. An initial test
compilation required the explicit null metadata constructor argument; rerun passed.
`git diff --check -- src` passed (line-ending conversion warnings only).

The live service now supplies captured library references to consumer analysis and
retains the capture on ProjectAnalysisState. Before publication and before actions
using ProjectAnalysisValidation, it re-evaluates the saved dependency graph and
compares graph edges, reference metadata, global properties, captured library
inputs, package assets text and artifact text. A mismatch schedules fresh analysis;
capture failures suppress the prior result. This validation does not create library
artifacts again or execute build targets.

Known dependency paths are observed as graph capture progresses, including missing
project paths, evaluated imports, sources, resources, lookups, analyzers, selected
compilers, EditorConfig candidates, assets and resolved artifacts. The existing
input-source watcher owns these subscriptions and disposes them with the project.
Dependency disk events invalidate the service; dependency document notifications
are excluded because only consumer inputs belong to the open-buffer observation
set. EditorConfig directories retain nonrecursive filtered watching.

Validation on 2026-10-15:

- DependencyRevalidation: **48 net472 passed**, no failures/skips (`20_24_16`),
  selection ProjectAnalysisServiceTests, PackageResolverUsesCapturedAssets,
  SavedDiamond and ProjectAnalysisValidation.
- LiveLibraryRevalidation: **1 passed** (`20_25_11`), real packaged SDK through
  the service, saved dependency changed during verification, old result withheld,
  fresh result published and consumer unsaved text preserved.
- SavedDependencyWatching: **71 passed** (`20_26_27`), adding all
  ProjectAnalysisInputTests and the live service case. The latter also saves a
  dependency after publication and verifies automatic refresh without Build.
- DependencyWatcherLifecycle: **24 passed** (`20_27_07`), final
  ProjectAnalysisInputTests plus LiveServiceRevalidatesSavedLibraries selection;
  includes dependency draft notifications ignored, saved deletion/recreation
  observed and open-document subscription released on disposal.

All runs used `dotnet test` on the VS unit-test project with `--no-restore`,
`--verbosity minimal --logger trx`, their named `--filter` selections, and results
under `artifacts/log/Debug/Milestone9/Iteration5/<run-name>`.
Saved reference configuration now invokes MSBuild's public
`AssignProjectConfiguration` metadata-only task with the same switches as
`Alarmlist.ProjectReferences.targets`. CPS/saved snapshots retain
CurrentSolutionConfigurationContents. Assignment precedes reference filtering and
preserves assigned/unassigned ordering, solution configuration/platform overrides,
disabled-reference defaults and explicit ReferenceOutputAssembly overrides. The
adapter's build-engine method throws if any project execution is attempted; no
new package dependency was needed (Tasks.Core is already supplied by the host SDK).
Reference consulted: [MSBuild assignment implementation](https://github.com/dotnet/msbuild/blob/main/src/Tasks/AssignProjectConfiguration.cs).

- SolutionConfiguration final focused run: **6 net472 passed**, no failures/skips
  (`20_29_43`), including transitive solution mappings, disabled project exclusion,
  explicit reference inclusion, saved diamond and live service behavior.
  Initial compilation required the explicit ITaskItem2 metadata API; an initial
  run had 4 passes/2 failures because the metadata dictionary's enumerable yields
  generic entries. Copying through its Keys/GetMetadata API fixed both failures.
- ConfigurationRegression: **127 net472 passed**, no failures/skips (`20_30_20`),
  selecting ProjectAnalysisServiceTests, ProjectAnalysisInputTests, SavedProject,
  CapturedLibraryProjectTests and SdkAnalysisBridgeTests. Same dotnet test command
  options/results-root as above. `git diff --check -- src` passed with only
  line-ending conversion warnings.

Saved evaluation now detaches GetAllGlobs include/exclude/remove patterns for
Compile, resources, lookups, analyzers and project references. Discovery watches
use MSBuildGlob matching and fixed directories (including absent directories),
then request a fresh MSBuild evaluation rather than adding members themselves.
Declared import candidates from the project/import XML are expanded into watch
hints even when an Exists condition currently excludes the import. Standard
ancestor Directory.Build.props/targets, Directory.Packages.props and global.json
candidates are also observed. Literal candidates use nonrecursive watches;
recursive item globs retain recursive watching. Unrelated child directories do
not invalidate a literal candidate. No Project/ProjectRootElement is retained in
the detached patterns.

- GlobDiscovery: **5 net472 passed**, no failures/skips (`20_33_07`), including a
  newly added library source refreshing the real service, initially absent nested
  directories, Exclude/Remove behavior and saved evaluation regression tests.
- ImportDiscovery: **33 net472 passed**, no failures/skips (`20_34_30`), selecting
  ProjectAnalysisInputTests, SavedProject and LiveServiceRevalidatesSavedLibraries.
  The new conditional-import case creates an optional props file, observes its
  event and verifies newly evaluated source membership.
- DiscoveryFinal: **3 net472 passed**, no failures/skips (`20_35_03`), selecting
  SavedGlobsDiscover, MissingConditionalImport and LiveServiceRevalidatesSavedLibraries
  after narrowing literal-candidate directory invalidation. Commands use the same
  dotnet test options/results-root as previous runs.

Package resolution now exposes validated candidate artifact paths through an
optional observer, before checking directory/file existence. The build entry
point keeps its strict behavior. The isolated worker returns detached watch paths
even when resolution fails, without returning partial library inputs; the host
installs those watches before reporting incomplete analysis. This permits recovery
from a missing artifact without requiring assets changes.

- PackageRecovery: **1 net472 passed**, no failures/skips (`20_36_37`). The extended
  PackageResolverUsesCapturedAssets test verifies missing-path observation, starts
  the live service with the artifact missing, observes Incomplete, restores the
  artifact and observes valid consumer XML while asserting unchanged assets text.
  Host-domain isolation assertions still pass.
- PackageRecoveryTaskRegression: full task suite **83 passed per framework**,
  no failures/skips (net8.0 `20_36_46`, net472 `20_36_47`). Command:
  `dotnet test src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj
  --no-restore --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone9/Iteration5/PackageRecoveryTaskRegression`.
  Existing CA1416 warning in shared test infrastructure remains.

Current-input validation now resolves each captured package selection again through
its selected SDK before comparing assets, identities, direct roots, artifact paths
and text. This detects changed fallback-folder precedence even if the previous
artifact and assets text remain unchanged. Temporary worker domains are disposed;
the additional resolution cost remains part of the performance acceptance work.

- PackageSelectionValidation: **2 net472 passed**, no failures/skips (`20_38_47`),
  selecting PackageResolverUsesCapturedAssets and LiveServiceRevalidatesSavedLibraries.
  The package scenario captures from a fallback folder, creates an identical
  artifact in the preferred folder, and verifies rejection of the old capture
  without modifying assets or relying on a watcher event.

Added native LibraryAnalysis scenarios for VS2022/2026: a referenced project stays
outside the solution, its abstract definition supplies consumer preview values,
saved changes refresh without Build, deletion suppresses stale rows and recreation
recovers. Consumer XML/library output absence is asserted.

- Native VS2022 `eng/test-visualstudio.ps1 -VisualStudioPath <VS2022>
  -RootSuffix AlarmlistExp -Filter 'Area=LibraryAnalysis'`: **1 passed**, no
  failures/skips (`20_40_34`). Fixture/cleanup evidence:
  `VisualStudioUI/SavedLibraryAnalysis/2022/25e4803cfd0b4cf0a86bf9efe7b80cc8`;
  TRX run `232d394cdb1a4172ac7c674be8332e8a`. This proves actual selected-SDK
  saved evaluation in the native host, out-of-solution references, refresh and
  deletion/recreation without Build.
- Native VS2026 matching `Area=LibraryAnalysis` run: **1 passed**, no failures/skips
  (`20_41_31`), TRX run `bb694d73cfda4aaf98a5d3e14ab66e1f`. Fixture and owned-IDE
  cleanup are retained under `VisualStudioUI/SavedLibraryAnalysis/2026/`.

The native scenario now loads the library for XML draft testing, forces a fresh
consumer analysis with an unsaved marker alarm, verifies the dependency draft is
absent, saves it and verifies the new inherited value. The first draft probe failed
opening a miscellaneous XML file (`44e8c466f12e48958bfe017fa1ebe5ac`); the next failed
an overly strict DTE project-count assertion (`45ba8f3602804830afb29e819d0f9333`).
The test now validates the returned project's full path and waits for XML editor
readiness. Native VS2022 draft run **1 passed**, no failures/skips (`20_45_51`),
TRX `547caee628404192ba67f0f925ac03a5`.

Extended the scenario to save/delete/recreate library inputs while its project
is unloaded, then verify refresh after reload. A direct out-of-process IVsSolution4
probe failed because UnloadProject requires the IDE UI thread (`20_46_38`, TRX
`c7f64fd09efa49e6b869017f09d7cbe3`). Removed that helper; the test now selects the
native project tree node and invokes Project.UnloadProject/Project.ReloadProject
through DTE. That run passed saved updates/deletion/recreation while unloaded,
but timed out after reload because selecting the library left preview without an
active document context (UI automation dump, fixture
`fc4e4ba13b0c4cd9b2ee859217cd592f`, TRX `1244594ece6246cab552740e51057729`).
The test now reactivates the consumer designer before asserting reload refresh.
One further run (`ff35392c28bb4cdd8aaf4b7b1f8ce760`) observed preview invalidation
between two separate assertions after the marker appeared. The draft check now
captures one row-name array and waits for both the marker and saved inherited
value in that same observation.

- Final expanded native VS2022 `Area=LibraryAnalysis`: **1 passed**, no failures/
  skips (`20_50_02`), TRX `63d02433349b45a28beeb29b4ea94b18`. Covers consumer draft
  precedence, dependency draft exclusion and Save, source refresh/deletion/
  recreation while the library is unloaded, and refresh after reload.
- Final expanded native VS2026 matching run: **1 passed**, no failures/skips
  (`20_50_39`), TRX `fb734cf25dd04217ba6ee302ce6734b2`. Both final runs retain
  fixture/cleanup evidence under `VisualStudioUI/SavedLibraryAnalysis/<host>/`.

Import discovery now includes historical MSBuild-evaluated property values, so
later reassignment cannot hide an earlier optional import path. Reserved
MSBuildThisFile properties are resolved case-insensitively for the declaring
import file. These are conservative watch hints only; actual import membership
still comes from MSBuild. Hint expansion is cancellable and ignores invalid
property-function expressions protected by a false import condition. The
upstream evaluator's conditional-import handling was reviewed before this change.

The service retains previous dependency watches until fresh discovery succeeds.
This fixes recovery when deleting a mandatory import aborts evaluation before the
import's watch can be recaptured. Identical discovery patterns are deduplicated
across failed retries; stopping the service clears retained watch data. Updated
the extension README with saved dependency/package behavior and remaining UI work.

- ImportPropertyHistory: **7 net472 passed**, no failures/skips (`20_52_37`),
  selecting SavedProjectEvaluationTests, MissingConditionalImport,
  PackageResolverUsesCapturedAssets and LiveServiceRevalidatesSavedLibraries.
- ImportRecovery: **51 net472 passed**, no failures/skips (`20_54_08`), selecting
  SavedProjectEvaluationTests, ProjectAnalysisServiceTests and
  LiveServiceRevalidatesSavedLibraries. The live scenario now deletes a required
  import, observes Incomplete, restores it, and verifies automatic recovery.
- EditorFullRegression: **509 net472 passed**, no failures/skips (`20_54_59`,
  duration 1m28s), including the subsequent false-condition hint safeguard and stop
  cleanup changes. Command: `dotnet test` on the VS unit-test project with
  `--no-restore --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone9/Iteration5/EditorFullRegression` (no filter).

Final iteration-5 validation and review:

- VSIXRegression: **12 net472 and 12 net8.0 passed**, no failures/skips
  (`20_56_58`), running the complete VS integration-test project with `dotnet test
  --no-restore --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone9/Iteration5/VSIXRegression`.
- Native `Area=LibraryAnalysis` reruns after the watch-retention/import-hint
  changes: **1 passed on VS2022 and 1 on VS2026**, no failures/skips;
  TRX runs `4ed313b5b37249ceb46cadff11b45045` (`20_58_12`) and
  `4e475bfd5371476e902be2d4e2187d9f` (`20_58_42`).
- SDKLibraryRegression: **16 net472 and 16 net8.0 passed**, no failures/skips
  (`20_59_05`). Command: `dotnet test` on the SDK test project with `--no-restore
  --filter "FullyQualifiedName~LibraryPackTests|FullyQualifiedName~LibraryProjectReferenceTests|FullyQualifiedName~LibraryExchangeTests"
  --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone9/Iteration5/SDKLibraryRegression`.

Final review also identified the first-load missing mandatory import case, where
no earlier successful capture supplies recovery watches. A private evaluation
with IgnoreMissingImports now supplies watch hints on failure only. The strict
evaluation error is rethrown; tolerant inputs never become compilation inputs or
published results. It executes no targets and retains no MSBuild project objects.

- InitialImportServiceRecovery: **8 net472 passed**, no failures/skips
  (`21_02_51`), selecting SavedProjectEvaluationTests and
  LiveServiceRevalidatesSavedLibraries. Covers strict rejection with detached
  recovery hints and actual service recovery when an initially absent required
  import is created, as well as the existing successful-capture/delete/recreate
  sequence. This focused run covers the final recovery change made after the
  full editor and native runs above.

Iteration 5 is complete: saved dependency capture, invalidation, failure recovery,
draft isolation, current-input checks and disposal have implementation evidence.
Library-aware UI/navigation remains iteration 6; integrated performance and full
milestone acceptance remain iteration 8. No milestone completion is claimed.

### Iteration 6 — complete

Iteration 5 committed as `f2f4dc9`. Initial UI inspection confirms that analysis
already carries library identity on alarm origins and the project preview includes
imported concrete alarms. ReferenceChoice currently labels only source filenames;
navigation still opens physical paths and validates them against captured text.
Those existing paths do not implement generated package definition views or the
portable-to-physical mapping for referenced-project sources.

Implementation sequence within this iteration:

1. Add explicit library origin labels to reference choices/completion and preview
   presentation; retain unavailable/deferred values and consumer-only editing.
2. Resolve navigation from detached captured library provenance: project sources
   use actual saved local files, packages use generated read-only definitions.
   Revalidate captured dependencies and preserve destination draft guards.
3. Cover stale actions, source mapping, generated views, Undo and disposal in
   focused tests, then run native navigation/UI cases on both supported hosts.

These are remaining deliverables, not passing evidence. No iteration-6 commit or
completion is claimed.

Initial implementation: reference choices and preview Source labels now include
library ID/version; both searches match that origin. Deferred names remain raw
expressions in reference choices and unavailable in preview values. Added a
detached AnalysisNavigationTarget resolver for captured local text, physical
referenced-project sources and generated package spans/captions. It uses compiler
origin maps, does not read disk, and rejects absent/ambiguous destinations. The
resolver is not yet connected to UI actions or a read-only native definition view.

Validation (`dotnet test` on the VS unit-test project, `--no-restore --verbosity
minimal --logger trx`, output under `Milestone9/Iteration6/`):

- OriginPresentation: **17 net472 passed**, no failures/skips (`21_05_33`), filter
  LibraryOriginPresentationTests, ReferenceSearch and AlarmTable.
- NavigationTargets: **4 net472 passed**, no failures/skips (`21_07_00`), filter
  AnalysisNavigationTargetTests and LibraryOriginPresentationTests. Generated
  positions differ from original source positions; unmapped/case-mismatched
  portable paths are rejected, and local unsaved text is retained without I/O.
- ProjectNavigationTargets: **2 net472 passed**, no failures/skips (`21_07_21`),
  filter LiveServiceRevalidatesSavedLibraries. Actual selected-SDK library origins
  resolve to captured physical source paths/text in both import-recovery variants.
- `git diff --check -- src`: passed.

Remaining: wire the resolver into all relevant designer/preview/diagnostic
navigation actions, implement native generated read-only document lifetime,
exercise resources/lookups and real packaged definitions, and complete the
iteration's UI, stale-action, draft/Undo and native-host acceptance checks.

Designer inherited-field/reference/group actions and project preview navigation
now dispatch detached navigation targets after their existing current-input and
draft checks. Added a shell-owned generated definition code window with an
immutable read-only text region and no-save/no-MRU/no-reopen RDT flags. Identical
open definitions are reused by enumerating shell frames; no global document cache
retains project state. The implementation follows the public
[CreateDocumentWindow contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivsuishell.createdocumentwindow?view=visualstudiosdk-2022)
and [RDT flags](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop._vsrdtflags?view=visualstudiosdk-2022).
Native generated-view behavior is not yet verified. An initial compilation error
used a nonexistent SOleServiceProvider marker; corrected the service request to
the OLE IServiceProvider interface and rebuilt successfully.

- NavigationWiring: **342 net472 passed**, no failures/skips (`21_09_47`, 1m26s),
  filter AnalysisNavigationTargetTests and EditorTests; results under
  `Milestone9/Iteration6/NavigationWiring`.
- PackagePreviewNavigation: **1 net472 passed**, no failures/skips (`21_11_32`),
  filter ProjectPreviewNavigatesPackageDefinition. Exercises a package row's
  actual preview command, rejection when current-input validation fails, generated
  span dispatch when it succeeds, and view disposal.
- Extended native LibraryAnalysis with inherited-value navigation to the actual
  out-of-solution project source. VS2022: **1 passed**, no failures/skips
  (`21_12_14`), TRX run `d2cf06238cb641a3b746a9148c1544d2`.

Added native LibraryDefinitions scenarios for both hosts using actual SDK Pack
and isolated NuGet restore. The first VS2022 run failed waiting for the generated
editor: the captured UI shows an invalid-argument error from the navigation action.
Pack, restore and inherited preview succeeded. Evidence: fixture
`VisualStudioUI/PackageDefinition/2022/3f08ce3fffc04ab2b7b7bbbbd069169d`,
TRX `f8975b2051364e4baf01cc898df34ca6` (`21_14_38`). Added operation-specific
HRESULT reporting to the native definition creation calls; diagnostic rerun is
pending. This is an unresolved implementation check, not passing acceptance.

Designer diagnostic navigation now uses the same detached target resolution,
retaining existing local source-version checks and current-input/draft validation.
Error List integration and validation of this subsequent change remain pending.

The diagnostic native rerun isolated `CreateDocumentWindow` returning
E_INVALIDARG (`0x80070057`); text initialization, language selection, read-only
flags and code-window setup succeeded. Fixture
`PackageDefinition/2022/1d7431e174ee417b8598c3948b3ced94`, TRX
`5a863fa91ac14ac7ae277d0a9a3367a2` (`21_16_52`). Replaced the custom URI document
moniker with a rooted temporary-path identity (no file or directory is written).
The moniker hypothesis is being validated in another native run; not yet proven.

That rerun also failed at CreateDocumentWindow with E_INVALIDARG: fixture
`PackageDefinition/2022/2cc146ed3ef84a769436ea4f825c04bc`, TRX
`7058238b5fcb4033ab5b199a68c0a8d3` (`21_18_44`). Changing the moniker alone did
not resolve creation. All owned test processes completed and cleanup evidence is
retained. Next investigation must examine document-data initialization and shell
embedding/registration, rather than treating this as an automation selector issue.
No native generated-definition acceptance or iteration-6 completion is claimed.

Further native investigation:

- Explicit SetUntitledDocPath initialization and a standard text-editor GUID
  each still returned E_INVALIDARG at direct window creation (TRX runs
  `cc1d9c80eafd4f9a87aefe982d3df1d4`, `3484fcae3086419c82b44168fc0b47e9`).
- Direct OpenSpecificEditor with the unregistered buffer caused an experimental
  VS2022 access violation in msenv.dll; Windows Application events confirmed the
  OpenSpecificEditor stack. Fixture `PackageDefinition/2022/34dd1aa99b454fba84e7e6fd57335846`,
  TRX `fbe3c4138d514601a09c74fbe8db8ae4`. Removed that opening path.
- Replaced low-level window creation with a registered LibraryDefinitionEditorFactory
  and non-saving IVsPersistDocData/IVsTextBufferProvider document. The shell now
  opens it through OpenDocumentViaProjectWithSpecific. Pending captured inputs
  exist only for the synchronous factory invocation and are removed in finally;
  open documents retain only their own buffer/definition, not analysis projects.
  Load/Save never touch disk. Factory output preserves the read-only region and
  no-save/no-reopen RDT flags. The preexisting editor-factory pattern is reused.
- The first registered-factory run visibly opened the generated XML definition
  at the mapped Name line, with portable library/source caption. Test then failed
  on a transient UIA 0x80040201 while enumerating controls during the document
  switch. Screenshot/automation evidence: `PackageDefinition/2022/8e84404eb71e467f8a86de8cb5a55cdc`,
  TRX `f0d151d4fa6a4b76b54ce00adc029925` (`21_24_08`). The test now selects the
  observed WpfTextView ID and retries that specific transition error. Read-only
  editing assertions still require a passing run.

Live Error List navigation also dispatches detached targets after the same
current-input checks; local diagnostic source-version checks remain intact.
Subsequent focused diagnostics/editor regression is still required.

- Native VS2022 LibraryDefinitions: **1 passed**, no failures/skips (`21_25_10`),
  TRX `48930e716ed241be9f14a5bd617d433d`. Actual locally packed/restored package
  navigation opens generated XML, attempts to type do not change its text,
  package bytes remain unchanged, consumer output remains absent, and the view
  closes normally. This supersedes the failed low-level-opening probes above.
- Native VS2026 `Area=LibraryDefinitions|Area=LibraryAnalysis`: **2 passed**, no
  failures/skips (`21_25_50`), TRX `bfb25df574de4aa0bc5c55fa8f35c1aa`. Covers both
  generated package navigation/read-only behavior and out-of-solution physical
  source navigation plus saved dependency draft/unload/reload behavior.

Added resource/lookup diagnostic navigation cases with distinct original and
generated spans. Full editor unit regression against the registered factory,
Error List dispatch and those new tests: **518 net472 passed**, no failures/skips
(`21_26_33`, 1m33s), results under `Milestone9/Iteration6/EditorRegression`.

Extended the native definition scenario to navigate twice and assert one tab,
close it, reopen it and close it again. VS2022: **1 passed**, no failures/skips
(`21_30_24`), TRX `8225d378181a4aeaae123bfbdd00e941`. VS2026: **1 passed**, no
failures/skips (`21_31_11`), TRX `ff4f74ee802846e691903deb0e390d2d`.
Remaining iteration gates include stale dependency actions and the
final review of all library-aware completion/navigation paths.

Review finding: the root project snapshot currently does not carry OutputType
into AnalysisContext, and SdkAnalysisWorker always calls strict AnalyzeSnapshots.
Imported deferred values are already unavailable in that result (covered by
LibraryBuilderTests), but analysis of a library project itself has no library
validation mode. Resolve this distinction before iteration completion; library
Build's deferred validation and final application strictness must remain intact.

Implemented the root-library distinction with `AnalyzeLibrarySnapshots`, reusing
SnapshotAnalyzer and the existing deferred evaluator. It returns detached values
and diagnostics but never final XML. The CPS snapshot captures OutputType into
AnalysisContext.IsLibrary; saved-input/library updates retain it and context
comparison invalidates on mode changes. The SDK worker requires the matching
library analysis entry point and selects it only for a library project.
Added parameter/translation/list deferral tests, structural/reference error tests,
mode-capture tests and an isolated packaged-bridge comparison with strict
application analysis. The SDK consumer contract asset also exercises this API.
SDK/extension documentation and repository instructions describe the distinction.

- LibraryDeferredValidationTests: **16 passed on each framework**, no failures or
  skips (`21_32_38`), results under `Iteration6/LibraryAnalysis`.
- LibraryProjectAnalysisDefersValues: **1 net472 passed**, no failures or skips
  (`21_32_54`), results under `Iteration6/LibraryBridge`.
- Full compiler regression after that build (`--no-build --no-restore`):
  **618 net472 and 620 net8.0 passed**, no failures or skips (`21_34_15/16`),
  results under `Iteration6/CompilerRegression`.
- Full editor regression: **523 net472 passed**, no failures/skips (`21_33_10`,
  1m30s), results under `Iteration6/LibraryModeRegression`.
- Expanded PackagedCompilerAnalyzesUnsavedSnapshotsWithOrigins contract:
  **1 passed on each test framework**, no failures/skips (`21_35_02`), results
  under `Iteration6/LibraryPackagedContract`. Both use dotnet MSBuild by default;
  the first explicit VS2022 desktop MSBuild run aborted before tests because
  vstest could not connect to testhost within 90 seconds (`21_36_39`,
  `Iteration6/LibraryDesktopContract`). After that process reached a terminal
  result, retrying outside the sandbox **passed 1 net472 test**, no failures or
  skips (`21_37_17`, `Iteration6/LibraryDesktopContractRetry`). The invocation
  selected VS2022 MSBuild.exe and restored the previous environment value.
- Native root-library mode: VS2022 **1 passed** (`21_37_50`, TRX
  `1068aec5fcbf4e7298f76a46a9b5ac80`) and VS2026 **1 passed** (`21_38_35`, TRX
  `6179ce8f0cbf44ed8b6e23e012e9dd63`), no failures/skips. The fixture evaluates a
  supplied parameter, confirms absence of final XML, removes the assignment in
  the unsaved buffer and observes an unavailable concrete preview row while saved
  source/output/artifact state stays unchanged. Added an explanatory XML-preview
  message directing library users to their consuming application.

Final navigation review: inherited fields, references and procedures share the
detached resolver; selected/project preview and live diagnostics also use it.
Action execution retains full current-input validation, generation/draft checks,
and physical destination text/draft checks. An ambiguous local mapping now returns
no target instead of throwing from diagnostic command availability evaluation.
Existing stale-action/draft tests and the new package-row validation test run in
the full editor suite; the real saved-library service tests cover dependency input
changes before publication and later revalidation. Native LibraryAnalysis covers
saved refresh, private dependency drafts, deletion/recovery and unload/reload.
Final focused bridge/navigation tests after the message/resolver changes:
**53 net472 passed**, no failures/skips (`21_39_00`, 23s), results under
`Iteration6/FinalBridgeNavigation`.

Iteration 6 acceptance: origin-aware reference search and concrete preview rows,
deferred availability, physical project-source navigation, generated read-only
package definitions, current-input/draft guards and document reuse/close/reopen
are implemented and validated by the compiler, packaged bridge, editor and native
evidence above. The native cases passed on both hosts. No essential iteration-6
gate remains. Templates/examples/documentation expansion and full integrated
acceptance/performance remain iterations 7 and 8; this does not claim their gates.
Final scoped diff check passed. The full-worktree check also reports preexisting
trailing whitespace in the unrelated North example edit, which is preserved.

### Iteration 7 — complete

Iteration 6 committed as `b9d3a5c`. Initial inspection confirms the VSIX has one
application project template and two item templates; template packaging explicitly
replaces the SDK version and zips each directory. General.xaml currently exposes
application output/culture settings but no library output or package identity
controls. Extend these existing mechanisms, retaining the built SDK version.

Implementation sequence within this iteration:

1. Add the library project template with reusable abstract/concrete definitions;
   extend template preparation, packaged registration/content checks and native
   project creation tests on both hosts. Add output type, library filename and
   package identity/version property controls with persistence/schema validation.
2. Add a persistent standalone library example graph with two consumers and a
   transitive library, demonstrating parameter, translation and lookup overrides.
   Keep it isolated from the existing North/South example's shared analyzer/source
   imports, and preserve the owner's uncommitted North edits.
3. Document CLI Restore/Build/Pack and native creation/reference/property workflows,
   plus package-feed setup and expected results. Validate the examples through
   isolated packaged SDK consumers on both runtimes and native template/property
   flows. Record actual results before marking this iteration complete and commit
   the entire successful iteration before starting integrated acceptance.

Implemented the Alarmlist Library project template with an abstract base and a
concrete derived alarm, OutputType=Library, package identity/version and the
build-selected SDK pin. Added output type, artifact filename and package identity
controls to the existing General property rule. Template packaging now includes
both project templates; package tests validate their SDK pin and actual library
Build/Pack. Example/acceptance preparation selects the application template
explicitly; release preparation validates the pins of both templates. No release
or publication command has run. UI fixture feed discovery likewise selects the
application template explicitly.

Validation:

- Initial package run (`21_41_26`): 12 passed and the new library test failed on
  each framework. ReadLinesFromFile's list-to-property conversion inserted
  semicolons between project XML lines. Replaced that conversion for the new
  template with ReadAllText, preserving complete XML before SDK substitution.
- Full package rerun: **13 net472 and 13 net8.0 passed**, no failures/skips
  (`21_41_53`), `Iteration7/TemplatesRetry`. Includes actual template Build/Pack.
- Property rule tests: **14 net472 passed**, no failures/skips (`21_42_19`),
  `Iteration7/PropertyRules`; validates MSBuild XAML loading, enum values and
  configuration-independent package identity persistence.
- Native LibraryTemplate creation/build/reopen: VS2022 **1 passed**, no failures
  or skips (`21_43_07`), TRX `e51049a1cfc74794a259d09e2b61fad7`. VS2026 **1 passed**,
  no failures/skips (`21_43_34`), TRX `5371ba1ec27d4171ab8cef4c512efe54`.
  Property-page interaction, persistent examples
  and their documentation/validation remain outstanding. Iteration not complete.
- Modified preparation/release scripts pass PowerShell parser checks; scoped diff
  check passes. These are syntax checks, not release execution or publication.

Added `example/Libraries`: Common -> Equipment -> LineA/LineB, with abstract and
concrete alarms, consumer parameter overrides, ancestor translation/list overrides
and different final consumer values. Its own Directory.Build files prevent the
North/South playground's shared sources/analyzers from being imported. A separate
Libraries.sln and `eng/prepare-library-example.ps1` prepare the SDK selected by the
built library template, using ignored local configuration and a content-specific
cache. The checked-in README documents expected values, native exploration and
local NuGet packing; no North sources were edited.

The package-test fixture copies these actual source files. LibraryExampleTests
checks both consumers' three concrete alarms, all parameter/code/translation
results, packs both dependencies, changes an isolated LineB copy to PackageReference,
restores and verifies byte-identical final XML.

- Initial project-build example checks: **1 passed per framework**, no failures or
  skips (`21_44_54`, `Iteration7/Examples`). Expanded package-equivalence checks:
  **1 passed per framework** (`21_46_18`, `Iteration7/ExamplePackages`).
- Explicit desktop VS2022 MSBuild: **1 net472 passed**, no failures/skips
  (`21_46_35`, `Iteration7/ExampleDesktop`), with environment restored afterward.
- Preparation script and documented local Pack commands succeeded, producing the
  two packages under the ignored `.feed` directory.
- Direct solution build in the sandbox returned failure after Common with zero
  reported warnings/errors. Diagnostic text/binlog retained under
  `Iteration7/library-example-solution.*`. The same build outside the sandbox
  succeeded for all four projects; its binlog is
  `Iteration7/library-example-solution-unsandboxed.binlog`. No code change was
  made to bypass solution/reference semantics.
- Expanded the test to start with actual Libraries.sln Restore/Build, then inspect
  both outputs and test package equivalence. **1 passed on each test framework**,
  no failures/skips (`21_47_26`, `Iteration7/ExampleSolution`), outside the sandbox.

Native property-page interaction and final documentation/instruction review remain
before iteration-7 completion. The integrated acceptance/performance gate remains
iteration 8.

Native property-page probes on VS2022 initially failed: the first timed out and
required termination of its identity-verified owned test IDE; the second captured
"No property pages for this selection" with the ALMX editor still active. Closing
that editor and explicitly focusing the project node opened the correct Alarmlist
page (fixture `7b6109f35ed2450cbfc9660f43f621cc`). Its retained UIA tree contains
Output type, Package ID/version and Library filename. That probe still failed
because it expected an Edit control before selecting the property row; the native
grid initially exposes only the selected Output type editor. The test now selects
each row before editing and checks saved project values plus a reopened build.
These failed probes are not passing native property-edit evidence.

The subsequent row-click and Focus/F2 probes timed out waiting for the Package ID
editor (`21_55_08`, `21_57_38`); UIA SelectionItem is unsupported (`21_55_55`).
The native grid's MSAA TAKEFOCUS/TAKESELECTION operation selects the intended row.
Using it, `Area=LibraryProperties` now edits Package ID, Package version and
Library filename, saves their project XML, reopens the solution and verifies the
renamed artifact and changed identity/version after Build:

- VS2022: **1 passed**, no failures/skips (`21_58_17`), TRX
  `3c6de57803254401ae0b198cf82c60ad`.
- VS2026: **1 passed**, no failures/skips (`21_58_55`), TRX
  `bbb82ee7495b4c578b3996e46f364a9c`.

The attempted diagnostic stack capture found that the earlier owned IDE had
already exited; no stack capture was produced. All later tests completed normally.
Output-type switching in the native page still needs explicit coverage, followed
by final iteration review and commit. The passing text-field checks do not prove
that enum interaction.

Final iteration-7 validation closes those remaining scoped checks:

- The native scenario now changes Library -> Application, saves and builds one
  concrete alarm into XML, then changes Application -> Library, saves/reopens and
  verifies the library artifact and removal of the obsolete application output.
  It retains the package identity/version/filename assertions above. Dialog lookup
  uses the property-grid identity rather than a localized window title. Initial
  lookup/focus probes failed (`22_00_35`, `22_01_08`); the final helper finds the
  owning window and waits for keyboard focus. VS2026 additionally needed a wait
  for refreshed property rows (`22_02_57` was a failed probe).
- `eng/test-visualstudio.ps1 -VisualStudioPath <VS2022> -RootSuffix AlarmlistExp
  -Filter 'Area=LibraryProperties'`: **1 passed**, no failures/skips (`22_01_48`),
  TRX `736c68550ecf47828bd955c2c6fac857`.
- Same command for VS2026: **1 passed**, no failures/skips (`22_03_39`), TRX
  `3c0906edbe0e4e68833a737ec6995a08`.
- Final LibraryExampleTests with `--framework net472` and
  ALARMLIST_TEST_MSBUILD_PATH selecting VS2022 desktop MSBuild: **1 passed**, no
  failures/skips (`22_03_54`), `Iteration7/ExampleSolutionDesktop`. Includes the
  actual solution Restore/Build, both consumers and project/package equivalence.
  The previous environment value was restored.
- Reviewed templates, package scripts, example sources and documentation links;
  scoped `git diff --check` passed. Existing unrelated North edits remain outside
  this iteration. No publication was performed.

Iteration 7 is complete (commit `f20c3fc`). Integrated regression, requirement audit and library
performance measurements remain required in iteration 8; this is not milestone
completion.

### Iteration 8 — complete

Completion requires the full acceptance matrix above, with current regression
evidence and actual library performance measurements. Planned remaining work:

1. Run solution regressions, resolve implementation/contract failures, and verify
   desktop-MSBuild consumers. Preserve unrelated North playground edits and
   distinguish their failures from validation of the committed baseline.
2. Audit the design contract against compiler, SDK, bridge and native tests;
   run native library workflows on both hosts after the final implementation.
3. Add library-specific performance workloads to the existing opt-in harness:
   saved dependency graph capture, saved-change recapture/analysis, imported
   preview publication/layout, and bridge/project unload. Retain raw samples,
   revision/runtime/workload metadata and correctness checks outside measurements.
   Include both native hosts for saved-refresh/unload evidence; do not infer live
   performance from isolated timings or add machine-dependent thresholds.
4. Record any limitations, review the final diff, and commit this iteration only
   after the essential checks pass. Native/performance skips in ordinary solution
   runs do not satisfy their acceptance gates.

Initial `dotnet test AlarmlistProject.slnx --no-restore --verbosity minimal
--logger trx --results-directory artifacts/log/Debug/Milestone9/Iteration8/FullRegression`
found a missing `IDesignerAnalysis.Navigate(AnalysisNavigationTarget)` implementation
in the performance harness's CapturedDesignerAnalysis. Added the same rejecting
adapter behavior as its existing navigation overload; validation pending.
The current-working-tree SDK ExamplePlaygroundTests North case also fails because
the unrelated North.almproj edit references South, an application project. The
SDK correctly requires OutputType=Library for that reference. Both user-modified
North files remain untouched. This first run is not a passing full regression.

Initial requirement audit (test bodies inspected, not merely test names):

| Contract requirement | Current evidence | Remaining verification |
| --- | --- | --- |
| Alarm/file/consumer/library parameter precedence; explicit empty; no inherited assignments/defaults for consumer-derived alarms | LibraryCompilationTests exercises all four precedence levels, three explicit-empty scopes and strict missing-value failure for the local derived alarm. Current compiler run: 618 net472 / 620 net8.0 passed. | Final integrated package/native scope audit. |
| Portable source identity, final consumer project identity and unavailable physical paths | LibraryCompilationTests checks evaluated identity fields and rejects both physical path expressions; captures retain portable diagnostic provenance. | Verify corresponding bridge/package assertions in final audit. |
| Dependency-first diamond deduplication; cycles and identity/payload conflicts | LibraryGraphTests asserts exact graph order, cycle chain, missing version rejection, case-insensitive identity version conflicts and a 4,096-node graph. Included in passing compiler suites. | Integrated project/package graph evidence. |
| Resource precedence by key/culture before parent fallback; sibling override requirement | LibraryResourceTests checks exact vs parent culture, empty/whitespace, reversed root order, sibling conflicts/explicit override, same-owner duplicates and actual consumer expression evaluation. Included in passing compiler suites. | Complete lookup, artifact, SDK and native sections of audit. |
| Whole-path, whole-entry and whole-completion-binding overrides | LibraryLookupTests checks removed path members, removed entry description/metadata, retained unrelated entries, replaced binding targets, all three sibling conflicts, explicit consumer overrides, rejected malformed/duplicate inputs and actual compiled values. Included in the passing compiler suites. | Integrated package/native lookup presentation remains part of final acceptance. |
| Detached package navigation maps original locations to generated definitions | AnalysisNavigationTargetTests checks generated ALMX spans and portable captions, rejects unknown/case-mismatched locations, and maps resource/lookup diagnostics to generated documents. Local navigation uses captured unsaved text at an unavailable disk path. Included in the passing editor/bridge suite. | Native read-only/non-saving behavior and physical project navigation require the separate host cases. |
| Deterministic versioned artifacts retain raw values, scopes and portable origins | LibraryArtifactXmlTests compares write/read/write bytes, distinguishes empty/unset/raw properties, retains dependency/resource/lookup identities and origin spans, and excludes physical paths and formatting comments. It rejects unsupported versions, malformed envelopes/spans, duplicate portable names, DTDs and external entities. Included in the passing compiler suites. | Final SDK artifact/package equivalence verification. |
| Deferred library values with strict final application validation | LibraryDeferredValidationTests asserts successful library analysis with unavailable parameter/localization/list values and no compiled XML, while application analysis/compilation fails for the same inputs. Structural/expression errors and consumer-only references remain errors. Included in the passing compiler suites. | Native library/application-mode evidence remains separately required. |
| Project-reference lifecycle, configuration and no-build consumption | LibraryProjectReferenceTests checks actual transitive output, unchanged artifacts after dependency source corruption in both no-build modes, strict subsequent Build failure with preserved output, Clean/Rebuild, explicit/solution configuration mapping, conditional removal and discovery without artifacts. | Final full regression and desktop-MSBuild rerun. |
| Mixed project/package equivalence and safe packing | LibraryPackTests compares compiled bytes before/after conversion of a mixed graph to packages, rejects non-packable dependencies and Application packing, protects a source at the package destination, and retains prior packages after failed builds. Together with LibraryProjectReferenceTests, 15 cases passed per framework in the inspected initial FullRegression TRX files (`22_04_42`). | Final full regression and desktop-MSBuild rerun; native install/update/uninstall remains unverified for real packages. |
| Saved dependency evaluation without target execution or loaded drafts | SavedProjectEvaluationTests creates a modified loaded MSBuild project but verifies only saved items/package metadata are captured in a separate evaluation. InitialTargets error sentinels never execute; import/glob changes, duplicates, missing-import discovery hints and recovery are asserted. SavedProjectGraphTests verifies dependency-first diamonds, cycles/recovery, distinct configurations and solution mapping. Included in the passing editor/bridge suite. | Native private-ALMX-draft gate remains failed on VS2022; watcher/publication race coverage is audited separately. |
| Captured dependency bytes and portable linked inputs | CapturedLibraryProjectTests checks explicit-empty resource culture, duplicate/order preservation, Link names, saved configuration and no output directories. ALMX/RESX/lookup/EditorConfig byte changes invalidate captures even with restored timestamps; Application dependencies and missing inputs fail. Included in the passing editor/bridge suite. | Final regression and native recovery evidence. |
| Revalidation races, retained watches and SDK-only NuGet resolution | SdkAnalysisBridgeLibraryTests changes dependency text during consumer validation and requires recapture before publication while retaining consumer drafts. It exercises later saves, glob membership, missing-import recovery, changed artifact/assets bytes, preferred package-folder changes and missing-package restoration. Resolver results are detached, and host assemblies must exclude the task and NuGet implementation assemblies. Included in the passing editor/bridge suite. | Final integrated rerun; corresponding native scenarios remain separate evidence. |
| Coordinated analysis 5/analyzer 3 transition | SdkAnalysisBridgeTests rejects missing, legacy 1–4 and unknown contracts while a compatible bridge remains usable, releases failed compiler images and excludes compiler assemblies from the host. AlarmAnalyzerTests rejects analyzer contracts 1/2; the packaged AnalysisContract asset checks version 5, library ownership and detached preview data from restored compiler binaries. | Final compiler/bridge/package regression. |
| Owner-only library analyzers/policy, combined consumer analyzer model | Added LibraryAnalyzerAndPolicyStayWithOwnerWhileConsumerAnalyzerSeesCombinedModel: the owning rule runs once with suppressed warning; consuming its artifact without a rule produces no diagnostics; a consumer rule sees imported/local alarms and emits both warnings without inheriting owner suppression. | Focused test passed on both frameworks; final regression pending. |
| Fresh package graph, offline reuse, updates/removal and conflicts | PackageRestoreTests packs separate dependency artifacts with exact nuspec versions, converts a project-reference fixture to packages and compares final XML, then forces restore from an empty feed using its warm cache. Missing artifacts fail without replacing output. Updates change compiled values; conflicting selected versions fail both Restore and subsequent Build while retaining output. Removing references excludes cached packages from output. Included in the earlier SDK regression. | Final SDK regression on both runtimes and desktop MSBuild. |
| Raw inherited definitions, procedure directives and ownership | LibrarySourceTests round-trips abstract state, unassigned/empty values, references, expression titles and Clear without flattening inherited fields, and verifies captured data survives mutation of original syntax. LibraryBuilderTests retains only owned definitions plus dependency identities, checks consumer/default provenance and validates all provided cultures. Included in the compiler regression. | Final compiler and downstream suites. |
| Output-kind transition and legacy ownership manifests | LibraryBuildTests verifies culture-independent library output/discovery, normalized versions, prior artifact/manifest preservation after failure, source-safe Clean and Application-to-Library retirement. Added VersionOneManifestStillOwnsPreviousApplicationOutputs checks legacy manifest consumption by both library Build and Clean, migration to version 2, and preservation of unrelated files. | Focused legacy cases passed on both frameworks; final task/SDK regression pending. |

This partial audit does not establish milestone completion. The pending rows and
performance work remain essential acceptance work.

Initial full-regression run finished with exit code 1. Recorded TRX evidence:
compiler **618 net472 / 620 net8.0 passed**; task unit **83 per framework passed**;
task integration **4 per framework passed**; VSIX integration **13 per framework
passed**; editor/bridge **524 net472 passed**. SDK tests: **74 passed / 1 failed
on each framework**, with the sole failure being the unrelated North application
reference described above. Native tests were opt-in skipped (66); performance
tests did not run because of the adapter compilation error. Ordinary build output
also reports existing CA1416 warnings in the shared test helper.

To distinguish the committed example from local playground edits, a focused
ExamplePlaygroundTests run temporarily substitutes HEAD's two North files only
in generated `artifacts/bin/.../testassets/Example/North` copies, uses
`--no-build --no-restore`, and restores those generated bytes in finally. No
repository example source is changed. Results remain pending; this is a separate
baseline check, not a claim that the current working-tree example builds.

The committed ExamplePlaygroundTests baseline check completed: **3 passed per
framework**, no failures/skips (`22_08_13`), retained under
`Iteration8/CommittedExampleBaseline`. Generated test copies were restored.
The performance adapter fix is verified by `dotnet test
src/VisualStudio/Alarmlist.VisualStudio.PerformanceTests/Alarmlist.VisualStudio.PerformanceTests.csproj
--no-restore --verbosity minimal --logger trx --results-directory
artifacts/log/Debug/Milestone9/Iteration8/PerformanceHarness`: **47 passed,
14 opt-in skipped**, no failures (`22_08_38`). This proves harness compilation and
ordinary report/deployment checks, not timing measurements. Library-specific
measurement implementation and native acceptance runs remain outstanding.

Added isolated library performance workloads using the existing Release/x64
BenchmarkDotNet harness and the packaged SDK. `1000-diamond` contains one abstract
template shared by Left/Right libraries with 500 concrete alarms each; the final
consumer supplies its parameter. Saved project capture uses the production path,
with an InitialTargets error sentinel forbidding accidental target execution.
Correctness checks verify three deduplicated artifacts, all 1,000 final names,
latest saved-template changes, no output writes, compiler host isolation and, for
the actual WPF preview, version labels and bounded realized rows.

- `eng/measure-visualstudio-analysis.ps1 -Job Short -Filter
  'Category=Performance&Host=Isolated&Area=Libraries'`: **1 xUnit case passed**,
  four successful measurements, no skips (`22_10_29`). Raw CSV/full JSON/logs and
  revision/runtime metadata: `artifacts/log/Release/AnalysisPerformance/
  5a4c206d245c4f559e1094c31d1a033d`. Means: saved graph/artifact capture 632.777 ms;
  saved-change recapture plus analysis 830.409 ms; captured-library analysis
  192.057 ms; populated consumer bridge disposal 0.764 ms. The disposal sample is
  short/noisy (BenchmarkDotNet minimum-iteration warning); it does not establish
  native project-unload latency or leak freedom. Full capture includes opening
  and disposing the dependency SDK domains. File writes and validation are outside
  the measured intervals; no machine-dependent thresholds are asserted.
- Same launcher with `Area=LibraryPreview`: **1 xUnit case passed**, two successful
  measurements, no skips (`22_12_01`). Raw evidence:
  `artifacts/log/Release/AnalysisPerformance/3cc5df56527c406895ac8becdc8c3452`.
  Actual WPF project-report creation/layout: 54.49 ms; publication/layout of an
  already captured/analyzed saved-library change: 62.39 ms. Capture and compilation
  are outside these UI intervals. Both runs use two warmups and five measured
  iterations; raw exported samples include outliers, with BDN summaries applying
  its standard outlier policy.

Native library saved-refresh/project-unload measurements on both IDE hosts,
final native regressions and the rest of the requirement audit remain open.

Additional acceptance work after the isolated measurements:

- Initial integrated VS2022 `Area~Library` run: **6 passed / 1 failed**, no skips,
  TRX `1fbe9fcac706470c82bec400025f4fa6` (`2026-10-15 22_13_01`). SavedLibraryAnalysis
  timed out returning from XML drafts to the designer. Its retained UIA tree shows
  the ALMX editor frame waiting for IntelliSense initialization; this is not proof
  of an analysis semantics failure, but the native gate remains failed until a
  successful rerun or fix. Fixture: `SavedLibraryAnalysis/2022/
  d5be84c8beaf4b8e98f93a2676743ff7`.
- The native package-management audit found that its original packages were only
  restore probes. Expanded the test to build real SDK library packages, verify
  the installed/updated version's imported concrete alarm in compiled output, and
  verify its removal after native uninstall. Validation of the expanded case is
  pending; earlier restore-probe passes do not prove these new assertions.
- Added LibraryExchangeTests coverage that exports only consumer-owned text,
  imports its translation and requires byte-identical resources/artifacts in both
  referenced libraries. `dotnet test
  src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj --no-restore
  --filter FullyQualifiedName~LibraryExchangeTests --verbosity minimal --logger trx
  --results-directory artifacts/log/Debug/Milestone9/Iteration8/LibraryExchange`:
  **2 passed per framework**, no failures/skips (`2026-10-15 22_16_12`). Run with
  no concurrent native IDE tests so the resource-editor safety guard remains valid.

Resumed 2026-10-18 at `f20c3fc`; the former unrelated North working-tree changes
are no longer present. They were not reverted by this task. The previous regression
failure remains historical evidence of the then-current working tree.

The focused native retry has again recorded a SavedLibraryAnalysis timeout at
the XML-draft-to-designer transition, with the same IntelliSense initialization
placeholder (`dbb6aa7f018448379dbd865961e8a545`). The runner remains in cleanup;
this is failed case evidence, not a completed suite result. A managed-stack
capture targeted that fixture's owned IDE to investigate the stall. Capture
`Iteration8/DesignerTimeout/heap-da32b1c9bdc44c3c8d7c233c608c04d3.json`
shows XmlSchemaServiceContext.LazyInitialize waiting on its parse thread and a
DTE main-thread dispatch waiting; the main managed stack is unavailable, so this
does not establish the root cause. After capturing evidence, only the failed
fixture's PID/start-time-matched IDE was stopped to release stalled cleanup.
The runner then reported the designer timeout and continued to the package case.

Added the native `Area=Libraries` measurement driver and both host wrappers:
saved external-library changes through unique consumer diagnostics, and consumer
project removal through diagnostic disappearance, with re-add correctness checks
outside timing. Raw samples, preserved-input/output checks and zero-project memory
samples are required. Ordinary report tests reject wrong workloads, incomplete
modes and a still-loaded project. PowerShell parsing and diff checks pass;
The shared automation helper also compiles through Windows PowerShell Add-Type
against the VS2022 interop assembly. Project compilation, report tests and actual
measurements remain pending. This work does
not yet provide native performance acceptance evidence.

The native retry finished with **0 passed / 2 failed**, no skips, TRX
`addab766068245b3bc9a27ec2d07d434` (`2026-10-18 08_45_36`). In addition to the
designer failure, PackageManagement2022 timed out obtaining the new IDE's DTE
object before creating the fixture project; it did not exercise package behavior.
The failed IDE was cleaned up. Ordinary measurement report validation now passes:
`dotnet test src/VisualStudio/Alarmlist.VisualStudio.PerformanceTests/Alarmlist.VisualStudio.PerformanceTests.csproj
--no-restore --filter FullyQualifiedName~RunReportTests --verbosity minimal
--logger trx --results-directory artifacts/log/Debug/Milestone9/Iteration8/LibraryLiveReports`:
**37 net472 passed**, no failures/skips (`2026-10-18 08_46_43`). This also compiles
the new native wrappers, but does not substitute for native measurement results.

First VS2026 Release library-measurement run (two warmups/two requested samples,
1,000 alarms) reached saved-library diagnostic publication but failed the
zero-open-editor precondition: reopening the template solution restored its
starter editor. Run `a9118b92bcd14fc6ae6ac20ec2d968d5`, TRX `08_48_26`;
this is harness failure evidence, not a performance result. Preparation now
closes restored fixture editors before measurement, clears the old project
automation reference and retains the strict zero-editor check. A corrected native
run is pending; no implementation or acceptance check was relaxed.

Corrected VS2026 harness validation passed **1 case**, no skips (`08_49_07`).
The subsequent Release measurement of both hosts passed **2 cases**, no skips,
TRX `Iteration8/LibraryLiveBoth/…08_50_10_net472.trx`. Command: performance project
`dotnet test --configuration Release --no-build --no-restore --filter
'Host=VisualStudio&Area=Libraries'`, with both host paths and the live opt-in set,
`ALARMLIST_VS_MEASUREMENT_ITERATIONS=5`, `ALARMLIST_VS_MEASUREMENT_ALARMS=1000`.
Each mode retains two warmups and all five measurements; no outliers are removed.

| Host | Saved library publication median / max | Consumer removal median / max | Raw fixture under `artifacts/log/Release/VisualStudioSmoke/` |
| --- | --- | --- | --- |
| VS2022 17.14.36930.0 | 1172.123 / 1205.131 ms | 24.596 / 1016.867 ms | `1d215b6047c14b9baaa3db41b9b6c6a7` |
| VS2026 18.10.12224.181 | 1137.975 / 1140.348 ms | 37.048 / 42.471 ms | `7ff04612d5be471e965b8173b67a5042` |

Validated reports under `LiveVisualStudioTests/e0336ebce820427ea58915a2dfb63b7b`
and `6345d3f2479149aeb47e25bb5cd9e471` confirm both modes, re-add correctness,
preserved inputs/output, zero loaded projects/documents at memory sampling and
owned-IDE cleanup. The 2022 removal outlier remains in the raw data. Timings include
disk writes and shell/COM/polling overhead; removal from the solution is the
measured project-unload operation. Whole-IDE memory observations and these timings
do not prove leak freedom. Native functional failures and the final integrated
regression/audit still prevent iteration completion.

Native VS2026 focused acceptance now passed **2 cases**, no skips, TRX
`1f81ecef471a4e668cda961496940f09` (`08_51_48`): real library package native
install/update/uninstall with compiled-output checks (`0bc00f33d41d4baab33cb8b704e02548`)
and saved-library/private-draft/navigation/unload/recovery behavior
(`120ef5e66ce54d2d9dd643f20732f777`). Both owned IDEs were cleaned up.

An experiment avoiding redundant XML-language assignment did not fix the VS2022
transition failure and was reverted. The run completed with **2 passed / 1 failed**,
no skips, TRX `81910b8d154848719ad7fd9a837bfc16` (`08_54_07`): real package management
and LibraryMode passed, while SavedLibraryAnalysis again timed out at designer
creation. Cleanup completed without intervention.

The retained UI and ActivityLog identify the shell's deferred editor placeholder
and `ReplaceFrameViewOnIntellisenseAvailable`. Inspection of the generated pkgdef
found no explicit editor deferral policy. Following Microsoft's documented
[editor registration guidance](https://devblogs.microsoft.com/visualstudio/async-loaded-net-projects-may-impact-visual-studio-extensions/),
both ALMX and generated-library editor factories now declare
`DeferUntilIntellisenseIsReady=false`: their analysis does not depend on the host's
language-project IntelliSense readiness. The package test verifies this value in
each editor's own registry sections, including repeated sections emitted by
separate registration attributes. The pinned VSSDK exposes this option through
the three-argument factory attribute constructor. Focused package validation
passed **1 test per framework**, no failures/skips (`10_34_15`/`10_34_16`), under
`Iteration8/EditorDeferralRegistration`; the initial test needed to account for
repeated pkgdef sections. Native VS2022 saved-library validation then passed
**1 case**, no skips, TRX `498c13cdcb844925bd58d6a0e3aadea7` (`10_35_22`), fixture
`04f32d0ed52d40a5a779d6edf43ead3a`. The previously failing transition now completes
through private drafts, saved changes, source deletion/restoration and project
unload/reload, with owned-IDE cleanup. Broader native and final solution regression
remain required before iteration completion.

The analyzer-ownership audit regression passed **1 case per framework**, no
failures/skips (`10_36_28`/`10_36_29`): compiler tests with
`--filter FullyQualifiedName~LibraryAnalyzerAndPolicyStayWithOwner`, retained in
`Iteration8/AnalyzerOwnership`. No production compiler change was needed.

Legacy manifest regression passed **2 cases per framework**, no failures/skips
(`10_38_24`/`10_38_25`): task unit tests with
`--filter FullyQualifiedName~VersionOneManifestStillOwnsPreviousApplicationOutputs`,
retained in `Iteration8/LegacyManifest`. No production manifest change was needed.

Further audit inspected LibraryExampleTests (both consumers' transitive parameter,
resource and lookup overrides, then byte-identical project/package output),
LibraryOriginPresentationTests (searchable library identity and unavailable values),
SdkAnalysisBridgeLibraryTests (detached ownership/location maps after domain unload),
and the native saved-library and generated-definition test bodies. Native project
reference management still used a foundation-only Application fixture. It now
creates a real Library containing an abstract template and concrete derived alarm;
after native add/remove and IDE restart, solution builds must include/exclude the
imported alarm and preserve its inherited value. The expanded case is pending
validation; the already-running full VS2022 suite uses the earlier test binary.

That full VS2022 run has recorded a DuplicateBeforeFirstBuild failure while
selecting the native Error List source filter. Fixture
`DuplicateBeforeFirstBuild/2022/ebc6651ea21b4719a72dec4d03c51654` retains the
timeout, screenshot and UIA tree: the expected ALM0001 row is present, but the
source dropdown has no choices. The test helper now reopens an empty native list
while waiting for initialization and still requires the actual requested choice;
no diagnostic assertions or product behavior were changed. Targeted validation
is pending after the current suite completes. This failure prevents claiming a
passing full native regression.

The full VS2022 run finished with **32 passed / 1 failed**, no skips, TRX
`c0d5a0a6cc66467991f68a604961204d` (`2026-10-18 10_36_04`). The only failure
was the empty native source filter described above. All seven library areas
passed, including saved-library analysis with the editor-deferral fix. A targeted
rerun covers DuplicateBeforeFirstBuild, SourceNavigation (also uses the helper)
and the expanded real-library ProjectReferenceManagement case.

The targeted VS2022 rerun passed **3 cases**, no failures/skips, TRX
`9505c7ea20f6446f8c5a9ed70a38d79f` (`2026-10-18 10_46_04`). Command:
`./eng/test-visualstudio.ps1 -VisualStudioPath '<VS2022>/Community'
-RootSuffix AlarmlistExp -Filter
'FullyQualifiedName~DuplicateBeforeFirstBuild|Area=Navigation|Area=LibraryReferences'`.
This validates the native filter initialization correction, all dependent
navigation assertions and real-library add/remove/restart compilation. Combined
with the 32 passing full-run cases, all 33 native VS2022 scenarios now have passing
evidence; the failed initial run remains above. The complete VS2026 suite follows.

The complete VS2026 run subsequently reproduced the empty source-filter failure
(`DuplicateBeforeFirstBuild/2026/0a460d6ef97340f3a9ee9c70fc5fc87d`), so reopening
alone is not a reliable correction. Read-only inspection of the installed shell's
ErrorListCommandTarget shows that its constructor creates all three localized
choices and its command handler returns them; they are not supplied by an alarm
provider. The helper now explicitly activates the Error List tool window through
the public DTE WindowKinds identity before opening/reopening the native selector.
This command-routing hypothesis still requires validation on both hosts; no
private shell API is referenced by the test or product.

The same run exposed an early native NuGet test acknowledgement:
`LibraryPackages/2026/d6e61bb491d1419d977e36d1f4cebab9` passed its project/assets
version check, then Build failed because a restore selected the previous version.
The retained UI still reports installed 1.0.0 while the saved project requests
2.0.0. The helper now also waits for the native package row to acknowledge the
requested installed version (or the Install button after uninstall), before
saving/building. The resolver correctly rejected the inconsistent graph and is
unchanged. This expanded operation-completion check is pending native validation.

Full VS2026 result: **24 passed / 9 failed**, no skips, TRX
`fd196cc2470b4bc5a0d702795bf9936e` (`2026-10-18 10_48_41`). Besides the two
failures above, LibraryProperties timed out focusing its native Library filename
editor (`0ae7617322a3440395cd75fab260634c`). Its failure capture encountered rejected
COM calls; six later cases refused startup while that owned IDE was still exiting.
The fixture's cleanup record confirms forced termination of its owned IDE, and a
subsequent process inventory confirmed no remaining devenv process. A diagnostic
capture for its verified PID/start time is retained under
`Iteration8/PropertyFocusTimeout/heap-54d284fc08dd43d89e6cabf2b8063452.json`; its
main managed stack is unavailable, so it does not establish the focus failure's
cause. No external IDE was stopped and no property behavior was changed.
A ten-case rerun covers all nine failures plus SourceNavigation, which shares the
updated source-selector helper. The new helper code compiles; execution is pending.

Within that rerun, native package management passed with the installed-state wait
(`LibraryPackages/2026/01c4740d8c0b48548d801ab355a96de0`). Error List pane activation
alone still failed (`DuplicateBeforeFirstBuild/2026/72e1c9cdf7984e559ff3cd1c6038542b`),
and LibraryProperties reproduced the same filename-editor focus timeout
(`000924b870de4229a05c12a4e1882a21`). The selector investigation now additionally
focuses the actual diagnostic table before opening the toolbar combo; that change
has not yet run. Failure capture now caches the real shell HWND during startup and
captures the UI before DTE build output, allowing modal/RPC-rejected failures to
retain their actual screenshot/tree. This is diagnostic harness work, not a claim
that the property failure is fixed. The remaining cases in the rerun are ongoing.

That rerun finished **7 passed / 3 failed**, no skips, TRX
`a4149ff6310f45c49fe72b6a9679acd0` (`2026-10-18 10_57_56`). Package management,
library mode, selected/table previews, list/reference completion and source
navigation passed. DuplicateBeforeFirstBuild and LibraryProperties remain failed.
ProjectPreview also timed out finding its project node, although its retained
actual-shell UIA tree/screenshot contains that node
(`7a8744f321564c95ad32b6ff14e55438`). Its process-based main-window lookup can
select a floating preview; it now uses the cached actual shell HWND. A four-case
run validates these three outstanding cases and SourceNavigation with the current
table-focus helper and improved failure capture.

The four-case run finished **2 passed / 2 failed**, no skips, TRX
`ed7330695a384397abe8dd6478b5382f` (`2026-10-18 11_04_15`). DuplicateBeforeFirstBuild
passed with table focus, and ProjectPreview passed with the actual shell HWND.
SourceNavigation exposed that the table cannot always receive focus after Build;
the helper now requests table focus only for an empty dropdown and only for a
visible, enabled, keyboard-focusable table. Existing populated choices still use
their native SelectionItem pattern.

The improved LibraryProperties capture
(`a783bdfd49d2455b88e3a8e68993dbc7/failure.png` and `automation.txt`) shows the native
"save changes made in property pages" prompt over the filename editor, with the
two identity values still pending. The test now uses the native Apply button and
waits for it to become clean after editing Package ID/version, before editing the
configuration-scoped filename. All subsequent saved-project, reopen and
Application/Library build-output assertions remain. A three-case VS2026 run is
validating the final selector and Apply workflow; neither is yet a passing gate.

The final three-case VS2026 rerun passed **3 cases**, no failures/skips, TRX
`cb3a6d7d6d28406bbeea846e7ba78e97` (`2026-10-18 11_07_54`): SourceNavigation,
LibraryProperties and DuplicateBeforeFirstBuild. Native Apply persists the identity
before filename editing; the original reopen and output-mode transitions pass.
The conditional table-focus recovery also passes both source-filter callers.
Together with the full run's unaffected cases and the preceding targeted passes,
all 33 VS2026 scenarios now have passing evidence. Earlier failed runs remain
recorded; they are not described as passing full suites. A five-case VS2022 run
revalidates the changed selector/navigation, native package acknowledgement,
property Apply workflow and project-preview window selection.

The five-case VS2022 rerun passed **5 cases**, no failures/skips, TRX
`e1e08b9025c04434aaf63c7f493f3ee6` (`2026-10-18 11_09_14`). All changed native
helpers and their affected workflows pass on both supported hosts. Final TRX
aggregation confirms all 33 scenario names per host have passing evidence from
the full and targeted runs; the original failed suites remain distinct evidence.
`git diff --check` passes. The full solution regression is now running under
`Iteration8/FinalRegression`, without a concurrent native IDE or measurement run.

VSIX/package regression now explicitly checks the generated-definition editor
registration in the packaged pkgdef. `dotnet test
src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj
--no-restore --verbosity minimal --logger trx --results-directory
artifacts/log/Debug/Milestone9/Iteration6/PackageRegression`: **12 net472 and
12 net8.0 passed**, no failures/skips (`21_31_28`). `git diff --check -- src`
also passed; Git reported only existing LF/CRLF conversion warnings.

### Final acceptance and requirement closure (2026-10-18)

The chronological evidence above retains failed experiments and checks that were
pending at the time. This closure supersedes those pending statuses; it does not
reclassify failed runs as successful.

Final solution command:
`dotnet test AlarmlistProject.slnx --no-restore --verbosity minimal --logger trx
--results-directory artifacts/log/Debug/Milestone9/Iteration8/FinalRegression`.
The command completed successfully. Its 13 suite/framework reports contain
**2,173 passed, zero failed, 84 opt-in skipped**:

| Suite | net472 passed | net8.0 passed |
| --- | --- | --- |
| Compiler | 619 | 621 |
| MSBuild task unit | 85 | 85 |
| MSBuild integration | 4 | 4 |
| Packaged SDK consumers | 76 | 76 |
| VSIX/package integration | 13 | 13 |
| Editor/bridge | 524 | — |
| Ordinary performance harness checks | 53 | — |

The 66 native and 18 performance opt-in skips are not acceptance evidence. Native
acceptance uses the full/targeted runs recorded above: aggregation of the latest
result for every scenario confirms **33 distinct passed scenarios per host**.
The changed project-reference test uses actual inherited library output; package
management uses actual packed libraries and checks compiled values after install
and update, then absence after removal. Changed shared UI helpers were revalidated
on both hosts. Separate isolated and live library measurements passed as recorded
above, including both native hosts with two warmups and five retained samples.

Final desktop SDK command used the same SDK test project with `--framework net472
--no-restore --verbosity minimal --logger trx --results-directory
artifacts/log/Debug/Milestone9/Iteration8/FinalDesktopSdk`, setting
`ALARMLIST_TEST_MSBUILD_PATH` to the installed VS2022 desktop MSBuild for the run
and restoring its previous value in `finally`. The process has exited; the
`2026-10-18_11_14_25_net472.trx` report confirms **76 passed, zero failed/skipped**.
No native IDE test or measurement overlapped these SDK/XLIFF checks.

Final audit closes every row of the initial requirement audit against the current
implementation, inspected test assertions and the passing results above:

| Requirement group | Completion evidence |
| --- | --- |
| Deterministic versioned artifacts, raw inheritance/groups/expressions, scopes, portable origins, abstract/concrete alarms | LibraryArtifactXmlTests, LibrarySourceTests and LibraryBuilderTests cover round trips, immutability, version/malformed-input rejection and retained definitions; final compiler suites pass. |
| Parameter precedence, explicit empty, consumer-derived isolation, consumer project identity and portable source identity | LibraryCompilationTests checks each scope and prohibited physical-path expressions; packaged AnalysisContract and bridge tests retain detached ownership/provenance. Final compiler, SDK and editor suites pass. |
| Deferred library values and strict application validation | LibraryDeferredValidationTests and native LibraryMode cases pass; library analysis exposes unavailable values without final XML, while applications require final values. |
| Graph order, diamonds, cycles, conflicts and global alarm identities | LibraryGraphTests plus compiler binding tests, LibraryProjectReferenceTests, LibraryPackTests and PackageRestoreTests cover compiler and mixed project/package graphs. Both SDK frameworks and desktop MSBuild pass. |
| Resource precedence, per-culture fallback and whole-unit lookup overrides | LibraryResourceTests and LibraryLookupTests cover conflicts, explicit overrides, empty/whitespace semantics and replaced path/entry/binding units. LibraryExampleTests verifies consumer-specific parameter/resource/lookup output and byte-identical project/package conversion. |
| Owner analyzers/configuration and consumer analysis | LibraryAnalyzerAndPolicyStayWithOwnerWhileConsumerAnalyzerSeesCombinedModel passes on both frameworks; owner suppression does not propagate and consumer rules see imported/local alarms. |
| Transactional output, legacy manifests, configuration/build ordering, no-build discovery and input-safe lifecycle | LibraryBuildTests (including version-1 migration and Clean), LibraryProjectReferenceTests and packaging/lifecycle suites pass on both runtimes, including desktop task loading. |
| Fresh/offline NuGet restore, selected graph, exact package dependencies, failed Pack preservation and reference-only consumers | PackageRestoreTests and LibraryPackTests exercise real isolated packages, mixed graphs, warm-cache offline restore, failure preservation and unrepresentable dependency rejection. Native install/update/remove and reopen workflows pass on both hosts. |
| Saved graph without Build, unloaded/out-of-solution dependencies, private dependency drafts and consumer-buffer precedence | SavedProjectEvaluationTests, SavedProjectGraphTests, CapturedLibraryProjectTests and SdkAnalysisBridgeLibraryTests cover saved captures, sentinels, imports/membership/configuration, failed recapture recovery and race revalidation. Native saved-library cases pass on both hosts through drafts, navigation, save, deletion/restore and unload/reload. |
| Analysis 5/analyzer 3, detached data and host isolation | Compiler analyzer contract tests, packaged AnalysisContract, SdkAnalysisBridgeTests and library bridge tests pass. Legacy contracts fail explicitly; NuGet/compiler implementations remain in the selected SDK AppDomain. |
| Completion, imported previews, local navigation and read-only generated package views | LibraryOriginPresentationTests and AnalysisNavigationTargetTests verify origins, unavailable values and generated maps. Native library/source-navigation/definition cases verify actual views, blocked edits, unchanged artifacts and lifecycle on both hosts. Generated editor buffers retain detached text/location data, not project analysis state. |
| Templates, properties, examples, docs and XLIFF boundaries | Native creation/property cases pass on both hosts, including identity Apply, reopen and output-kind transitions. LibraryExampleTests covers both consumers and transitive overrides. ConsumerTranslationExchangeLeavesImportedLibraryResourcesAndArtifactsUnchanged passes on both SDK frameworks and desktop MSBuild. SDK/extension documentation and the backlog identify the implemented scope. |
| Capture, saved refresh, preview and unload performance | Four isolated bridge measurements, two actual WPF preview measurements and native saved-refresh/project-removal measurements on both hosts passed their correctness checks; raw samples and environment/revision metadata are retained at the paths above. |

No essential Milestone 9 implementation or acceptance gate remains open. The
existing framework-independent consumer, net472/net8.0 compiler/task and net472
extension boundaries are preserved. Both requested branches exist; implementation
commits are on `dev/milestone-9`. No merge or publication was performed.

Performance results are observations for the recorded machines/workloads, without
machine-dependent thresholds. Whole-IDE memory samples and project-removal timings
do not establish leak freedom. Historical vendor/manual/release gates in earlier
milestones remain outside this milestone, as do Shared Projects, XML IntelliSense,
syntax generation and legacy bridge adapters. Raw generated evidence remains in
ignored artifacts; it is not committed as source. Final documentation changes need
diff/link consistency review, not another unchanged-code test run.

### Iteration 9 — example shortcuts (complete 2026-10-18)

Deliver two scripts opening the existing reusable-library solution, one with
ProjectReference and one with PackageReference. Share source projects and use
`UseLibraryPackages` to switch references. Preparation persists the selected mode
for IDE evaluation, packs the local libraries before package restore and avoids
stale same-version cache contents. A shared launcher deploys the matching extension
and supports both IDE hosts. No publication or duplicated example sources.

Depends on iterations 1–8. Validate both consumers' equivalent output, transitive
package dependencies and switching back; run the affected SDK example/exchange
tests on both frameworks. Check PowerShell/batch syntax and prepare both modes
with the real built SDK. Review docs and commit after these checks pass.

Implemented `scripts/start-library-project-references.bat` and
`scripts/start-library-packages.bat`, sharing `eng/start-library-example.ps1`.
Both open the existing Libraries.sln; VS2026 is the default, with
`-VisualStudioVersion 2022` and `-PrepareOnly` supported. The existing four projects
condition references on `UseLibraryPackages`, persisted in ignored
`LibraryExample.local.props`. Package preparation packs Common/Equipment using
project references, then restores package mode into a fresh cache so repacked
fixed-version examples do not reuse stale payloads. Sources are not duplicated.

Validation evidence:

- `dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
  --no-restore --filter 'FullyQualifiedName~LibraryExampleTests|FullyQualifiedName~LibraryExchangeTests'
  --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone9/Iteration9/ExamplesFinal`: **3 passed per
  framework**, zero failures/skips. Both consumers produce identical XML in both
  modes and after switching back. Assets identify both transitive dependencies
  as package/project appropriately; XLIFF isolation still passes. An initial test
  incorrectly assumed project identities disappear from assets; it failed on both
  frameworks and was corrected to assert NuGet's actual dependency type.
- Both batch shortcuts with `-VisualStudioVersion 2022 -PrepareOnly` completed
  successfully, exercising real extension deployment and Windows PowerShell
  preparation. The initial nested preparation could not resolve Get-FileHash;
  SHA-256 now uses the framework stream API, and both shortcuts passed afterward.
- `dotnet msbuild example/Libraries/Libraries.sln /t:Build /nr:false /v:minimal`
  succeeded with the prepared package mode. PowerShell parsing, ignored local
  configuration, diff and documentation links were checked.

No interactive IDE was opened by these checks. VS2026 deployment/opening was not
rerun because the user's experimental instance was already open on North/South;
that instance and its example sources were left untouched. The launch path uses
the existing experimental-profile/solution command pattern. This follow-up changes
example preparation and shortcuts, not compiler/SDK/extension behavior; no full
solution or native functional suite was repeated.

## Follow-up plan: Solution Explorer dependencies (2026-10-18)

### Intended outcome and scope

The owner requests project references and NuGet packages used by an Alarmlist
project to appear in Solution Explorer like a .NET project's dependency tree.
The supplied screenshot establishes the grouped native-tree presentation.
The following details are the proposed implementation direction, not evidence of
working behavior or approval of additional compiler features.

```text
LineA
  Dependencies
    Projects
      Equipment
    Packages
      Example.Equipment (1.0.0)
```

The example illustrates both possible groups; actual entries follow evaluated
references in the active configuration. In the library playground, project mode
shows Equipment under Projects, while package mode shows Example.Equipment under
Packages. Use native/localized labels and icons where supported (for example,
Abhängigkeiten, Projekte, Pakete in German). Do not add net472/net8.0 groups:
framework-independent `.almproj` consumers do not target those task runtimes.
Do not add empty .NET Frameworks/Assemblies groups merely to copy the screenshot.

Confirmed scope is visibility of used project references and NuGet packages.
Proposed related behavior: package version and unresolved-state presentation,
refresh after reference edits/restore/configuration changes, and existing native
reference-management actions from appropriate nodes. Direct references must be
distinguishable from transitive dependencies; any transitive expansion is read-only
and must use the already selected graph, never a package-cache scan. Package nodes
must remain visible as unresolved when restore fails, rather than disappear.

No compiler/artifact semantic changes, new TargetFramework requirement, automatic
Build/Pack on tree expansion, private NuGet host loading, or new package-management
implementation. Preserve existing Add Project Reference and NuGet workflows.

### Inspection and architecture

Current `Alarmlist.DesignTime.targets` declares ProjectReferences,
ReferenceManagerProjects and PackageReferences, and registers the corresponding
XAML rules. `PackageReference` already participates in ProjectSubscriptionService;
`ProjectReference` currently uses Project/BrowseObject contexts. These declarations
and the successful native dialogs do not prove grouped dependency-tree support.
The extension pins CPS SDK 17.9.380 and remains net472.

Use the [.NET project-system reference](https://github.com/dotnet/project-system)
to inspect how CPS tree composition, dependency providers and evaluation/restore
updates work. Its [dependency-tree issue 9415](https://github.com/dotnet/project-system/issues/9415)
also shows why successful restore alone is insufficient evidence of tree visibility.
First verify which public extension points are available in the pinned CPS SDK
and on both supported IDE hosts. Prefer supported CPS composition. Do not turn on
unrelated .NET capabilities to activate a provider with unmet language/runtime
assumptions. If Alarmlist needs its own CPS tree provider, document that compatibility
reason and reuse detached saved-graph/package-resolution data rather than introducing
a second resolver. Keep callbacks cancellable and detach subscriptions on unload.

### Independently reviewable iterations

| Iteration | Deliverable and dependencies | Validation and completion criteria |
| --- | --- | --- |
| 10 | Native dependency root and project-reference group. Depends on iteration 9. Probe public CPS extension points, select/document the supported integration, publish evaluated direct project references with stable identities and missing-project state. Wire relevant existing add/remove commands without duplicating project items. | Rule/provider tests for conditions, duplicate prevention, missing/unloaded references and disposal; package/rule regression; native VS2022 and VS2026 checks for visible root/group/children, add/remove, reopen and no Build on expansion. Finish only when actual host presentation works on both hosts. |
| 11 | NuGet package group and resolution state. Depends on iteration 10. Display direct package IDs, requested/resolved version information and pending/unresolved state. Integrate selected restored dependency data; expose supported read-only transitive expansion without treating transitives as direct editable references. Connect existing NuGet management commands. | Tests for install/update/uninstall, failed restore/recovery, version selection, condition/configuration changes and stale-result suppression; assert no NuGet implementation assembly enters the VS host. Native both-host checks verify package nodes and versions before/after restore and removal, including transitive Example.Common if expansion is provided. Project group remains usable. |
| 12 | Integrated lifecycle and library-playground acceptance. Depends on iteration 11. Validate the two shortcut modes, mixed project/package projects, localized presentation and cleanup; document actual supported actions and limitations. | Unit/package regression for affected components and native both-host scenarios: project/package switching, reload, dependency deletion/recovery, configuration change, keyboard expansion/selection and available context actions. Verify no duplicate nodes, no stale children after unload/reopen, and no source/output writes merely from browsing. Update this plan with commands, artifacts and results, and commit the completed iteration. |

### Decisions to resolve during the integration probe

- Exact public CPS tree/provider contracts and capabilities usable on both hosts;
  do not promise API names based only on upstream main.
- Whether native providers supply empty-group hiding, localization, warning icons,
  transitive expansion and browse properties, and what minimal Alarmlist adapter
  is needed. Keep direct dependency visibility mandatory regardless of provider.
- Map available node commands to existing supported workflows. Do not silently
  enable removal of transitive dependencies or implement broader package editing.

Planning-only validation: inspected current capabilities/rules, reference capture
boundaries and pinned extension packages; reviewed the plan and local links.
No implementation or tests run for iterations 10–12. The existing user edit to
`example/Libraries/LineB/LineB.almproj` is outside this planning change.

### Iteration 13 — resolved alarm-list names (complete 2026-10-18)

Independent of planned iterations 10–12; follows the owner's display correction.
Use the same current compiler analysis and preview culture as the editor to label
alarms in its list/compact selector. Fall back to raw expressions, identity, then
the existing unnamed label when resolution is unavailable. Search both displayed
names and raw source/identities. Refresh on analysis/culture/input changes without
committing or discarding drafts or retaining stale translations. Match declarations
by physical source location, not potentially duplicate alarm identities.

Validate localization, inherited/expression names, culture/filter updates, stale
inputs, unavailable values and draft/selection preservation in editor tests; run
the editor unit suite and record results before committing. No compiler change.

Implemented current-analysis list titles and shared compact-selector labels,
including inherited names. Declaration matching checks captured text/version and
physical location, scanning source positions once for the list. Filtering includes
resolved labels, raw local names and identities; asynchronous updates retain item
instances and keep a selected private draft visible without reloading its fields.
Documentation and repository guidance describe the new presentation.

Focused validation (`--filter 'FullyQualifiedName~AlarmListNames|FullyQualifiedName~AlarmListRefresh|FullyQualifiedName~LocalizationBaml'`)
passed 6 cases under `Iteration13/Focused`. The first full editor regression
finished with 525 passed and 4 failed: two existing navigation/table tests exposed
automatic reselection after explicit deselection, and two accessibility cases
still expected the old identifier instead of the now-resolved inherited label.
The refresh now distinguishes an empty filter result gaining items from an
explicitly deselected populated list. Accessibility asserts the resolved label in
both wide and compact layouts. Rendered WPF tests also verify culture changes
update the visible list text. Final full-suite validation is pending; no native
deployment or user source change is part of this fix.

Final validation: `dotnet test
src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
--no-restore --verbosity minimal --logger trx --results-directory
artifacts/log/Debug/Milestone9/Iteration13/EditorRegressionFinal` completed with
**529 passed, zero failed/skipped** (`2026-10-18_22_07_02_net472.trx`). This includes
rendered WPF localization refresh and wide/compact accessibility, inherited names,
all XML newline forms, unavailable values, filter re-entry, drafts, stale inputs,
explicit deselection and existing navigation/lifecycle tests. Diff and local
documentation links pass review. Native IDE suites and the full solution were
not rerun for this editor-only change; the built VSIX has not been redeployed into
the user's running instance. The unrelated LineB project edit remains untouched.

### Iteration 14 — library example RESX editor compatibility (complete 2026-10-18)

The owner reports that opening Common, Equipment and LineA's `Labels.en.resx`
in Visual Studio fails with invalid ResX input / missing valid reader and writer
resheader tags. Inspection confirms all three files contain only root/data/value
elements. The shipped Translations.resx item template and North/South resources
already contain the standard resmimetype, version, reader and writer headers.

Read-only reproduction: Windows PowerShell loaded System.Windows.Forms and
enumerated each file with ResXResourceReader and UseResXDataNodes=true. All three
failed with the same missing-reader/writer-header message shown in the screenshot.
The compiler's Text/ResxReader accepts the string data and explicitly skips headers;
successful compilation therefore did not establish native resource-editor
compatibility. No source files were changed by this probe.

**Deliverable:** make these three checked-in example resources readable and
editable in the native resource editor without changing library localization
semantics. This is independent of planned dependency-tree iterations 10–12 and
depends only on the existing library example. Preserve the user's LineB project edit.

Implementation plan:

1. Add the XML declaration and four standard RESX headers to Common, Equipment
   and LineA's Labels.en.resx, following the existing translation item template.
   Preserve the Inspection keys, xml:space attributes and values exactly:
   `Inspect $(Site)`, `Check $(Site)` and `Inspect line $(Site)` respectively.
   Inspect the remaining checked-in example resources for the same omission;
   keep any additional repair limited to actual native-editor compatibility.
2. Add a Windows/.NET Framework compatibility regression that reads the actual
   copied playground resources through ResXResourceReader and verifies string
   keys/values. Follow the existing test asset-copy workflow; do not test only a
   separately synthesized resource fixture or only header presence. Keep native
   RESX APIs confined to tests, not the compiler or cross-runtime tasks.
3. Run LibraryExampleTests and LibraryExchangeTests on both supported frameworks
   to confirm project/package equivalence, translation overrides and XLIFF input
   protection remain intact. Repack the example when checking package consumption;
   a previously cached package is not evidence for the repaired files.
4. Using isolated copies of the real example files and the existing native test
   harness, open each resource on VS2022 and VS2026. Verify the resource grid shows
   Inspection and the original value, edit/save/reopen a value in the fixture, and
   verify that `$(Site)` remains literal in saved RESX. Do not edit the user's
   persistent example or close their existing IDE. Run these checks sequentially.

Completion criteria: framework reader regression, both-framework example/exchange
tests and native open/edit/save/reopen on both hosts pass; original resource values
and resulting consumer alarms remain unchanged by the header-only repair. Record
commands/results and any host blocker here before committing the successful
iteration. No new RESX format, compiler deserialization, global migration or
automatic rewriting of user resources is authorized by this fix.

Planning validation: reviewed the three resources, the existing template/shared
resource headers, compiler header handling and native localization test harness;
the read-only reader probe reproduced all three failures. No automated test suite
or implementation change was run in this planning step.

Implementation and validation (2026-10-18):

- Added the XML declaration and four standard RESX headers to Common, Equipment
  and LineA, preserving Inspection, xml:space and all original translation values.
  Auditing the other example resources found no additional missing headers.
- Added three net472 native-reader regressions over the copied real playground
  resources. System.Windows.Forms is a test-only framework reference; compiler
  parsing and runtime contracts are unchanged.
- Added LibraryExampleResources native scenarios using copies of those same files.
  Each checks the key/value grid, keyboard editing, save/reopen and literal
  `$(Site)` preservation. Only isolated fixture resources are edited.
- `dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
  --filter 'FullyQualifiedName~LibraryExampleTests|FullyQualifiedName~LibraryExchangeTests'
  --verbosity quiet`: **3 passed net8.0; 6 passed net472**, no failures/skips.
  The example test freshly packs both libraries and compares consumer output
  bytes across project/package/project modes.
- `eng/test-visualstudio.ps1 -VisualStudioPath <host> -Filter
  'FullyQualifiedName~LibraryExampleResources'`: **1 passed per host**, sequentially
  on VS2022 and VS2026 using AlarmlistExp. TRX runs
  `bee8afd9245e420e856d5c6a606d6b5a` and `ab88e441239346d0a867c8df40d17a1a`.
  Each run covers all three resources; screenshots remain in its scenario logs.
  The user's main VS2022 process was preserved. All iteration-14 gates passed.

### Iteration 15 — usable XML keyboard editing (planned 2026-10-18)

The owner reports that the ALMX XML view accepts inserted text, but deletion and
Enter/newline do not work. Restore ordinary XML editing in the embedded editor.
This fix is independent of planned iterations 10–12 and 14; preserve completed
work and the unrelated user edit to LineB.almproj.

Inspection finds an embedded VS code window/text view in AlmxEditorPane. The pane
implements IOleCommandTarget, intercepts designer Return/clipboard/Undo commands,
and forwards other QueryStatus/Exec calls to the code window. SplitEditorControl
also participates in designer Return handling. This makes focus/command routing
a diagnostic candidate, not a proven root cause. Several native scenarios use
VisualStudioAutomation.ReplaceXmlBuffer; successful programmatic replacement does
not prove that physical Backspace, Delete and Enter reach the text editor.

**Deliverable:** typing, character/selection deletion and newlines work normally
in writable ALMX XML panes, including after switching between Designer, Split and
XML modes. The existing shared-buffer, Undo, save and designer-draft contracts
remain intact. Generated package-definition documents must remain read-only.

Implementation and validation plan:

1. Reproduce in an isolated native IDE fixture on VS2022 and VS2026. Focus the
   actual XML text surface and use real keyboard input to insert a marker, press
   Backspace, forward Delete, delete a selection and press Enter. Observe caret,
   selection and captured shared-buffer text after each operation. Test both XML
   mode and the XML pane in Split mode, including focus round trips to a designer
   field and a shell window. Record native evidence and command results; compare
   with Open With > XML (Text) Editor if needed to isolate hosting from language
   service behavior. Never reproduce by modifying the user's current document.
2. Trace QueryStatus/Exec and keyboard preprocessing for the failing commands,
   including VSStd2K RETURN/BACKSPACE/DELETE and relevant standard edit commands.
   Verify the active text-view command chain, editor focus, read-only state and
   whether the host reports success without executing a command. Select a fix
   from observed evidence using supported VS editor/CPS contracts. Scope designer
   handlers to their actual focused controls; keep QueryStatus free of edits.
   Do not implement ad hoc string deletion/newline insertion as a substitute for
   repairing the native text editor's command path.
3. Add regressions at the affected command/focus boundary and native keyboard
   scenarios covering insertion, Backspace/Delete with and without selection,
   Enter, multiline selection replacement, Cut/Paste, Undo/Redo and save/reopen.
   Assert the actual buffer and persisted text, not only keystroke delivery or
   absence of exceptions. Retain normal XML indentation behavior rather than
   hard-coding one whitespace layout. Include a well-formed final XML state and
   verify the designer reflects it without losing source comments/expressions.
4. Validate designer Enter/Shift+Enter, private drafts, mode switching and shared
   document Undo so the XML repair does not redirect designer keys into the source.
   Verify read-only generated library views still reject editing. Run the editor
   unit suite plus affected native editor/lifecycle/definition scenarios on both
   hosts sequentially, using owned fixtures and preserving open user IDEs.

Completion criteria: physical keyboard deletion and newline operations pass in
both embedded layouts on both supported hosts; insertions, selection, Undo/Redo,
save/reopen and designer synchronization work; designer field semantics and
read-only views remain protected. Record reproduction, root cause, final changes,
commands/results and any essential host blocker before declaring this iteration
complete and committing it. No compiler/language-format change or XML IntelliSense
feature expansion is included.

Planning validation: read editor initialization, command forwarding, designer key
handling and the existing XML automation approach; reviewed this plan and paths.
No native reproduction, implementation change or automated test suite was run in
this planning step. The user report is not yet attributed to a specific handler.

User-supplied reproduction evidence: `20261018-2020-10.0108382.mp4`, attached to
this conversation and explicitly identified by the owner as evidence for this
XML editing issue. The attachment is accessible (5,914,805 bytes), but its video
contents have not been independently reviewed here; no video decoder was available
on PATH during intake. Do not infer specific keystrokes or a root cause from file
presence. Review the recording alongside the native reproduction in step 1.
The recording and its machine-specific temporary path are not repository assets.
