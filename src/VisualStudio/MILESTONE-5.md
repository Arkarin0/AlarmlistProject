# Milestone five: localization through RESX and XLIFF exchange

Status: iterations one through seven are complete. Captured resources, catalogue
validation, localization expression evaluation, multi-culture SDK builds, detached
analysis, saved-resource observation, language selection and completion are
implemented. XLIFF exchange follows in iterations eight and nine.
The [implementation contract](MILESTONE-5-DESIGN.md) records the core
decisions and public names. The planning
baseline is `8034f00` on `dev/localization`, based on
`feature/ParametersAndLocalization`. Complete and validate each iteration, then
commit it before starting the next. Record actual results here as work proceeds;
the acceptance criteria below are not claims of passing tests.

The outcome is an author who maintains translations in Visual Studio's built-in
RESX editor, previews an alarm in a selected language, and builds several language
outputs from the same ALMX sources. RESX remains authoritative. XLIFF exchanges
translations with external tools and never becomes a build or preview input.

## Agreed behavior

| Area | Contract |
| --- | --- |
| Expressions | Use `$(loc:Key)` in Name, Code, Category, Description, and procedure text. FullyQualifiedName and ReferenceName remain literal. Keep raw expressions in source, edits, and local copies. Show raw text while focused and evaluated text when unfocused. |
| Keyword mode | The default displayed language is `keyword`. Display the complete literal `$(loc:Key)`, including syntactically valid keys not yet declared in any RESX file. Keyword mode is also an allowed build output. Still validate resource structure and duplicate catalogue entries. |
| Culture lookup | Look up the requested culture, then its parents, stopping before invariant resources. An empty translation counts as missing and continues parent lookup; for example, an empty `de-DE` entry may use `de`. Never fall back to an unqualified default RESX, an unrelated language, or keyword mode. If no nonempty translation is found, report an error. |
| Catalogue | Keys are project-wide and case-sensitive, including inherited alarm content. Allow dots and hyphens, such as `Alarm.Inspection` and `alarm-inspection`. Duplicate keys within a culture are errors; the same key in different cultures is expected. |
| Parameters and nesting | A translation such as `Inspection = "Inspect $(par1)"` uses the consuming alarm's parameter scope. With `par1=001`, it produces `Inspect 001`. Parameter assignments remain literal and replacement text is not rescanned. Initially disallow nested localization expressions inside translations; preserve escaped literal tokens. |
| Resources | RESX supplies compiler and designer translations. Use the built-in resource editor; do not introduce a custom translation-table editor. An unqualified keyword catalogue such as `Translations.resx` is optional. Language-specific files may define the catalogue; unqualified values never supply translation fallback. |
| Saved resources only | Build, preview, completion, localization diagnostics, and XLIFF export use saved RESX contents. Unsaved resource edits remain private to the resource editor. Refresh after a save or another change to the saved file; do not save editor buffers automatically. |
| Build selection | Shared `AlarmlistCultures` lists build cultures. Default to `keyword` when the property is absent or empty. Emit a separate folder for each culture. A validation failure for any requested culture fails the batch and preserves every previous culture output. |
| Preview selection | Singular `AlarmlistCulture` selects the active project's designer language and initially uses `keyword`. Persist it in the shared project. It does not change the build-culture list. |
| Exchange | Export RESX to XLIFF 2.0 and import translations into RESX. Target Trados Studio, memoQ, and Phrase TMS. Source culture is configurable and defaults to en. Any conflict rejects the whole import with a conflict report. |
| Exclusions | CAB support is cancelled. NuGet alarm libraries and release publication are outside this milestone. |

Milestone four's declaration-only parameter inheritance, explicit-empty parameter
assignments, abstract alarms, Clear behavior, and literal identity/reference
fields remain in force. Translation expressions participate in the existing
compiler evaluation rules rather than introducing a second evaluator in the IDE.
The saved-resource rule supersedes the earlier proposal to expose unsaved RESX
edits. It does not change milestone four's existing unsaved ALMX analysis.
The owner confirmed the remaining core choices above on 2026-10-07. Empty
translations count as missing; this differs from parameter assignments, where
an explicitly empty assignment still stops scope lookup.

## Remaining implementation details and exchange decisions

Core decisions are frozen in the implementation contract. The owner confirmed
on 2026-10-07 that exchange should support three leading translation tools, use a
configurable source culture defaulting to English, and reject the entire import
when conflicts exist. No owner-facing exchange decisions remain open. Tool
interoperability must still be tested in the exchange iterations.

| Detail | Proposed starting point |
| --- | --- |
| Key grammar | Freeze delimiters, whitespace, and escaping while preserving the agreed case sensitivity and support for dots/hyphens. |
| Resource inclusion | Introduce an evaluated resource item, provisionally `AlarmlistResource`, with explicit culture metadata and documented filename conventions. Support defaults, exclusions, explicit selection, linked files, and conditions. |
| Culture identity | Use .NET culture identities and the actual parent chain; stop before invariant. Validate unsupported names, duplicate selections, and output-path collisions before writing. |
| Public contracts | Freeze captured resource inputs, analysis contract version, diagnostics, output item names, and Clean/GetTargetPath behavior together. Existing diagnostic IDs stay unchanged. |
| XLIFF workflow/profile | Target Trados Studio, memoQ, and Phrase TMS with a documented XLIFF 2.0 subset for string resources, stable keys, notes, and protected expressions. AlarmlistXliffSourceCulture defaults to en and can be overridden per export. Verify round trips and reject unsupported structures explicitly. |
| XLIFF conflicts | Stop the whole import before changing any file, report all detected conflicts, and require their resolution. Never apply a partial subset or silently overwrite newer translations. |

The native resource editor's behavior in an `.almproj` project was an integration
question resolved for the installed hosts in iteration one. Verify preview refresh
in iteration six; capturing its unsaved model is not required. The extension supports VS2022
17.9+, while the newer Resource Explorer was introduced in 17.11; do not silently
raise the minimum version or assume all hosts provide the same resource-editing
surface.

## Implementation sequence

Iterations 1-7 deliver the core RESX build and designer workflow. Iterations 8-9
add XLIFF exchange after that workflow is usable. Iteration 10 closes integrated
acceptance. Each iteration includes relevant regression tests and documentation;
testing is not deferred wholesale to the final iteration.

| Iteration | Deliverable | Depends on |
| --- | --- | --- |
| 1 | Implementation contract and native resource-editor feasibility | Existing milestone-four baseline |
| 2 | Captured RESX inputs and culture catalogue | 1 |
| 3 | Localization evaluation and compiler diagnostics | 2 |
| 4 | Multi-culture SDK builds and output lifecycle | 3 |
| 5 | Detached localization analysis and SDK bridge | 3-4 |
| 6 | CPS resource inputs and saved-resource observation | 4-5 |
| 7 | Language selector and localization completion | 5-6 |
| 8 | XLIFF export | 2-4 and the frozen exchange profile |
| 9 | XLIFF import with conflict protection | 6, 8 |
| 10 | Integrated acceptance and documentation | 1-9 |

### Iteration 1: contract and native editor feasibility

- Write the milestone-five implementation contract, preserving the confirmed
  behavior and settling core implementation details, supported RESX content,
  diagnostic IDs, and the output ownership model. Track the exchange decisions
  for resolution before the XLIFF iterations.
- Exercise a small `.almproj` fixture with the built-in RESX editor on the
  supported VS2022/VS2026 workflows. Check project item association, open/edit/save,
  undo/discard, and notification when the saved file changes.
- Establish coherent capture of saved resource files. Verify that editing or
  discarding a dirty resource leaves consumers on the saved version and that saving
  triggers refresh. Unsaved native-editor buffer access is outside the requirement.
- Confirm the affected test baselines and record the existing deferred IDE check
  separately. Keep prototype code only when it becomes a supported integration
  or a repeatable test; do not leave exploratory product paths enabled.

Exit: a reviewable contract and evidence for the native-editor integration path.
Run focused project-item/schema tests and the feasibility scenarios. Record host
versions and any unverified support claims.

Commit: `Define localization contracts and verify native resource editing`.

### Iteration 2: captured RESX inputs and culture catalogue

- Add immutable, versioned resource snapshots and explicit culture inputs to the
  compiler. Host adapters capture saved RESX contents; keep filesystem access and
  MSBuild types outside compiler evaluation.
- Parse supported string entries, comments, whitespace, and explicit-empty values
  with useful locations. Prohibit DTDs and reject unsupported object/file-reference
  content without deserialization or external resource loading.
- Build a project catalogue that detects duplicate keys per culture and performs
  parent lookup without invariant/default fallback. Keep provenance for the
  requested culture, actual supplying culture, resource file, and key.
- Preserve existing non-localized callers until the coordinated analysis-contract
  transition; do not break the working extension between iterations.

Exit: compiler tests on `net472` and `net8.0` cover malformed XML, duplicate and
case-distinct keys, empty entries following missing-entry lookup, multiple files
per culture, parent chains, no default fallback, an optional unqualified catalogue,
and detached snapshots that need no disk access.

Commit: `Add captured RESX resources and culture lookup`.

### Iteration 3: expression evaluation and diagnostics

- Extend expression parsing and the compiler evaluator for localization tokens,
  keyword mode, and translations containing existing property expressions.
- Evaluate inherited translations using the consuming alarm's Alarm -> File ->
  Project scope. Share the field-dependency graph for alarm-field properties,
  including cycles introduced through translations. Do not rescan replacement
  strings or accidentally enable recursive parameter/localization expansion.
- Preserve raw/effective separation, escaping, literal identity fields, abstract
  template behavior, procedure ordering, and Clear exclusions. Validate structural
  errors without demanding contextual values from non-emitting templates.
- Add stable localization diagnostics with key, requested culture, and relevant
  ALMX/resource locations. Keep existing diagnostic IDs and source serialization.

Exit: compiler, analysis, and output tests on both frameworks cover `Inspect 001`,
literal keyword output for undeclared keys, exact/parent/missing/empty translations,
rejected nested localization, escaped tokens, cross-file inheritance, template-only
inputs, field cycles, and excluded entries. Empty translations either find a
nonempty parent value or report a missing-translation error.
Equivalent captured inputs produce the same values in compile and analysis paths.

Commit: `Evaluate localization expressions in the compiler`.

### Iteration 4: multi-culture SDK builds and output lifecycle

- Add evaluated resource items and `AlarmlistCultures` to the task/SDK. Capture one
  coherent input set for the batch and use compiler-owned culture evaluation.
- Validate cultures and output paths, then evaluate and serialize every requested
  output before replacing any destination. Stage writes and define rollback for
  ordinary write/cancellation failures; do not claim crash-atomic multi-file writes.
- Define the plural output item contract and migrate Build/GetTargetPath consumers.
  Track exact generated paths for Clean, removed cultures, custom output paths,
  and FileWrites. Never delete unrelated files by scanning culture directories.
- Preserve conditional/removed inputs, output exclusions, design-time guards,
  framework-free `.almproj` consumers, and both task runtimes. Add CPS property
  rules and update the SDK usage documentation with the public names.
- State the failure-preservation guarantee precisely: a failed ordinary Build
  preserves prior outputs. Rebuild invokes an explicit Clean first; do not promise
  preservation of files that Clean has already removed.

Exit: task tests and packaged SDK consumers on both frameworks cover multiple
cultures, keyword output including absent/empty build-culture defaults, parent
fallback, invalid selection, unchanged output bytes after a late culture error,
removed/conditional resources, linked files,
custom paths, Clean/Rebuild/GetTargetPath, and unrelated-file preservation.
Exercise desktop MSBuild task loading as well as `dotnet msbuild`.

Commit: `Build and manage separate alarm outputs for each culture`.

### Iteration 5: detached analysis and SDK bridge

- Extend captured context and detached results with resource versions, selected
  preview culture, translation provenance, and compiler diagnostics. Preserve raw
  expressions independently of evaluated values and compiled XML availability.
- Update the compiler contract, packaged consumer asset, isolated worker, bridge,
  and host-owned DTOs together. Require the new contract rather than retaining an
  unreleased compatibility path. Keep compiler assemblies out of the VS host.
- Make saved-resource/culture changes invalidate pending results. Preserve
  cancellation, version/generation checks, AppDomain disposal, and location mapping.

Exit: compiler snapshot tests, package-contract tests, and net472 bridge tests
verify build/preview parity, missing translations, parent provenance, unavailable
XML on errors, stale-resource rejection, and SDK/configuration replacement.

Commit: `Expose localization through detached analysis and the SDK bridge`.

### Iteration 6: project resources and saved-file changes

- Capture evaluated resource membership, order, metadata, and selected culture
  through CPS. Use the same project inputs as builds; avoid IDE-only discovery.
- Observe saved resource files using the integration proved in iteration one.
  Always use saved contents, including while the resource editor is dirty. Typing,
  undoing, or discarding unsaved edits must not publish new translations or make
  otherwise valid saved translations unavailable.
- Handle resource add/remove/rename, external edits, linked files, project context
  changes, and save notifications. Revalidate saved resource identity and content
  before publishing or navigating. Missing or invalid saved files produce normal
  input/translation errors; never substitute unsaved content. Do not save a
  resource merely to obtain preview.
- Extend watcher and lifetime tests; retain weak document ownership where needed,
  notifying presentation collections, and disposal outside callback locks.

Exit: project-input and observer tests cover membership and stale-result races.
Functional IDE cases verify that native RESX edits remain invisible until saved,
discard retains saved translations, and save/external changes refresh consumers.
Cover new unsaved resources and linked resources in two projects on both hosts.
Closing the resource editor or project releases owned subscriptions and watchers.

Commit: `Refresh localization analysis from saved resource changes`.

### Iteration 7: language selector and completion

- Preview the toolbar interaction before changing its layout. Add the active
  project's language selector with keyword mode and available cultures. Persist
  `AlarmlistCulture` through the project system; keep `AlarmlistCultures` independent.
  Handle read-only/global property values without pretending they were changed.
- Switch focused fields between raw and resolved display using the existing rules.
  Preserve active drafts, undo, selection, and focus during culture/saved-resource
  changes. Copies retain expressions and previews clear while analysis is stale.
- Add localization completion with project keys, translated previews, and parent
  provenance where useful. Revalidate inputs before completion or navigation;
  preserve full localization tokens in keyword mode.
- Update accessibility names, keyboard interaction, and acceptance instructions.

Exit: view-model/BAML tests and functional IDE scenarios cover language switching,
two-project context, persisted selection, dirty ALMX drafts, focus transitions,
completion remaining on saved resources while edits are pending and refreshing
after save, raw expression copies, and error recovery.
The RESX authoring/build/preview workflow is usable after this iteration.

Commit: `Add designer language selection and localization completion`.

### Iteration 8: XLIFF export

- Implement an explicit export command/target for the agreed profile. It must not
  run automatically during Build or become an alternative compilation input.
- Export saved RESX contents even when an editor has pending changes. Do not read
  unsaved translation values or trigger a save as part of export.
- Use configurable AlarmlistXliffSourceCulture (default en). Preserve stable key mapping, comments,
  whitespace, source/target cultures, and property-expression placeholders. Keep
  absent entries distinguishable from explicit empty values for round trips;
  both count as untranslated for culture lookup.
- Record source and target baselines sufficient to detect stale/conflicting
  imports, without embedding machine-specific paths. Document unsupported XLIFF
  structures and the supported translator workflow.

Exit: deterministic export/profile tests cover multiple cultures, missing and
empty targets, Unicode, escaping, notes, placeholders, and exclusion of pending
editor changes. Export changes neither RESX inputs nor compiled outputs.
An exported fixture opens in a selected
compatible translation tool; record which interoperability claim was checked.

Commit: `Export RESX translations through the supported XLIFF profile`.

### Iteration 9: XLIFF import and conflict protection

- Validate the profile, key/culture mapping, duplicates, placeholders, and export
  baselines before mutation. Restrict target paths to selected project resources;
  paths in an exchange file do not authorize filesystem writes.
- Detect source changes and conflicting target edits since export. Provide a
  reviewable dry-run result and explicit apply operation; never silently overwrite
  newer translations. Reimporting unchanged translations is a no-op.
- Apply accepted translations to RESX while preserving unrelated entries,
  comments, and surrounding format where supported. Preflight the entire import
  before writing and protect against partial updates on ordinary write failures.
- Refuse imports targeting dirty resource editors with a clear diagnostic so
  pending edits can be saved or discarded first. Never overwrite their disk files
  behind the editor or save their pending edits automatically. Successful imports
  update saved RESX files and refresh native editor/analysis state through the
  existing saved-file integration.

Exit: round-trip tests cover export -> translate -> import -> multi-culture build,
including stale sources, independently edited targets, malformed/unsupported
content, unknown keys/cultures, placeholder damage, dirty editors, and repeat
imports. Verify the resulting translations in both build output and IDE preview.

Commit: `Import XLIFF translations with conflict and dirty-buffer protection`.

### Iteration 10: integrated acceptance and documentation

- Run the combined compiler/task/SDK/extension suites and fresh-cache package
  consumers. Verify both compiler/task frameworks, desktop MSBuild loading, and
  the matching SDK/VSIX pair without publishing either package.
- Run the localization functional suite in VS2022 and VS2026, including native
  RESX authoring with updates visible only after save, culture switching, linked
  projects, failure recovery, multi-culture output preservation, and XLIFF exchange.
  Retain failure artifacts and distinguish skipped or blocked checks from passing
  results.
- Measure resource-heavy analysis and repeated open/close behavior with existing
  opt-in tooling. Preserve raw measurements, process ownership, and lifetime
  checks; do not introduce machine-dependent performance thresholds.
- Finish SDK/VS documentation, examples, acceptance steps, and the milestone result
  record. Document support limitations established by the feasibility work and
  any remaining release gates.

Exit: every implemented workflow has evidence at the appropriate compiler,
package, and IDE level. Remaining manual/release checks are explicit; no blocked
functional check is relabeled as complete.

Commit: `Complete localization acceptance coverage and documentation`.

## Validation and completion rules

Use the commands in [AGENTS.md](../../AGENTS.md), the
[SDK test workflow](../SDK/Alarmlist.SDK.Tests/README.md), and the
[Visual Studio workflow](README.md). Run the affected suites first and include
downstream consumers when compiler, task, packaging, or bridge contracts change.
Do not use `--no-build` against stale package contents. Normal test runs that skip
opt-in IDE scenarios are not evidence that those scenarios passed.

At each iteration, record changed behavior, commands/results, limitations, and the
commit. Keep its implementation buildable with the current SDK/extension pair.
If an essential acceptance check cannot run, record the blocker before continuing
with dependent work rather than calling the iteration successful.

The existing milestone-four VS2026 `PropertiesAndCompletion` scenario remains
pending at the owner's request after an authentication dialog blocked it. This
plan neither reruns it nor closes it. Existing screen-reader, high-contrast,
mixed-DPI, typing-paint, and long-session release checks also retain their recorded
status in [milestone four](MILESTONE-4.md).

No release publication is part of these iterations. A later release still needs
the authoritative SDK published and verified before distributing its matching
VSIX.

## Iteration-one results (2026-10-07)

- Added the implementation contract and separate VS2022/VS2026 native resource
  editor cases in the existing functional test harness. No localization runtime
  behavior has been enabled yet.
- Compiler baseline: 150 tests passed on net472 and 150 on net8.0. CPS rule
  baseline: all 7 RuleTests passed. The UI test project builds, and both new
  resource cases skip normally when live testing is not explicitly enabled.
- The complete VS2022 17.14.36930.0 resource case passed: native table opening,
  keyboard drafts, Undo, Escape cancellation, cell commit, Save, and reopening.
  Unfinished drafts remained off disk. The native editor automatically saved
  committed cells; the contract and test distinguish that save boundary from a
  pending draft. The owned IDE exited normally. Artifacts:
  `artifacts/log/Debug/VisualStudioUI/NativeResourceEditor/2022/9190053c2a5f424b985073dea439a70a/`.
- After adding bounded retry for rejected IDE startup automation calls, the
  complete VS2022 resource case passed again (one passed, none skipped). TRX:
  `artifacts/log/Debug/VisualStudioUI/TestResults/5668c9423c7f47adb4a67249e69e8560/`.
- The complete VS2026 18.10.12217.157 resource case passed on retry (one passed,
  none skipped), including draft isolation, Undo/Escape, cell commit, Save, and
  reopening. The owned IDE exited normally. The earlier startup failure is superseded by this
  successful run. Artifacts:
  `artifacts/log/Debug/VisualStudioUI/NativeResourceEditor/2026/9f14bcd491074d32813ea993fc2fc065/`.
- Deployed the current extension to the VS2022 and VS2026 AlarmlistExp profiles.
  The older VS2022 17.9 surface remains unverified.
- Iteration one is accepted for the installed hosts. The separate, previously
  deferred milestone-four test stays pending. Commit:
  `c036a4d` (`Define localization contracts and verify native resource editing`).

## Iteration-two results (2026-10-07)

- Added immutable `ResxTextSnapshot` inputs and a copied, read-only resource list
  on the resource-aware `CompilationContext` overload. The original constructor
  and analysis contract two remain available to the working IDE bridge.
- Added string-only RESX parsing with locations, comments, Unicode and whitespace
  preservation. Object/file references, serialization metadata, DTDs, malformed
  XML and unsupported structures produce diagnostics without loading resources
  from disk or deserializing objects.
- Added ordinal keys, duplicate diagnostics with related locations, normalized
  culture identities and parent-chain lookup. Empty values continue the parent
  search; invariant resources and unrelated languages never supply fallback.
  Compilation and analysis validate resource inputs even in keyword mode.
- Compiler tests: 203 passed on each of net472 and net8.0. MSBuild task tests:
  6 unit and 4 integration tests passed on each framework. Packaged SDK suite:
  20 passed on each framework. Existing SDK bridge tests: all 22 passed on net472.
  Commands used the affected project paths in AGENTS.md; the bridge run filtered
  `FullyQualifiedName~SdkAnalysisBridgeTests`. No tests were skipped in these runs.
  The existing shared test-helper CA1416 warning remains on net8.0.
- Commit: `55d986e` (`Add captured RESX inputs and culture catalogue`).

## Iteration-three results (2026-10-07)

- Added `$(loc:Key)` evaluation in all localizable fields and procedure text.
  Keyword mode retains the full token, including undeclared keys; other property
  expressions still evaluate normally. Escaping and substitutions remain single-pass.
- Translations use the consuming alarm/file/project scope, including built-in
  properties and the existing scalar dependency graph. Raw source expressions,
  literal identities/references, declaration-only inheritance, abstract templates,
  and Clear exclusions are preserved. Direct nested localization is rejected.
- Missing/nested translation diagnostics include the requested culture and key;
  relevant RESX locations accompany ALMX usage locations. Translated parameter,
  syntax, context and cycle failures keep the existing diagnostic IDs. Unavailable
  values do not expose raw expressions as effective values; errors suppress XML.
- Compiler, analysis and output coverage: 236 tests passed on each framework.
  MSBuild unit/integration suites: 6/4 passed on each framework. Packaged SDK
  suite: 20 passed on each framework. SDK bridge: 22 passed on net472. No skips;
  the existing shared test-helper CA1416 warning remains. The consuming-file test
  uses an absolute captured path, as required by the existing built-in properties.
- Commit: `a7ae209` (`Evaluate localization expressions in the compiler`).

## Iteration-four results (2026-10-07)

- Added evaluated AlarmlistResource items, explicit/inferred cultures and default
  RESX globs with output exclusions. Added build/preview project properties and
  native resource item rules. Saved XML inputs are captured once for all cultures.
- Build and GetTargetPath now return CompiledAlarmlistFiles with Culture metadata.
  Every output, including keyword, has its own culture directory. Updated direct
  task consumers, SDK tests, IDE fixtures, example instructions and tooling paths.
- All cultures validate and serialize before installation. Staged replacements,
  additions and retirements roll back on ordinary write/cancellation failures.
  Per-project/configuration/platform manifests track exact owned files; Clean
  preserves unrelated files and supports never-built projects. Retired paths are
  no longer owned after successful retirement. Input/output collisions are rejected.
- Compiler: 236 passed on each framework. Task unit/integration: 36/4 passed on
  each framework. SDK package consumers: 33 passed on each framework through
  dotnet msbuild, plus all 33 on net472 through VS2026 desktop MSBuild. The previous
  ALARMLIST_TEST_MSBUILD_PATH value was restored. No skips in these runs.
- Extension checks: 8 schema and 22 bridge tests passed on net472; 10 VSIX/template
  consumer tests passed on each framework. Native RESX functional cases passed
  with the new SDK rules on both installed hosts (one each, zero skipped):
  VS2022 TRX directory `artifacts/log/Debug/VisualStudioUI/TestResults/0b1e1e9dbdc144f8a65151e4b4c358e7/`;
  VS2026 `artifacts/log/Debug/VisualStudioUI/TestResults/89a208ba4e6b4d78bcaa032c6a91ee26/`.
  Each case used and cleaned up its owned IDE. The deferred milestone-four case
  was not run. Minimum VS2022 17.9 remains unverified.
- A new Clean-before-build regression initially failed because its directories
  did not exist; the fix and complete SDK rerun passed. Existing CA1416 warnings
  in the shared test helper remain. No power-loss atomicity is claimed.
- Recorded the owner's exchange decisions and researched Trados/memoQ/Phrase
  compatibility; source defaults to en and every import conflict rejects the batch.
- Commit: `a897f6e` (`Build and manage separate alarm outputs for each culture`).

## Iteration-five results (2026-10-07)

- Coordinated analysis contract three across the compiler, packaged consumer,
  isolated worker, bridge and host-owned DTOs. Older unreleased contracts are
  rejected with update guidance. Existing raw/effective fields, properties,
  procedure/Clear origins, abstract state and compiled XML gating remain intact.
- Added captured resource identities/versions, selected culture, raw catalogue
  declarations, duplicate ambiguity and compiler-owned translation lookup origins.
  Each alarm records actual localization uses and evaluated availability separately
  from lookup availability. Results stay immutable after subsequent analyses and
  after unloading the SDK domain. No compiler types enter the VS host.
- The bridge accepts captured saved-resource text and delegates filename inference
  to the SDK. Explicit-empty culture remains distinct. Resource or culture changes
  invalidate in-flight project results without requiring ALMX changes. CPS capture
  of resource membership and filesystem refresh remain iteration-six work.
- Compiler: 244 passed on each framework. Task unit/integration: 36/4 passed on
  each framework. SDK package suite: 33 passed on each framework. The extended
  packaged analysis-contract case also passed under VS2026 desktop MSBuild;
  the prior environment setting was restored.
- Extension unit suite: all 262 passed on net472, including 26 bridge cases and
  resource/culture generation-race tests. VSIX/template consumer suite: 10 passed
  on each framework. No skips in these runs; the existing shared-helper CA1416
  warning remains. No new IDE interaction is claimed by this bridge iteration.
- Commit: `ccac17f` (`Expose localization through detached analysis and the SDK bridge`).

## Iteration-six results (2026-10-07)

- CPS captures evaluated resource membership, order, duplicates, culture metadata
  and preview/build selections separately. Analysis captures saved resource bytes
  once per path, without accessing resource buffers. Native and XML resource drafts,
  Undo, cancellation and editor close do not invalidate saved translations.
- Saved changes refresh each linked project context. Resource removal, rename,
  missing/invalid files and recovery use existing input/SDK diagnostics. Publication
  and navigation revalidate captured content as well as project generation; resource
  changes racing ahead of watcher events cannot publish or navigate stale results.
  Resource diagnostic navigation uses captured saved text and source locations.
- Extension unit suite: 270 passed on net472. The final malformed-resource recovery
  assertions also passed in a focused rerun. No skips.
- VSIX/template consumers: 10 passed on each framework, with no skips.
- Functional saved-resource/linked-context cases passed on both installed hosts
  (one each, zero skipped): native draft/Undo/discard/save, parent lookup, project
  scope, external changes, missing-translation recovery and solution reopen. Preview
  did not save ALMX or replace compiled outputs. Screenshots were inspected.
  VS2022 TRX directory: `artifacts/log/Debug/VisualStudioUI/TestResults/4ef76c8a22aa49f9970eb2b5f1ecef9e/`;
  VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/16ba8d5946e4427b8d8ec88916a9837c/`.
  Each run closed its owned IDE. The deferred milestone-four test was not run;
  minimum VS2022 17.9 remains unverified.
- Commit: `e1de42e` (`Refresh localization analysis from saved resource changes`).

## Iteration-seven results (2026-10-07)

- Added the project preview-language selector, shared CPS persistence, independent
  linked-project choices, global/read-only failure handling and draft preservation.
  Project choices remain stable during analysis refresh. The toolbar uses existing
  responsive layout and accessibility conventions.
- Added case-sensitive localization completion from SDK catalogue/provenance,
  saved-input validation before display and insertion, late-result guards, full
  raw token insertion and one-edit Undo. Resource diagnostics explicitly open the
  XML editor, preserving resource drafts and compiled outputs.
- Extension unit suite: 275 passed on net472. VSIX/template consumers: 10 passed
  on each framework. Performance harness: 26 passed, six opt-in measurements
  skipped; no new performance measurements are claimed.
- VS2022 linked-context/language persistence passed in TRX directory
  `artifacts/log/Debug/VisualStudioUI/TestResults/d641577d34d9486ca44fd92dd706eb59/`.
  Completion, dirty-RESX isolation, save refresh, Undo and exact resource navigation
  passed in `artifacts/log/Debug/VisualStudioUI/TestResults/8819ffc388f74da18293bd1eb8bf26ea/`.
  Completion and keyword-selector screenshots were inspected.
- VS2026 linked-context/language persistence passed in
  `artifacts/log/Debug/VisualStudioUI/TestResults/17127d763242455090eb731c2c288d33/`.
  The completion case passed on the owner's requested retry in
  `artifacts/log/Debug/VisualStudioUI/TestResults/4889625e8f444e2796f4aab4525209b7/`
  (one passed, zero skipped), including dirty-resource isolation, completion/Undo,
  save refresh, keyword mode and exact XML diagnostic navigation. Earlier attempts
  encountered UIA readiness timeouts or stopped at the foreground safeguard while
  installer permission prompts were present. No security prompt was automated.
  UIA readiness timeouts retry within the existing bounded wait; keyboard focus
  safeguards remain intact. Both VS2026 screenshots were inspected. This retry's
  cleanup used the harness's bounded forced termination of its owned IDE.
- Both hosts have the current iteration deployed to AlarmlistExp. Each functional
  case closed its owned IDE. Minimum VS2022 17.9, screen-reader/high-contrast and
  other recorded release checks remain unverified. The separately deferred
  milestone-four test was not run.
- Commit: `Add designer language selection and localization completion`.

## Exchange compatibility decision (2026-10-07)

The three target tools are Trados Studio, memoQ, and Phrase TMS, selected from
the leading CAT suites reported by the [ELIS 2026 survey](https://elis-survey.org/wp-content/uploads/2026/03/ELIS-2026-Report.pdf).
This is an industry-survey basis, not a universal popularity ranking.
Use XLIFF 2.0 as the common exchange subset: [Trados documentation](https://developers.rws.com/studio-api-docs/apiconcepts/filetypesupport/bilingual_file_type_plugin_introduction.html)
and [memoQ documentation](https://docs.memoq.com/current/en/Workspace/xliff-2-files.html)
cover this format, while [Phrase TMS requires the 2.0 version header](https://support.phrase.com/hc/en-us/articles/5709613119516--XLIFF-1-2-and-2-0-XML-Localization-Interchange-File-Format-TMS).
This replaces the initial 2.1 proposal. Documentation compatibility is not evidence
of completed round trips; those remain iteration-eight/nine acceptance work.
Source culture defaults to en, configurable by AlarmlistXliffSourceCulture and a
per-export override. It has no effect on compilation/preview fallback.
Any import conflict rejects the whole batch and reports conflicts for resolution.

## Architecture and reference basis

- Follow Roslyn's separation of captured additional text and compiler options:
  [AdditionalText](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/AdditionalText.cs)
  and [CompilationOptions](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Compilation/CompilationOptions.cs).
  Adapt these boundaries to the existing ALMX snapshot/context model; no Roslyn
  dependency is needed.
- Keep resource evaluation in the compiler, task orchestration in MSBuild, and
  consumer integration in the SDK, following the
  [Arcade SDK organization](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk)
  and this repository's existing package-test fixtures. Preserve the pinned
  `Arkarin0.DotNet.Arcade.Sdk` integration.
- Microsoft's [resource editor documentation](https://learn.microsoft.com/en-us/visualstudio/ide/managing-application-resources-dotnet?view=visualstudio)
  describes .NET-project resource editing and version-specific UI. Custom ALMX
  project support and save-triggered refresh still require iteration-one evidence.
- Use the [OASIS XLIFF 2.0 specification](https://docs.oasis-open.org/xliff/xliff-core/v2.0/os/xliff-core-v2.0-os.html)
  to define and test the exchange subset. Supporting that subset must not be
  described as support for arbitrary XLIFF documents.
