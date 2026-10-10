# Milestone seven: ALMX editor user experience

Status: direction confirmed on 2026-10-10; initial source review and design
proposals recorded. No production UI changes implemented. The owner selected
improving the existing file editor's UI/UX as this milestone's purpose, then
identified procedure editing as the worst experience and requested improvement
suggestions. The earlier custom-procedure-group suggestion was not selected.

## Intended outcome and confirmed scope

Make everyday ALMX authoring easier: find an alarm, understand its values, edit
fields and procedures, and see the result without excessive scrolling or confusing
focus changes. Scope covers the existing Visual Studio designer and its interaction
with the XML editor, preview, references, parameters and diagnostics.

Procedure editing is the first priority; the precise interaction and preferred
layout are still being discussed. Do not treat the proposals below as
owner-approved interaction changes. Interpret the
owner's "alms file editor" as the existing `.almx` editor, not a new file format.

Custom procedure groups, alarm libraries, project references, Shared Projects,
NuGet distribution, syntax generation and new language semantics are outside the
initial UX scope. Existing manual/vendor/release gates in milestones four through
six remain separate; this plan does not close them.

## Source review: observations and hypotheses

Reviewed the current XAML, code-behind, layout settings and nearby focus/keyboard
tests. These findings describe the source; no live IDE usability session was run.

| Observation | UX hypothesis to evaluate |
| --- | --- |
| `EditFileControl.xaml` places an expanded file parameter control, filter/actions, project context and status above the alarm list and detail area. | File/project controls consume space needed for the selected alarm, especially in Split mode. |
| Alarm parameters, reference selection, scalar fields and both procedure sections share one long detail form. | Grouping by the editing task may reduce scrolling and make procedures easier to reach. |
| Scalar rows expose XML-presence checkboxes plus repeated inheritance actions and origin information. | Compact state labels and contextual actions could explain ownership with less repetition, while preserving absent/empty/inherited distinctions. |
| Procedures present inherited, excluded, local and effective sequences, with substantial explanatory text. | A clearer primary editing sequence and secondary origin/exclusion details could improve readability; compiler provenance must still control every row. |
| `OnFieldLostFocus` commits within the designer; leaving for shell controls retains drafts. Enter commits, Shift+Enter permits multiline input, and Escape cancels after completion handling. | Commit boundaries and raw/resolved display need usability review; layout changes must not accidentally discard or commit drafts. |
| The alarm list is fixed at 220 pixels and becomes a selector below 600 pixels. Split settings already persist. | A resizable navigator and intentional narrow layout may improve use in docked panes. |
| `SplitEditorControl` always displays orientation and swap controls, even outside Split mode. | Contextual layout controls could remove inactive visual noise. |

## Proposed design directions

Two procedure-focused layouts accompany the discussion as interactive conversation
mockups with synthetic alarms; they are not production code or evidence of WPF
behavior. The mockups illustrate navigation and information hierarchy, not the
complete draft, completion, source-preservation or accessibility contract.

- **Inline sequence (initial recommendation):** show entries in their effective
  order, label inherited entries as read-only and edit local entries directly.
  Expand the selected entry's editor/actions; keep other entries readable and compact.
  Put Instructions/Reset selection at the top. Keep type and reorder/remove actions
  compact and move source/exclusion details into a secondary disclosure.
- **Outline and editor:** show a compact ordered entry list beside a larger editor
  for the selected entry. This may suit long procedures and long multiline text,
  at the cost of an extra selection step and less text visible at once.
- Both directions retain compiler-owned provenance and the existing whole-section
  local-copy operation. The mockups only demonstrate a simple inherited prefix
  plus local entries; Clear/excluded/unsupported content requires separate design
  and validation. No per-inherited-entry override is promised.
- Prioritize adding entries where needed, focusing the new editor immediately,
  and preserving focus/selection during reorder. Keyboard move/duplicate/insert
  commands and drag-and-drop are suggestions, not confirmed scope. Every pointer
  operation needs a keyboard alternative.
- Suggested text-entry contract to evaluate: Enter inserts a newline and
  Ctrl+Enter commits, with completion-popup keys taking precedence. This would
  change the current behavior and is not yet a confirmed decision. Define draft
  handling when selecting another entry and retain source-buffer Undo/Redo.
- Keep raw expression editing and resolved text easy to distinguish. Any proposed
  adjacent result or explicit source/result toggle must be reconciled with the
  existing focus-dependent expression contract before implementation.

Wider designer tabs, collapsible sections, navigator sizing and quieter file
settings remain secondary ideas. They should support the procedure workflow,
not delay it. Resolved previews stay in the existing dockable preview window.

## Design constraints and references

- Preserve the shared VS-owned text buffer, source-preserving edits and coordinated
  Undo/Redo from [milestone two](MILESTONE-2.md). A layout-only interaction must not
  edit XML or save files. Do not silently change commit boundaries.
- Keep the existing WPF/MVVM separation and per-view semantic adapter. Compiler
  analysis remains authoritative for effective values, expressions, inheritance,
  ordering, exclusions and diagnostics. No new compiler in the UI or duplicate
  project-analysis subscription.
- Preserve current raw-on-focus/resolved-on-blur behavior, completion, explicit
  empty parameter values and local XML presence until any change is separately
  specified and documented. UI simplification must not merge distinct states.
- Preserve stale-result invalidation, saved-only RESX/lookup capture, linked
  project contexts, and drafts during shell/preview-culture interactions.
- Use themed native WPF controls and follow Microsoft's
  [Visual Studio UX guidelines](https://learn.microsoft.com/en-us/visualstudio/extensibility/ux-guidelines/visual-studio-user-experience-guidelines).
  Review tab order, access keys, focus visibility and automation names with layout.
- Retain the existing Roslyn-inspired open-text/derived-model separation documented
  in [milestone two's architecture references](MILESTONE-2.md#shared-file-handling-and-its-references).
  This is a UI proposal, not a new compiler architecture or Roslyn dependency.
  Arcade build/package organization and supported frameworks remain unchanged.
- Keep notifying collections, stable draft controls, weak document ownership and
  view disposal. Layout improvements must not reintroduce view retention or reset
  focus/caret on analysis refresh.

## Numbered iterations

Iteration 1 records the agreed direction and a reviewable proposal. Iterations
2-6 are a proposed sequence; their exact deliverables depend on the workflow and
layout decisions below. Keep the compiler, SDK and extension usable at each
boundary; include tests/docs with the behavior and commit each successful iteration.

| Iteration | Deliverable and dependencies | Validation and completion criteria |
| --- | --- | --- |
| 1. UX review and milestone direction | Record confirmed scope, source findings, layout alternatives, constraints and open questions. No dependency. | Review documentation diff, paths/links and consistency. Distinguish observed source from inferred usability issues and concepts from implementation. |
| 2. Procedure layout | Implement the chosen inline or outline structure and section navigation after defining the draft/focus contract. Depends on iteration 1 and the layout decision. | WPF/model and focused live checks on both hosts. Preserve XML, drafts and source selection during navigation; display local/inherited/excluded states correctly, including multiple Clear entries. |
| 3. Procedure editing actions | Improve add/type/reorder/remove with immediate focus, stable selection and suitable keyboard commands; specify insertion/duplication if selected. Depends on iteration 2. | Regressions for source order, Clear boundaries, unsupported XML, completion, Enter/Escape/Tab, Undo/Redo and refresh while typing; live keyboard workflows on both hosts. No draft loss or unexpected edits. |
| 4. Procedure inheritance and expressions | Simplify provenance and local-copy presentation; clarify raw/resolved values without weakening source semantics. Depends on iteration 3 and any explicit expression-display decision. | Preserve inheritance/exclusions, expression-preserving section copies and stale-input rejection. Test drafts, copy/undo, linked contexts and culture changes. Inherited content stays read-only unless the user invokes a defined operation. |
| 5. Supporting editor UX | Prioritize only surrounding changes needed by the procedure workflow: quick access, space in Split mode, validation, parameters and preview navigation. Broader scalar/form redesign is optional follow-up. Depends on iterations 2-4. | Preserve absent/empty/local/inherited scalar distinctions, current diagnostics and saved-input validation. Test compact panes, keyboard access, empty/error states and context changes. |
| 6. Integrated UX acceptance | Validate the agreed common workflows, responsive behavior and lifecycle after the redesign. Depends on iterations 2-5. | VS unit, VSIX/template and functional IDE checks on VS2022/2026; targeted layout/lifetime measurements where changed. Record manual keyboard, theme/high-contrast, screen-reader and DPI evidence separately from automated checks. Carry unrun gates explicitly. |

## Decisions still needed

- Confirmed priority: procedure editing. Which parts matter most within it:
  inserting/reordering entries, long text, inheritance, or expression editing?
- Prefer Inline sequence, Outline and editor, or another interaction structure?
- Should milestone seven change any current Enter/Escape/focus or raw/resolved
  behavior, or first improve its presentation and discoverability?
- Which pane sizes and typical alarm/procedure lengths represent daily use?

Acceptance will be expressed as task scenarios after these decisions: select an
alarm, edit a local field, override then restore an inherited field, edit/reorder
a procedure, insert an expression, inspect its resolved preview and undo the edit.
Do not claim usability improvement solely from a new layout or passing unit tests.

## Actual iteration record

### Iteration 1: source review and direction (2026-10-10)

Recorded the owner's UX priority and subsequent procedure-editing priority, and
linked this plan from the repository guide and backlog. Reviewed
`EditFileControl.xaml`, its focus handlers,
`ParameterListControl.xaml`, `SplitEditorControl`, `EditorLayoutViewModel`, the
extension project configuration and existing expression-focus/live keyboard tests.
No production implementation, file-format changes or build configuration changes.

Validation: documentation diff/whitespace, relative paths and consistency reviewed.
Tests were not run for this documentation-only iteration. Existing WPF/live tests
were read, not executed; browser concept validation is not IDE validation.
The isolated browser concept check exercised add, local copy, selected-entry text,
reordering and narrow/wide layouts without JavaScript errors. The restricted
browser launch failed; the isolated headless rerun outside the sandbox succeeded.
The existing owner edit to `example/North/Alarms1.almx` is preserved and excluded.

Remaining: resolve the workflow/layout questions and refine iteration 2. The
implementation iterations and milestone acceptance have not been completed.
