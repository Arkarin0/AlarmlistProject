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
| [Milestone 2](src/VisualStudio/MILESTONE-2.md) | Shared ALMX document, designer/XML editing, and editor lifecycle. |
| [Milestone 3](src/VisualStudio/MILESTONE-3.md) | Reference-aware editing, analysis, diagnostics, previews, and lifecycle/performance follow-ups. |
| [Milestone 4](src/VisualStudio/MILESTONE-4.md) | Property expressions, parameters, abstract alarms, and designer support; see its [design contract](src/VisualStudio/MILESTONE-4-DESIGN.md). |
| [Milestone 5](src/VisualStudio/MILESTONE-5.md) | Localization and XLIFF, including HMI lookup, named-list, and preview follow-up iterations; see its [design contract](src/VisualStudio/MILESTONE-5-DESIGN.md) and [lookup specification](src/VisualStudio/HMI-LOOKUPS.md). |
| [Milestone 6](src/VisualStudio/MILESTONE-6.md) | EditorConfig, diagnostic policy, external analyzers, and compatibility/lifecycle validation iterations. |
| [Milestone 7](src/VisualStudio/MILESTONE-7.md) | ALMX editor UI/UX: procedures, property fields, IntelliSense, searchable references, file/alarm scope separation and preview-table rendering/column preferences; accepted direction and open interaction details. |

Read the selected plan's current status and remaining gates before resuming it.
[FUTURE-FEATURES.md](FUTURE-FEATURES.md) holds ideas and design history; it does not
authorize implementation or supersede the confirmed scope in a milestone plan.

## Project map

Alarmlist compiles XML-based `.almx` source files into a resolved XML alarm list.
It provides a compiler library, MSBuild tasks, and a NuGet-distributed MSBuild SDK
for `.almproj` projects. The solution is `AlarmlistProject.slnx`.

| Location | Responsibility |
| --- | --- |
| `src/Compiler/` | `Alarmlist.Core`: source parsing, syntax, binding, diagnostics, resolved models, output writers, culture/resource catalogues in `Localization/`, captured path/list catalogues and XSD in `Lookups/`, and detached snapshot/provenance results in `Analysis/`. |
| `src/Compiler.UnitTests/` | Compiler tests organized by text parsing, syntax, binding, analysis, and output. |
| `src/Analyzers/Alarmlist.SampleAnalyzer/` | Explicit opt-in, dual-framework contract-1 sample rule; also a package/UI test asset. |
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
| `scripts/` | Batch launchers that deploy the extension and start the Visual Studio 2022 or 2026 experimental instance. |
| `example/` | Persistent two-project designer playground opened by the launchers; `eng/prepare-example.ps1` prepares the SDK matching the built VSIX without replacing sample sources. |
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
  literal inline punctuation and immutable captured results. ALM0001-ALM0025
  remain mandatory errors regardless of NoWarn or per-file severity. Determine
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
  uses the same bound inputs and normal XML output writer. Contract 3 also accepts
  captured CompilationContext. Preserve raw expressions separately from evaluated
  values, abstract state, availability, and declaration/assignment origins. The
  unreleased contracts 1 and 2 are no longer supported by the bridge. `CompiledXml` is unavailable
  on errors; in-memory previews must never save source or write build output.
- Contract 3 localization results retain captured resource identities/versions,
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

- Milestone-six analyzers are explicit `AlarmlistAnalyzer` DLL items. Preserve
  saved dependency-byte capture, contract-1 detached inputs, cooperative cancellation,
  ALM0025 failure reporting, and diagnostic policy before output installation. The
  net472 loader must reject changed/conflicting images in an existing AppDomain;
  VS replaces the domain on payload changes, while desktop builds need a new process.
  Do not claim silent reload or load compiler/plugin types into the VS host domain.

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
  Build/GetTargetPath expose `@(CompiledAlarmlistFiles)` with Culture metadata;
  the task exposes OutputFiles. Preserve explicit-empty resource Culture metadata,
  linked/conditional items and output exclusions. Preview AlarmlistCulture is separate.
- Lookup builds capture saved `AlarmlistLookup` items once with the same input batch.
  Default `**/*.almlookup` inclusion follows source exclusions; explicit, linked,
  conditional and duplicate inputs retain their semantics. Protect lookup files
  from output/manifest/exchange collisions. `AlarmlistLookupSchema` locates the
  packaged authoritative XSD. XLIFF protects named placeholder occurrences as well
  as parameter/lookup expressions, preserving text around leading inline codes.
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
- The evaluated `AlarmlistAnalysisCompilerAssembly` property identifies the resolved
  SDK's net472 compiler independently of the build task runtime. The extension's
  `SdkAnalysisBridge` loads it in a dedicated AppDomain and copies contract-3 data
  into host-owned results. Do not load compiler types in the VS host domain, add a
  compiler project reference, or substitute another SDK on failure. Call bridge
  operations off the UI thread; owners must dispose on SDK/configuration changes
  and unload. `AlarmlistProjectAnalysis` owns the service through CPS dynamic-load
  callbacks and the active evaluation source. Preserve evaluated Compile order and
  duplicates, open-buffer precedence (including XML-only views), immediate stale
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
- CPS also owns evaluated `AlarmlistLookup` order, duplicates and linked membership.
  Capture saved lookup bytes only, watch regeneration/deletion, and compare captured
  text/path/order/version before publication, completion or navigation. Lookup drafts
  and close notifications must not expose unsealed changes. The bridge requires the
  lookup-capable contract-3 context and copies catalogue labels, metadata, placeholder
  names and usage provenance into detached host DTOs. Never parse catalogues or RESX
  templates in the IDE. Template completion inserts explicit empty named arguments;
  lookup/member/metadata completion inserts compiler-supplied expressions, with no
  automatic metadata defaults or range validation. Suppress ambiguous path choices.
- Designer semantics flow through per-view `IDesignerAnalysis` and the VS adapter.
  Preview culture changes persist shared AlarmlistCulture through CPS without
  changing build cultures or committing ALMX drafts. Global overrides disable the
  selector; write failures retain evaluated state. Use
  the native Alarmlist toolbar's active-document command route for preview culture;
  never retain an editor globally for toolbar state. Leaving the designer for shell
  controls preserves drafts; explicit commits and intra-designer focus changes still
  apply. Localization completion uses
  SDK catalogue/provenance, revalidates saved inputs before display and insertion,
  and discards late results after editing, focus or context changes. Navigate RESX
  diagnostics through the XML editor without saving pending resource edits.
  Keep source presence/local drafts separate from inherited effective displays.
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
- Resolved previews use SDK effective fields and surviving procedure entries.
  The dockable Alarmlist Preview window follows the shell's DocumentFrame selection
  through a weak document context. Detach on hide/close and invalidate pending
  preview actions on document switches. Keep previews outside the designer; reuse
  its captured analysis without creating another compiler or project subscription.
  The alarm table excludes abstract templates and uses immutable notifying rows
  from that same snapshot. Row selection drives read-only details without editing
  the designer; filtering/sorting preserve literal values. Revalidate captured
  inputs and selection/context lifetime before copying or navigating, including
  ordinary text copies from the preview. Keep row virtualization enabled.
  Materialize table rows only for the visible preview's document context, rather
  than duplicating a project-wide table in every open editor view.
  Clear both previews during drafts or stale/unavailable analysis; project XML
  requires whole-project success. Require context-aware contract 3 and preview
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
  zero-based Error List positions converted from compiler locations. Clear obsolete
  live rows on pending analysis/configuration changes/unload; report input/SDK
  failures separately without inventing ALM codes. Revalidate all captured inputs
  and project lifetime before navigation. Never clear another table source's data.
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
  correctness checks outside measured intervals. Retain row-virtualization and
  no-source-commit checks; these measurements do not establish screen-paint latency.
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
