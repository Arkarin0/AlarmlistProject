# Milestone eight: custom procedure groups

Status: implementation authorized on 2026-10-13. Iterations 1 through 3 are complete;
configured creation and integrated acceptance remain.

## Outcome and confirmed contract

Replace fixed Instructions/Reset sections with ordered named groups across the
compiler, SDK, designer and previews. Make a clean format/API transition; migrate
active repository examples, templates and tests together. No legacy adapters or
automatic migration tool are included.

- Source: `<TestProcedure><Group Name="Instructions"><Title>$(loc:InstructionsTitle)</Title><Step>Text</Step></Group></TestProcedure>`.
  Title is optional; existing entry kinds and Clear retain their semantics.
- Output: Group/Name/Title with evaluated titles and existing compiled
  TestProcedureItem entries. Empty groups remain represented.
- IDs match `[A-Za-z_][A-Za-z0-9_.-]*`, compare ordinally ignoring case, and
  preserve authored spelling. Duplicate local IDs are errors.
- Match inherited groups by identity, retain base order/spelling, append new local
  groups in declaration order, and merge entries with existing Clear semantics.
- Absent titles inherit, then fall back to the ID. Explicit empty titles stop
  inheritance and display the ID. Expression errors do not trigger fallback.
- Titles support the same expressions as procedure text in the consuming alarm,
  including inherited titles, keyword culture, literal substitution, provenance
  and deferred abstract-template evaluation.
- Removing a local declaration reveals any inherited group. Clear empties entries,
  not the heading. No whole-group exclusion or identity renaming.
- Reject legacy Instructions/Reset source elements explicitly; retain unrelated
  unknown-element skipping and DTD prohibition.
- Replace fixed properties with ordered collections in syntax, resolved models,
  analysis and detached host DTOs. Include raw/effective titles, availability,
  origins and local/effective/excluded entries. Advance analysis contract 3 to 4
  and analyzer contract 1 to 2 together with adapters and sample analyzer.
- Dynamic designer groups retain existing entry editing, draft/focus, adorners,
  Clear visibility, completion and source-preserving Undo behavior. Add group,
  edit title, remove local declaration, and move newly introduced local groups.
  Inherited group positions stay fixed. Protect unsupported source content.
- Preview uses one procedures column with ordered group headings and entries,
  including empty headings. Preserve column preferences and virtualization.

Creation settings (destination physical file, captured EditorConfig):

```ini
[*.almx]
alarmlist_procedure.default_groups = Instructions, Reset, Verification
alarmlist_procedure.group.instructions.title = $(loc:InstructionsTitle)
alarmlist_procedure.group.verification.title = Verification
```

Missing/unset list defaults to Instructions and Reset; an explicit empty list
creates no groups. Trim list entries, reject invalid/duplicate IDs, and block
creation with actionable feedback for invalid settings. Missing titles omit Title;
explicit empty writes an empty Title. Revalidate context/configuration before
insertion. Configuration changes never rewrite alarms; duplication preserves groups.

## Architecture, migration and exclusions

Keep resolution and configuration interpretation compiler-owned, with immutable
detached results across the SDK AppDomain boundary. Preserve net472/net8.0 and
SDK packaging/runtime selection. Follow the existing compiler layers and the
[Roslyn compiler reference](https://github.com/dotnet/roslyn/tree/main/src/Compilers)
reviewed during planning; no new generator or build infrastructure is required.

Migration replaces `<Instructions>…</Instructions>` with
`<Group Name="Instructions">…</Group>` and likewise for Reset. Replace fixed
public properties with group collection access and rebuild external analyzers
against contract 2. SDK and VSIX must be updated together for analysis contract 4.
Old contracts fail explicitly rather than silently dropping groups.

Libraries, project references, Shared Projects, syntax generation, broader XML
IntelliSense and publication are excluded. Earlier manual accessibility/display,
vendor and release gates remain separate under their existing milestone plans.

## Numbered iterations and planned acceptance

Each iteration depends on the preceding one. Keep compiler/SDK/extension usable
together at boundaries; review and commit each successful iteration before the
next, recording changes, commands, results and limitations separately below.

1. **Record contract:** this plan, AGENTS link and backlog update. Review links,
   paths and consistency; no build required.
2. **Coordinated transition:** dynamic parsing/binding/evaluation/output,
   analysis/analyzer contracts, bridge, source projection, existing entry editing
   and previews; migrate active fixtures/templates/examples. Require compiler,
   task, SDK and extension unit/package suites and focused native editor/preview
   checks on both hosts.
3. **Group authoring:** add/title/remove/reorder, completion and Undo. Cover source
   preservation, inherited behavior, focus/private drafts, accessibility metadata
   and native keyboard workflows on VS2022 and VS2026.
4. **Creation defaults:** compiler-owned detached settings, destination-scoped
   creation and stale-context guards. Cover linked consumers, malformed/empty/
   missing lists and subsequent saved configuration changes.
5. **Integrated acceptance:** solution build/tests, full native suites on both
   installed VS hosts, isolated procedure/preview measurements, migration and
   requirement audit. Keep manual/vendor/release gates explicitly separate.

Regression coverage includes duplicate IDs ignoring case, cross-file/multilevel
inheritance, ordering, title overrides/fallback, Clear provenance, empty groups,
localization/lookup errors, abstract templates, legacy rejection, preserved outputs
on failure, arbitrary group counts, cultures during drafts, stale analysis, linked
contexts, navigation, reload, Undo/Redo and lifetime collection. Measurements use
many groups/long procedures without machine-dependent thresholds.

## Actual iteration record

### Iteration 1 — contract documentation

Recorded the approved plan and linked it from repository instructions and backlog.
The starting working tree was clean. Reviewed documentation paths, scope and
contract consistency. Tests were not run (documentation only).

Committed as `983cb98`.

### Iteration 2 — coordinated transition

Implemented ordered syntax/resolved groups, source and output XML, consuming-alarm
title evaluation, title/group/entry provenance, mandatory ALM0026/ALM0027 identity
diagnostics, analysis contract 4 and analyzer contract 2. Migrated active fixtures,
examples, templates, package verification and native scenarios. Designer sections
now follow compiler groups and source declarations; previews include empty headings.
New-alarm creation/templates explicitly contain Instructions and Reset until
iteration 4 supplies captured configuration. No implicit compiler groups remain.

Tests exposed stale detached section presentation and canonical spelling changes
after analysis publication. Removed sections now clear their data/commands, and
retained sections adopt compiler identity spelling without replacing draft controls.
Source copies preserve titles and reject repeated titles. Migration also updated
old test assumptions about implicit groups and analyzer contract numbers.

Validation (all paths below are under `artifacts/log/Debug/Milestone8/Iteration2/`):

- Compiler tests: 492 net8.0 and 490 net472 passes, zero failures/skips (`Compiler`).
  Includes custom order, case-insensitive identities, inherited localized titles,
  explicit empty, provenance, empty output groups and legacy rejection.
- `dotnet test AlarmlistProject.slnx --no-restore --verbosity minimal --logger trx
  --results-directory artifacts/log/Debug/Milestone8/Iteration2/Regression`:
  task unit 68 and integration 4 per framework; SDK 53 and extension package 12
  per framework; performance infrastructure 47 passes/12 opt-in skips. Ordinary
  native cases are opt-in skips, not native acceptance. The extension unit run
  failed two checks (canonical spelling and a report expectation), retained as
  failed evidence. Compiler counts match above.
- Subsequent full extension unit run (`FinalUnit`): 458 passed/one failed, zero
  skips. The remaining expectation incorrectly required a removed Reset group.
  Corrected that expectation; `FinalCorrections` passed all 13 selected cases,
  including the report, custom groups, new-alarm defaults and explicit contract-3
  rejection. Combined evidence covers all 460 current unit cases; no failed run
  is relabelled as passing. Earlier investigations remain under Initial/Unit2.
- Packaged custom-group build/failure-preservation test: one pass on each
  framework (`GroupConsumer`), including NoWarn refusing to suppress ALM0026.
- Native custom Verification-group editing/Clear/drafts/Undo and AlarmTable:
  two passes per host, zero skips, using `eng/test-visualstudio.ps1` with
  `Area=Procedures|FullyQualifiedName~AlarmTable2022` (VS2022/AlarmlistExp) and
  corresponding 2026 filter (VS2026/AlarmlistM7). Native report IDs:
  `456ba43a476444a59f7e0f947146addc`, `6a0395bb27b44300a85a6a6b8e9ea847`.
- Compiler and VSIX builds succeeded. Solution builds retain the two previously
  documented CA1416 shared-test-helper warnings. Reviewed diff and whitespace.

The sandbox incorrectly reports Git diff/build as outside a work tree; those
commands succeeded with the ordinary host execution permission. No unrelated
changes were present. No essential iteration-2 blocker remains; final milestone
acceptance will rerun the complete suites after authoring/defaults work.

### Iteration 3 — group authoring

Added group creation, expression-capable title editing, inherited-title removal
and navigation, local declaration removal, and local-only reordering. Group actions
keep focus on the affected title (or the new-ID field when no group remains).
Title and entry drafts share one source-preserving Undo transaction. Stale group
creation is rejected after draft/context changes. Removal refuses unsupported XML,
comments and attributes; title edits preserve existing attributes. Updated usage
documentation and accessibility expectations.

Validation:

- VSIX build succeeded. Focused editor tests passed 38 cases.
- Full editor unit suite: 470 passed, zero failures/skips, using `dotnet test
  src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
  --no-restore --verbosity minimal --logger trx --results-directory
  artifacts/log/Debug/Milestone8/Iteration3/FinalUnit`.
- `eng/test-visualstudio.ps1 -Filter 'Area=Procedures'` passed one scenario on
  each installed host (VS2022/AlarmlistExp and VS2026/AlarmlistM7), zero skips.
  It covers existing Clear/draft/entry operations plus Add group, private title
  drafts, expression title commit, local reorder/focus, removal and Undo, preserving
  saved source and output. Reports: `4751bfef7fb84c3f8707d0e4ec9aaf0b` and
  `544a4af039cc4940a9ac7e0ea3bf4f55`.
- Initial full units failed two old accessibility expectations; initial native
  test checked all groups for absence of the second group's draft. Corrected those
  test expectations and reran successfully; failed reports remain retained.
- Reviewed diff, source guards, documentation and whitespace. No essential
  iteration-3 blocker remains. Manual display/accessibility gates remain separate.
