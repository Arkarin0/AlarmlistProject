# Milestone ten: procedure-group presentation and splitter refinement

Status: implementation authorized; iteration 1 complete, iterations 2–3 pending.

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
