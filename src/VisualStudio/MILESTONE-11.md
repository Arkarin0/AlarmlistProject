# Milestone eleven: generated alarm definitions end to end

Status: iterations 1–8 complete; original acceptance recorded on 2026-10-23.
Follow-up iterations 9–15 complete; follow-up acceptance recorded on
2026-10-23. See the collection/playground extension below. Original acceptance
does not cover these additions. Preview follow-up planning (iteration 16) is
complete; iterations 17–21 are complete with combined acceptance recorded below.
The owner approved the combined preview/designer plan after the
interactive layout review; completed iteration evidence remains unchanged.
Execution resumed during iteration 20 after the owner explicitly removed the
20% allowance stop for this goal. The earlier pause checkpoint remains below.
Owner decisions were confirmed on 2026-10-21.
Implementation was authorized on 2026-10-22. The original 20% allowance pause
was honored and subsequently waived by the owner for completion of this goal.
Later follow-ups 22–27 are complete, including multilingual enum captions and
Alarm Summary terminology. For the multilingual-caption/Summary goal (24–27), the owner
re-enabled the 20% remaining-allowance stop; that current rule supersedes the
earlier goal's waiver. See the follow-up evidence at the end of this document.
Iterations 28–29 are complete: editor-compatible RESX samples and dedicated
project-pinned Summary windows. Their goal retained the 20% allowance stop and
completed above it; validation and recovery evidence is recorded below.

## Outcome and confirmed scope

A repository fork can add, remove, reorder or retype ordinary alarm fields by
editing one XML definition and rebuilding matching compiler, SDK and VSIX tools.
The definition drives syntax/resolved models, parsing/serialization, expression
field discovery, analysis/analyzer metadata, designer fields and preview columns.
No field-specific handwritten change is required for supported scalar types.

Confirmed types: string, signed Int64, decimal, finite double, boolean, enum,
date-only, time-only and timestamp with explicit UTC offset. Expressions remain
available for every ordinary field. Definitions support required/optional fields,
literal defaults, inclusive numeric bounds and enum choices. Designer controls
are metadata-driven, retaining inheritance, drafts, completion and navigation.
Keyword localization tokens remain in keyword output, with typed validation
explicitly deferred; concrete-culture output validates the evaluated value.

FullyQualifiedName, ReferenceName, abstract state, parameters and procedure/group
structure remain reserved specialized concepts. Existing ordinary string fields
and their default behavior remain unchanged in this repository. Additional fields
exist in test fixtures, not as new built-in product fields.

Originally excluded (iterations 1–8): per-project schema loading, arbitrary CLR/plugin types, custom
collections, local timestamps without offsets, durations, cross-field rules,
conditional visibility, XML IntelliSense, VS Code support, inherited-entry
reordering, publication and separate OS theme/DPI display checks.

## Design and implementation defaults

### Definition and generation

- Add a versioned XML definition under `src/Compiler/Definition/`, with a checked-in
  XSD and a deterministic standalone net8.0 generation tool under `eng/tools/`.
  Keep generated compilation inputs under the existing artifacts/obj layout;
  do not check generated build outputs into Git. Shared eng targets own generation.
- Ordinary field descriptors contain stable name/XML element name, scalar type,
  required flag, optional literal default, numeric minimum/maximum, enum tokens
  and optional display labels, label/help, multiline hint and preview visibility.
  Definition order controls ordinary source-insertion/output/form/preview order.
  Default labels use names; fields are optional, have no default/bounds, are
  single-line and preview-visible unless specified. The built-in definition
  explicitly retains existing display hints, ordering and preview widths.
- Use one field name for C# property, XML element and expression suffix; require
  a valid unqualified XML name and non-keyword C# identifier. Reject names
  colliding case-insensitively with each other, reserved source constructs,
  well-known expressions or generated API members. Enum tokens are nonempty,
  case-sensitive, unique identifiers; labels do not change serialized tokens.
- Validate the whole definition before producing outputs: unknown settings,
  invalid combinations, duplicate names/tokens, invalid defaults, inverted bounds
  and defaults outside bounds fail with definition path/line diagnostics. Defaults
  are literal values, not expressions. Prohibit DTDs and external resolution.
- Generate field declarations/accessors, descriptors and repetitive mappings;
  keep inheritance traversal, expression evaluation, XML preservation, diagnostics
  and procedure algorithms handwritten. Use partial classes and shared helpers.
  Syntax retains authored text; resolved models expose typed values plus value
  state. Generated ordinary accessors follow the selected definition, while the
  standard definition preserves existing constructor/accessor source compatibility.
- Generation runs before compilation for compiler and host descriptor inputs,
  including clean/design-time builds. Bootstrap the tool without referencing the
  compiler or extension. Use input/tool fingerprints, per-project/configuration
  output paths and atomic output replacement; avoid concurrent multi-target writes.
  A failed generation must fail compilation, never silently reuse stale output.
  Normal clean removes generated inputs. Test no-op and fresh builds explicitly.

### Evaluation and typed values

- Preserve existing raw-field inheritance: absent or authored empty ordinary
  fields fall back through the reference chain. Apply a definition default only
  when that lookup produces no authored value. An expression evaluating to empty
  does not restart inheritance or default lookup. Do not materialize defaults
  in source when opening, editing another field or saving.
- Evaluate expressions in the consuming alarm's context using the existing
  dependency graph and single-pass substitution. Validate/convert the result
  afterward. Keep raw text, evaluated text, canonical typed value, provenance and
  state distinct: absent, valid, deferred or invalid. Optional absent fields are
  valid; required fields need a nonempty resolved value or applicable default.
  A supplied empty/whitespace-only non-string value is invalid. String whitespace
  remains a value. New validation errors are mandatory and prevent output install.
- String results retain exact text. Typed parsing permits surrounding whitespace
  but never machine-culture separators; preserve source spelling. Integers use
  signed base-10 without grouping; decimal uses a dot without exponent/grouping;
  double additionally permits exponent notation, but rejects NaN/infinities and
  overflow. Boolean accepts true/false and XML 1/0; output is true/false.
  Enum values match declared tokens exactly. Bounds apply only to numeric types.
- Date uses `yyyy-MM-dd`; time uses `HH:mm:ss` with optional 1–7 fractional digits;
  timestamps use those date/time components separated by T and mandatory Z or
  `+/-HH:mm` offset. Reject invalid dates, leap seconds, 24:00 and offsetless
  timestamps. Preserve timestamp offset, not the machine timezone. Use net472-safe
  immutable date/time representations rather than public DateOnly/TimeOnly APIs.
- Canonical output uses invariant numeric text (Int64 decimal digits, decimal
  G29, double G17 with a normalized exponent), ISO dates/times with trailing
  fractional zeros removed, and explicit timestamp offsets (zero as Z).
  Verify identical canonical results on both runtimes, including boundary values.
  Absent optional strings retain existing empty-element output; absent optional
  typed values are omitted. Concrete typed values never silently become zero,
  false, a default enum token or the current date.
- Defer typed conversion only for compiler-tracked unresolved consumer inputs in
  libraries/abstract templates and intentionally retained localization tokens in
  keyword mode. Track this through dependent expressions; do not infer deferral
  by scanning substituted strings for token-shaped text. Other invalid literals
  still fail. Keyword XML preserves the evaluated token text; typed accessors are
  unavailable and analysis reports deferred status. Concrete cultures validate
  normally. Known-invalid literals in abstract/library definitions still diagnose.
- New fields receive `AlarmlistAlarm<FieldName>` expression names and completion.
  Field references continue substituting evaluated text, preserving string behavior;
  typed canonicalization is an output/API operation, not a second expression pass.

### Contracts, SDK and libraries

- Provide immutable field descriptors, typed value states and default provenance
  in analysis. Advance analysis 5 to 6 and analyzer 3 to 4 together with the SDK
  bridge, sample analyzer, package consumers and native host tests. Use generated
  scalar accessors plus generic name-based access for tools/analyzers. Sample
  analyzer rules must tolerate a schema without the ordinary fields they inspect.
- Cross the SDK AppDomain boundary with detached names, type tags, canonical text,
  primitive values and metadata only. Never load generated compiler/enum classes
  into the VS host. Compare definition fingerprints before semantic editor actions;
  a mismatched SDK/VSIX reports a clear error and retains XML-only editing.
- Record a semantic schema fingerprint in library format 2 and verify it across
  project/package/transitive references before binding. The semantic fingerprint
  covers reserved format version, field names/order/types, defaults, required flags,
  bounds and enum tokens; UI labels/help/widths do not affect library compatibility.
  A separate full-definition fingerprint identifies matching generated tooling.
- This unreleased transition requires rebuilding old format-1 libraries and
  contract-3 analyzers; reject them explicitly with rebuild guidance. Do not
  reinterpret foreign-schema libraries or silently discard their fields. ALMX
  source does not gain a mandatory schema marker; preserve unknown-element skipping
  and source preservation. Removed fields are no longer compiled, not deleted.
- Keep both task runtimes, saved-project capture, package selection, transactional
  output, Clean ownership and library consumer-value deferral. Generate schema-aware
  templates/sample construction rather than leaving references to removed fields.

### Designer and preview

- Replace fixed field lists/type checks with descriptors. Ship generated fallback
  metadata for local editing before analysis; accept SDK results only after the
  matching-definition check. Reconcile rows by stable field name without replacing
  controls or discarding drafts during analysis refresh.
- Use reusable WPF templates, not generated per-schema XAML. Strings use the
  expression editor; numeric/date/time fields use that editor with format help.
  Boolean/enum fields add literal-choice helpers alongside the expression editor.
  Choosing a literal is an explicit draft edit; merely focusing never replaces an
  expression. Retain commit/cancel, inheritance reset, source navigation and Undo.
- The compiler owns validation; the designer shows field diagnostics, expected
  type/bounds, inherited/default origin and deferred values. Metadata is plain
  data, never executable XAML. Identity/reference/procedure controls stay specialized.
- Drive alarm lists, resolved previews, table columns, filtering, clipboard output
  and completion from the same metadata. Use identity as the display fallback if
  Name is removed. Sort typed valid values by type, absent/deferred/invalid states
  separately, enum values by declaration order and timestamps by instant.
  Keep column preferences keyed by stable field identity; ignore removed columns
  and add new fields at their default positions without resetting surviving widths.
  Preserve existing preview preference IDs. Ordinary fields named Source, Identity
  or TestMethod use `Field:<name>` IDs so they coexist with the built-in columns;
  labels remain presentation only. Optional absent values render empty, deferred
  tokens retain their text, and invalid values are marked. Sorting keeps valid,
  absent, deferred and invalid groups separate in both directions.

## Numbered iterations and acceptance

Each iteration depends on its predecessor and includes its tests/documentation.
Keep the standard compiler, SDK and extension usable together at every commit.
Alternate schemas are introduced progressively in isolated fixtures, not presented
as supported product configurations before their end-to-end gate passes.

| Iteration | Deliverable | Required validation/completion evidence |
| --- | --- | --- |
| 1 | Record confirmed scope, design defaults, iteration boundaries and exclusions; assign backlog proposal. | Review documentation, references and paths. No build/tests for this documentation-only iteration. |
| 2 | Definition schema, deterministic generator, build integration, generated baseline syntax/resolved members and descriptors. Preserve baseline behavior. | Generator validation/determinism tests; clean/no-op/parallel/design-time generation; standard compiler tests on both frameworks and VS build. Invalid definition cannot leave usable stale output. |
| 3 | Compiler field pipeline driven by descriptors: parsing, inheritance/defaults, typed conversion, expression dependencies, output and generic access. | All scalar types, required/default/bounds/enum errors, culture independence, raw preservation, cycles, optional absence, keyword/abstract/library deferral; compiler/task suites on both frameworks. Standard output goldens unchanged. |
| 4 | Coordinated analysis-6/analyzer-4 bridge transition and library-format-2 schema checks, with all producers/readers updated together. | Bridge isolation, analyzer metadata/execution, matching/mismatched SDKs, library/ProjectReference/NuGet diamond and mismatch cases, old-version rejection, both task hosts and packaged consumers; native project/editor loading on VS2022 and VS2026. |
| 5 | Metadata-driven designer fields, literal helpers, diagnostics/default provenance and schema-aware creation/templates. | WPF draft/focus/inheritance/Undo/navigation tests; standard and alternate-schema native field editing on both VS hosts. Verify expression preservation for every editor type. |
| 6 | Dynamic alarm lists, previews, columns, completion, filtering and clipboard export, preserving existing procedure presentation. | Typed ordering, missing/deferred values, removed Name fallback, column preference reconciliation, localization/expression completion, project preview and source navigation on both hosts. |
| 7 | Full fork customization fixture and reproducible fork workflow. | From an isolated copy, edit only the definition: remove Category, retype Code, add fields spanning every supported type and change labels/order. Build compiler/SDK/VSIX, compile project/library/package consumers, run analyzer and native designer/preview checks on both hosts. Never modify the working checkout or normal example sources. Restore standard experimental deployment afterward. |
| 8 | Integrated acceptance and maintenance documentation. | Solution build; compiler, task unit/integration, SDK, editor and VSIX consumer suites across supported frameworks; desktop-MSBuild SDK run; native affected scenarios on both IDEs, sequentially. Document actual results/limits, generator usage, compatibility/rebuild guidance and schema authoring. |

An essential failed or unavailable check blocks its iteration. Record actual
commands/results separately from this planned acceptance, review the diff, commit
each successful iteration and report its hash before proceeding. Implementation
iterations are authorized; continue through the gates unless the allowance pause
threshold is reached or a required external decision blocks progress.

## References and architecture

- [Roslyn Syntax.xml](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Syntax/Syntax.xml)
  provides the external-definition reference; adapt the separation of generated
  structure and handwritten behavior without reproducing Roslyn's full syntax tree.
- [Arcade SDK](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk)
  guides shared eng/build ownership. Preserve the pinned local
  Arkarin0.DotNet.Arcade.Sdk 1.0.0-Preview imports and net472/net8.0 targets.
- [.NET date/time formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings)
  and [numeric formats](https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings)
  inform canonical conversion; cross-runtime tests must establish actual parity.
- Milestones [4](MILESTONE-4-DESIGN.md), [8](MILESTONE-8.md),
  [9](MILESTONE-9-DESIGN.md) and [10](MILESTONE-10.md) govern preserved expression,
  procedure, library and draft behavior except the explicit version transitions above.

## Implementation evidence

### Iteration 1 — plan and scope (2026-10-21)

Recorded the owner's decisions and concrete implementation defaults after reading
the syntax/evaluation/output pipeline, analysis/analyzer contracts, library format,
designer templates and preview-column implementation. Linked repository guidance
and assigned the syntax-generation backlog proposal. Reviewed the documentation
diff and local links. Tests were not run: documentation only.

### Iteration 2 — generated baseline and build integration (2026-10-22)

Added the authoritative XML definition and XSD, a deterministic net8.0 generator,
generated syntax/resolved members and immutable compiler/host descriptors, and
shared build integration. Definition validation includes identifiers, scalar
literals, enum choices, bounds and presentation metadata. Separate semantic and
tooling fingerprints distinguish compatibility from presentation changes. Outputs
stay in intermediate directories; atomic replacement and locking protect parallel
generation, and unchanged content retains its timestamp.

Validation completed:

- Generator unit suite: 36 passed on net8.0.
- Compiler unit suite: 622 passed on net472 and 624 on net8.0.
- Visual Studio extension build: succeeded with zero warnings/errors.
- Packaged extension integration suite: 13 passed on each framework.
- `eng/test-alarm-definition-generation.ps1`: clean, fresh design-time, parallel,
  no-op, invalid-definition rejection and recovery passed. Generated compiler and
  host declarations for all nine scalar types compiled on both frameworks.
  Final lifecycle logs are under `artifacts/log/Debug/AlarmDefinitionGeneration/55a550e10bf04434834d8f75593cdda7`.
- Reviewed the diff, links and paths; `git diff --check` passed.

The fresh design-time check initially exposed inherited SkipCompilerExecution
settings preventing the tool from building; removing those host-only globals from
the tool build fixed the failure, and the full lifecycle check passed afterward.
Invalid definitions fail the build before compilation even when the previous
complete generated file remains on disk. Baseline behavior is preserved. Alternate
definitions are not yet supported end to end; descriptor-driven runtime behavior
starts in iteration 3. Native IDE and display checks were not run for this iteration.
Iterations 3–8 had no completed acceptance evidence at this boundary.

### Iteration 3 — descriptor-driven compiler values (2026-10-22)

Parsing, local source serialization, inheritance access, expression field lookup,
analysis field enumeration, library source locations, resolved construction and XML
output now follow the generated definition. Added immutable generic field values
with raw/evaluated/canonical text, typed value, state and default provenance.
Generated accessors preserve the standard constructor API and expose nullable
typed scalars for alternate definitions. The generator and compiler share literal
conversion rules. Defaults apply only after authored inheritance lookup fails and
remain literal; source output never inserts their values.

ALM0029 is a mandatory scalar/required/bounds error. Keyword localization and
lookup tokens retain text and defer validation through dependent fields, without
rescanning inserted parameter text. Concrete cultures validate converted values;
abstract and library consumer values retain their existing deferred semantics.
Canonical output preserves string text, omits absent typed fields and formats
typed values independently of machine culture.

Validation completed (commands run from repository root with `--verbosity quiet
-m:1 -nr:false` for dotnet test):

- `dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj`:
  654 passed on net472; 656 on net8.0. Includes scalar boundaries, temporal and
  numeric canonical forms, invalid values, bounds and required/absent states.
- `dotnet test eng/tools/AlarmDefinitionGenerator.UnitTests/AlarmDefinitionGenerator.UnitTests.csproj`:
  36 passed on net8.0.
- `dotnet test src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj`:
  91 passed on each framework.
- `dotnet test src/MSBuild/AlarmList.MSBuild.IntegrationTests/AlarmList.MSBuild.IntegrationTests.csproj`:
  4 passed on each framework.
- `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj`:
  13 passed on each framework.
- `eng/test-alarm-definition-generation.ps1`: all lifecycle gates and an isolated
  alternate-schema compiler passed on both frameworks. The fixture removes the
  normal string fields, retypes Code and covers every scalar, literal defaults,
  bounds, expressions/cycles, inheritance, raw-source preservation, keyword and
  concrete-culture localization/lookups, library deferral and mandatory errors.
  It also checks legal lowercase/type-like field names. Final logs:
  `artifacts/log/Debug/AlarmDefinitionGeneration/1711c5b30674486bb82967cbe8824d43`.
- Diff and path review completed; `git diff --check` passed. The existing shared
  test helper CA1416 warning remains; no tests failed or were skipped.

A library-source namespace collision found during the lifecycle build was fixed
before the successful rerun. These are compiler/runtime and packaged-standard
checks, not alternate-schema native designer acceptance. Analysis/analyzer
contracts and library schema compatibility remain at their existing versions
until iteration 4; alternate end-to-end toolsets remain unsupported at this boundary.
No display checks were run. Iterations 4–8 remain outstanding.

Iteration 3 follow-up: contract review found that known-invalid literals in
abstract/library definitions were incorrectly deferred. Added declaration-time
scalar checks for nonempty token-free expressions, while retaining consumer-input
deferral. The alternate fixture now explicitly rejects invalid literals in both
abstract and concrete library declarations. Lifecycle/runtime checks passed on
both frameworks (`6b99a2f01b264b568529abdb95371466`); compiler suites again passed
654/net472 and 656/net8.0. Diff review passed. This corrects the earlier acceptance
gap before the coordinated iteration 4 transition.

### Iteration 4 — coordinated contracts (2026-10-22)

Advanced analysis to 6, analyzers to 4 and libraries to format 2. Analysis and
analyzer inputs expose generated metadata and fingerprints; field results include
type, state, canonical/primitive value and default origin. The bridge copies only
host-owned metadata and permitted primitive values and rejects a mismatched
full tooling fingerprint before semantic use. Designer guards retain drafts,
block edits through pending refreshes after a mismatch, and keep XML editing
available. Libraries validate semantic fingerprints before parsing sources;
old formats and analyzer contracts report rebuild guidance. The sample analyzer
skips missing/non-string Code fields.

Refined library deferral to track actual missing consumer inputs and required
values through dependent expressions. Known-invalid evaluated values still fail.
Abstract malformed expressions report invalid field state. The isolated fixture
covers typed/default states and sample-analyzer tolerance of retyped Code.

Completed validation (dotnet test commands used `--verbosity quiet -m:1 -nr:false`):

| Command/suite | Result |
| --- | --- |
| `dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj` | 661/net472 and 663/net8.0 passed. |
| `dotnet test eng/tools/AlarmDefinitionGenerator.UnitTests/AlarmDefinitionGenerator.UnitTests.csproj` | 36/net8.0 passed. |
| `dotnet test src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj` | 91 passed on each framework. |
| `dotnet test src/MSBuild/AlarmList.MSBuild.IntegrationTests/AlarmList.MSBuild.IntegrationTests.csproj` | 4 passed on each framework. |
| `dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj` | 85/net472 and 82/net8.0 passed using .NET MSBuild. |
| Same SDK suite with `--framework net472` and `ALARMLIST_TEST_MSBUILD_PATH` selecting VS2022 desktop MSBuild | 85 passed; prior environment value restored. |
| `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj` | Final full suite: 564 passed. Earlier bridge subset: 53 passed; draft/mismatch guard: 1 passed. |
| `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj` | 13 passed on each framework. |
| Native functional suite (command below) | 4 passed on VS2022 17.14.26; then 4 on VS2026 18.10.3. No skipped cases. |

```powershell
eng/test-visualstudio.ps1 -VisualStudioPath '<host installation>' -Filter 'Area=SharedEditor|Area=LibraryReferences|Area=Analyzers'
```

`eng/test-alarm-definition-generation.ps1` passed clean/design-time/parallel/no-op/
failure-recovery checks and both alternate-schema runtime variants on both
frameworks, including required fields without defaults and deferred dependencies.
Logs: `artifacts/log/Debug/AlarmDefinitionGeneration/7f589f6871c24febbcd289f404a77790`.
The final evaluated-library-value regression then passed in that isolated fixture
on both runtimes (`artifacts/m11-contract-evaluated-library.log`). Native TRX
folders are `artifacts/log/Debug/VisualStudioUI/TestResults/c1a38842e6054e32869e922234b99758`
and `artifacts/log/Debug/VisualStudioUI/TestResults/e91aff9540ee4e35aa4ad8dfbed4bfbf`.
Other command output is retained in `artifacts/m11-contract-*.log`.

Reviewed changes, paths and documentation; `git diff --check` passed. The existing
shared test helper CA1416 warning remains. No required check failed or was skipped.
No display/DPI checks were run. Alternate-schema native designer/preview acceptance
remains in iterations 5–7; those features are not claimed complete here.

### Iteration 5 — metadata-driven designer fields (2026-10-22)

Ordinary field rows and source insertion order now use generated descriptors.
Labels, multiline behavior, format/bounds/required help and default/deferred/invalid
status come from metadata and compiler analysis. Shared expression controls retain
raw drafts and resolved display. Boolean/enum helpers expose explicit literal
choices beside the editor and in its context menu; opening/focusing does not edit,
while choosing the same inherited literal can deliberately create a local override.
Identity, reference and procedure controls retain their specialized behavior.

Creation omits Name when it is removed or retyped. Packaged templates omit removed
or retyped sample prose, respect field order and never write defaults or invent
required values. Missing required values remain compiler diagnostics. Generator
lock files are excluded from packaged templates. The isolated designer fixture
copies source, edits only the definition, builds matching tools, checks each scalar
and restores the standard experimental deployment on both IDE hosts.

Completed validation:

- Generator suite: 37 passed (`artifacts/m11-designer-generator.log`).
- Designer/editor regressions: 366 passed on net472, with the separate theme/DPI
  form cases excluded. Command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj --filter 'FullyQualifiedName~Editing&FullyQualifiedName!~DirectFieldFormAligns&FullyQualifiedName!~Theme' --verbosity quiet -m:1 -nr:false`.
  Final evidence: `artifacts/m11-designer-host-final.log`. Covers draft refresh,
  focus, inheritance, source navigation, Undo, all nine metadata field types,
  compiler-owned states and deliberate identical inherited-literal overrides.
- Packaged extension/template consumers: 13 passed on each of net472/net8.0,
  including the assertion that no generator lock files ship. Command:
  `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj --verbosity quiet -m:1 -nr:false`.
  Evidence: `artifacts/m11-designer-package.log`.
- Standard native `Area=MetadataFields|Area=Keyboard`: 3 passed, no skips, on
  each VS2022/VS2026 host using `eng/test-visualstudio.ps1`. Evidence:
  `artifacts/m11-designer-native-2022.log` and `artifacts/m11-designer-native-2026.log`.
- `eng/test-alarm-definition-designer.ps1` with both installed IDE paths: isolated
  compiler/SDK/VSIX build passed without warnings; alternate native field scenario
  passed once on each host, exercising every scalar's expression focus, literal
  commit/Undo and boolean/enum menu actions. Standard deployment restoration and
  metadata scenario passed on each host. Evidence: `artifacts/fork11/21903b2f/`
  and `artifacts/m11-designer-fork.log`.
- Reviewed diff/paths and `git diff --check` passed. An intermediate build attempted
  to replace the running test assembly and failed with a file lock; after that
  test exited, subsequent builds and the final suites passed. Desktop suites ran
  sequentially. No separate OS theme/DPI display checks were performed.

The alternate designer fixture is this iteration's field-editing gate, not full
fork acceptance: dynamic lists/previews/completion are iteration 6, and complete
alternate library/package/analyzer workflows remain iteration 7. Iterations 6–8
are still required before declaring the milestone complete.

### Iteration 6 — metadata-driven lists and previews (2026-10-23)

Alarm lists, preview columns, filtering, clipboard headers and completion now use
field descriptors and compiler-owned canonical values. Sorting uses typed scalar
values and enum declaration order, with explicit absent/deferred/invalid groups.
Removed Name falls back to identity. Column preferences retain surviving IDs;
new columns use definition defaults. Ordinary Source, Identity and TestMethod
fields receive distinct column IDs so they coexist with specialized columns.
Detail views preserve null for absent values and show definition-default provenance.
Procedure element names are rejected as ordinary fields, matching reserved scope.

Completed validation:

- Generator tests: 41 passed (`artifacts/m11-preview-generator.log`).
- Focused preview/editor tests: 43 passed (`artifacts/m11-preview-focused.log`).
- Final editor regression command from iteration 5: 375 passed, none skipped
  (`artifacts/m11-preview-host-final.log`). This includes canonical names, default
  provenance, typed ordering, column preference migration and field completion.
- Packaged extension integration command from iteration 5: 13 passed on each
  framework (`artifacts/m11-preview-package.log`).
- Native metadata/table tests: 2 passed on VS2022. VS2026's combined run passed
  metadata/table tests but exposed a floating preview obscuring the culture
  toolbar. Docking that persisted window before selecting culture fixed test
  setup; both preview-window cases then passed on each host. Evidence:
  `artifacts/m11-preview-native-2022.log`, `artifacts/m11-preview-native-2026.log`,
  `artifacts/m11-preview-window-2022.log` and
  `artifacts/m11-preview-window-2026-retry.log`.
- Strengthened native metadata preview checks, including exact source navigation:
  one passed on each host (`artifacts/m11-preview-final-native-2022.log` and
  `artifacts/m11-preview-final-native-2026.log`).
- `eng/test-alarm-definition-designer.ps1 -IncludePreview` with both installed
  IDE paths: isolated compiler/SDK/VSIX build passed without warnings; two native
  cases passed per host. The fixture removes Name/Category, retypes Code, adds all
  scalar types and colliding specialized column names, and tests hidden-column
  defaults, real cell values, typed sorting, completion, filtering and clipboard.
  Standard deployment restoration passed one metadata case per host. Evidence:
  `artifacts/fork11/a6ee7a03/` and `artifacts/m11-preview-fork.log`.
- The broader editor suite caught an absent detail value becoming an empty string;
  preserving the SDK's null state fixed that regression before the final pass.
  Diff/path review and `git diff --check` passed. No separate OS theme/DPI checks
  ran. Full fork package/library/analyzer acceptance remains iteration 7.

### Iteration 7 — full fork workflow (2026-10-23)

Added `-FullWorkflow` to the isolated designer fixture, with a packaged consumer
runner and dual-framework verifier/analyzer assets. The product customization is
still only the definition: remove Name/Category, retype Code, change labels/order
and add every scalar type. The workflow creates private feeds/caches, packs a
library with deferred consumer expressions, builds ProjectReference and NuGet
consumers on .NET/desktop MSBuild, executes analyzers compiled against the package,
and verifies canonical output plus packaged analysis on both runtimes. It also
checks matching library fingerprints and output preservation on analyzer/scalar
failure. Documentation explains full and diagnostic-only invocation.

Initial harness corrections isolated inherited NuGet mappings, selected the
package-identity library filename and separated analyzer output directories by
framework. Consumer retry `artifacts/fork11/406b0aa3/consumer-checks/28163d57`
passed. The subsequent fresh full run `artifacts/fork11/f062bbb8` passed:

- Production compiler/SDK/VSIX build: zero warnings/errors.
- Four application builds (project/package on each task host), each with
  verified analyzer execution; eight output/API verifier runs across both runtimes.
- Both intentional failures (`FORK002`, `ALM0029`) retained prior output.
- Two native field/preview cases passed on each IDE, no skips.
- Standard experimental deployment restoration: one metadata case passed per IDE.

Evidence: `artifacts/m11-full-fork-final.log`, the fixture's `build.log`,
`consumer-checks/9a08f9c9/`, `native-2022.log`, `native-2026.log` and restoration
logs. A final acceptance review strengthened the fixture by explicitly reversing
the surviving Description/Code order and checking emitted field order. That
updated full run also passed, including both restoration checks:
`artifacts/fork11/2f219cc3`, consumer evidence `consumer-checks/9f4efd3e`, and
`artifacts/m11-full-fork-reordered.log`. Its counts match the earlier full run
above, with zero skipped native cases and zero production build warnings/errors.
Command: `eng/test-alarm-definition-designer.ps1 -VisualStudio2022Path '<installed VS2022>' -VisualStudio2026Path '<installed VS2026>' -FullWorkflow`.
Reviewed source, documentation, paths and diff; `git diff --check` passed.
No separate OS theme/DPI checks ran. Integrated standard-schema regression
acceptance remains iteration 8; the milestone is not yet complete.

### Iteration 8 — integrated acceptance (2026-10-23)

Expanded the definition authoring guide with production rebuild instructions,
literal formats, defaults/inheritance, expression validation, metadata defaults
and fork package compatibility. Updated SDK/extension documentation to describe
the implemented metadata workflow and current contract.

Final standard-definition validation:

- `dotnet build AlarmlistProject.slnx --verbosity quiet -m:1 -nr:false`: passed;
  two existing CA1416 warnings in the shared test helper, zero errors.
- Generator suite: 41 passed on net8.0.
- Compiler suite: 661/net472 and 663/net8.0 passed.
- Task unit suite: 91 passed on each framework.
- Task integration suite: 4 passed on each framework.
- SDK suite using .NET MSBuild: 85/net472 and 82/net8.0 passed.
- Editor suite: 577/net472 passed; the filter excludes `DirectFieldFormAligns`
  and `Theme` display cases. No selected tests were skipped.
- Packaged VSIX suite: 13 passed on each framework.
- SDK suite with VS2022 desktop MSBuild selected by `ALARMLIST_TEST_MSBUILD_PATH`:
  85/net472 passed; the previous environment value was restored.
- `eng/test-alarm-definition-generation.ps1`: all lifecycle and alternate-runtime
  checks passed; evidence under
  `artifacts/log/Debug/AlarmDefinitionGeneration/3077f5c18d944462bf846fb3ec043057`.

Evidence: `artifacts/m11-integrated-build.log`, `m11-integrated-generator.log`,
`m11-integrated-compiler.log`, `m11-integrated-tasks.log` and
`m11-integrated-taskintegration.log`. Test commands use the project paths listed
in iteration 4, with `--verbosity quiet -m:1 -nr:false`. Later logs are
`artifacts/m11-integrated-sdk.log`, `m11-integrated-editor.log` and
`m11-integrated-vsix.log`, `m11-integrated-desktop-sdk.log` and
`m11-integrated-generation.log`. These suites ran sequentially, followed by the
affected native scenarios on both hosts described below.

The first integrated VS2022 native batch exposed two test setup failures:
`PropertiesAndCompletion2022` assumed the concrete alarm was initially selected,
but captured UI state showed its abstract template selected; `ScalarCommitAndUndo2022`
clicked a mode selector covered by a persisted floating preview and remained in
XML mode. The tests now hide that preview before interaction and explicitly select
the concrete parameter-test alarm. Assertions are unchanged. Evidence is retained
in `artifacts/m11-integrated-native-2022.log` and the corresponding native failure
folders. The batch finished with 20 passed and three failed, no skips. The third
failure, `LibraryMode2022`, still expected `(unavailable)` while captured output
correctly showed the new `(deferred)` state; its assertion now checks that explicit
state. All three corrected cases passed on rerun (`m11-integrated-native-2022-retry.log`,
TRX `0c2b084364a94e1c8ee310bdef51f514`). Original TRX:
`artifacts/log/Debug/VisualStudioUI/TestResults/fbd472bf0fb54b73a72948437ed11a52`.

The VS2026 batch passed 22 cases and exposed one native property-page timing issue:
Visual Studio's known save-changes prompt disabled Apply while the last identity
cell lost focus. The test now handles that owned prompt and waits for application;
saved project values and built artifact assertions remain intact. The corrected
case passed on both hosts (`m11-integrated-properties-2026-retry.log` and
`m11-integrated-properties-2022-retry.log`). VS2026 original TRX is
`485f5c1999b04e549c7afd60ef79ac3c`; final retry TRX folders are
`90f0ba03e9b34c7d80703cba1ec7cdd1` and `289078c957a946e4a687f95067bd8b63`.
All TRX folders are under `artifacts/log/Debug/VisualStudioUI/TestResults/`.

Reconciled the latest result for each native test name across the original and
retry reports: all 46 cases passed (23 per host), none skipped or left failing.
Native command: `eng/test-visualstudio.ps1 -VisualStudioPath '<installed host>' -Filter 'Area=MetadataFields|Area=MetadataPreview|Area=Keyboard|Area=Properties|Area=PreviewTable|Area=Preview|Area=SharedEditor|Area=LibraryReferences|Area=Analyzers|Area=Localization|Area=Lists|Area=Lookups'`.
The Localization partial class also contributes its library, resource and XLIFF
scenarios. Retries selected the exact affected method names. No production-code
change was needed in this iteration. Native test maintenance guidance is in AGENTS.md.

Completion audit:

| Confirmed requirement | Evidence |
| --- | --- |
| Definition-only generation, stable baseline and safe build lifecycle | Iterations 2/7; final 41 generator tests and lifecycle run, including clean/design-time/parallel/no-op/failure recovery. |
| All nine scalar types, expressions, defaults, bounds, inheritance and deferred validation | Iterations 3/4; final compiler suites on both frameworks and alternate-runtime fixtures, including raw-source preservation and keyword/concrete-culture behavior. |
| Matching analysis/analyzer/library contracts and packaged task hosts | Iteration 4; final SDK/task/VSIX suites, desktop-MSBuild run, and iteration 7 packaged fork consumers/analyzers. |
| Metadata designer, lists, completion, typed previews and preserved editing | Iterations 5/6; final 577 editor tests and 46 reconciled native cases, plus alternate-schema field/preview tests on both hosts in iteration 7. |
| Remove/retype/reorder/add fields by changing only the definition | Iteration 7's isolated reordered fork: matching production tools, project/NuGet libraries, analyzers, canonical output and both native hosts; standard deployments restored. |
| Documentation, reproducibility and scope control | Authoring guide, SDK/extension docs, AGENTS.md and per-iteration evidence reviewed; documented paths and `git diff --check` passed. |

No required implementation or acceptance gate remains open. Separate OS theme/DPI
checks were excluded as requested. Publication, runtime per-project schemas,
collections, VS Code support and inherited-entry reordering remain outside scope.
The two existing test-helper CA1416 warnings remain; no new build warning was
introduced. The allowance check before completion showed 30% remaining, above
the owner's 20% pause threshold.

## Authorized follow-up: playground and definition-controlled collections

Authorized 2026-10-23. This extends the original scalar-only scope; it does not
invalidate the completed iterations above. Continue on the current dev branch.
The account allowance was 28% remaining at entry; pause at 20% remaining.

### Design contract

- Keep fork-time generation and matching toolsets. Introduce definition version 2
  with ordinary Field, SimpleCollection, ComplexCollection and optional
  TestProcedure declarations. Version 1 remains scalar-only with its implicit
  procedure support. The standard version-2 definition explicitly includes
  TestProcedure, preserving existing ALMX behavior.
- SimpleCollection declares Name, scalar Type and existing scalar constraints /
  enum choices. ComplexCollection declares Name and one or more ordinary scalar
  Field children. Both support Label, Help, MinCount (default 0), optional MaxCount.
  Names share the alarm member namespace; record field names are local to their
  record. Reject nesting, duplicate names, invalid constraints and collisions.
- Source uses <Tags><Item>value</Item><Clear /></Tags> for simple collections and
  <Checks><Item><Description>text</Description></Item></Checks> for records. XML
  names are case-sensitive. Collection declaration order determines collection
  output/form order; item order is authored order. Emit resolved items without
  Clear directives. Do not silently discard malformed declared collection content.
- Missing or empty local containers inherit; local items append. Clear removes
  all preceding effective items. Duplicate values/records are allowed. Records
  have no inheritance key and are inherited whole. Only local items can be
  edited, deleted or reordered. No inherited-entry reordering or keyed merging.
- Reuse scalar expressions, validation and defaults inside items. Alarm property
  expressions retain their existing scope. Add explicit $(item:FieldName) sibling
  record-field references with dependency/cycle diagnostics; no indexes,
  cross-entry references or aggregates. Inserted values remain literal. Defaults
  never create items or materialize in source. Count validation uses effective
  items; abstract/library analysis retains existing deferred consumer validation.
- TestProcedure is optional once in version 2, under its existing XML name and
  existing specialized semantics. Disabled procedure content produces an error;
  creation defaults, templates, completion and designer commands honor capability.
- Analysis exposes detached collection descriptors, local/effective/excluded
  items, typed states and origins. Library serialization preserves raw expressions
  and directives. Coordinate analysis/analyzer/library version transitions when
  these payloads change and include definitions in fingerprints. Retain rebuild
  diagnostics and XML/draft access for incompatible tools.
- Designer uses scalar rows for simple collections, expandable field forms for
  records and the existing procedure editor. Stable internal draft identities
  distinguish duplicate entries and survive refresh; do not add source IDs.
  Iterations 14–15 delivered collection counts with a separate detail view.
  The preview follow-up below supersedes that presentation with one column per
  collection. No host-side scalar parsing or inferred inheritance/provenance.
- The interactive shortcut defaults to VS2026 and accepts VS2022 / PrepareOnly.
  Each invocation builds a fresh isolated source copy, matching SDK/VSIX and
  sample project using a private package feed/cache; deploy to the dedicated
  AlarmlistDefinitionExp profile. Preserve previous runs and standard examples.
  Edit the checked-in sample definition and rerun to try another toolset.
- Out of scope: nested collections, arbitrary CLR types, runtime project schemas,
  multiple renamed procedure sections, individual inherited-item overrides,
  aggregate expressions, publication and OS theme/DPI display checks.

### Independently reviewable iterations

| Iteration | Deliverable and dependency | Validation and completion gate |
| --- | --- | --- |
| 9 | Record this follow-up contract and repository guidance. | Review paths, consistency and diff; documentation-only commit. |
| 10 | Interactive scalar-definition launcher and checked-in definition/ALMX samples, using current production capabilities. Depends on 9. | Prepare and compile sample on both task hosts; validate alternate designer on both IDE hosts sequentially, failure behavior and source preservation. |
| 11 | Version-2 definition model, validation, deterministic metadata generation and compatibility foundations. Depends on 10. | Generator positive/negative/fingerprint tests, baseline build on both frameworks. Keep new runtime declarations guarded until consumed. |
| 12 | Collection source/resolved models, inheritance, expression/typed/count validation and XML output; optional procedure capability. Depends on 11. | Compiler tests on both frameworks, isolated alternate-definition runtime fixtures, unchanged baseline and existing transactional task failure checks. Collection-specific packaged task failures remain in 13 because production generation stays guarded. |
| 13 | Coordinated analysis/analyzer/library contracts, SDK packaging and bridge metadata/provenance. Depends on 12. | Both-runtime package/project/library/analyzer tests, including collection-specific transactional task failures; mismatch and detached bridge tests; baseline designer remains usable. |
| 14 | Collection designer edits, drafts, completion, detail preview and optional procedure UI/templates. Depends on 13. | Editor preservation/draft/identity tests, native collection/procedure-disabled scenarios on both IDE hosts. |
| 15 | Extend playground with collections, authoring docs and integrated fork acceptance. Depends on 14. | Full alternate fork and no-procedure fork acceptance, downstream suites on both runtimes and both IDE hosts; record remaining gates. |

Do not describe generated metadata alone as working collection support. During
staged implementation, reject definitions requiring unavailable runtime support
rather than producing a toolset that loses their data. Keep new capabilities
internal/guarded until the coordinated consumers are implemented.

### Iteration 9 evidence

Reviewed existing generator/XSD, ReferenceableCollection inheritance, procedure
syntax/resolved types, isolated fork fixture and launcher/package preparation.
Consulted Roslyn's compiler layer layout and Arcade's SDK/tools layout; retain
existing project boundaries and shared eng tooling, with no new dependency.
References: https://github.com/dotnet/roslyn/tree/main/src/Compilers and
https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.
Documentation only; tests not run. Remaining iterations are planned, not complete.

### Iteration 10 implementation and validation (complete)

Added scripts/start-custom-definition.bat, eng/start-custom-definition.ps1 and
example/CustomDefinition samples/guide. The optional DefinitionFile argument
selects a separate input without modifying the standard definition. Preparation
copies source into a unique artifacts/def11 directory, builds matching SDK/VSIX,
uses a private sample feed/cache, compiles with both task hosts and deploys only
to AlarmlistDefinitionExp. PrepareOnly omits IDE launch. Earlier runs are retained.

Preparation passed on VS2022 and VS2026 (initial logs under
artifacts/definition-playground/ff32d28b and 58a7ad33); the batch shortcut was also
invoked from the scripts directory. The revised short-path run is
artifacts/def11/a3cf8620. Tool builds reported zero warnings/errors. Both task-host
sample builds passed; inspected two concrete alarms, Code 8, decimal 12.5,
boolean/date values, default Code 7 and unchanged source/definition hashes.
PowerShell parser validation passed. An invalid scalar type supplied through
DefinitionFile failed with ALMGEN001 before deployment/launch; retained evidence:
artifacts/definition-playground/invalid-launch.log and 48c981c6/build-tools.log.

Initial native dedicated-profile attempts exposed
first-start automation delay and nested NuGet paths up to 274 characters. The
launcher now uses the shorter artifacts/def11 prefix (longest equivalent test
package path 258). Subsequent native startup reached the designer. Failure captures
showed the fresh profile's welcome page taking document activation and a small
default preview dock realizing only one of two virtualized table rows. The draft
text visible in that screenshot is general guidance, not an active-draft status.
Metadata native tests now explicitly activate their owned document, hide stale
floating previews during editor input and size the preview as existing project
preview tests do. They also wait for completion popup dismissal and cancellation
instead of sending back-to-back Escape keys. All existing assertions remain.
Initial reports: ff32d28b TestResults/33596bccb39d472f801c93d0f02471a8 and a3cf8620
TestResults/2e373f6f6d784e86bb4eb8041d298f30 beneath their
artifacts/log/Debug/VisualStudioUI directories.

Final native evidence, all under artifacts/def11/a3cf8620:

- VS2022: 2 passed, 0 failed/skipped, native-2022-final.log; TRX directory
  artifacts/log/Debug/VisualStudioUI/TestResults/5a8f50ab61764e4d955c1a21639fab55.
- VS2026: metadata fields passed in native-2026.log; preview initially timed out
  during fresh-profile startup. The preview retry passed (1 passed, 0 skipped)
  in native-2026-retry.log, TRX directory
  artifacts/log/Debug/VisualStudioUI/TestResults/7d8d668b74b54cb693be296677aee5d5.
  Initial report: TestResults/d7ea4f92122e4d5496f7f44b535bc33b. All four distinct
  field/preview host cases passed in their final results.
- Native command: isolated eng/test-visualstudio.ps1 -VisualStudioPath
  '<installed host>' -RootSuffix AlarmlistDefinitionExp -Filter
  'Area=MetadataFields|Area=MetadataPreview'. The final VS2026 retry selected only
  Area=MetadataPreview. The native startup wait now allows 180 seconds for a new
  profile's automation interface; behavior/assertion waits remain 60 seconds.
- git diff --check passed; standard compiler definition and original North/South
  sources are unchanged. No OS theme/DPI checks ran. Interactive first-use VS
  setup remains documented; tests own and close their IDEs. No publication.

Allowance reached 21% remaining, then the account tool reported a renewed window
with 100% remaining before the pause threshold was reached. Continue to check the
current allowance before further iterations. No collection runtime changes have
been made in iteration 10.

### Iteration 11 implementation and validation (complete)

Added a version-2 definition model and XSD shapes for simple/complex collections
and optional TestProcedure. Scalar fields and record fields share literal/type
validation; collection counts are checked for nonnegative Int32 ranges, names for
collisions, and nested collections/duplicate procedure declarations are rejected.
Version 1 retains implicit procedures and disallows version-2 declarations.
Generated compiler/host metadata includes immutable collection descriptors and
procedure availability, with semantic versus presentation fingerprints. The
standard version-1 library fingerprint remains
6fef46acca6088624113e4d230a39963d0f9044645b62a54c36d1eca748a0c18. The full tooling
fingerprint advances to generator-2 so matching compiler/SDK/VSIX are rebuilt.

The CLI still rejects collections and procedure-disabled definitions before
installing output. This is an intentional staged capability guard, not completed
runtime support. Version-2 scalar definitions with TestProcedure are supported.
The standard definition remains version 1. Updated the invalid-version lifecycle
fixture from version 2 to version 999; version 2 is now a recognized schema.

Validation so far:

- Generator unit tests: 69 passed (m11-iteration11-generator-final.log), including
  legacy semantic identity, nine scalar shapes, record scopes, count/name/nesting
  failures, presentation/semantic fingerprints and atomic rejection in all roles.
- Compiler unit tests: 661 net472 and 663 net8.0 passed
  (m11-iteration11-compiler-tests.log). Existing CA1416 test-helper warning only.
- Editor unit tests: 577 passed, excluding DirectFieldFormAligns and Theme checks
  as requested (m11-iteration11-editor-tests.log). This rebuilt matching SDK/host
  assets and exercised bridge behavior.
- Generation lifecycle: passed, including alternate scalar compiler checks on both
  frameworks. Evidence: artifacts/log/Debug/AlarmDefinitionGeneration/824528ce66734254b14526029a6547cc;
  wrapper log m11-iteration11-generation-lifecycle.log.
- Initial sandboxed test-host connection timed out, and a sandboxed build returned
  nonzero without compiler diagnostics. The authorized unsandboxed test/build runs
  above passed. These were execution-environment failures, not successful checks.
- Packaged SDK tests: 85 net472 and 82 net8.0 passed using the .NET MSBuild host
  (m11-iteration11-sdk-tests.log), including packaged APIs and libraries. Both
  generated compiler targets and the host compiled successfully. All wrapper logs
  are under artifacts/. git diff --check passed. No native or OS display reruns
  were needed for this metadata-only foundation; native collection acceptance
  remains a required later gate. No runtime collection support is claimed here.

### Iteration 12 implementation and validation (complete)

Added guarded compiler collection syntax and immutable resolved item values.
Local containers preserve Item/Clear ordering, absent containers and record fields;
malformed known collection content fails XML parsing with source positions.
ReferenceableCollection supplies inherited-first/Clear behavior. Record defaults
remain literal, do not create entries and are not materialized in source XML.
Resolved output uses canonical scalar values in definition order and omits the
procedure section when the definition disables it. Existing public scalar
constructors and the standard source/output behavior are retained.

Evaluation shares the scalar codec and consuming-alarm expression context.
Explicit $(item:FieldName) references support forward sibling dependencies and
cycle errors, including translated text and named localization arguments. They
are rejected outside record context. Parameter replacement strings are not
rescanned. Keyword-mode typed deferral propagates through sibling references.
Collection fields carry raw/evaluated/canonical/typed state separately. Effective
cardinality uses mandatory ALM0030; abstract and library counts defer until an
application consumes them. Local entry/field source versions are preserved.

The production generator guard remains. The isolated fixture driver calls the
internal generator to compile source-linked runtime checks, without changing
the standard definition or shipping a partially supported SDK. The tooling
fingerprint advances to generator-3 for the resolved-model shape. Collection
members are reserved to avoid generated CLR name collisions. HasTestProcedure
is a static read-only property to avoid unreachable-code warnings in forks.

Validation evidence:

- eng/test-alarm-collections.ps1 passed: 52 checks with procedures disabled and
  52 enabled, plus 7 minimum-count checks, on each of net472 and net8.0. Logs:
  artifacts/log/Debug/AlarmCollections/5ac53c9623af4572b3bc5543039ec3d9;
  wrapper artifacts/m11-iteration12-collections.log. Covers all scalar shapes,
  inheritance across files, duplicates/Clear, sibling cycles/context, literal
  defaults, malformed source, localization, consumer deferral and output.
- Generator tests: 73 passed (artifacts/m11-iteration12-generator.log).
- Compiler tests: 664 net472 / 666 net8.0 passed
  (artifacts/m11-iteration12-compiler.log), including record-context and exchange
  protection regression tests. Existing CA1416 test-helper warning only.
- Task unit tests: 91 per framework passed; integration tests: 4 per framework
  passed (artifacts/m11-iteration12-tasks.log and task-integration.log).
- Editor compatibility tests: 577 passed, excluding the requested display checks
  (artifacts/m11-iteration12-editor.log). No native collection acceptance is
  claimed; that remains iteration 14. Collection-specific packaged task/output
  preservation tests are explicitly assigned to iteration 13 because the staged
  production generation guard still prevents such packages in this iteration.
- Initial custom-fixture runs caught incorrect test source-version property names
  and the need to assert XML exceptions for malformed source. Corrected fixtures
  passed; these initial runs were failures, not acceptance evidence.
- Reviewed the diff and documentation; git diff --check passed. No publication.

### Iteration 13 implementation and validation (complete)

Coordinated analysis 7, analyzer 5 and library format 3. Detached collection
descriptors and procedure capability accompany local/effective/excluded entries,
per-field scalar results and declaration/Clear origins. Invalid reference chains
have no effective collections. Library source maps now capture and restore
containers, entries and record fields, including navigation to generated package
definitions; forged collection origin keys are rejected. Existing normalized
source serialization preserves expressions and Clear without storing resolved
consumer values or defaults. Old analysis/analyzer/library versions require a
rebuild even for the unchanged standard scalar schema.

The SDK bridge validates and copies the new types into serializable host-owned
objects, retaining the scalar primitive whitelist. Procedure-disabled definitions
have no creation defaults; source and programmatically constructed syntax both
reject procedure content instead of silently dropping it. Compiler and template
generation are now enabled for collections and disabled procedures. Host
generation remains explicitly guarded until iteration 14 supplies the designer;
this allows real compiler/SDK package acceptance without advertising an incomplete
collection VSIX. The standard definition remains version 1 during this stage.

Validation evidence:

- Generator tests: 74 passed (artifacts/m11-iteration13-generator.log).
- Compiler tests: 666 net472 / 668 net8.0 passed
  (artifacts/m11-iteration13-compiler.log), including rejection of analyzer 4 and
  library format 2 in addition to earlier obsolete contracts.
- eng/test-alarm-collections.ps1 passed on both frameworks, with 70 checks for
  disabled procedures, 68 enabled and 7 minimum-count checks. Evidence:
  artifacts/log/Debug/AlarmCollections/c05b00519aa341909560ea07d3620e00;
  wrapper artifacts/m11-iteration13-collections-final.log. Covers detached bridge
  copies, library expression/location round trips, forged origins, Clear
  exclusions and invalid-chain unavailability, plus iteration-12 cases.
- eng/test-alarm-collection-fork.ps1 passed real package acceptance with procedures
  enabled (artifacts/col13/28937bd7, consumer-checks/6d57c322) and disabled
  (artifacts/col13/12b462c2, consumer-checks/7ba32262). Both .NET and desktop
  MSBuild consumed project and NuGet libraries, ran collection-aware analyzers
  compiled against the package, and verified typed XML/API results on net472 and
  net8.0. ALM0030 count and ALM0029 item failures on both hosts preserved previous
  output. Wrapper: artifacts/m11-iteration13-fork.log. No IDE deployment.
- Editor unit tests: 578 passed with display checks excluded
  (artifacts/m11-iteration13-editor-final.log). The initial run had 576 passed / 1
  failed because the matching-contract mismatch fixture still declared version 6;
  updated it to 7 and added explicit version-6 rejection before the passing run.
- Initial new fixture compilation lacked the LibraryTextSnapshot namespace;
  corrected before successful runtime acceptance. Initial standalone package
  fixture lacked the Windows PowerShell ZIP assembly; explicitly loading
  System.IO.Compression.FileSystem fixed it. Initial failures remain in logs.
- Standard packaged SDK tests passed: 85 net472 / 82 net8.0
  (artifacts/m11-iteration13-sdk.log). Final focused compiler/fixture reruns also
  cover programmatic procedure rejection, whitespace-only Clear content and a
  legal AlarmDefinition field shadowing the new collection helper. The new
  internal _collections member name is rejected at generation.
- Reviewed diff/docs; git diff --check passed. No publication. Collection
  designer/native gates remain iterations 14–15.

### Iteration 14 implementation and validation (complete)

Added metadata-driven scalar collection rows and expandable record fields, using
the shared expression editor and explicit literal helpers. Local entries support
add/delete/move and Clear; inherited and excluded entries remain read-only with
compiler-provided source navigation. Internal draft identities survive local
moves and analysis refresh without adding source IDs. Container baseline checks
protect concurrent XML edits; collection/scalar/parameter drafts commit as one
buffer transaction. Defaults remain absent until an explicit edit.

Record completion offers $(item:FieldName) siblings and validates captured inputs.
The shared lexical completion control now recognizes item:. Source navigation can
select record fields. Disabled TestProcedure removes creation/UI/preview support
and editing-service commands reject it. Production host generation is enabled.
Preview adds one fixed Collections count column and expandable read-only entry
details, using compiler scalar states/canonical values without host parsing.

Validation so far:

- Generator: 74 passed (artifacts/m11-iteration14-generator.log).
- Initial editor regression run: 586 passed with DirectFieldFormAligns/Theme
  display checks excluded (artifacts/m11-iteration14-editor.log). Added focused
  completion/preview checks: 10 passed (artifacts/m11-iteration14-focused.log).
  Final full regression run: 588 passed
  (artifacts/m11-iteration14-editor-final.log).
- Native enabled-procedure fork artifacts/col13/856d1d5a: VS2022 and VS2026 each
  passed one MetadataCollections scenario. Disabled-procedure fork
  artifacts/col13/15b3f4a7: both hosts also passed. Reports/logs are results-2022,
  results-2026 and native-2022.log/native-2026.log in each fork; wrapper
  artifacts/m11-iteration14-native-final.log. Tests cover inherited read-only
  values, absent defaults on focus, scalar edits/local ordering/delete/Clear/add,
  sibling completion, cancellation, typed record edits and resolved detail preview.
  The dedicated AlarmlistDefinitionExp profiles leave standard deployments alone.
- Initial native fork 670baf11 failed SDK extraction/resolution with the long
  MetadataCollections workspace name. Shortening the scenario directory to Col14
  fixed project creation. Fork 332305c2 then exposed the missing item: completion
  prefix; added the prefix plus a regression test before the passing native run.
- No OS theme/DPI checks or publication. The standard definition remains version
  1 until iteration 15, which adds collection/no-procedure playground samples and
  integrated fork/downstream acceptance.
- Expanded native acceptance added draft preservation during collapse and focus
  recovery after deleting the last local entry. Fork feb327ab caught an unwanted
  draft commit during collapse: the native focus transfer can target the scroll
  host, and explicit theme header styles bypass implicit Tag setters. The handler
  now recognizes collapsed collection ancestors and their header toggles.
  Final expanded cases passed on both hosts for enabled procedures in
  artifacts/col13/8d547c32 and disabled procedures in artifacts/col13/26231b8a,
  one passed scenario per host/fork, zero failures/skips. Wrapper:
  artifacts/m11-iteration14-native-collapse.log. These supersede the earlier
  four-case evidence for focus/collapse behavior. Each isolated build had zero
  warnings/errors. Case-insensitive sibling completion has a focused regression.
- Final focus/completion/collection regression selection: 54 passed, zero
  failures/skips (artifacts/m11-iteration14-focus-final.log), including the final
  shared focus handling and case-insensitive record completion. Reviewed source
  preservation, notification lifetimes, docs and diff; git diff --check passed.
  Remaining work is iteration 15's samples and integrated acceptance.

### Iteration 15 implementation and validation (complete)

The standard definition is now version 2 with the original four string fields and
explicit TestProcedure. ALMX behavior stays the same; its semantic fingerprint is
now fa0563f730677abd484c1f50a0c8c4aaa84c84a85e13c841db4a61b266658bbc, so
libraries/toolsets must be rebuilt together. Version-1 input compatibility remains
covered by the generator tests.

Extended example/CustomDefinition with Tags, Numbers and flat Checks, inherited
and local entries, duplicate values, Clear, typed parameters, sibling references
and absent defaults. Corrected the sample's procedure title to a Title element.
Added explicit AlarmDefinition.NoProcedures.xml / Alarms.NoProcedures.xml
alternatives. The launcher accepts SampleFile and copies it as Alarms.almx; it
never removes content from a chosen source to force compatibility. Updated the
authoring/playground/SDK guides. A final review also removed an inferred item
location from defaulted-field preview navigation; only compiler field origins are
used, covered by a regression test.

Validation so far:

- Full solution command: dotnet test AlarmlistProject.slnx --filter
  'FullyQualifiedName!~DirectFieldFormAligns&FullyQualifiedName!~Theme'. Exit 0,
  2,433 passed, zero failed, 98 opt-in cases skipped. Breakdown: generator 74;
  compiler 666 net472 / 668 net8.0; task unit 91 each; task integration 4 each;
  SDK 85 net472 / 82 net8.0; editor unit 589; packaged extension integration 13
  each; performance infrastructure 53. The skipped cases were 80 opt-in native
  cases and 18 opt-in benchmark/live-performance cases. OS display checks were
  excluded as requested. Log: artifacts/m11-iteration15-solution.log. Native
  collection cases are run separately below, not claimed by this solution run.
- eng/test-alarm-collections.ps1 passed: disabled 70, enabled 68 and minimum-count
  7 checks on each framework. Evidence:
  artifacts/log/Debug/AlarmCollections/d3606c995d1f4edd8c872c3a20a42df3 and
  artifacts/m11-iteration15-collections.log.
- Executed scripts/start-custom-definition.bat -PrepareOnly from scripts/ for
  VS2026: artifacts/def11/fc3102af, log artifacts/m11-iteration15-playground.log.
  Executed eng/start-custom-definition.ps1 with VisualStudioVersion 2022,
  DefinitionFile example/CustomDefinition/AlarmDefinition.NoProcedures.xml,
  SampleFile example/CustomDefinition/Alarms.NoProcedures.xml and PrepareOnly:
  artifacts/def11/c49e6bea, log artifacts/m11-iteration15-playground-no-procedures.log.
  Both runs built samples with .NET and desktop MSBuild and deployed dedicated
  profiles without launching an IDE. Output inspection verified two concrete
  alarms, inherited tag ordering/duplicates, Count/Copy values 8 and 7, default
  Code/Count 7, Clear removal, enabled/disabled procedure output and retained
  ordinary TestMethod fields. Source SHA-256 comparisons matched both originals.
- Combined fork acceptance passed via eng/test-alarm-collection-fork.ps1 with
  DesktopMSBuildPath, Designer, VisualStudio2022Path and VisualStudio2026Path.
  Enabled procedures: artifacts/col13/537a8ab9, consumer-checks/cd377f3f.
  Disabled procedures: artifacts/col13/8874b7b7, consumer-checks/693373aa.
  Both forks passed project/NuGet library consumption, packaged API verification
  on net472/net8.0, analyzer execution and transactional failure preservation on
  .NET/desktop MSBuild. Both forks also passed MetadataCollections on VS2022 and
  VS2026: four passed native cases, zero failures/skips. Native reports are in
  each fork's results-2022/results-2026 and native-2022.log/native-2026.log.
  Wrapper: artifacts/m11-iteration15-fork-full.log. Builds had zero warnings/errors.
- PowerShell parser validation, authoring-guide relative links, sample output and
  diff review passed; git diff --check passed. No generated outputs are tracked.
  Account allowance was 49% remaining at final acceptance, above the requested
  20% pause threshold. No required implementation or validation gate remains for
  this follow-up. OS display checks remain explicitly excluded; opt-in broader
  live/performance suites and publication were not part of this acceptance run.

## Preview follow-up: collection columns and compiled XAML

Owner direction confirmed 2026-10-23. This section replaces the earlier
collection-count presentation decision, without changing completed evidence.
Implementation is authorized on dev/milestone-11. The owner has waived the 20%
remaining-allowance pause for this goal; OS theme/DPI display checks remain excluded.

### Confirmed outcome and exclusions

- Every declared collection has its own preview column, in both embedded and
  project/window previews. Labels supply headings; collection declaration order
  supplies default order. User visibility, width and ordering remain supported.
- Simple collections support List and Concatenated presentation. Complex
  collections use compiled WPF DataTemplates, repeated once per effective record.
  No HTML renderer, browser dependency, custom layout language, separate Table
  renderer, runtime XAML loading or project-supplied template plugins are added.
- Preserve compiler evaluation, collection order, duplicates, inherited-first
  append, Clear, scalar states, origins and optional TestProcedure behavior.
  This is presentation metadata, not an ALMX or compiled-output format change.
- Templates are trusted fork source compiled into the matching extension. This
  is not a sandbox for untrusted XAML. Compiler, tasks, libraries and analyzer
  execution remain independent of WPF. No new runtime per-project schema.

### Definition and simple presentation contract

Use one optional Preview child on each collection in the version-2 definition.
The following syntax is supported by completed iterations 17–18:

```xml
<SimpleCollection Name="Measurements" Type="String" Label="Measurements">
  <Preview DisplayType="List" Symbol="Bullet" />
</SimpleCollection>
<SimpleCollection Name="Tags" Type="String" Label="Tags">
  <Preview DisplayType="Concatenated" Separator=", " />
</SimpleCollection>
<ComplexCollection Name="Checks" Label="Inspection checks">
  <Field Name="Name" Type="String" />
  <Field Name="Value" Type="Decimal" />
  <Field Name="Unit" Type="String" />
  <Preview DisplayType="Xaml"
           TemplateFile="Preview/Checks.xaml" TemplateKey="ChecksPreview" />
</ComplexCollection>
```

- List is the simple default; Symbol accepts Bullet (default), Number and None.
  Concatenated defaults to Separator=", "; preserve explicit empty separators
  and separator whitespace. No trimming, deduplication or value reordering.
- Add PreviewVisible and PreviewWidth to collection metadata using ordinary
  field conventions (true and 180 by default; width range 40–2000). Use Label
  for headings. Reject duplicate Preview children, unknown display types and
  settings inapplicable to the selected collection kind or display type.
- Show compiler-provided display text and distinguish absent, invalid and
  deferred values. An empty effective collection is distinct from unavailable
  analysis; do not present an invalid reference chain as an empty list.
- Existing definitions without Preview remain usable. Complex collections use
  a built-in compiled record template displaying labelled scalar fields when
  no custom template is selected. TemplateFile and TemplateKey must occur
  together; custom selection is optional, not a new required definition field.

### Compiled-template and build contract

- TemplateFile names a fork-owned XAML ResourceDictionary relative to the
  definition file; TemplateKey selects a string-keyed DataTemplate in it.
  Reuse a dictionary across collections without duplicate build inclusion.
  Package it as a WPF Page/BAML resource in the host assembly. No XamlReader
  path, runtime filesystem lookup or loading from .almproj/.almlib/NuGet alarm
  packages. The installed VSIX must work without the original template files.
- Shared eng build integration resolves an explicit template/resource manifest
  before WPF markup compilation. The current BeforeTargets=CoreCompile hook is
  too late for discovery; address ordering deliberately. Include declared local
  resource dependencies in incremental inputs, clean and isolated fork copying.
  Missing resources and invalid XAML must fail the build without stale outputs.
- Keep the net8.0 generator platform-neutral: it validates declaration/path/key
  metadata and emits deterministic descriptors/manifest; WPF compilation and a
  host-side resource validation check validate actual resource types and keys.
  Do not execute templates in compiler generation or SDK consumer builds.
- Preview settings and declared template/resource content affect tooling
  compatibility, not semantic library fingerprints. Capture deterministic hashes
  of the same declared inputs for matching toolsets without embedding absolute
  paths or introducing WPF references into compiler/SDK assemblies. Test edits,
  removals, no-op builds, clean rebuilds and parallel target-framework builds.
  Coordinate contract versions only if serialized bridge payloads change;
  record the exact decision and mismatch tests in the implementation evidence.
- Publish a small host-owned record presentation contract: Fields indexed by
  definition name, with DisplayText, compiler-provided typed Value, State and
  label; plus record position/inherited state and detached origins. Make binding
  members public where WPF requires it. Never expose compiler objects, mutable
  document models or live project services to templates.
- Document one-way bindings such as Fields[Name].DisplayText. Supply reusable
  theme resources. Templates own record visuals, while the host owns collection
  repetition, row selection, navigation and expansion. Support ordinary WPF
  layout/styling/triggers; adding custom converters or code remains explicit fork
  development, not a new plugin mechanism. Preview is read-only.
- Build checks validate selected keys and DataTemplate types; representative
  render tests detect binding errors. Do not claim WPF compilation statically
  validates every arbitrary binding path. Missing keys/resource failures at
  runtime show a diagnostic and readable default template where recoverable;
  never silently blank the collection or promise isolation from faulty fork code.

### Column behavior and compatibility

- Use stable Collection:<name> IDs and template-capable DataGrid cells. Preserve
  existing scalar/built-in IDs, including Field:Collections. Remove the old
  aggregate Collections column; ignore its saved preference, retain surviving
  columns' settings, and append newly discovered collection columns predictably.
  Reset restores definition defaults. Reconcile obsolete sort selections too.
- Collection sorting is disabled initially: no sorting by rendered text or
  invented aggregate ordering. Scalar sorting remains compiler-typed. Search
  and clipboard/plain-text representations use deterministic field/item text,
  independent of template visual trees; retain existing search scope otherwise.
- Preserve compiler-origin detail/navigation access. Column changes, refresh,
  expansion and template rendering must not save source, materialize defaults,
  commit drafts or lose selection. Keep invalid/deferred state indicators visible
  even when a custom record template omits a field.
- Bound visible entry rendering for large collections and provide explicit
  expansion plus access to all entries; never silently truncate values. Preserve
  row virtualization and recycling. Initially show ten entries; expansion exposes
  all entries and copying includes all contents. Validate with representative
  large-preview measurements.

### Numbered iterations and acceptance

| Iteration | Deliverable and dependencies | Validation and completion gate |
| --- | --- | --- |
| 16 | Record confirmed preview direction, design defaults, exclusions and repository guidance. Depends on completed 15. | Documentation diff, links/path and consistency review; focused documentation commit. No runtime changes or test-pass claim. |
| 17 | Per-collection columns and simple List/Concatenated end to end: definition validation/generated metadata, cells, stable IDs, saved preference migration, value states, copy text and bounded rendering. Complex records use the built-in compiled template. Depends on 16. | Generator tests; editor column/render/state/migration/draft tests; both-runtime downstream metadata compatibility checks; native simple-column cases on both IDE hosts. Keep custom-template declarations explicitly guarded until 18. Update authoring docs with supported behavior. |
| 18 | Custom compiled XAML dictionaries, template keys, stable record binding contract, build/resource manifest, tooling fingerprint integration and error fallback. Depends on 17. | Clean/no-op/incremental/failing builds, missing/duplicate/wrong-type resource tests, binding/render fixtures, packaged VSIX checks without source files, compiler/SDK WPF-isolation checks; native custom-template cases on VS2022/2026. Validate edited/deleted resource inputs and matching-toolset rejection. Update authoring docs. |
| 19 | Playground templates and both simple modes, enabled/disabled procedure samples, launcher copying of declared assets, integrated acceptance and final evidence. Depends on 18. | Existing collection runtime and packaged fork workflows on net472/net8.0 and both MSBuild hosts; native column/template/refresh/draft checks on both IDE hosts sequentially; focused large-preview performance/recycling checks and affected regression suites. Record actual passes, failures, skips and remaining gates separately. |
| 20 | Approved procedure-style simple/complex designer entries, aligned fields and actions, inherited adorners, individual/bulk collapse and multiline tooltips. Depends on 19. | Draft/source/undo/identity tests, shared alignment, inherited navigation, tooltip states, collapse and focus tests; functional native checks on both IDE hosts. |
| 21 | Combined preview/designer regression acceptance and final authoring/sample evidence. Depends on 20. | Both frameworks/task hosts and both IDE hosts, resource packaging/failure checks, measured large-preview rendering/recycling and affected regression suites. No OS display checks. |

Iteration 19 completes preview-specific samples and acceptance; iteration 21 is
the final integrated gate after the newly accepted designer iteration 20.

### Approved designer layout and interaction

Use bordered procedure-style rows with a shared 26-DIP adorner gutter, aligned
headers/value areas and right-aligned actions. Inherited rows reuse
InheritedValueAdorner, remain read-only and retain Source navigation. Simple rows
have one full-width expression editor without a repeated label. Complex fields
share a label-column width within their collection. Add text-labelled icons to
Add entry, Clear previous entries, Collapse entries and Expand entries.

Start entries expanded. Individual toggles and collection-level collapse/expand
apply to local and inherited entries; new entries expand and receive focus. Keep
state/adorners/validation/actions visible when collapsed. Retain local collapse
state through moves/refresh via draft identities, and inherited state via compiler
origins. State is session-only; view actions never commit/discard drafts or write
source. Clear remains an explicit directive, excluding preceding effective
entries while retaining subsequent ones, without a value editor.

Collapsed-header tooltips are left-aligned, one field per line in definition
order. Simple entries show the value; records show Label: value. Local tooltips
use current draft expressions; inherited tooltips use compiler effective values.
Distinguish absent/defaulted, invalid and deferred states without host evaluation.
Expose equivalent accessible help text and expansion for full content.

Each implementation iteration must leave matching compiler/SDK/extension usable,
include its tests and documentation, and be committed before the next iteration.
Do not advertise guarded capabilities as working. No push, merge or publication
is part of this plan. OS theme/DPI display checks remain excluded; functional
native rendering and focused performance checks are acceptance gates.

### Implementation details to resolve within the iterations

No owner scope decision blocks this plan. Record the exact manifest format,
resource dependency handling, WPF target ordering, any bridge version transition
and measured visible-entry limit before finalizing the relevant iteration.
If implementation requires a scope change, update this section explicitly.

Architectural references: follow the existing [Arcade SDK structure](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk)
for shared build integration, preserving this repository's pinned imports. Use
[WPF DataTemplates](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-templating-overview)
for record presentation. Keep the existing compiler/host separation; there is no
reason to introduce a new compiler architecture for this presentation change.

### Iteration 16 evidence

Reviewed current column construction, saved layout IDs, collection detail models,
host WPF project and generation target ordering. Recorded the replacement preview
contract and iterations 17–19, and updated AGENTS.md to supersede the old count-only
guidance. Reviewed document consistency, referenced local paths and diff;
git diff --check passed. Runtime
tests were not run: this iteration changes documentation only. Implementation and
all acceptance gates for 17–19 remain pending.

### Iteration 17 implementation and validation (complete)

Replaced the aggregate column with Collection:<name> columns, retaining surviving
saved preferences and appending newly discovered collections. Added validated
List/Bullet/Number/None and Concatenated presentation, collection width/visibility,
detached bridge copying and a compiled default record template. Cells initially
show ten entries and expand explicitly; clipboard text includes every entry.
Collection sorting is disabled, and expansion buttons do not trigger source
navigation. Empty, unavailable and scalar value states remain distinct.

Generator tooling fingerprint advances to generator-4. Semantic hashing and
library format remain unchanged. Analysis 7/analyzer 5 remain: additive descriptor
properties are protected by the existing exact tooling-fingerprint check before
reflection copying; no new compiler value/origin payload is introduced. Custom
template declarations are explicitly guarded until iteration 18.

Validation:

- Generator suite: 81 passed.
- Editor regression suite: 596 passed, zero failed/skipped; OS display filters
  excluded (artifacts/m11-iteration17-editor.log). The initial sandbox run was
  cancelled before results; rerunning with installed-tool access succeeded.
- Final focused preview/render/migration selection: 27 passed, zero failed/skipped
  (artifacts/m11-iteration17-focused-final.log), including compiled simple and
  complex template rendering, ten-entry expansion and complete copy content.
- Isolated runtime/bridge checks passed on net472/net8.0: 71 procedure-disabled,
  69 enabled and 7 minimum-count checks per framework. Evidence:
  artifacts/log/Debug/AlarmCollections/5d51494163174661aac86fa72d98e77b;
  artifacts/m11-iteration17-runtime-final.log.
- Native fork checks: enabled artifacts/col13/004ee38f and disabled
  artifacts/col13/de369546 each passed one MetadataCollections case on VS2022 and
  VS2026: four passed, zero failed/skipped. Builds had zero warnings/errors.
  Cases include separate collection clipboard columns, canonical record values,
  Clear and existing draft/focus scenarios. Wrapper: artifacts/m11-iteration17-native.log.
- Source/diff review and git diff --check passed. No push or publication.
  Custom templates, playground extensions and approved designer layout remain
  iterations 18–21; this evidence does not claim those gates.

### Iteration 18 implementation and validation (complete)

Complex collection definitions now select a directly declared DataTemplate using
definition-relative TemplateFile and TemplateKey. The generator captures local
Source/UriSource dependencies recursively, rejects escaping/external/link paths,
and stages an owned manifest before WPF markup compilation. No-op staging retains
timestamps; removed templates leave neither staged files nor compiled resources.
Final host compilation verifies the captured bytes. Template contents affect the
generator-5 tooling fingerprint, not semantic library identity. The bridge copies
only detached strings; WPF resources are compiled into the extension.

The read-only binding contract exposes named/ordered fields, labels, display text,
primitive typed values, states, defaults, position and compiler-provided origins.
Missing dictionaries, keys and incorrect resource types use the built-in layout
with a visible diagnostic. Fork XAML remains trusted compiled code; arbitrary code
failures are not isolated. Representative binding/render tests are required;
compilation alone does not validate all binding paths.

Validation and evidence:

- Generator: 86 passed (artifacts/m11-iteration18-generator-verified.log).
- Focused editor preview/metadata tests: 24 passed, zero failed/skipped
  (artifacts/m11-iteration18-editor-final.log), including missing/incorrect-key
  fallback. Those two added tests first exposed an uncaught InvalidDataException;
  the exception filter was corrected and the complete selection rerun.
- Custom compiled-template binding/render test: one passed in isolated fork
  artifacts/col13/bdeb3ccb (artifacts/m11-iteration18-template-fixed.log).
- Clean native builds and four native cases passed: procedures enabled in
  artifacts/col13/3e9d8b14 and disabled in artifacts/col13/38b44f9c, on VS2022 and
  VS2026 sequentially. Each confirmed the custom template while its original
  source directory was unavailable; sources were restored afterward.
  Wrapper: artifacts/m11-iteration18-native-verified.log.
- Both-framework compiler/bridge runtime fixtures passed with procedures enabled,
  disabled and minimum-count definitions. Evidence:
  artifacts/log/Debug/AlarmCollections/48939d470f6f48e39388772fbab83919;
  artifacts/m11-iteration18-runtime.log.
- Lifecycle checks passed for no-op timestamps, dependency changes, missing keys,
  invalid XAML, missing dependencies, removal from compiled resources and restored
  inputs. Evidence: artifacts/m11-iteration18-lifecycle-verified.log and
  artifacts/col13/bdeb3ccb/preview-lifecycle.
- Earlier native/build attempts identified target ordering, unqualified MSBuild
  metadata batching and WPF temporary-project verification problems. These were
  corrected before the passing runs above. The lifecycle harness also required
  BOM-aware XML reading. Sandbox test-host connection attempts timed out;
  installed-tool access succeeded. No failed run is counted as acceptance.
- Reviewed source, documentation and diff; git diff --check passed. Playground
  launcher acceptance, designer changes and combined performance/regression gates
  remain iterations 19–21. No OS theme/DPI checks, push or publication.

### Iteration 19 implementation and validation (complete)

Both playground definitions now demonstrate Concatenated tags, numbered List
measurements and a compiled inspection-record template with a merged palette.
The launcher uses the generator's validated manifest to copy declared resources
from the original definition directory into the private compiler/tooling copy and
the reference playground. Playground directory copying now includes resources.
The samples and authoring instructions describe the installed-resource behavior.

Acceptance evidence so far:

- PrepareOnly launcher passed with procedures enabled on VS2022
  (artifacts/def11/6913f857) and disabled on VS2026 (artifacts/def11/a71bcf20),
  including SDK sample builds on both task runtimes and experimental deployment.
  Both copied template dictionaries are present in the playground. Logs:
  artifacts/m11-iteration19-launcher-enabled.log and
  artifacts/m11-iteration19-launcher-disabled.log. Initial sample validation
  rejected Preview before record fields; samples were corrected to schema order.
- Packaged project/library/NuGet/analyzer/API consumers passed on both task hosts
  for the compiled-template enabled and disabled toolsets from iteration 18:
  artifacts/col13/3e9d8b14/consumer-checks/9b8e104e and
  artifacts/col13/38b44f9c/consumer-checks/1f9d3a9d. Native source-unavailable
  template acceptance and both-framework runtime evidence from iteration 18
  remain applicable; this iteration does not change runtime presentation code.
- Two large-preview rendering/recycling scenarios passed. With 1,000 rows and
  100 entries per row, simple first-layout/scroll measured 1182/21 ms with four
  realized rows; complex measured 272/31 ms with two realized rows. Assertions
  verify ten initial entries, complete copying, reused row containers and no
  expansion-state leakage to recycled rows. Measurements are single-run Debug
  observations, not benchmark guarantees. Evidence:
  artifacts/m11-iteration19-rendering-measurement.log.
- Editor regression: 604 passed, zero failed/skipped
  (artifacts/m11-iteration19-editor-verified.log). An earlier run was cancelled
  without a result and rerun with explicit exclusions for the three dedicated
  simulated theme/scale rendering methods. No OS theme/DPI changes were made.
- Reviewed sample paths, launcher dependency copying and documentation;
  git diff --check passed. Iterations 20–21 retain designer and final combined
  acceptance responsibilities. No push, merge or publication.

### Iteration 20 checkpoint — paused, not complete

Account usage reached 80% used / 20% remaining. Stopped further implementation
and the native wrapper; no iteration-20 commit or success claim.

Uncommitted changes add a shared bordered entry template with 26-DIP gutter,
inherited adorners, record-label shared widths, right-aligned actions, command
icons, individual/bulk expansion, left-aligned multiline tooltip/accessibility
text and visible states. Model changes retain inherited expansion by compiler
origin through pending analysis and local expansion through moves. New local
entries still expand and focus. Clear has no fields and an explicit explanation.

Validation completed before pausing:

- Three focused state/draft/tooltip tests passed
  (artifacts/m11-iteration20-state-tests.log); earlier model selection: 15 passed.
- Two parallel WPF selections timed out. Moving the arrow resource declaration
  did not resolve those timeouts. Collection presentation fixtures were placed
  in a nonparallel xUnit collection; the subsequent focused selection passed
  all 28 tests (artifacts/m11-iteration20-layout-serial.log). Broader regression
  remains necessary to establish the fix beyond that selection.
- Private fork artifacts/col13/e71d68a6 built with zero warnings/errors and its
  procedure-enabled VS2022 native case passed, including bulk collapse preserving
  a draft and multiline accessible help. The wrapper was interrupted at the
  allowance threshold; remaining IDE/procedure combinations are not accepted.
  The private template source directory was restored after interruption.

Resume with inspection of the uncommitted diff and remaining native logs. Add
explicit rendered alignment/adorner, individual-collapse, pending-analysis,
selection/undo and focus-recovery coverage; review inherited state identity and
tooltip state presentation. Finish native acceptance on both hosts sequentially,
update authoring documentation and evidence, and commit iteration 20 only after
its gates pass. Then perform iteration 21. No push, merge or publication.

### Iteration 20 resumed implementation and evidence (complete)

The owner waived the allowance stop and requested completion. The approved
layout now applies to simple and complex entries, with a shared 26-DIP gutter,
InheritedValueAdorner, shared record-label widths, right-aligned local/Source
actions and four distinct VS command monikers alongside their text labels.
Individual and bulk collapse keep state/actions visible. New entries expand and
focus; moves preserve the existing expansion state and recover focus to a visible
action when the moved row is collapsed. Inherited state uses compiler library,
source, alarm and entry origins across refreshes, including pending intervals.
Tooltips/accessibility text follow definition order, preserve raw local drafts
and invalid inherited expressions, and label defaulted/absent/invalid/deferred
content without host expression evaluation. Clear remains an explicit directive.

Validation:

- Editor regression: 608 passed, zero failed/skipped
  (artifacts/m11-iteration20-editor-final.log). Collection presentation fixtures
  now run without parallel WPF resource initialization; no prior timeout recurred.
- Final rendered simple/complex alignment selection: two passed
  (artifacts/m11-iteration20-alignment-final.log). Checks cover shared editor
  positions, actual inherited adorners, 26-DIP inset, right-aligned actions,
  distinct icons, simple-label omission, left-aligned tooltip text and accessible
  help. Individual/bulk collapse retains raw drafts, selection, source bytes,
  selected alarm and a single undoable eventual commit.
- Model tests cover duplicate inherited origins, pending analysis, bulk actions,
  absent/default fields and definition-ordered tooltip states. Native tests add
  collapsed moves, accessible multiline draft content and new-entry focus.
- Four native scenarios passed, zero failed/skipped: procedures enabled in
  artifacts/col13/ed8146b5 and disabled in artifacts/col13/af2a2275, each on
  VS2022 then VS2026. Both builds had zero warnings/errors; all template sources
  were restored after packaged-resource validation. Wrapper:
  artifacts/m11-iteration20-native-final.log.
- Authoring/playground documentation updated; source/diff review and
  git diff --check passed. Iteration 21 remains the final combined acceptance.

### Iteration 21 combined acceptance (complete)

Added compiled negative XAML fixtures for missing record fields and missing
members, and inspected WPF binding status in visual/logical descendants of the
actual custom templates. The initial trace-listener approach produced no errors
without debugger tracing; local-value enumeration also missed template bindings.
The final check enumerates dependency-property descriptors and detects PathError
and UpdateTargetError, including dictionary-index failures. Both negative cases
and the actual selected templates pass their respective rejection/acceptance
checks. Authoring documentation describes this validation requirement.

Confirmed evidence:

- Generator suite: 86 passed. Compiler: 666 net472 and 668 net8.0 passed. MSBuild
  task units: 91 per framework; task integration: four per framework. Logs:
  artifacts/m11-iteration21-{generator,compiler,tasks,integration}.log.
- Preview/binding/rendering selection: 18 passed
  (artifacts/m11-iteration21-bindings-rendering-verified.log). The two deliberate
  binding errors are detected after successful XAML compilation. Actual custom
  templates passed the same binding inspection in both final isolated forks:
  artifacts/m11-iteration21-template-ed8146b5.log and
  artifacts/m11-iteration21-template-af2a2275.log (one test each).
- Final collection runtime/bridge checks: 72 procedure-disabled, 70 enabled and
  seven minimum-count checks per framework. Evidence:
  artifacts/log/Debug/AlarmCollections/69a064699f68492c985f874958d3a0cb;
  artifacts/m11-iteration21-collection-runtime.log.
- Final template lifecycle passed: no-op timestamps, changed dependencies,
  missing key, invalid XAML, missing dependency, removal from staged/compiled
  resources and restoration. Evidence:
  artifacts/m11-iteration21-template-lifecycle.log;
  artifacts/col13/ed8146b5/preview-lifecycle.
- Both final custom toolsets passed project/library/NuGet/analyzer/API consumer
  checks on .NET and desktop MSBuild. Evidence:
  artifacts/col13/ed8146b5/consumer-checks/0fb6e271 and
  artifacts/col13/af2a2275/consumer-checks/0da9e495.
- Final large-preview sample: 1,000 rows × 100 entries, ten initially visible.
  Simple first-layout/scroll: 1060/22 ms, four realized rows; complex: 260/28 ms,
  two realized rows. Recycling, complete copy and independent expansion state
  passed. These are Debug single-run observations, not performance guarantees.
- Iteration 20's 608 editor regressions, final alignment checks and four native
  cases cover the final production code. Only tests/documentation changed in 21;
  no native re-run is implied. Native cases cover both procedure variants and
  VS2022/2026 sequentially, with source XAML unavailable during template rendering.

- Standard SDK regressions: 85 net472 and 82 net8.0 passed, zero failed/skipped
  (artifacts/m11-iteration21-sdk.log). These use the default .NET MSBuild host;
  the separate custom-toolset checks above cover desktop MSBuild.
- VSIX packaging/template-consumer regressions: 13 per framework passed, zero
  failed/skipped (artifacts/m11-iteration21-vsix.log).

Final requirement review:

| Requirement | Acceptance evidence |
| --- | --- |
| Stable collection columns, preference migration, symbols/separators, empty/unavailable states, duplicates, ordering and full copy | Iteration 17 editor/native checks and iteration 21 preview/rendering selection. |
| Compiled templates, keys/types, dependencies, fingerprints, rebuilding and packaging | Iteration 18 generator/host checks; final lifecycle, binding and packaged-consumer runs above. |
| Samples and installed templates independent of original sources, with procedures enabled/disabled | Iteration 19 playground preparation and iteration 20's four native cases. |
| Shared alignment, inherited adorners, commands, collapse, tooltip/accessibility content and focus | Iteration 20 rendered, model and native checks, plus 608 editor regressions. |
| Draft/source/selection/undo preservation, pending analysis, duplicate origins and Clear | Iteration 20 editor/native checks and final both-framework collection runtime/bridge checks. |
| Bounded rendering and recycling | Iteration 21 large-preview measurements above, including complete copy and independent expansion state. |

The final diff and documented paths were reviewed; git diff --check passed.
Iterations 1–16 and historical checkpoints remain intact. All scoped acceptance
gates are complete. Trusted compiled fork templates are not sandboxed; binding
validation requires representative rendering, and the measurements above are
single-run observations. OS theme/DPI checks remain excluded. No push, merge or
publication was performed.

### Iteration 22 — label contrast and inherited heading spacing (complete)

Follow-up scope: fix collection-entry text colors and the inherited procedure
heading marker overlapping its text. Give the explicit collection-entry style
the dynamic VS tool-window text brush and reserve the same 26-DIP heading inset
for inherited and local titles. No compiler, draft or source-format changes.
Validate rendered light/dark resource changes, collapsed/expanded collection
headers, actual inherited adorners and title text bounds in focused editor tests,
then run editor regressions. OS theme/DPI changes remain excluded. Commit after
successful validation; no push or merge.

Focused validation: six rendered tests passed, zero failed/skipped
(artifacts/m11-iteration22-focused.log). These check resource updates, shared
title spacing, actual inherited markers, text bounds before/after focus and
existing draft/undo behavior. No OS theme or DPI settings were changed.

Editor regression: 610 passed, zero failed/skipped
(artifacts/m11-iteration22-editor.log), using iteration 20's exclusions for the
three dedicated theme/scale display methods. The focused heading tests use
in-process resources and rendering only. Diff review and git diff --check passed.
Native IDE scenarios were not rerun for this two-setter presentation fix.

### Iteration 23 — visible Boolean and Enum collection choices (complete)

Add the ordinary-field literal-choice dropdown to editable simple and complex
collection fields. Choices come from generated metadata; keep expression entry,
read-only inherited values, shared label alignment and themed controls. Opening
or dismissing the menu must preserve drafts and source; selection uses the
existing explicit draft action. Validate choices, focus/keyboard access, selection
and undo with rendered editor tests, then run editor regressions. Commit this
iteration on dev/milestone-11 after validation; no push.

Implemented the existing themed literal-choice menu and PreserveDraft handling
beside local collection editors, with editor space reserved for the dropdown.
Inherited controls remain read-only. Updated playground usage instructions.
Six focused rendered tests passed, including four Boolean/Enum × simple/complex
cases exercising Enter activation, metadata choices, dismissal without draft
changes, menu-command selection, explicit commit and undo. Existing alignment,
theme-resource, inherited-adorner and collapse tests also passed.
Evidence: artifacts/m11-iteration23-focused.log.

Editor regressions: 614 passed, zero failed/skipped, using the same three display
method exclusions as iteration 22 (artifacts/m11-iteration23-editor.log).
Diff review and git diff --check passed. Native IDE scenarios and OS theme/DPI
checks were not run for this reuse of the existing field-menu interaction.

## Authorized follow-up: multilingual enum captions and Alarm Summary

Iterations 24–27 are authorized. Preserve completed evidence above. Work on
dev/milestone-11, commit each validated iteration, and pause when any reported
account allowance window has 20% or less remaining. This replaces the earlier
waiver for this new goal. No push, merge, publication, PDF generation or OS
theme/DPI checks. Goal started with 98% remaining in the reported window.

| Iteration | Deliverable and acceptance |
| --- | --- |
| 24 | Optional enum Value LabelKey referencing project RESX; generated metadata and tooling fingerprint only; compiler-owned culture/resource fallback and ALM0031 warnings for every missing configured key per concrete application culture. Test generator, missing/empty/ambiguous resources, policy and unchanged output on both frameworks. |
| 25 | Shared caption presentation for ordinary and collection dropdowns, resolved summary values, inherited displays and effective tooltips. Custom template DisplayText translates, Value stays the token. Copy captions; search captions/tokens; retain declaration-order sorting. Test refresh, keyboard selection and draft/selection/collapse/undo preservation. |
| 26 | English/German playground resources, parent fallback and missing-caption sample; document semantics and verify RESX/XLIFF. Both-framework compiler/SDK checks, editor regressions and sequential VS2022/2026 native acceptance with procedures enabled/disabled. |
| 27 | Rename user-facing Preview to Alarm Summary, project command to Show Alarm Summary, and related language/scope/accessibility wording. Explain summary templates are independent of printed/PDF layout. Preserve internal IDs, commands, preferences and project properties; validate editor/project entry points and package registration. |

Caption resolution follows the selected alarm culture and existing compiler
resource precedence, including library resources. Captions are literal text,
without expression or placeholder evaluation. Fallback is Label then Name;
keyword mode uses fallback without missing-caption warnings. Concrete application
analysis/build checks all configured keys, even unused choices; library builds
defer warnings to consumption. ALM0031 follows warning suppression/promotion.
Reuse detached translation results; never resolve cultures or read RESX in the
host. Keep semantic library fingerprints and compiled enum tokens unchanged.
Unavailable analysis uses fallback captions. Editable expressions and local draft
tooltips remain raw; absent/invalid/deferred states stay distinct. Field/collection
labels, help and Boolean captions are outside this localization scope.

### Iteration 24 implementation and validation (complete)

Added Value/@LabelKey to the definition schema and generated ValueLabelKeys
metadata, advancing the tooling generation fingerprint to generator-6 while
preserving semantic fingerprints. Existing internal descriptor construction
remains source compatible through an optional final argument. Caption keys follow
the existing case-sensitive resource-key grammar. Ordinary and collection keys
are included in detached analysis translations even when absent from RESX.

Application evaluation checks all configured keys through ResourceCatalogue and
reports configurable ALM0031 warnings once per key/culture. Keyword and library
analysis skip the warning. No expression evaluation or new resource I/O was added.
The initial compiler run exposed a legacy assertion that every built-in diagnostic
was non-configurable; it now explicitly permits only the new warning.

Validation so far: generator 91 passed; compiler 671 net472 / 673 net8.0 passed;
task units 91 per framework passed. Generated collection fixtures passed both
frameworks: 79 procedure-disabled, 77 enabled and seven minimum-count checks.
These include actual compilation/analysis checks for missing captions, NoWarn,
warnings-as-errors, library deferral, absent analysis translations and unchanged
compiled enum tokens. Logs: artifacts/m11-iteration24-{generator,compiler-final,
tasks,runtime}.log. Runtime evidence:
artifacts/log/Debug/AlarmCollections/e842e693c57843b1b33aa8dfaadc725e.
SDK downstream validation passed: 85 net472 / 82 net8.0; editor regressions 614
passed, zero failed/skipped, with the same three display-method exclusions as
iteration 23. Logs: artifacts/m11-iteration24-{sdk,editor}.log. Commands used
dotnet test on the generator, compiler, task, SDK and editor test projects and
eng/test-alarm-collections.ps1 for the generated runtime fixture. Diff review and
git diff --check passed. Native caption presentation remains an iteration-26 gate;
the host still displays the existing labels at this iteration boundary.

### Iteration 25 implementation and validation (complete)

Added one detached-translation caption formatter shared by scalar/collection
choice menus, resolved field/table/detail displays, inherited entries and custom
template DisplayText. Value retains the typed token. Local expression/draft
tooltips remain raw. Choice objects and commands retain identity across caption
refreshes; unavailable/keyword translations use definition fallbacks. Copy uses
captions, search includes captions and raw tokens, and typed sorting is unchanged.
The table shares one translation dictionary across rows per analysis refresh.

The first focused build found a test-only internal enum exposed by a public test
method; the first run then found incorrect test column IDs and caption setup
before a draft-driven fallback refresh. Corrected those fixtures. The focused
selection subsequently passed 39 tests (artifacts/m11-iteration25-focused-verified.log).
The final editor regression passed 624 tests, zero failed/skipped, including
additional scalar inheritance and simple formatting checks
(artifacts/m11-iteration25-editor.log). It uses the same three display-method
exclusions as iteration 24. Diff review and git diff --check passed. Native
language/resource-refresh acceptance remains in 26.

### Iteration 26 samples and combined acceptance (complete)

Both playground definitions now declare enum caption keys. English/German RESX
files demonstrate de-DE parent fallback and intentionally missing Priority.Low;
the existing launcher copies the resources with the playground. Sample build
cultures are keyword, en and de-DE. Custom templates display Priority.DisplayText.
Added XLIFF round-trip coverage for caption keys without alarm expression usages.
The isolated native collection scenario now checks installed custom-template
captions, German parent fallback, saved RESX refresh, English/keyword switching,
translated clipboard content and unchanged ALMX/compiled enum tokens. Procedures
enabled/disabled and both IDE hosts were checked as required gates.

Compiler/XLIFF regression passed 672 net472 / 674 net8.0 tests; the native runner
built with zero warnings/errors (artifacts/m11-iteration26-{compiler,runner}.log).
Full fork consumer checks passed with procedures enabled (artifacts/col13/cedbcdc2,
consumer-checks/92b3fff6) and disabled (artifacts/col13/371bf367,
consumer-checks/d1c14dce), using both dotnet and desktop MSBuild. Enabled native
checks passed on VS2022 and VS2026 with template sources unavailable. The first
disabled VS2022 run stopped at the foreground-input safeguard when another
process gained focus, after German caption/resource-refresh checks. No product or
safeguard change was needed; the same compiled test passed on retry. Original
failure evidence and retry logs remain in the disabled fork.

Disabled VS2026 also passed; all four native cases passed with original template
sources unavailable. Both interactive launchers passed with -PrepareOnly: VS2022
enabled in artifacts/def11/b16aabbc and VS2026 disabled in artifacts/def11/83236259.
Their copied RESX inputs built keyword/en/de-DE output under both task hosts,
reporting the intentionally missing Priority.Low ALM0031 warning. Logs:
artifacts/m11-iteration26-playground-{enabled,disabled}.log and each fork's
build-sample-{core,desktop}.log. Existing standard SDK regression (85/82) and
editor regression (624) remain valid from iterations 24/25; this iteration adds
samples/tests only. Diff review and git diff --check passed. No OS theme/DPI
checks were performed; no new acceptance blocker remains for this iteration.

### Iteration 27 Alarm Summary terminology (complete)

Renamed the window, project action, language/scope controls, accessible names and
related guidance/errors to Alarm Summary. Added visible and accessible guidance
that collection templates customize resolved-data inspection independently of
document/PDF layouts. Internal command names/IDs, tool-window GUID, XML attributes,
project properties and saved column settings remain unchanged. Updated current
authoring/SDK/extension documentation and native UI selectors; historical milestone
evidence retains its original terminology. Added rendered guidance/tab coverage
and native project-window caption/help assertions. Editor, package and sequential
native entry-point/language/preference checks passed as recorded below.

Focused summary/column/project tests passed 22 cases. The final editor regression
passed 625 cases with the same three display-method exclusions as iteration 26;
package/consumer integration passed 13 per framework. Native runner build had
zero warnings/errors. Logs: artifacts/m11-iteration27-{focused,editor,package,
runner}.log. Sequential native Area=Preview|Area=PreviewTable acceptance passed
all three VS2022 cases (TRX directory
artifacts/log/Debug/VisualStudioUI/TestResults/18be4077e5de405e96d84f5c8b57a440).
All three VS2026 cases also passed (TRX directory
artifacts/log/Debug/VisualStudioUI/TestResults/6eced69b4c8a4bb1aac3bc255c45ba37).
Native logs: artifacts/m11-iteration27-native{2022,2026}.log. Both hosts exercised
the existing View.AlarmlistPreview command, renamed project-menu entry, window
caption and purpose text, language switching, project-specific column persistence,
draft preservation, clipboard actions and source navigation. Diff/links and
git diff --check passed. Iterations 24–27 are complete; the latest allowance
check showed 76% remaining, above the requested stop. No push, merge, publication
or OS theme/DPI checks were performed.

## Authorized follow-up: iterations 28–29

Implement on dev/milestone-11; commit each validated iteration, without pushing.
Pause at 20% account allowance remaining. Starting allowance: 73% remaining.
Preserve earlier evidence. No OS theme/DPI checks or publication.

28 — Make both custom-definition sample RESX files compatible with the native
resource editor using the four standard headers. Preserve caption values and the
intentional missing German key. Exercise the actual samples with the framework
RESX reader and native VS2022/2026 resource editors, including caption editing and
Summary refresh. Revalidate isolated sample copying and multilingual builds.
Existing generated playgrounds and their edits are preserved.

29 — Project > Show Alarm Summary creates a new session-only window pinned to the
selected project on every invocation. Keep the general-purpose View command and
its existing identity. Pinned windows display project names (numbered duplicates)
and full paths, without a scope selector. Each owns filter/sort/selection/tab
state; language and saved columns remain shared per project. Reuse shared project
analysis. Unload clears results without retargeting; reload restores them. Test
multiple projects/duplicates, focused command routing, settings, drafts, source
navigation and disposal, with sequential native validation on both IDEs.

### Iteration 28 implementation and validation (complete)

Added the template's standard RESX headers and xml:space="preserve" to both
playground resources. Native collection acceptance now copies those actual files,
opens both in Resource Explorer and edits the German caption there before checking
Summary refresh. Added two framework ResXResourceReader regression cases; both
passed (artifacts/m11-iteration28-resx.log). Native runner build passed with zero
warnings/errors (artifacts/m11-iteration28-runner.log). No compiler behavior changed.
All four isolated native cases passed on VS2022/VS2026, procedures enabled and
disabled (artifacts/col13/c85c5895 and 4838dfb4; native-2022/native-2026 logs and
TRX reports). Each opened English/German samples, edited/saved a caption in the
native resource editor and observed Summary refresh with source/enum tokens
preserved. Both launchers passed -PrepareOnly (artifacts/def11/055a4765 enabled,
19f425d5 disabled), building keyword/en/de-DE outputs with dotnet and desktop
MSBuild. Copied resource hashes match the checked-in samples. Logs:
artifacts/m11-iteration28-{native,playground-enabled,playground-disabled}.log.
Diff review and git diff --check passed. Existing generated playgrounds were
not rewritten; README documents safe header-only repair or a fresh launcher run.

### Iteration 29 implementation and validation (complete)

The existing general window retains its GUID and command. A separate registered
multi-instance, transient window reuses its presentation/lifecycle and shared
project analysis; each project-menu invocation allocates a session identity and
numbered caption. Pinned view models reject retargeting and render a read-only
project label with full-path help. Hide/detach captures only filter/sort/selection
identities and tab index, then disposes subscriptions and analysis adapters.
Language and column persistence remain project-owned. This follows the VSSDK
[multi-instance tool-window pattern](https://learn.microsoft.com/en-us/visualstudio/extensibility/creating-a-multi-instance-tool-window).
Focused model/BAML tests passed 20 cases. Full editor regression passed 632 cases
with the same three display-method exclusions as iteration 27. Package/consumer
integration passed 13 cases per framework; native runner build had zero warnings
and errors. Logs: artifacts/m11-iteration29-{focused,editor,package,runner}.log.
Initial restricted build returned failure without diagnostics;
the normal approved test invocation built successfully. Initial test-only await
warnings were annotated consistently with nearby persistence tests.

The first VS2022 pinned-window run exposed lower-cased shell canonical paths in
captions. Project selection now retains the captured project's display spelling.
The native fixture also gives floating windows a deterministic usable viewport.
Both existing general Summary scenarios passed in that run; the revised pinned
scenario passed separately, including hide/reopen, duplicate views, shared
language/columns, private draft preservation, focused toolbar routing, project
unload/reload, source navigation, closing a duplicate and IDE restart. Logs:
artifacts/m11-iteration29-native2022{,-retry}.log. Passing pinned TRX directory:
artifacts/log/Debug/VisualStudioUI/TestResults/b11625539df547dca0037e04b6e9f409.

VS2026 passed the three existing Summary/table scenarios. Its first pinned test
stopped at the keyboard foreground safeguard when another process gained focus;
the unchanged test passed on retry, including restart. No safeguard was relaxed.
Logs: artifacts/m11-iteration29-native2026{,-retry}.log; passing retry TRX directory:
artifacts/log/Debug/VisualStudioUI/TestResults/38284ea0ffa44a9eab79cb89309bb5ef.

The final VS2022 table regression also passed (one case), completing the same four
functional scenarios on both IDE hosts sequentially. Log:
artifacts/m11-iteration29-table2022.log; TRX directory:
artifacts/log/Debug/VisualStudioUI/TestResults/393b647fee3141bc80db746ea5417d10.
Diff, links and git diff --check passed. No remaining scoped acceptance blocker.
OS theme/DPI checks remained excluded. Latest allowance check: 62% remaining.
No push, merge or publication was performed.
