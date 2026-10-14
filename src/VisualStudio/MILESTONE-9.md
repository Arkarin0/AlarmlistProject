# Milestone nine: reusable alarm libraries

Status: iterations 1–2 complete (2026-10-14); 3–8 planned.
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
