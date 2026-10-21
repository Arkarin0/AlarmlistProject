# Milestone ten: procedure-group presentation and splitter refinement

Status: iterations 1–4 complete; follow-up iteration 5 authorized and planned.
Original scoped acceptance passed on VS2022 and VS2026. Historical gates remain separate.

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
  below the ID. Groups initially expand, with existing entry and Clear operations.
  Follow-up iteration 4 adds individual collapse and Expand/Collapse all.
- Preserve absent-title inheritance/ID fallback, explicit-empty inheritance stop
  with ID display, and unavailable/error expression behavior. Never rename IDs
  through title editing. Preserve private drafts, commit/cancel, Undo/Redo,
  source preservation and stale-input protection.
- Retain ScopeDivider resizing, keyboard access, persisted proportions and draft
  preservation. Add a centered decorative grip with hover/focus feedback; the
  entire divider remains draggable, with no extra tab stop. Keep collapse rules.

This changes presentation only: no compiler/source/public API/analysis contract,
preview-table, XML IntelliSense, group-renaming or
publication changes. Historical manual/vendor/release gates remain separate.
No product decisions remain open.

The owner authorized collapsible groups and procedure search after iteration 3,
explicitly excluding additional display checks. This supersedes the original
always-expanded decision. Retain headings and IDs when collapsed, preserve private
drafts and controls, and reopen a group for source navigation or authoring focus.
Expansion is transient per open editor/group identity, survives refresh while the
group exists, and is not written to source or persisted settings.
Search is case-insensitive literal substring matching within the selected alarm:
group IDs, raw/resolved titles and local/inherited entry text (including cleared
entries). Show a match list and previous/next navigation, without filtering away
editors. Searching and navigating results preserve drafts; reveal a matched group
and cleared row without moving focus into its editor. Refresh results with current
draft/analysis state and discard stale result targets. No OS theme, DPI-switching,
or additional palette/render display checks are part of these follow-ups.

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
| 4 | Collapsible group bodies, Expand/Collapse all, draft-safe controls and automatic source/authoring expansion; usage documentation. | Focused model/WPF tests for independent/all toggles, unchanged source, draft preservation, refresh and source navigation; native Procedures scenario on both supported hosts. Depends on iteration 3. |
| 5 | Procedure search, match list and previous/next navigation with current raw/resolved local/inherited text; documentation. | Focused search/WPF regressions covering case-insensitive matches, cleared/inherited entries, drafts, refresh and no matches; native Procedures on both hosts and packaged VSIX integration on both frameworks. Depends on iteration 4. |

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

Validation and review:

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

### Iteration 3 — splitter grip and integrated acceptance (2026-10-21)

Retained the existing scope GridSplitter and handlers; added a centered decorative
two-line grip, themed hover/focus feedback, accessible keyboard help and an 11-DIP
full-width drag area. No extra focusable control or layout preference is added.
Scope tests now cover grip alignment, theme/focus colors, draft-safe resize and
palette/scaled renders. The native scope scenario also drags the centered grip
with a private field draft and verifies unchanged source. Native group captures
hide the owned IDE's floating preview first so it cannot obscure the designer.
Updated the usage guide to describe the grip and full-width drag target.

Validation: full editor suite with the splitter
change passed **557 net472 tests**, zero failures/skips, using the iteration-2
command with `--verbosity quiet`. After allowing the focus render to settle,
three scope-layout cases passed again. Final contrast review replaced hover
background changes with a thicker grip so matching high-contrast foreground and
border brushes cannot hide it; the seven group-heading/scope-layout cases passed
after that final style adjustment.

Final native acceptance:

- `eng/test-visualstudio.ps1 -VisualStudioPath <host> -Filter
  'Area=Procedures|Area=Scopes'`: **2 passed per host**, zero failures/skips.
  Final TestResults runs: `e2285d3556284a1087ef16d3868f8b74` (VS2022),
  `f0c5657f099d4aa5bdd2e74aa6039539` (VS2026). The earlier VS2022 splitter
  run also passed both cases (`2f757a03ff13421394cc9447a0a8aec1`).
- Native scope cases verify keyboard and centered-grip mouse resize with a private
  draft, collapse/reopen, persisted proportions and unchanged source/output bytes.
  Procedure cases retain completion, title navigation, group/entry authoring,
  Undo and saved creation-default refresh. Desktop suites ran sequentially.
- Reviewed WPF group/scope/grip renders under
  `artifacts/log/Debug/Milestone10/iteration3/` at the same three palettes,
  widths and scales as iteration 2, including settled keyboard-focus colors.
  Reviewed native group screenshots as additional presentation evidence.
  Palette and scale coverage uses WPF resources/render scaling; it does not
  claim a separate OS high-contrast or per-monitor DPI-switching exercise.
- `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj
  --verbosity quiet -m:1 -nr:false`: **13 passed net472 and 13 passed net8.0**,
  zero failures/skips, including a final run against the rebuilt VSIX after the
  hover adjustment. Focused final layout filter:
  `FullyQualifiedName~DesignerScopesScrollCollapse|FullyQualifiedName~GroupHeading`
  with the editor unit project and the same switches: **7 passed net472**.
- Reviewed final diff, whitespace, documentation paths and acceptance coverage.
  No implementation gate remains. Compiler/SDK-wide suites were not rerun because
  their implementation/contracts were unchanged; package-consumer validation was
  included. No publication occurred. Existing release/vendor gates are unaffected.

### Iteration 4 — collapsible groups (2026-10-21)

Added draft-preserving individual and all-group toggles. Headers and IDs remain
visible; bodies retain their controls. Expansion survives analysis refresh while
the group exists. Source navigation and authoring focus expand the target group.
Updated usage documentation and repository guidance for the approved follow-ups.

Validation: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
--filter FullyQualifiedName~ProcedureCollapse --verbosity quiet -m:1 -nr:false`
passed **1 net472 test**, covering drafts, refresh, independent/all toggles,
navigation and unchanged source. Native `eng/test-visualstudio.ps1
-VisualStudioPath <host> -Filter 'Area=Procedures'` passed **1 per host**, including
collapse/reopen with a private draft and existing authoring/Undo scenarios.
TRX run IDs: `2ca70562def84531b9bde9ecf7815ab2` (VS2022),
`25427de03de943a3aa4fab7129d780da` (VS2026).
Reviewed diff and whitespace; no blocker remains. No display checks were run.
