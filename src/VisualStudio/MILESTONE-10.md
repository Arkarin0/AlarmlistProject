# Milestone ten: procedure-group presentation and splitter refinement

Status: iterations 1–2 complete; iteration 3 pending.

## Outcome and confirmed design

Improve the ALMX designer with a GroupBox for each procedure group, one editable
heading, and a recognizable file/alarm resize handle. The owner selected a
title-like heading, an always-visible read-only `Group ID: …` label beneath it,
and a centered themed splitter grip.

- Reuse the existing ExpressionTextBox and TitleField, including completion,
  inheritance indicator/context menu, source navigation and focus handling. Show
  resolved text at rest and raw expressions on focus. Use a borderless resting
  heading, hover edit feedback and a clear focus border. Wrap long headings within
  available width without overlapping actions; retain controls during refresh.
- Remove the duplicated heading and separate title-input row. Keep the authored
  group ID visible and read-only. Place existing actions in a wrapping toolbar
  below the ID. Groups remain expanded, with existing entry and Clear operations.
- Preserve absent-title inheritance/ID fallback, explicit-empty inheritance stop
  with ID display, and unavailable/error expression behavior. Never rename IDs
  through title editing. Preserve private drafts, commit/cancel, Undo/Redo,
  source preservation and stale-input protection.
- Retain ScopeDivider resizing, keyboard access, persisted proportions and draft
  preservation. Add a centered decorative grip with hover/focus feedback; the
  entire divider remains draggable, with no extra tab stop. Keep collapse rules.

This changes presentation only: no compiler/source/public API/analysis contract,
preview-table, XML IntelliSense, group-renaming, individual group collapse or
publication changes. Historical manual/vendor/release gates remain separate.
No product decisions remain open.

## Architecture

Reuse existing WPF controls, bindings and source-preserving editor commands in
UI/EditFileControl.xaml and its view models. Compiler-owned semantics continue
through the existing detached analysis, consistent with the repository's Roslyn
layering direction; packaging/build remain under existing Arcade configuration.
No new architectural pattern, dependency or upstream source adaptation is needed.
See [milestone eight](MILESTONE-8.md) for group semantics and
[milestone seven](MILESTONE-7.md) for scope layout and draft rules.

## Numbered iterations and planned acceptance

Each iteration depends on its predecessor. Record actual evidence separately;
commit every successful iteration before starting the next. An essential failed
or unavailable native check blocks completion, not merely documentation.

| Iteration | Deliverable | Validation and completion criteria |
| --- | --- | --- |
| 1 | Record confirmed contract, boundaries and gates; link repository guidance and assign the future-feature proposal. | Review diff, paths, links and consistency. Documentation only; no build/tests required. |
| 2 | GroupBox presentation and editable headings, retaining all authoring behavior; WPF and native regression coverage. | Editor unit suite and native procedure scenarios on VS2022 and VS2026: keyboard editing/completion, title-source navigation, group operations, Undo/Redo and unchanged source for presentation actions. |
| 3 | Themed splitter grip, scope tests, usage documentation and integrated acceptance. | Editor unit suite, packaged VSIX integration tests on both frameworks, native procedure/scope scenarios on both hosts; rendered light/dark/high-contrast narrow/wide and enlarged-scale layouts. |

Acceptance covers local/inherited/empty/missing/localized/erroneous/long titles,
multiple/empty groups, drafts during analysis refresh and splitter interaction,
keyboard accessibility, and unchanged source for presentation-only actions.
Run desktop-interacting suites sequentially. Retain actual commands, results,
render evidence and remaining limitations below; planned checks are not evidence.

## Implementation evidence

### Iteration 1 — milestone contract (2026-10-21)

Recorded the approved design and iteration gates, linked this plan from AGENTS.md,
and assigned the existing future-feature proposal to milestone ten. Reviewed the
documentation diff, relative links and consistency with milestone-eight semantics.
Tests were not run: this iteration changes documentation only. No blocker remains.

### Iteration 2 — group boundaries and editable headings (2026-10-21)

Moved the existing expression editor into a themed GroupBox header and removed
the duplicate title row. Headers retain the original field/context-menu/navigation
bindings, completion and inherited-value behavior. A bounded header template wraps
long expressions; each group shows its read-only ID and existing wrapping actions.

Added WPF coverage for inherited/raw/resolved titles, localized long headings,
missing/empty/error titles, theme resources, stable controls/selection across
refresh, Escape/Enter, source selection and Undo. Extended the native procedure
scenario with heading completion/navigation and ID/raw/resolved presentation.

Evidence so far (not iteration completion):

- Focused editor test filter `FullyQualifiedName~Group|FullyQualifiedName~Procedure`:
  **46 passed net472**, zero failures/skips.
- `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
  --verbosity minimal -m:1 -nr:false`: **557 passed net472**, zero failures/skips.
- Initial sandbox build stopped producing output and was cancelled. The normal
  Windows-access single-worker build/test completed successfully. No repository
  build configuration was changed.
- Initial VS2022 native run failed in the pre-existing creation-defaults helper:
  polling queued another Add key before acknowledgement, creating two alarms.
  The helper now waits for creation or an explicit stale-input refusal before
  retrying, clears acknowledged refusals, and asserts the exact alarm count.
  No application behavior was changed for this test correction.
- `eng/test-visualstudio.ps1 -VisualStudioPath <host> -Filter 'Area=Procedures'`:
  **1 passed per host**, zero failures/skips. Final TestResults runs:
  `b3a74da8a1654c7a87ab0c11f4d37ff1` (VS2022),
  `591e70f29cef411d84cabee647c2cb57` (VS2026). The failure evidence remains in
  `c78cb4e5717547eab6720029e0f4b299`.
- After improving render framing, reran `FullyQualifiedName~GroupHeading`:
  **4 passed net472**. Reviewed complete GroupBox images under
  `artifacts/log/Debug/Milestone10/iteration2/` for light (900 DIP, 100%),
  dark (650 DIP, 150%) and high-contrast (440 DIP, 200%) resource palettes.
  Headings wrap, IDs/actions remain separated and borders remain visible.
  These are WPF resource-palette/scaled renders, not a claim of changing the OS
  accessibility theme or display DPI. Native interaction was verified separately.
- Updated designer usage documentation and reviewed the final diff/whitespace.
  No iteration-2 blocker remains. Splitter and integrated acceptance follow.
