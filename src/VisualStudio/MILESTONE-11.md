# Milestone eleven: generated alarm definitions end to end

Status: planning iteration 1 complete; implementation iterations 2–8 are planned,
not implemented or validated. Owner decisions were confirmed on 2026-10-21.

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

Excluded: per-project schema loading, arbitrary CLR/plugin types, custom
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
each successful iteration and report its hash before proceeding. No implementation
is authorized solely by this plan; begin it on the owner's implementation request.

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
diff and local links. Tests were not run: documentation only. Iterations 2–8 have
no implementation or acceptance evidence yet.
