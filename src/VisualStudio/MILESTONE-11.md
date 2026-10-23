# Milestone eleven: generated alarm definitions end to end

Status: iterations 1–8 complete; original acceptance recorded on 2026-10-23.
Follow-up iterations 9–13 complete; iterations 14–15 pending as of
2026-10-23. See the collection/playground extension below. Original acceptance
does not cover these additions.
Owner decisions were confirmed on 2026-10-21.
Implementation was authorized on 2026-10-22, with a pause requested when the
remaining account allowance reaches 20%. Check allowance periodically and before
starting each iteration; record incomplete work honestly if that threshold is met.

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
  Preview shows collection counts with a separate detail view, not variable table
  columns. No host-side scalar parsing or inferred inheritance/provenance.
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
