# Milestone nine: reusable alarm libraries

Status: iteration 1 complete (2026-10-13); iterations 2–8 remain planned.
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
