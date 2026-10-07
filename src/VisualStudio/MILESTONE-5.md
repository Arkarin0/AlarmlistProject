# Milestone five: localization through RESX and XLIFF exchange

Status: planned; localization implementation has not started. The planning
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
| Expressions | Use `$(loc:Key)` in localizable alarm content. Keep raw expressions in source, edits, and local copies. Show raw text while focused and evaluated text when unfocused. |
| Keyword mode | The default displayed language is `keyword`. Display the complete literal `$(loc:Key)`. Keyword mode is also an allowed build output. |
| Culture lookup | Look up the requested culture, then its parents, stopping before invariant resources. For example, `de-DE` may use `de`. Never fall back to an unqualified default RESX, an unrelated language, or keyword mode. An unresolved translation is an error. |
| Catalogue | Keys are project-wide, including inherited alarm content. Duplicate keys within a culture are errors; the same key in different cultures is expected. |
| Parameters | A translation such as `Inspection = "Inspect $(par1)"` uses the consuming alarm's parameter scope. With `par1=001`, it produces `Inspect 001`. Parameter assignments remain literal and replacement text is not rescanned. |
| Resources | RESX supplies compiler and designer translations. Use the built-in resource editor; do not introduce a custom translation-table editor. |
| Build selection | Shared `AlarmlistCultures` lists build cultures. Emit a separate folder for each culture. A validation failure for any requested culture fails the batch and preserves every previous culture output. |
| Preview selection | The proposed singular `AlarmlistCulture` selects the active project's designer language and initially uses `keyword`. Persist it in the shared project. It does not change the build-culture list. |
| Exchange | Export RESX to XLIFF and import translated values into RESX. Define conflict and stale-export handling before importing. |
| Exclusions | CAB support is cancelled. NuGet alarm libraries and release publication are outside this milestone. |

Milestone four's declaration-only parameter inheritance, explicit-empty parameter
assignments, abstract alarms, Clear behavior, and literal identity/reference
fields remain in force. Translation expressions participate in the existing
compiler evaluation rules rather than introducing a second evaluator in the IDE.

## Defaults to finalize in iteration one

These are implementation proposals, not additional owner decisions. Resolve them
in the implementation contract before dependent work; ask the owner only if an
alternative materially changes the intended workflow.

| Detail | Proposed starting point |
| --- | --- |
| Missing build-culture list | Build `keyword` when `AlarmlistCultures` is absent. Reject malformed entries and define how an explicitly empty list differs from absence. |
| Missing versus empty | An explicit empty translation is a valid value and stops parent lookup. Only a missing key falls through. |
| Keyword validation | Preserve syntactically valid localization tokens without requiring a translated value. Still validate resource structure and duplicate catalogue entries. |
| Keys | Use ordinal, case-sensitive keys; support useful RESX names such as `Alarm.Inspection` and `alarm-inspection`. Freeze delimiters, whitespace, and escaping rules. |
| Expression targets | Use the same fields as milestone four: Name, Code, Category, Description, and procedure text. FullyQualifiedName and ReferenceName stay literal. |
| Translation contents | Allow existing property expressions inside translations. Reject nested localization expressions in the first profile rather than introducing recursive localization. Preserve escaped literal tokens. |
| Resource inclusion | Introduce an evaluated resource item, provisionally `AlarmlistResource`, with explicit culture metadata and documented filename conventions. Support defaults, exclusions, explicit selection, linked files, and conditions. |
| Unqualified RESX | It may hold the key catalogue and authoring context, but its values never supply an implicit translation. Define whether it is optional and how an explicit XLIFF source language obtains source text. |
| Culture identity | Use .NET culture identities and the actual parent chain; stop before invariant. Validate unsupported names, duplicate selections, and output-path collisions before writing. |
| Public contracts | Freeze captured resource inputs, analysis contract version, diagnostics, output item names, and Clean/GetTargetPath behavior together. Existing diagnostic IDs stay unchanged. |
| XLIFF profile | Start with a documented XLIFF 2.1 subset supporting string resources, stable keys, notes, and protected expression placeholders. Reject unsupported structures explicitly. |

The native resource editor's behavior in an `.almproj` project is an integration
question, not a confirmed capability. Its handling of unsaved edits must be proved
early. The extension supports VS2022 17.9+, while the newer Resource Explorer was
introduced in 17.11; do not silently raise the minimum version or assume all hosts
provide the same resource-editing surface.

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
| 6 | CPS resource inputs and live resource observation | 4-5 |
| 7 | Language selector and localization completion | 5-6 |
| 8 | XLIFF export | 2-4 and the frozen exchange profile |
| 9 | XLIFF import with conflict protection | 6, 8 |
| 10 | Integrated acceptance and documentation | 1-9 |

### Iteration 1: contract and native editor feasibility

- Write the milestone-five implementation contract, settling the proposals above,
  supported RESX/XLIFF content, diagnostic IDs, and the output ownership model.
- Exercise a small `.almproj` fixture with the built-in RESX editor on the
  supported VS2022/VS2026 workflows. Check project item association, open/edit/save,
  undo/discard, and the source available while the resource editor is dirty.
- Determine whether its unsaved model is exposed through a text buffer or another
  supported API. Establish how to capture coherent versions without saving files.
  If this is unavailable on a supported host, document the concrete limitation and
  resolve the live-preview requirement before implementing dependent behavior.
- Confirm the affected test baselines and record the existing deferred IDE check
  separately. Keep prototype code only when it becomes a supported integration
  or a repeatable test; do not leave exploratory product paths enabled.

Exit: a reviewable contract and evidence for the native-editor integration path.
Run focused project-item/schema tests and the feasibility scenarios. Record host
versions and any unverified support claims.

Commit: `Define localization contracts and verify native resource editing`.

### Iteration 2: captured RESX inputs and culture catalogue

- Add immutable, versioned resource snapshots and explicit culture inputs to the
  compiler. Keep filesystem access and MSBuild types outside compiler evaluation.
- Parse supported string entries, comments, whitespace, and explicit-empty values
  with useful locations. Prohibit DTDs and reject unsupported object/file-reference
  content without deserialization or external resource loading.
- Build a project catalogue that detects duplicate keys per culture and performs
  parent lookup without invariant/default fallback. Keep provenance for the
  requested culture, actual supplying culture, resource file, and key.
- Preserve existing non-localized callers until the coordinated analysis-contract
  transition; do not break the working extension between iterations.

Exit: compiler tests on `net472` and `net8.0` cover malformed XML, duplicate and
case-distinct keys, empty versus missing, multiple files per culture, parent
chains, no default fallback, and detached snapshots that need no disk access.

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
literal keyword output, exact/parent/missing/empty translations, escaped tokens,
cross-file inheritance, template-only inputs, field cycles, and excluded entries.
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
cultures, keyword output, parent fallback, invalid selection, unchanged output
bytes after a late culture error, removed/conditional resources, linked files,
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
- Make resource/culture changes invalidate pending results. Preserve cancellation,
  version/generation checks, AppDomain disposal, and location mapping.

Exit: compiler snapshot tests, package-contract tests, and net472 bridge tests
verify build/preview parity, missing translations, parent provenance, unavailable
XML on errors, stale-resource rejection, and SDK/configuration replacement.

Commit: `Expose localization through detached analysis and the SDK bridge`.

### Iteration 6: live project resources and unsaved edits

- Capture evaluated resource membership, order, metadata, and selected culture
  through CPS. Use the same project inputs as builds; avoid IDE-only discovery.
- Integrate the supported native-editor observation path proved in iteration one.
  Unsaved edits are authoritative. Do not silently use disk when a dirty resource
  cannot be captured; show an unavailable/pending result instead.
- Handle resource add/remove/rename, external edits, linked files, project context
  changes, and closing/discarding documents. Revalidate resource identity and text
  before publishing or navigating. Never save a resource merely to obtain preview.
- Extend watcher and document lifetime tests; retain weak document ownership,
  notifying presentation collections, and disposal outside callback locks.

Exit: project-input and observer tests cover membership and stale-result races.
Functional IDE cases verify native RESX edit/undo/save/discard, external changes,
and correct behavior for linked resources in two projects on both supported hosts.
Closing the resource editor or project releases owned subscriptions and watchers.

Commit: `Refresh localization analysis from live resource edits`.

### Iteration 7: language selector and completion

- Preview the toolbar interaction before changing its layout. Add the active
  project's language selector with keyword mode and available cultures. Persist
  `AlarmlistCulture` through the project system; keep `AlarmlistCultures` independent.
  Handle read-only/global property values without pretending they were changed.
- Switch focused fields between raw and resolved display using the existing rules.
  Preserve active drafts, undo, selection, and focus during culture/resource
  changes. Copies retain expressions and previews clear while analysis is stale.
- Add localization completion with project keys, translated previews, and parent
  provenance where useful. Revalidate inputs before completion or navigation;
  preserve full localization tokens in keyword mode.
- Update accessibility names, keyboard interaction, and acceptance instructions.

Exit: view-model/BAML tests and functional IDE scenarios cover language switching,
two-project context, persisted selection, dirty ALMX drafts, focus transitions,
completion after unsaved resource edits, raw expression copies, and error recovery.
The RESX authoring/build/preview workflow is usable after this iteration.

Commit: `Add designer language selection and localization completion`.

### Iteration 8: XLIFF export

- Implement an explicit export command/target for the agreed profile. It must not
  run automatically during Build or become an alternative compilation input.
- Select the source language explicitly. Preserve stable key mapping, comments,
  whitespace, source/target cultures, and property-expression placeholders. Keep
  missing translations distinguishable from explicit empty values.
- Record source and target baselines sufficient to detect stale/conflicting
  imports, without embedding machine-specific paths. Document unsupported XLIFF
  structures and the supported translator workflow.

Exit: deterministic export/profile tests cover multiple cultures, missing and
empty targets, Unicode, escaping, notes, and placeholders. Export changes neither
RESX inputs nor compiled outputs. An exported fixture opens in a selected
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
- Define the open-editor boundary: update through a supported undoable buffer
  operation or refuse conflicting dirty resources with a clear diagnostic. Never
  overwrite their disk files behind the editor. Refresh native editor/analysis
  state through the existing integration.

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
  RESX authoring, culture switching, linked projects, failure recovery, multi-culture
  output preservation, and XLIFF exchange. Retain failure artifacts and distinguish
  skipped or blocked checks from passing results.
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
  project support and unsaved capture still require the iteration-one evidence.
- Use the [OASIS XLIFF 2.1 specification](https://docs.oasis-open.org/xliff/xliff-core/v2.1/os/xliff-core-v2.1-os.html)
  to define and test the exchange subset. Supporting that subset must not be
  described as support for arbitrary XLIFF documents.
