# Agent instructions

## Scope and working approach

These instructions apply to the entire repository. Follow any more specific
`AGENTS.md` in a directory you change, and honor the user's task instructions.

- Check `git status --short` before editing and preserve unrelated changes.
- Read the implementation, nearby tests, and relevant project files before
  changing behavior. Keep changes focused; avoid unrelated formatting or renames.
- Treat the checked-in source and build configuration as authoritative. Update
  this guide when project structure or supported workflows change.
- When the user gives a new durable guideline or corrects an earlier decision,
  check whether it is documented and update the relevant instructions in the same
  iteration. Revise superseded guidance rather than leaving contradictory rules.
  Keep milestone-specific scope and acceptance decisions in the milestone plan,
  and link them here when they affect future work across the repository.
- Never add credentials, machine-specific paths, or generated build outputs.
- Report what changed, which checks ran, and any checks that could not run.

## User documentation

- Keep user-facing feature descriptions in `documentation/features/` and guided
  tutorials in `documentation/tutorials/`, written in English.
- Use the corresponding `TEMPLATE.md` for new articles and follow
  [the documentation guide](documentation/README.md). Replace placeholders,
  remove unused optional sections, and verify examples against supported behavior.

## Milestone planning and iteration workflow

Plan milestone work as numbered, independently reviewable iterations before
implementation. Maintain the plan as work proceeds, following the existing
`src/VisualStudio/MILESTONE-<number>.md` convention for Visual Studio milestones.
Extend the existing milestone document for follow-up work instead of creating a
competing plan or renumbering completed iterations.

1. Record the milestone's intended outcome, confirmed scope, exclusions, design
   decisions, and open questions. Consult the architectural references below.
2. Give each iteration a concrete deliverable, dependencies, relevant validation,
   and completion criteria. Keep the compiler, SDK, and extension usable together
   at iteration boundaries. Include tests and documentation with the behavior
   they cover instead of postponing all validation to the final iteration.
3. Implement and validate one iteration at a time. Update the remaining plan when
   user decisions or findings change scope; preserve completed results and make
   changed assumptions explicit.
4. Record actual changes, validation commands/results, and remaining limitations
   in the milestone document. Keep planned acceptance separate from evidence.
   If an essential check fails or cannot run, record the blocker and do not call
   the iteration successful. Keep separately deferred manual, vendor, or release
   gates visible even when the scoped implementation checks pass.
5. **Commit after each successful iteration, before starting the next**, unless
   the user explicitly asks to defer commits. Include its implementation, tests,
   plan/results, and relevant instruction updates in a focused commit. Review
   the diff and stage only that iteration's files or hunks; preserve unrelated
   working-tree and staged changes.
6. **Show a short summary after each iteration's commit**: iteration identifier,
   completed behavior, validation result, commit hash, and any remaining blocker
   or next step. Continue already-authorized iterations without waiting for
   another confirmation; ask only when a required decision remains unresolved.

For documentation-only iterations, review the diff, links, paths, and consistency;
a full build is normally unnecessary. State that tests were not run. Do not
describe an iteration as committed until Git confirms the commit succeeded.

Existing plans and iteration records:

| Plan | Scope |
| --- | --- |
| [Milestone 14](src/VisualStudio/MILESTONE-14.md) | Explain this value: compiler-owned chosen contributors, read-only designer/Summary explanations, opt-in capture and analysis-8 SDK transition. |
| [Milestone 2](src/VisualStudio/MILESTONE-2.md) | Shared ALMX document, designer/XML editing, and editor lifecycle. |
| [Milestone 3](src/VisualStudio/MILESTONE-3.md) | Reference-aware editing, analysis, diagnostics, previews, and lifecycle/performance follow-ups. |
| [Milestone 4](src/VisualStudio/MILESTONE-4.md) | Property expressions, parameters, abstract alarms, and designer support; see its [design contract](src/VisualStudio/MILESTONE-4-DESIGN.md). |
| [Milestone 5](src/VisualStudio/MILESTONE-5.md) | Localization and XLIFF, including HMI lookup, named-list, and preview follow-up iterations; see its [design contract](src/VisualStudio/MILESTONE-5-DESIGN.md) and [lookup specification](src/VisualStudio/HMI-LOOKUPS.md). |
| [Milestone 6](src/VisualStudio/MILESTONE-6.md) | EditorConfig, diagnostic policy, external analyzers, and compatibility/lifecycle validation iterations. |
| [Milestone 7](src/VisualStudio/MILESTONE-7.md) | ALMX editor UI/UX: procedures, property fields, IntelliSense, searchable references, file/alarm scope separation and preview-table rendering/column preferences; accepted direction and open interaction details. |
| [Milestone 13](src/VisualStudio/MILESTONE-13.md) | Export providers: Library/Exports project purpose, selectable XML/JSON/Zenon, external packages/DLLs and transactional artifacts. |
| [Milestone 12](src/VisualStudio/MILESTONE-12.md) | Authorized XML IntelliSense for ALMX, .almlookup and .almproj files, and native Visual Studio Shared Projects; follow its accepted defaults and 15% remaining-allowance stop. |

Read the selected plan's current status and remaining gates before resuming it.
Milestone 14 activates analysis contract 8 for the complete opt-in ExplainSnapshots
API and detached SDK bridge. Keep analyzer 5, library format 3, export contract 1
and semantic definition fingerprints unchanged. Ordinary analysis/builds retain no
explanation graphs. Capture chosen contributors in compiler evaluation; never
reconstruct semantics in the host. Entry targets carry defining source/version,
not effective indices; revalidate captured inputs before publication/navigation.
[Milestone 12](src/VisualStudio/MILESTONE-12.md) uses the native XML completion
presenter with host lexical context and generated descriptors; compiler-owned
expression/reference semantics remain in detached analysis. Native XML completion
shares detached candidate construction with designer controls. Embedded XML borrows
the designer's selected analysis context; standalone XML must not choose among
multiple owners. Validate current saved inputs before displaying or inserting
semantic candidates, and keep request snapshot/caret/selection stamps through
asynchronous insertion. Native semantic acceptance uses `Area=XmlSemanticCompletion`
through `eng/test-visualstudio.ps1`, separately from definition-shape checks.
Lookup XML hints use the embedded authoritative XSD; project hints use embedded
SDK rules. Keep discovery extension-local, preserve native XML/MSBuild command
fallback and schema choices, and never change machine-wide schema catalogs.
`Area=XmlCatalogCompletion` validates installed lookup/project hints without
opening an XSD; `NativeSchemaBaseline` uses ordinary XML to test native tooling
independently. Run these fixtures sequentially with other native/WPF suites.
Milestone 12 default-playground acceptance uses `Area=DefaultSharedExample` and
the real preparation script on edited isolated copies. Preserve shared membership
and consumer-specific analyzer scopes; the shared container must have no analyzer
or compiler pipeline. `Area=SharedExampleAcceptance` covers the focused playground;
`Area=XmlCompletionPerformance` records first-use and 20 warm structural requests
per XML surface with 1,000 alarms. Keep native 100 ms polling overhead explicit,
use descriptive p50/p95 reports without latency thresholds, and validate stale
insertion/cancellation separately. Never infer collectibility from process memory.
Native XML completion fork acceptance uses
`eng/test-alarm-collection-fork.ps1 -Designer -SkipConsumers
-XmlCompletion` with both IDE paths. Run it sequentially with native fixtures in
an idle experimental profile; it checks procedure-enabled/disabled definitions.
[Milestone 11](src/VisualStudio/MILESTONE-11.md) plans fork-time generated alarm
definitions across compiler, SDK and metadata-driven designer, including typed
scalar expressions. The collection follow-up introduced analysis 7, analyzer 5
and library format 3. Libraries require the matching semantic definition fingerprint;
the SDK bridge requires the full tooling fingerprint and copies only detached
metadata and primitive scalar values. Reject old libraries/analyzers with rebuild
guidance. Mismatched toolsets retain XML editing and drafts but block designer
changes until a compatible result arrives, including through pending refreshes.
Ordinary designer fields use generated descriptors and shared expression controls.
Literal helpers are explicit draft actions; focus and analysis refresh must never
materialize inherited values or defaults. Keep template samples schema-aware and
leave missing required values to compiler diagnostics. The isolated
`eng/test-alarm-definition-designer.ps1` fixture validates alternate fields on both
IDE hosts and restores standard experimental deployments; run it sequentially
with other desktop tests. Preview columns use metadata labels/visibility and stable
IDs; ordinary Source/Identity/TestMethod fields use `Field:<name>` IDs alongside
the built-in columns. Sort compiler-provided typed values, preserving separate
absent/deferred/invalid groups; never parse values again in the host. Use
`-FullWorkflow` for fork acceptance: it adds isolated packaged project/library/
NuGet/analyzer checks on both task hosts and enables native preview checks.
`eng/test-alarm-definition-fork-consumers.ps1` can rerun consumer checks against
an existing isolated fork. Keep its feeds/caches private and compile verification
assets against the packaged compiler, not a compiler project reference.
Do not add runtime per-project schemas or inherited-entry reordering under this scope.
Enum Value LabelKey metadata references project RESX captions. Follow milestone 11
iterations 24–26: compiler-owned culture/resource fallback, literal caption text,
and configurable ALM0031 warnings for all missing configured keys in concrete
application cultures. Keyword/library builds defer caption warnings. Keep stored
tokens, compiled values and semantic fingerprints unchanged; transport detached
translation results and never resolve resource inheritance in the host.
Milestone 11 follow-up iterations 9–15 authorize an isolated interactive definition
playground, simple scalar collections, flat scalar-field record collections and
one optional existing TestProcedure capability. Follow the plan's version-2
compatibility and staged capability guards; do not advertise metadata-only
generation as end-to-end collection support. Preserve version-1 behavior and
inherited-first/Clear semantics. Nested collections and keyed inherited-record
overrides remain excluded.
Iteration 11 adds version-2 collection descriptors and procedure capability
metadata, preserving version-1 input semantic fingerprints while advancing
the full tooling fingerprint (now generator-6 after enum caption resource keys).
Compiler/SDK generation is enabled in iteration 13. Iteration 14 adds host
collection editing and optional-procedure UI, removing the remaining host guard.
Collection drafts share the atomic document transaction and reject concurrent
changes to their source container. Keep local entry identities through moves and
analysis refresh; inherited/excluded entries remain read-only compiler projections.
Record completion adds sibling fields without making defaults into source values.
Iteration 14 implemented a fixed collection-count column and separate entry
details. The authorized iterations 16–21 replace that column with one stable
Collection:<name> column per collection: List/Concatenated for simple collections
and compiled WPF DataTemplates for complex collections. Follow the plan's
presentation contract, saved-column migration and template build/validation gates.
Templates belong to the rebuilt extension, never runtime project/library inputs;
keep WPF and template objects out of compiler/SDK analysis boundaries.
Milestone 11 iteration 27 names the user-facing window Alarm Summary and the
project action Show Alarm Summary. Summary templates describe resolved data
inspection, independently of document/PDF layout. Preserve existing internal
Preview identifiers, command names/IDs, window GUID, XML presentation attributes,
AlarmlistCulture and AlarmlistPreviewColumnLayout for compatibility. Use summary
wording in user-facing language, scope, accessibility, messages and documentation.
Designer iteration 20 uses procedure-style collection entries with a shared
26-DIP adorner gutter, shared record-label widths and right-aligned actions.
Collapse and expansion preserve drafts; collapsed tooltips use current draft or
compiler effective values, left-aligned with one field per line. View actions
never materialize defaults or save source. Follow the milestone's acceptance gates.
Iteration 30 encloses designer collections and their cleared entries in one border.
Keep sticky headers bounded by their owning group in the existing alarm scroll
area; reuse the actual header for collapse and automation. Detach scroll/layout
subscriptions on unload and reveal keyboard-focused editors beneath the header
without committing drafts. Do not introduce per-collection scrolling.
Iteration 31 applies the same behavior to Test procedure and its nested groups.
Pin each group header below its sticky parent, keeping editable titles, inherited
title adorners, stable group IDs and collapse controls visible together. Keep
all collection kinds consistent; do not limit sticky headers to generated lists.
The standard definition now uses version 2 with explicit TestProcedure and the
original four fields. This changes its semantic fingerprint; rebuild libraries
and matching tools. The interactive shortcut accepts an explicit SampleFile as
well as DefinitionFile; the checked-in procedure-disabled pair demonstrates both.
Never strip a user's chosen source automatically to fit a different definition.
Iteration 12 supplies compiler collection parsing, inherited-first/Clear
resolution, typed values and output. Record sibling expressions use $(item:FieldName)
only in record context, including translated expressions; defaults stay literal.
Run eng/test-alarm-collections.ps1 for isolated generated-runtime checks on both
frameworks. Its test-only generator access must not enable partial production
toolsets. ALM0030 cardinality failures are mandatory; library/abstract minimum and
maximum counts defer until concrete application consumption.
Analysis collections expose compiler-owned local/effective/excluded entries,
per-field scalar states and item/Clear origins. Library location maps include
containers, entries and record fields; reject forged or obsolete artifact maps.
Use eng/test-alarm-collection-fork.ps1 for isolated procedure-enabled/disabled SDK
package acceptance on both task hosts, including transactional collection failures.
Its -Designer option checks both IDE hosts in AlarmlistDefinitionExp; -SkipConsumers
selects designer-only acceptance. Supply both VisualStudio2022Path and
VisualStudio2026Path. Run native fixtures sequentially and keep their private
workspace paths short enough for desktop SDK extraction.
The collection-fork fixture accepts -RootSuffix to use a different idle experimental
profile when the usual playground is open. Never close an existing user IDE to
deploy tests. If using AlarmlistExp, restore its standard extension afterward via
the ordinary native fixture's deployment preparation on both hosts.
[Milestone 10](src/VisualStudio/MILESTONE-10.md) governs procedure-group GroupBoxes,
editable title headings, always-visible read-only group IDs and the scope splitter
grip, plus collapsible group bodies and procedure search in follow-up iterations.
Keep search/collapse state out of source and preserve drafts during view actions.
Preserve existing title semantics, draft lifecycle and source navigation;
these are presentation changes, not compiler or source-format changes.
[Milestone 9](src/VisualStudio/MILESTONE-9.md) authorizes reusable alarm libraries,
ProjectReference/NuGet and native reference management; its
[design contract](src/VisualStudio/MILESTONE-9-DESIGN.md) governs dependency scopes,
resource overrides, saved-project analysis and the coordinated analysis-5/analyzer-3
transition. Native integration on both supported IDE hosts is an essential first
iteration gate. Read the plan's implementation evidence and remaining gates before
resuming. Consumer analysis captures saved dependency projects in private MSBuild
collections without executing targets, including unloaded/out-of-solution projects.
Dependency drafts remain private; consumer ALMX buffers retain precedence. Keep
dependency discovery watches through failed recapture so missing imports and
package artifacts can recover, and revalidate the graph and package selection
before publication or editor actions. NuGet resolution stays in the selected SDK
AppDomain; never load its implementation assemblies into the VS host.
Package-content groups use restored declared files and selected build assets, never
cache enumeration. Keep missing-file/manifest watches through failed capture.
Revalidate selected package/version and captured bytes before opening content;
reject escaping paths and junctions/symlinks. Use the virtual read-only editor:
HTML/build files are plain text, library artifacts XML, analyzer DLLs detached
identity/path information. Browsing must not execute targets or load analyzer DLLs.
[Milestone 8](src/VisualStudio/MILESTONE-8.md) defines the authorized custom
procedure-group transition, including analysis contract 4 and analyzer contract 2.
Groups use case-insensitive stable identities and expression-capable titles;
Instructions/Reset are creation defaults, not implicit compiler sections.
Resolve creation-only EditorConfig settings for the destination physical source in
the compiler and expose detached `ProcedureCreation` data. Revalidate captured
inputs before adding an alarm; invalid defaults block creation without invalidating
existing alarms. Saved settings never rewrite existing or duplicated content.
Historical milestone records remain evidence of their original versions.
[FUTURE-FEATURES.md](FUTURE-FEATURES.md) holds ideas and design history; it does not
authorize implementation or supersede the confirmed scope in a milestone plan.

## Project map

Alarmlist compiles XML-based `.almx` source files into a resolved XML alarm list.
It provides a compiler library, MSBuild tasks, and a NuGet-distributed MSBuild SDK
for `.almproj` projects. The solution is `AlarmlistProject.slnx`.

| Location | Responsibility |
| --- | --- |
| `documentation/` | User-facing feature references and workflow tutorials; start with its README and use the section templates. Documentation coverage and review evidence are recorded in `documentation/PLAN.md`. |
| `src/Compiler/` | `Alarmlist.Core`: source parsing, syntax, binding, diagnostics, resolved models, output writers, culture/resource catalogues in `Localization/`, captured path/list catalogues and XSD in `Lookups/`, detached snapshot/provenance results in `Analysis/`, and captured tooling assembly binding in `Extensibility/`. |
| `src/Compiler.UnitTests/` | Compiler tests organized by text parsing, syntax, binding, analysis, and output. |
| `src/Exporters/` | External zenon keyword-XML provider/package and its dual-framework tests; SDK tests verify packaged NuGet and DLL consumers. |
| `src/Analyzers/Alarmlist.SampleAnalyzer/` | Explicit opt-in, dual-framework contract-5 sample rule; also a package/UI test asset. |
| `src/shared/Configuration/` | Saved EditorConfig discovery and analyzer/dependency byte capture shared by build and IDE hosts; no compiler file I/O. |
| `src/MSBuild/AlarmList.MSBuild/` | MSBuild task implementation and logging. |
| `src/MSBuild/AlarmList.MSBuild.UnitTests/` | Task unit tests and mocked build-engine behavior. |
| `src/MSBuild/AlarmList.MSBuild.IntegrationTests/` | Integration tests invoking MSBuild against sample projects. |
| `src/MSBuild/SampleData/` | Shared ALMX samples and a project that invokes the task directly. |
| `src/SDK/Alarmlist.SDK/` | SDK packaging project, `sdk/` entry points, and `tools/` props/targets. |
| `src/SDK/Alarmlist.SDK.Tests/` | Tests of the packaged SDK, isolated consumer projects, and build lifecycle. |
| `src/VisualStudio/Alarmlist.VisualStudio/` | Visual Studio 2022/2026 CPS/VSSDK extension, templates, and MVVM designer/XML split editor. `Editor/` owns host integration and open-buffer observation, `Documents/` shared models, `Editing/` source-preserving edits, `Analysis/` the SDK bridge, project snapshots/scheduling and file watching, `Diagnostics/` the live Error List source/navigation, `ProjectSystem/` CPS subscriptions, and `UI/` views/view models. |
| `src/VisualStudio/Alarmlist.VisualStudio.UnitTests/` | `net472` editor/model, XML preservation, SDK bridge isolation/package-worker, CPS build-check, and MSBuild XAML schema tests. |
| `src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/` | Packaged VSIX and template-consumer tests on both frameworks. |
| `src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/` | All automated functional IDE scenarios under xUnit, including FlaUI keyboard interaction; out-of-process net8.0-windows/x64 runner. |
| `src/VisualStudio/Alarmlist.VisualStudio.PerformanceTests/` | Opt-in xUnit-hosted BenchmarkDotNet bridge/WPF measurements and running-IDE latency/memory validation; net472 only. |
| `src/shared/VisualStudioTesting/` | Shared C# IDE automation, automatic test deployment preparation, and opt-in performance-driver invocation/report validation. |
| `src/shared/Unittesting/` | Shared compiler/task test helpers linked through build configuration. |
| `eng/` | Repository build settings, version properties, and common build tooling. |
| `eng/tools/VisualStudioHeapProbe/` | Optional net8.0/x64 ClrMD diagnostics for explicitly selected test IDE, test host or smoke-driver processes; separate from benchmarks and the VSIX. |
| `eng/tools/AlarmDefinitionGenerator/` | net8.0 build-time XML definition validation and deterministic compiler/host metadata generation; shared targets write only intermediate outputs. Its adjacent UnitTests project and `eng/test-alarm-definition-generation.ps1` cover generation and build lifecycle. |
| `scripts/` | Batch launchers that deploy the extension and start the Visual Studio 2022 or 2026 experimental instance. |
| `example/` | Default North (XML)/South (JSON) designer playground with a native Shared Project for common ALMX/RESX/lookups and a standalone Zenon keyword-XML project. Launchers use `eng/prepare-example.ps1` to prepare matching SDK/analyzers/exporter without replacing sources, membership or solution files. Ignored Example.local.props selects the exporter version; both package hashes select the cache. `-ExampleRoot` supports isolated copies. |
| `example/Libraries/` | Independent four-project library playground, including two consumers and transitive parameter/resource/lookup overrides; `eng/prepare-library-example.ps1` prepares its matching SDK without touching North/South. |
| `example/SharedProjects/` | Native Shared Project with ALMX, saved translations/lookups and two consumers; `eng/prepare-shared-example.ps1` selects the matching SDK without replacing edited sources/projects. |
| `template/` | Reference XML implementation/tests outside the current solution; active implementation lives in `src/`. |

Read `src/SDK/Alarmlist.SDK/README.md` for SDK usage and
`src/SDK/Alarmlist.SDK.Tests/README.md` for the package-test workflow.
Read `src/VisualStudio/README.md` for extension packaging and experimental IDE testing.

## Reference repositories and architectural direction

The project owner has explicitly identified **Arcade** and **Roslyn** as lookup,
template, and architectural reference repositories. Use them when deciding how
to organize and extend this project. They establish the intended direction for
the repository's structure, build infrastructure, and compiler design.

| Reference | How it guides this repository |
| --- | --- |
| [dotnet/arcade](https://github.com/dotnet/arcade) | Repository engineering and build infrastructure: shared root props/targets, `eng/` tooling and version configuration, consistent artifact layout, MSBuild SDK organization, packaging, and SDK integration-test patterns. Apply this guidance to the root build files, `eng/`, and `src/SDK/`. |
| [dotnet/roslyn](https://github.com/dotnet/roslyn) | Compiler architecture and code organization: clear boundaries between source text, syntax, binding/reference resolution, diagnostics, compilation, and output, with corresponding compiler tests and separate build integration. Apply this guidance to `src/Compiler/`, `src/Compiler.UnitTests/`, and the boundary with `src/MSBuild/`. |

Useful starting points:

- [Arcade SDK](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk)
  for SDK entry points, props/targets organization, and shared build conventions.
- [Arcade SDK tests](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.Tests)
  for package-consumer tests, especially the `TestProjectFixture` and `TestApp`
  patterns already referenced by this repository's SDK test README.
- [Roslyn compiler sources](https://github.com/dotnet/roslyn/tree/main/src/Compilers)
  for compiler layers, diagnostics, API responsibilities, and test organization.

When making structural changes:

- Consult the relevant reference implementation before introducing a new
  architectural pattern, and adapt it to the ALMX domain and this project's size.
- Keep the compiler independently usable, the MSBuild task focused on invoking
  it and reporting results, and the SDK focused on consumer build integration.
  Keep shared repository build policy in the root configuration and `eng/`.
- Treat this alignment as design guidance. It does not imply that every upstream
  feature is implemented here or that Roslyn packages must become dependencies.
- Preserve this repository's supported frameworks, public contracts, and
  `Arkarin0.DotNet.Arcade.Sdk` integration when adapting upstream patterns.
  Check the pinned version before copying current upstream build configuration.
- Explain significant departures from these reference patterns in the change
  description, including the local requirement that motivates the choice.

## Build environment and dependencies

- The shared configuration targets `net472;net8.0` and sets C# 12. Keep code and
  dependencies compatible with both frameworks; language support does not imply
  that a newer runtime API is available on .NET Framework.
- The in-process Visual Studio extension and its CPS unit tests target only
  `net472`; the VSIX supports Visual Studio 2022 17.9+ and 2026 x64. Its package-consumer
  integration tests still target both repository frameworks. Do not propagate
  this host-specific target to the compiler, tasks, or SDK.
- Use a .NET SDK that supports the `.slnx` solution format. `global.json` declares
  `tools.dotnet` as `9.0.302` for the repository tooling and pins
  `Arkarin0.DotNet.Arcade.Sdk` to `1.0.0-Preview`. The `tools.dotnet` entry is not
  the standard `sdk.version` pin; check `dotnet --info` when diagnosing selection.
- Running `net472` tests requires Windows and .NET Framework. Running `net8.0`
  tests requires a compatible test runtime. Report missing prerequisites rather
  than changing target frameworks to bypass them.
- Root `Directory.Build.props` and `Directory.Build.targets` import the Arcade
  SDK. Preserve these imports and inspect shared settings before overriding them
  in an individual project.
- `NuGet.config` uses nuget.org and the repository-local `.packages/` cache.
  Restore may require network access on a fresh checkout.
- Check `Directory.Packages.props`, `eng/Versions.props`, and the affected project
  when changing dependencies. `ManagePackageVersionsCentrally` is currently
  `false`, and projects also contain explicit versions; editing a central entry
  alone may not change the resolved dependency. Follow the existing convention
  and verify the effective version.
- Treat `artifacts/`, `.packages/`, `.vs/`, `bin/`, and `obj/` as generated/local
  state. Do not edit or commit them as implementation changes.

## Build and validation commands

Run commands from the repository root. Build and test restore dependencies by
default.

```powershell
dotnet build AlarmlistProject.slnx
dotnet test AlarmlistProject.slnx
```

For focused validation, choose the relevant suite:

```powershell
dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj --framework net8.0
dotnet test src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj --framework net8.0
dotnet test src/MSBuild/AlarmList.MSBuild.IntegrationTests/AlarmList.MSBuild.IntegrationTests.csproj --framework net8.0
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj --framework net8.0
dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj
dotnet test src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj
```

- Add or update regression tests for behavior changes using existing xUnit
  conventions and helpers. Assert observable behavior, diagnostics, and output,
  rather than duplicating implementation details.
- Compiler EditorConfig compatibility tests use a pinned upstream archive, cached
  under `artifacts/testassets/editorconfig/`. Their xUnit fixture downloads it only
  when missing and verifies SHA-256; a warm cache needs no network. See
  `src/Compiler.UnitTests/Configuration/README.md` for explicit suite adaptations.
- Start with the affected suite. For shared compiler, build configuration, task,
  or packaging changes, also validate the affected downstream suites and both
  target frameworks when the environment supports them. Omitting `--framework`
  runs both; a `net8.0`-only pass does not verify `net472` compatibility.
  Build/test invocations sharing compiler or task intermediate outputs must run
  sequentially; separate test projects do not imply independent build outputs.
- Documentation-only changes normally need a diff and path/command review, not
  a full build. Do not claim tests passed unless they were actually run.
- The SDK package is generated during Build under
  `artifacts/packages/<Configuration>/NonShipping/`. Avoid `--no-build` when
  validating changed SDK contents unless that package and the test assets have
  already been rebuilt.
- SDK tests use an isolated local feed and cache. Add scenarios in
  `src/SDK/Alarmlist.SDK.Tests/testassets/` and use the existing
  `TestProjectCollection`, `_fixture.CreateTestApp(...)`, and `TestApp` helpers.
  Shared ALMX samples are copied from `src/MSBuild/SampleData/` at build time.
- SDK test MSBuild logs are retained under
  `artifacts/log/<Configuration>/SDKTests/<TargetFramework>/<Test>/<Instance>/`.
  Inspect them when a package-consumer build fails.
- Both SDK test frameworks use `dotnet msbuild` by default. To verify desktop
  MSBuild task loading, set `ALARMLIST_TEST_MSBUILD_PATH` to the installed Visual
  Studio `MSBuild.exe` and run the SDK suite with `--framework net472`, as shown
  in its README. Restore any previous environment-variable value afterward.

## Compiler and file-format contracts

- Milestone six adds captured `.editorconfig` inputs and diagnostic policy in
  `Configuration/` and `Diagnostics/`. Resolve options without compiler filesystem
  access. Preserve physical-source scoping, case-sensitive globs, root/unset,
  literal inline punctuation and immutable captured results. ALM0001-ALM0028
  remain mandatory errors regardless of NoWarn or per-file severity. Milestone 11
  also makes ALM0029 (scalar/required/bounds validation) and ALM0030 (collection
  cardinality) mandatory. Definition
  defaults remain literal and source serialization never materializes them.
  Determine
  success after configurable warning promotion and preserve previous outputs on
  failure. See `src/VisualStudio/MILESTONE-6.md` for scope and implementation status.

- Keep the existing flow: `Text/AlmxFile` reads source XML into `Syntax` models;
  `Binding/Binder` merges sources and resolves references; `AlarmCompiler`
  produces a `CompilationResult`; `Output` writers serialize resolved alarms.
  Keep compiler behavior independent of MSBuild-specific APIs.
- `Text/AlmxTextSnapshot` and `AlarmCompiler.CompileSnapshots` analyze captured
  unsaved text without disk access. Preserve fresh syntax per parse, snapshot
  versions on source locations, and location-free diagnostic callers. Source
  locations use one-based lines/columns and an exclusive end column.
- `Text/ResxTextSnapshot` captures saved resource text with an explicit culture and
  version. Hosts supply saved contents; compiler parsing never reads the resource
  path or deserializes RESX objects. Preserve string-only parsing, DTD prohibition,
  case-sensitive project-wide keys, duplicate-input diagnostics, and parent-culture
  lookup that stops before invariant resources. Empty translations continue the
  parent search; whitespace remains a value. The resource-aware CompilationContext
  overload must coexist with the original constructor until the coordinated bridge
  contract transition. Resource validation also runs in keyword mode.
- Named list expressions `$(list:List#Entry)` use captured `.almlookup`
  inputs, preserve literal values, and never rescan them. Optional descriptions
  may translate; missing descriptions do not invalidate identifiers. List entries
  can merge across files; duplicate identities are errors. Template argument/list
  bindings are completion hints, not membership validation or template signatures.
  Preserve detached list/metadata/binding analysis and saved-input validation in
  the editor. Do not introduce a built-in DeviceTag field.
- Localization expressions use `$(loc:Key)`. Keyword mode retains the complete
  literal token; real cultures resolve translations containing property expressions
  in the consuming alarm scope and field-dependency graph. Never rescan parameter
  replacement strings. Reject direct nested localization, preserve escaped tokens,
  and defer contextual lookup for abstract templates and excluded procedure entries.
- Generated `.almlookup` XML uses `urn:alarmlist:lookup:1` and the authoritative
  `src/Compiler/Lookups/AlarmlistLookup.xsd`, embedded in the compiler and shipped
  at SDK `tools/Schemas/AlarmlistLookup.xsd`. `LookupTextSnapshot` captures text;
  `Text/LookupReader` validates without reading its path, external schemas or DTDs.
  Keep project-wide case-sensitive path identities, per-path member identities,
  literal metadata and immutable catalogue results. See
  `src/VisualStudio/HMI-LOOKUPS.md` for the extension contract and iteration status.
- Named localization arguments use `$(loc:Key, name="text", value=$(Parameter))`;
  formatted RESX templates use `{name}` and literal `{{`/`}}`. Lookup expressions
  use `path:`, `property:`, `button:` and `meta:` with `#`-separated identities.
  Parse quoted arguments and template placeholders before substitutions; inserted
  parameter/metadata/argument values remain literal. Preserve single-culture label
  lookup, complete keyword tokens, reference errors without bounds validation, and
  deferred contextual resolution for abstract/excluded content. Additive analysis
  data exposes captured lookup files, immutable entries and usage provenance.
- `AlarmCompiler.AnalyzeSnapshots` returns the read-only, versioned analysis
  contract from `Analysis/`. Preserve local/effective field separation, procedure
  origins and Clear exclusions, and unavailable effective values for invalid
  reference chains. Reuse compiler resolution rules rather than duplicating them
  in editor code. The SDK's `AnalysisContract` consumer asset verifies this API
  from the restored package without a compiler project reference.
- The optional `AnalyzeSnapshots(snapshots, includeCompiledXml: true)` overload
  uses the same bound inputs and normal XML output writer. Contract 8 also accepts
  captured CompilationContext. Preserve raw expressions separately from evaluated
  values, abstract state, availability, and declaration/assignment origins. The
  unreleased contracts 1, 2, 3, 4, 5 and 6 are no longer supported by the bridge. `CompiledXml` is unavailable
  on errors; in-memory previews must never save source or write build output.
  `AnalyzeLibrarySnapshots` uses deferred consumer-value validation and never
  returns final XML. Capture the root project's OutputType in the IDE analysis
  context, preserve it through input updates, and invalidate on mode changes.
  Application analysis and compilation remain strict about missing values.
  Both ALMX editor factories explicitly opt out of host IntelliSense deferral:
  their captured analysis does not depend on language-project initialization.
  Verify this policy in the packaged editor registration and native loading tests.
  Package definition navigation uses the registered in-memory editor factory,
  captured generated text and compiler-provided location maps. Keep its documents
  read-only and non-saving, and retain no project analysis state in open buffers.
  Project-reference navigation uses captured physical source mappings and the
  existing current-input and destination-draft checks.
- Contract 8 localization results retain captured resource identities/versions,
  normalized selected culture, raw entries and ambiguity, compiler-owned parent
  lookup origins and per-alarm LocalizationUses. Lookup availability is distinct
  from evaluated-value availability. Keep these detached and immutable, with no
  compiler objects crossing the bridge. Never infer translation origins in the IDE.
- Expressions evaluate in the compiler after binding. References inherit parameter
  declarations and raw content, never parameter assignments. Assignment lookup uses
  the current alarm, its file, then captured project items; explicit empty stops
  fallback. Abstract alarms bind but do not emit output. Preserve source-file scope,
  single-pass escaping/substitution, scalar dependency diagnostics, and local raw
  source serialization. Keep literal identity/reference fields out of evaluation.
- ALMX source serialization and compiled output serialization are distinct.
  Source XML uses an `Alarmlist` root and supports references and procedure
  directives; compiled XML uses an `AlarmList` root and resolved values. Preserve
  element names and casing unless the task explicitly changes the format.
- Preserve reference inheritance, local overrides, procedure ordering, and
  `Clear` behavior. Extend relevant parser, binder, and syntax tests when
  changing these semantics, including references across multiple files.
- Preserve DTD prohibition and unknown-element skipping in XML parsing.
- Keep diagnostic IDs stable: `ALM0001` duplicate alarm name, `ALM0002` missing
  reference, `ALM0003` self-reference, and `ALM0004` circular reference. Use the
  existing diagnostic descriptors/bag and preserve error propagation through
  `CompilationResult` and MSBuild logging.
- Compilation errors must fail the MSBuild task without writing a new compiled
  output. Test failure paths as well as successful compilation.

## MSBuild SDK contracts

- Milestone-nine library builds use OutputType=Library and produce one
  culture-independent `.almlib`. Milestone 13 uses Exports as the default project
  purpose and retains Application as a compatibility alias; XML remains the default
  export selection.
  Preserve captured library references, portable project-relative/Link names,
  deferred consumer values and transactional output ownership. Manifest version 2
  distinguishes library output kinds. Export manifest version 3 records actual
  provider/culture artifacts and bounded retained temporary ownership scopes;
  continue reading versions 1 and 2. ProjectReference
  builds use toolset configuration assignment; IDE/BuildProjectReferences=false
  consumption must not rebuild dependencies. GetTargetPath stays local and
  GetAlarmlistReferenceClosure discovers transitive identities/paths without Build.
  Protect project/import and dependency inputs during Build/Clean and library
  artifacts during XLIFF exchange. Package discovery uses NuGet's selected assets
  graph and declared files, never a package-cache scan. Normalize NuGet versions
  consistently in artifact production and output discovery. Pack builds first,
  uses exact library dependency versions, rejects non-packable project dependencies,
  and installs packages only after successful staging. Preserve runtime asset
  subdirectories in the SDK package. Follow the milestone plan for acceptance
  evidence and remaining editor integration.

- Milestone 13 uses AlarmlistExportTypes to select provider IDs and explicit
  AlarmlistExportProvider declarations (AssemblyPath, TypeName, Dependencies,
  OutputFileName and Option_<name>). Capture only selected provider code and
  explicitly listed dependencies; protect all declared provider paths from
  Build/Clean collisions. Provider packages use ordinary build assets selected
  by MSBuildRuntimeType, not the consumer TargetFramework. Never instantiate
  exporters during design-time builds, library builds, GetTargetPath or Clean.
  Output discovery uses last-success manifest records; XML keeps its pre-build
  predicted path. Providers return relative artifact bytes; only the build host
  publishes them. Validate contract/definition compatibility, input collisions,
  duplicate paths and links before publication. Compile/export all cultures before
  staging any selected result. Dispose providers before publication so disposal
  failure also preserves previous outputs. Preserve legacy BOM-free XML build
  bytes independently of the standalone XML file writer's encoding preamble.
  SDK verification exporters must compile against the packaged compiler contract.
  Built-in JSON format 1 uses ordered field/collection records, compiler canonical
  numeric values and explicit valid/absent/deferred states. Preserve Int64/Decimal
  digits, enum tokens and temporal canonical strings; never re-evaluate expressions
  or pass generated CLR enum objects to a serializer. Keep format version separate
  from the provider contract and library format versions.
  The external zenon provider renders compiler-owned localization segments as
  @Key@ through the shared XML projection. Preserve literal/default/typed values;
  reject literal @, adjacent keys without a nonempty literal separator, and keys
  requiring substitution. Never infer keyword boundaries from resolved strings.
  Its ordinary package build assets register both task runtimes without shipping
  the host compiler or activating transitively. Full zenon import/language-table
  generation and vendor runtime certification remain outside milestone 13.
  Milestone 13 native export acceptance uses Area=ExportBuild alongside
  Area=XmlCatalogCompletion on both IDE hosts. Run sequentially with native/WPF
  suites. Collection fork acceptance adds -Exports to build the matching zenon
  package and validate XML/JSON/zenon through project/NuGet library consumers,
  failure preservation and output switching/Clean/Rebuild. Repeat consumer checks
  with -Collections -Exports against each installed desktop MSBuild host.

- Milestone-six analyzers are explicit `AlarmlistAnalyzer` DLL items. Preserve
  package registration through ordinary build assets selecting net472 for desktop
  MSBuild/Visual Studio and net8.0 for .NET MSBuild. The sample analyzer package is
  opt-in project-local tooling; do not bundle Alarmlist.Core, add buildTransitive
  activation or discover arbitrary Roslyn analyzer/cache DLLs. Direct DLL items
  remain supported. See milestone-nine iterations 17–19 for package/tree scope.
  Preserve saved dependency-byte capture, contract-5 detached inputs, cooperative cancellation,
  ALM0025 failure reporting, and diagnostic policy before output installation. The
  net472 loader must reject changed/conflicting images in an existing AppDomain;
  VS replaces the domain on payload changes, while desktop builds need a new process.
  Do not claim silent reload or load compiler/plugin types into the VS host domain.
  Analyzer tree children are SupportedDiagnostics declarations, including rules
  without violations. Capture them through the selected SDK loader without calling
  Analyze, share descriptor validation with execution, and copy detached metadata
  before disposal. Preserve declarations for invalid ALMX, clear stale children on
  load/descriptor failure, and label rule severity as the descriptor default rather
  than one effective project-wide setting. Nodes never edit policy or delete rules.

- Keep consumer `.almproj` projects usable without a `TargetFramework`.
  Task assembly selection depends on `MSBuildRuntimeType`: .NET MSBuild loads
  `tasks/net8.0`, while desktop MSBuild loads `tasks/net472`.
- The SDK package must contain both task runtimes, the compiler, and runtime
  dependencies. MSBuild supplies `Microsoft.Build.*`; do not bundle those DLLs.
  Preserve the SDK package layout and its package-content tests.
- Preserve default `**/*.almx` inclusion, exclusions for output/intermediate
  and hidden directories, explicit item selection, conditional items, and user
  overrides of output paths and names.
- Builds currently recompile selected inputs on each invocation so removed or
  conditional inputs are reflected. Do not add incremental skipping without
  covering these cases. Preserve the design-time-build guard.
- Localization builds use `AlarmlistResource` saved RESX items and semicolon-separated
  `AlarmlistCultures` (default keyword). Capture inputs once per batch. Validate and
  serialize all cultures before output installation; preserve rollback and ownership
  tracking. Outputs are `<OutputPath>/<culture>/<CompiledAlarmlistFileName>`.
  Build/GetTargetPath expose `@(CompiledAlarmlistFiles)` with Culture metadata
  (and ExportProvider for export artifacts);
  the task exposes OutputFiles. Preserve explicit-empty resource Culture metadata,
  linked/conditional items and output exclusions. Preview AlarmlistCulture is separate.
- Lookup builds capture saved `AlarmlistLookup` items once with the same input batch.
  Default `**/*.almlookup` inclusion follows source exclusions; explicit, linked,
  conditional inputs retain their semantics. Protect lookup files
  from output/manifest/exchange collisions. `AlarmlistLookupSchema` locates the
  packaged authoritative XSD. XLIFF protects named placeholder occurrences as well
  as parameter/lookup expressions, preserving text around leading inline codes.
- [Milestone 12](src/VisualStudio/MILESTONE-12.md) iteration 5 normalizes evaluated
  Compile/resource/lookup inputs at task and MSBuild snapshot boundaries. Keep
  equivalent normalized absolute paths once per consumer in first evaluated order
  using the platform path comparer; do not resolve filesystem links. Repeated
  resources must agree on effective raw Culture (omitted infers the filename,
  explicit empty stays empty). Library duplicates must also agree on portable
  Link names; application Link is presentation only. Reject metadata conflicts
  before output installation. Preserve compiler declaration diagnostics and raw
  snapshot APIs. Shared imports use MSBuildThisFileDirectory for physical paths;
  IDE capture evaluates saved imports without executing targets. XLIFF resource
  capture uses the same identity policy. Matching rebuilt SDK/VSIX are required.
- Milestone 12 iteration 6 uses the installed native C# Shared Project container
  with SharedProjectAppliesTo=Alarmlist, not a new project factory. The dedicated
  template imports only SDK tools/Alarmlist.Shared.targets for file rules and item
  templates, using explicit Import Sdk/Version attributes. Do not import the
  consumer build pipeline or add the Alarmlist analysis capability to .shproj.
  Keep SharedProjectReferences/SharedImports on consumers. Shared files belong to
  .projitems with MSBuildThisFileDirectory paths; native reference removal must
  preserve them. Native Shared Project tooling is an IDE prerequisite only.
  Consumer projections use the existing dependency-tree graft, evaluated
  ProjectInstance.ImportPaths and DefiningProjectFullPath metadata. Preserve
  conditions, hidden items, first physical membership and Link captions. Do not
  enumerate source directories, enable unrelated Managed capabilities or execute
  targets for browsing. Imported source nodes open physical shared files, while
  consumer projection membership remains non-removable; use the owning shared
  project for item changes and native reference management for imports.
- Explicit ExportAlarmlistXliff reads saved resources and writes XLIFF 2.0 plus
  local baseline sidecars, independently of Build/Clean. Keep exact culture values,
  protected expressions, stable keys and baseline presence/comments. Validate all
  target pairs before transactional installation; preserve input/output collision
  guards and explicit overwrite. XLIFF is never a compilation input. Vendor
  interoperability requires actual tool evidence, not schema validation alone.
- Explicit ImportAlarmlistXliff defaults to a saved-input dry run; apply rejects
  the whole batch on conflict and never runs from Build/Clean. Preserve exact
  culture/baseline/placeholder checks, selected-resource destination constraints,
  repeat-import no-ops, saved-byte revalidation and ordinary-failure rollback.
  Windows apply uses read-only VS automation and refuses open destination resource
  editors (including clean editors with private native cell drafts); never save,
  close or activate them. Keep VS assemblies out of the task and compiler. An
  inaccessible IDE fails closed. Retain newer independent edits and recovery
  backups if rollback cannot safely restore an installed file.
- `Clean` deletes configured/tracked outputs and owned temporary files and preserves unrelated files.
  Keep `Build`, `Clean`, `Rebuild`, `Restore`, `GetTargetPath`, custom build hooks,
  and consumer `Directory.Build.props/targets` imports working.
- The SDK test project's conditional `ProjectReference` metadata handles
  command-line package builds versus Visual Studio and
  `BuildProjectReferences=false`. Keep its `ProjectReferenceTests` passing when
  changing project-reference or target-framework handling.
- Update the SDK README and consumer tests when changing public SDK properties,
  defaults, package layout, or lifecycle behavior.
- Keep CPS capabilities and XAML rules in the SDK's `tools/` directory. The VSIX
  templates must use the built SDK version; do not add private compiler copies
  or hard-coded local package paths. Keep the CPS up-to-date provider requesting
  builds until removed/conditional input handling has an alternative guarantee.
  Keep Folder out of declared file item types in ProjectItemsSchema; its rule uses
  ProjectFileFolderItems so persisted empty folders can reload in CPS.
- The Solution Explorer dependency tree uses the public CPS PhysicalViewRootGraft
  contract, scoped to Alarmlist projects. Direct project references come from the
  active evaluated snapshot, retain normalized path identities and missing-project
  warning state, and never trigger Build/Pack while browsing. Keep subscription
  cancellation/disposal, version/configuration guards, localized labels and native
  CPS reference-management actions. See milestone-nine iterations 10–12 for scope;
  do not enable unrelated .NET capabilities to obtain internal dependency providers.
  Package nodes reuse the SDK's shared graph selection through the isolated bridge
  and existing project-analysis watches. Preserve direct requested/resolved versions,
  visible pending/unresolved states, read-only transitive edges and current-assets/
  configuration/request guards. Bound repeated graph expansion and route package
  changes through native NuGet management, not project-reference deletion.
- The evaluated `AlarmlistAnalysisCompilerAssembly` property identifies the resolved
  SDK's net472 compiler independently of the build task runtime. The extension's
  `SdkAnalysisBridge` loads it in a dedicated AppDomain and copies contract-8 data
  into host-owned results. Do not load compiler types in the VS host domain, add a
  compiler project reference, or substitute another SDK on failure. Call bridge
  operations off the UI thread; owners must dispose on SDK/configuration changes
  and unload. `AlarmlistProjectAnalysis` owns the service through CPS dynamic-load
  callbacks and the active evaluation source. Preserve evaluated Compile order,
  open-buffer precedence (including XML-only views), immediate stale
  result invalidation, and generation/text/identity checks before publication.
  Shared VS buffers remain VS-owned; disposing analysis must not close them.
  Document observation must use weak ownership: waiting for TextDocumentDisposed
  while strongly retaining the document can keep closed VS2022 editor graphs alive.
  Export dynamic load components through the named unconfigured-project scope,
  and release analysis on CPS scope disposal as well as capability unload. Bridge
  remoting may resolve only the already-loaded extension assembly in the VS host;
  scope its resolver to boundary calls and never resolve SDK assemblies there.
- CPS owns evaluated resource membership/order/metadata and preview culture.
  Capture RESX from saved bytes only, never open-document buffers. Ignore resource
  draft/close notifications; observe saved changes and compare captured resource
  text/path/order/culture before publication or navigation. Disk resource versions
  are scoped to the project generation, so version equality alone is insufficient.
  Preserve linked project contexts and resource locations without retaining native
  resource-editor graphs. Resource capture must never save buffers or build output.
- CPS also owns evaluated `AlarmlistLookup` order and linked membership.
  Capture saved lookup bytes only, watch regeneration/deletion, and compare captured
  text/path/order/version before publication, completion or navigation. Lookup drafts
  and close notifications must not expose unsealed changes. The bridge requires the
  library-capable contract-8 context and copies catalogue labels, metadata, placeholder
  names and usage provenance into detached host DTOs. Never parse catalogues or RESX
  templates in the IDE. Template completion inserts explicit empty named arguments;
  lookup/member/metadata completion inserts compiler-supplied expressions, with no
  automatic metadata defaults or range validation. Suppress ambiguous path choices.
  Quoted argument text stays literal. Accepting completion for an empty argument
  slot or a whole-slot expression draft replaces its enclosing quotes in both
  designer and XML; never reinterpret tokens embedded in ordinary literal text
  or suggest evaluated nested localization arguments. See milestone 12 iteration 9.
  If a suggestion request is withdrawn during analysis refresh, consume its next
  completion key once only while draft/caret/selection remain unchanged; never
  turn interrupted acceptance into a source commit or bypass currentness checks.
  Route native explicit completion commands to the focused editable designer
  expression field; forwarding them to the hidden XML command target loses the
  designer request. XML focus retains the normal native XML command route.
- Designer semantics flow through per-view `IDesignerAnalysis` and the VS adapter.
  Embedded XML keyboard editing inherits native text-editor bindings only while
  the source view has aggregate focus; designer controls retain WPF draft editing.
  Preserve focus-handler disposal and verify physical keys in XML and Split modes,
  not only programmatic buffer replacement (milestone-nine iteration 15).
  Preview culture changes persist shared AlarmlistCulture through CPS without
  changing build cultures or committing ALMX drafts. Global overrides disable the
  selector; write failures retain evaluated state. Use
  the native Alarmlist toolbar's active-document command route for editor culture;
  an explicitly selected project preview routes culture to that project's CPS
  writer and also provides a window selector when no editor is open. Never retain
  an editor globally for toolbar state. Leaving the designer for shell
  controls preserves drafts; explicit commits and intra-designer focus changes still
  apply. Milestone 12 iteration 7 also preserves drafts across consumer selection
  and Designer/Split/XML mode changes; these presentation actions never commit.
  Keep the consumer selector's PreserveDraft focus behavior, including its child
  controls. Enter/Save and ordinary field-to-field commits retain their contracts.
  Consumer selection belongs to the physical text document, shared across its
  designer and native XML views through a weak document key. A known opening
  consumer supplies the initial choice; enumeration order never does. Removing
  membership or unloading a selected consumer requires a new choice. Failed
  evaluation retains known ownership while blocking semantic actions. Keep
  project/import byte guards and recovery watches through missing imports;
  request authoritative CPS reevaluation without targets or source saves. Leave
  disk reloads and XML cache ownership to CPS; ProjectRootElement.Reload itself
  marks XML dirty and must not be used to refresh live project imports.
  Freshness reads must share read/write/delete access. Seed private watch-hint
  evaluations from captured XML roots rather than MSBuild's writer-denying disk
  loader. Queue fallback reevaluation past native UI transactions and stamp it
  with the requesting analysis state; discard it when CPS has already advanced.
  Localization completion uses
  SDK catalogue/provenance, revalidates saved inputs before display and insertion,
  and discards late results after editing, focus or context changes. Navigate RESX
  diagnostics through the XML editor without saving pending resource edits.
  ALMX source navigation follows the personal Tools > Options > Alarmlist choice,
  defaulting to exact source-position selection in Designer. Keep Find's XML
  behavior, unsupported-location XML fallback and unapplied destination drafts.
  Navigation layout changes must not commit drafts in another editor view.
  Keep source presence/local drafts separate from inherited effective displays.
  Alarm-list and compact-selector names use current compiler-resolved values in
  the editor's preview culture, with raw-expression/identity fallback. Match source
  declarations by location and captured text/version; never reuse stale labels.
  Keep raw names and identifiers searchable and preserve filtered active drafts
  during semantic refresh. See milestone-nine iteration 13 for regression evidence.
  Only the SDK's provenance determines inheritance; equal text is not evidence.
  Revalidate current project inputs before copying/picking/navigating, preserve
  active drafts, and dispose notification subscriptions on view close. WPF adorners
  remain decorative, themed, and outside editing/compilation logic. Never dispose
  a FileSystemWatcher while holding the lock used by its callbacks.
- Bind designer item sources through read-only notifying collections and immutable
  presentation rows through the existing notification base. WPF's non-notifying
  collection cache and PropertyDescriptor subscriptions can retain closed views
  and analysis snapshots. Keep analysis DTOs independent of UI notification types.
- Add concise contextual tooltips to editor/preview controls when they clarify
  actions, states or unfamiliar concepts. Keep essential guidance visible and
  provide equivalent accessible help for keyboard/screen-reader users. Help must
  preserve focus and drafts; see [milestone seven's tooltip design](src/VisualStudio/MILESTONE-7.md#contextual-tooltips-and-help-2026-10-10).
- Localization completion searches key substrings without case sensitivity,
  prioritizing key prefixes, and inserts exact SDK-provided spelling. Never
  extend matching to translated text or template argument names, change compiler
  key identity, or bypass saved-input validation; see milestone seven's follow-up.
- Resolved summaries use SDK effective fields and surviving procedure entries.
  The general-purpose Alarm Summary window defaults to Follow active editor through
  a weak DocumentFrame context, or explicitly selects a loaded project. Project >
  Show Alarm Summary creates a new session-only pinned window on each invocation
  (milestone 11 iteration 29). Keep the general window's existing GUID/commands.
  Dedicated windows have read-only project scope and independent filter, sort,
  selection and tab state; language and saved columns remain shared per project.
  Keep pinned identity across editor switches/closure and window hide; never
  silently fall back after unload. Detach on hide/close, retaining only lightweight
  view state, and invalidate pending actions on scope changes.
  Keep previews outside the designer; reuse CPS-owned captured project analysis
  without creating another compiler, project subscription or dummy editor.
  The alarm table excludes abstract templates and uses immutable notifying rows
  from that same snapshot. Row selection drives read-only details without editing
  the designer; filtering/sorting preserve literal values. Revalidate captured
  inputs and selection/context lifetime before copying or navigating, including
  ordinary text copies from the preview. Keep row virtualization enabled.
  Materialize table rows only for the visible preview's selected context, rather
  than duplicating a project-wide table in every open editor view.
  Follow-active previews clear during that editor's private drafts. Explicit
  project previews include shared unsaved ALMX buffers and visibly explain that
  unapplied designer field edits are excluded; never auto-apply those drafts.
  Clear all scopes during stale/unavailable analysis; project XML requires
  whole-project success. Require context-aware contract 8 and preview
  support, and copy XML as a string outside the required property map.
- Designer lookup caches must validate current text/version before reuse and be
  keyed by immutable analysis state, captured input, and selected source. Keep
  unchanged preview controls stable so text selection/focus survives refreshes,
  while refreshing command availability during asynchronous validation.
- Procedure presentation uses SDK local/effective/excluded entries and origins;
  never infer Clear exclusions or execution order from displayed text. Section
  copies require current resolved inputs and produce one buffer edit with a leading
  Clear plus each effective entry once. Preserve the other section and surrounding
  XML, and refuse copies that would discard unsupported local content. Keep local
  draft controls stable during semantic refreshes.
- Designer BAML tests use the pinned shell assembly as a PackageDownload test
  asset; table tests use matching Shell.Framework and Interop assets, and document
  observer tests use the matching Text.Data, CoreUtility and Editor assets. Menu
  icon tests also use matching Imaging and ImageCatalog assets. This preserves
  the CPS test host's MSBuild dependency family. Keep those versions aligned with
  the extension's VS SDK; do not bundle these host assemblies in the VSIX.
- Use VS image monikers for editor commands and recognized project folders.
  Folder appearance must preserve CPS linked/excluded/missing state indicators
  and must not change item membership or special-folder behavior. Custom item
  template icons have editable vector sources under `eng/assets/VisualStudio/`;
  regenerate them with `eng/render-template-icons.ps1`, and never copy template
  artwork into consumer projects. See milestone seven's follow-up scope.
- Live diagnostics belong to `Alarmlist.LiveDiagnostics`, separate from Build
  diagnostics. Use immutable table snapshots, SDK IDs/messages/severities, and
  non-Build ErrorSource.Other classification. The Error List's Build-only filter
  intentionally hides them; explain the IntelliSense source filter rather than
  relabelling live results as Build or changing the user's filters automatically.
  Preserve zero-based Error List positions converted from compiler locations.
  Clear obsolete live rows on pending analysis/configuration changes/unload; report input/SDK
  failures separately without inventing ALM codes. Revalidate all captured inputs
  and project lifetime before navigation. Never clear another table source's data.
  Build rows have no captured source version; require a matching current diagnostic
  and validated inputs before routing their ALMX source through this preference.
  Track asynchronous navigation through an owned task collection and join through
  a separate factory on disposal; do not fire-and-forget via ThreadHelper's factory.
- Release the authoritative SDK package to NuGet.org before distributing its
  matching VSIX. `eng/release-visualstudio.ps1` separates candidate preparation,
  explicit SDK publication, and fresh-cache verification of the published SDK.
  Do not bundle a private SDK in the VSIX or modify users' NuGet sources on install.
- Command-line builds create a VSIX without deploying it. Visual Studio builds
  deploy to `AlarmlistExp`, which is also the F5 launch instance. Use Visual Studio
  2022 or 2026 for this workflow. Use desktop MSBuild for explicit experimental deployment
  and `eng/test-visualstudio.ps1` to launch the functional UI xUnit suite. Package
  tests alone do not verify Visual Studio UI behavior.
- The smoke script accepts `-RootSuffix` (default `AlarmlistExp`). Match the suffix
  used for deployment; a separate experimental profile allows validation without
  replacing an extension DLL held by an existing IDE session.
- The manual `scripts/start-vs2022.bat` and `scripts/start-vs2026.bat` launchers
  prepare and open `example/Example.sln`. Keep its generated SDK pin/NuGet config
  ignored, its cache tied to package contents, and its source files persistent.
  Its local Directory.Build files isolate this consumer from repository policy.
  The example targets link Shared/Hmi.almlookup and HmiInstructions.almx into both
  projects; RESX Set/Press patterns demonstrate named arguments, individually
  translated HMI labels and explicit metadata without duplicating sentence keys.
  Milestone-six Diagnostics.almx samples use saved per-project/root EditorConfig,
  explicit sample analyzer references, and intentional nonfatal warnings. The
  preparation helper builds both analyzer runtimes into ignored example/.analyzers;
  preserve these references and validate changes through ExamplePlaygroundTests.
- `scripts/start-custom-definition.bat` prepares the editable samples in
  `example/CustomDefinition/` using `eng/start-custom-definition.ps1`. It builds
  an isolated source copy under artifacts/def11, a matching SDK
  and VSIX, and a private consumer feed/cache; use AlarmlistDefinitionExp instead
  of replacing standard AlarmlistExp deployments. Preserve prior runs and edits.
  The default host is VS2026; -VisualStudioVersion 2022 and -PrepareOnly are
  supported. Definition changes take effect only after rebuilding matching tools.
  Sample RESX files must include the standard resmimetype/version/reader/writer
  headers used by the translations item template. Compiler acceptance alone does
  not establish native Resource Explorer compatibility; preserve reader and
  native editor coverage for editable resource samples (milestone 11 iteration 28).
- The library shortcuts `scripts/start-library-project-references.bat` and
  `scripts/start-library-packages.bat` share `eng/start-library-example.ps1` and
  open `example/Libraries/Libraries.sln` (VS2026 by default, optional VS2022).
  Preparation persists `UseLibraryPackages` in ignored `LibraryExample.local.props`.
  Package mode packs dependencies with project references before restoring all
  projects into a fresh cache. Keep sources shared and North/South untouched;
  validate switching and equivalent consumer output through LibraryExampleTests.
- `eng/measure-visualstudio-live.ps1` (also reached through the compatibility
  `test-visualstudio.ps1 -MeasureAnalysis` option) measures running-IDE buffer-to-diagnostic
  latency and repeated designer close/process-memory samples. Parameters select
  1-100 measured iterations and 1-10,000 additional fixture alarms. Keep unique
  diagnostic markers, two warmups, source/output preservation, and raw CSV data;
  do not add machine-dependent thresholds. These timings include 50 ms polling
  and COM overhead, exclude editor opening, and are not keystroke-paint timings.
  Optional CLR counters must match the owned IDE process; unavailable is not zero.
  Release the automation driver's temporary COM wrappers before sampling; do not
  force collection in the measured IDE.
  Whole-IDE memory trends without forced collection do not prove leak freedom.
- `eng/measure-visualstudio-analysis.ps1` runs opt-in x64 net472 measurements of the
  packaged SDK bridge and actual WPF designer BAML in Release through xUnit and
  BenchmarkDotNet in the dedicated PerformanceTests project. Dry validates the
  harness, Short takes two warmups/five samples, and Full uses default sampling.
  It keeps raw CSV/JSON, summaries, test results and revision/runtime details under
  `artifacts/log/`. Normal tests skip measurements; do not introduce machine-dependent
  timing assertions. These measurements exclude live VS/RDT capture and CPS debounce.
- The isolated `Area=Localization` performance workload captures 10,002 translations
  for 1,000 alarms. Keep its culture/parent-origin and named-argument correctness
  checks outside measured intervals, and pass the same captured resources into
  designer publication. Its completion measurement filters localization keys.
- The isolated `Area=Preview` performance workload uses 1,200 alarms with long
  effective procedures and more than 1,000 reference choices. Measure actual WPF
  report resize/scroll layout and reference-popup creation with compilation and
  correctness checks outside measured intervals. Also measure independent project
  report materialization/header layout from captured analysis, without an editor
  document adapter. Retain row-virtualization and no-source-commit checks; these
  measurements do not establish screen-paint latency.
- The isolated `Area=Procedures` workload extends designer and preview scenarios
  to fourteen ordered groups per alarm with inherited expression titles. Preserve
  group/title correctness, source-state and virtualization checks outside timing.
  See milestone eight for the implementation and acceptance record.
- The isolated `Area=Libraries` workload measures production saved-project graph
  capture, saved-change recapture/analysis, captured-library analysis and consumer
  bridge disposal for a three-library diamond with 1,000 concrete alarms. Its
  `Area=LibraryPreview` companion measures actual WPF project-report creation and
  publication of precomputed changed analysis. Keep saved writes, setup and
  correctness checks outside timing; verify deduplication, consumer values,
  source/output isolation and preview virtualization. These do not replace native
  saved-refresh/project-unload evidence or establish leak freedom.
- The isolated `Area=Analyzers` bridge workload captures the sample analyzer and
  saved configuration for 1,000 alarms, validating warning counts and host isolation
  outside timing. Its paired range scenario uses a full positive Int64 glob range;
  retain both ordinary-pattern and large-range measurements. Keep its sample DLL
  a test asset, never a compiler reference in the performance host. Functional
  `Area=Analyzers` cases belong in UIIntegrationTests.
- Live `Host=VisualStudio&Area=Analyzers` performance cases extend that measurement
  driver with saved sample-analyzer/configuration inputs, unique analyzer messages,
  and a third saved-configuration mode. Require the workload marker and complete
  three-mode reports; restore configuration and preserve source/output/DLL hashes.
- The live `Host=VisualStudio&Area=Libraries` workload measures a saved external library
  change through unique consumer diagnostic publication, then consumer removal
  from the solution through diagnostic removal. Re-add and verify the consumer
  outside timing. Retain raw samples and project-unload memory observations;
  project removal includes shell overhead and does not establish leak freedom.
- Live performance xUnit wrappers require `ALARMLIST_RUN_LIVE_VS_TESTS=1` and explicit
  host paths;
  they run only on net472 in PerformanceTests and use the dedicated live measurement
  driver. Report/ownership validation tests also belong in PerformanceTests.
  Preserve ordinary-run skipping, trait filters,
  cross-process serialization, complete-report validation and the driver's ownership
  of its test IDE. Keep the matching deployed configuration/profile explicit.
- Functional and live-performance xUnit setup shares `VisualStudioTestDeployment`.
  Compare the actual deployed VSIX payload with the built test asset, automatically
  deploying only if missing/outdated. Use the selected host's desktop MSBuild and
  the opt-in `eng/targets/VisualStudioTestDeployment.targets` to reuse VSSDK tasks
  without rebuilding runner-held assemblies. Keep setup under the shared mutex,
  refuse open target profiles, retain setup logs and verify the installed bytes.
  Ordinary tests never deploy. No independent deployment command is needed before
  opted-in xUnit runs; first-run IDE screens still require interactive setup.
- All functional UI integration tests require `ALARMLIST_RUN_UI_VS_TESTS=1` and the
  same explicit host paths/profile. Their net8.0-windows runner uses FlaUI 5 outside
  the IDE; keep its dependencies out of the VSIX. Preserve normal-run skipping,
  the shared live-test mutex, target-field/foreground checks before keyboard input,
  retained failure artifacts and cleanup of only the owned process. Each case owns
  a fresh IDE and isolated fixture/feed/cache. These tests
  require an unlocked desktop and cover project/editor lifecycle as separate xUnit
  cases; package-consumer coverage remains in IntegrationTests. The PowerShell
  smoke entry point only launches xUnit; keep functional assertions in C#.
  Use the session's captured shell HWND for shell lookup and failure capture;
  process main-window discovery can select a floating tool window, and modal
  failures can reject DTE calls. Capture UI evidence before requesting build logs.
  Native NuGet tests must await the UI's installed-state acknowledgement as well
  as saved project/assets changes before building the updated consumer.
  Native editor scenarios must select their intended alarm explicitly when source
  replacement or navigation can change selection. Hide or dock a persisted floating
  preview before mouse input it could obstruct, except when the scenario explicitly
  tests that window's placement or restoration. Preserve the functional assertions.
  Fresh-profile metadata tests must activate their owned document when the welcome
  page takes focus and provide a usable preview viewport before counting virtualized
  rows. Wait for completion dismissal before cancelling its draft; do not rely on
  back-to-back native keys or a previously persisted tool-window size.
- Run WPF editor unit tests, native IDE functional tests and isolated WPF
  measurements sequentially. They share desktop focus even when their project
  files and experimental profiles are isolated; overlapping runs can close menus
  or redirect keyboard input and invalidate interaction evidence.
  On shared/RDP desktops, concurrent user keyboard and mouse input can also
  interfere; account for that input before attributing interaction failures to
  the product or test stability.
- Run SDK XLIFF apply tests separately from native IDE tests. Their resource-editor
  guard inspects running IDEs; concurrent test-IDE startup/shutdown can make that
  check fail closed and invalidate an otherwise isolated package-consumer run.
- `eng/capture-visualstudio-heap.ps1` captures managed object counts, root paths
  and thread stacks from an explicitly selected test process using the optional
  ClrMD tool. Preserve PID/start-time identity checks and new report paths. Keep
  diagnostic capture separate from timing measurements; do not force collection
  in the inspected process or treat a reachable object as proof of a leak.
- `eng/prepare-visualstudio-acceptance.ps1` creates and builds an isolated linked
  North/South fixture from the SDK version in the built VSIX template. Follow
  `src/VisualStudio/ACCEPTANCE.md` for interactive checks and preserve the source/
  output baseline hashes. Fixture preparation alone does not validate the IDE.

## Coding style

- Follow `.editorconfig`: UTF-8, a final newline, spaces, four-space C#
  indentation, two-space XML/MSBuild/PowerShell indentation, and LF for shell
  scripts. Preserve surrounding line endings and avoid whole-file reformatting.
- Use the existing block-style namespaces and braces on new lines. Keep using
  directives outside namespaces with `System` imports first.
- Prefer explicit types where inference is unclear, C# keyword type names,
  `_camelCase` private/internal instance fields, `s_camelCase` private/internal
  static fields, and PascalCase constants, following `.editorconfig`.
- Preserve existing license headers and use the configured header for new C#
  files. Avoid renaming public symbols, package IDs, XML elements, or paths to
  normalize the repository's existing `AlarmList`/`Alarmlist` casing.
