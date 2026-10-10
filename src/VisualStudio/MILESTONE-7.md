# Milestone seven: ALMX editor user experience

Status: implementation authorized on 2026-10-10; the owner accepted the latest
full-editor and Preview View designs and requested autonomous implementation,
validation and a commit after each successful iteration. The owner selected
improving the existing file editor's UI/UX as this milestone's purpose, then
identified procedure editing as the worst experience and requested improvement
suggestions. The earlier custom-procedure-group suggestion was not selected.

Execution update (2026-10-10): the owner explicitly lifted the earlier commit
deferral and authorized implementation through the remaining iterations. Preserve
unrelated changes, commit each successful iteration, and record evidence here.
Monitor account usage at focused-task/iteration boundaries. At 12% remaining,
finish the current focused task safely, record the checkpoint, pause the goal and
wait for feedback. The initial reading was 66% remaining. Earlier discussion
records below describe historical deferral, superseded by this authorization.

## Intended outcome and confirmed scope

Make everyday ALMX authoring easier: find an alarm, understand its values, edit
fields and procedures, and see the result without excessive scrolling or confusing
focus changes. Scope covers the existing Visual Studio designer and its interaction
with the XML editor, preview, references, parameters and diagnostics.

Procedure editing remains the first priority; the inline procedure preview is
accepted as a design direction. Property fields, reference selection, dark-theme
legibility, file/alarm scope separation, IntelliSense suggestion formatting and
the preview table's procedure rendering/column customization are also confirmed
scope. Interaction
details explicitly marked as proposals below are still under discussion. Interpret the
owner's "alms file editor" as the existing `.almx` editor, not a new file format.
Contextual tooltips are confirmed scope wherever they help explain an action,
state or unfamiliar concept across the editor and preview.

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

- **Inline sequence (accepted direction):** show entries in their effective
  order, label inherited entries as read-only and edit local entries directly.
  Expand the selected entry's editor/actions; keep other entries readable and compact.
  Give Instructions and Reset their own group headers, as in the accepted preview.
  Keep type and reorder/remove actions
  compact and move detailed source explanations into a secondary disclosure.
  Cleared entries stay inline with the visibility control specified below.
- **Outline and editor (alternative not selected):** show a compact ordered entry list beside a larger editor
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

Property editing and separate file/alarm areas are now confirmed additions,
specified below. Wider designer tabs and navigator sizing remain secondary ideas.
Resolved previews stay in the existing dockable preview window.

## Adorner and cleared-entry discussion (2026-10-10)

Owner direction:

- Keep the existing inheritance adorner. Inheritance and exclusion are separate
  states: an inherited entry retains its adorner even when cleared.
- Keep cleared entries visible in the procedure group with muted styling.
- Confirmed in the follow-up: locally authored cleared entries remain editable
  in place. Clearing affects execution, not source editability. Inherited cleared
  entries remain read-only and retain their inheritance adorner. Do not disable
  a local cleared row or its editing controls merely because it is excluded.
- Add a hide/show symbol to each group to control cleared-entry visibility
  independently for Instructions and Reset. This supersedes the earlier proposal
  to put cleared entries only in a separate exclusion disclosure.

Suggested details for discussion, not finalized behavior:

- Initially show cleared entries. Use a group-header eye toggle with a contextual
  label such as "Hide cleared entries (3)" / "Show cleared entries (3)" and a
  keyboard/automation-accessible pressed state. Keep the hidden count visible.
- Use themed muted styling plus an explicit "Cleared" state, with exclusion
  provenance available. Preserve text contrast and the inheritance adorner;
  neither state should be represented by color alone.
- Preserve selection/copy and source navigation on cleared rows. Show enabled
  editing controls on local cleared rows; keep focused text readable while the
  row retains its Cleared indicator. Muted styling must not imply that editing
  reactivates an entry, and editing must not silently move/remove a Clear.
- Preserve compiler-owned order and exclusion provenance, including multiple Clear
  directives and earlier local entries. Do not derive a merged order from text or
  assume exclusions are always an inherited prefix. Review the analysis contract
  before choosing the combined presentation algorithm.
- Keep Clear directives visible as boundaries/actions when cleared rows are
  hidden. A visibility toggle only changes presentation: it must not change XML,
  execution, compilation, Undo history or commit a pending draft.
- Hiding a focused/selected cleared row should move focus to its group toggle
  without changing another entry or committing/cancelling its draft. Preserve the
  draft for when that row is shown again. Preference persistence across alarms/reopens is
  still open; no persistent setting is finalized.

The initial interactive conversation mockups did not include cleared rows or
faithfully reproduce the WPF adorner. The subsequent raster preview shows the
adorner, inherited+cleared and editable local+cleared rows, and independent group
visibility toggles; the owner accepted that direction. It remains a design
preview, not evidence that the actual WPF implementation satisfies the contract.

Adorner placement follow-up: after considering the supplied outlined TextBlock
reference and a dedicated indicator column, the owner refined the column design:
use a narrow inheritance-indicator column between labels and editors, omit column
headers, and right-align the inheritance icon within its column, next to the editor.
Reserve its space on local rows too so editors stay aligned; show the chain only
for inherited values and retain origin information and the editor's inheritance
context menu. This refines the two-column form with an indicator gutter while
preserving labels left and editors right. The supplied image does not establish
a requirement for resize handles or a red selection border.

## Property fields, references and editing scopes (2026-10-10)

Confirmed owner decisions:

- Use a two-column property form: labels in the left column, editors in the
  right. The owner explicitly selected this over two complete fields side by side.
  The accepted refinement adds a narrow inheritance-indicator column between them,
  with a right-aligned icon and no column headers.
- Remove scalar/identity XML-presence checkboxes. Users should not enable fields
  before editing them or separately enable their serialization. A committed edit
  creates or updates that field's local XML value automatically.
- Move "Use inherited value" from its repeated button into the field's context
  menu. Keep the inheritance adorner; distinguish this from removing the
  meaningful Abstract template checkbox.
- Fix the unreadable Abstract template checkbox/text in dark mode using VS theme
  resources. Validate checked/unchecked, focused and disabled states in both light
  and dark themes; do not hard-code a light foreground for every theme.
- Replace separate reference typing/selection with one editable, searchable
  combobox so users can find references by typing in projects with 1,000+ alarms.
- Clearly separate file-wide editing from editing the selected alarm. The supplied
  clip demonstrates a horizontal draggable divider and the ability to expand or
  collapse either area. Apply that interaction pattern within the designer.
- Accepted follow-up: move the analysis project context out from beneath
  "Filter alarms" into the labelled file-wide area. Use a static context label
  for one project and a dropdown only when multiple project contexts are available.
  Keep Filter alarms with alarm navigation in the alarm-editing area.

Proposed implementation details consistent with those decisions:

- File-wide pane above: clearly labelled "File settings", holding file parameters
  and document/project context. Alarm pane below: clearly labelled selected-alarm
  context, with alarm filtering/navigation, scalar fields, reference, abstract
  state, alarm parameters and procedure groups. Keep Add/Delete alarm actions in
  the alarm area. Give each area its own scrolling and visible collapsed header;
  keep the splitter usable by keyboard as well as mouse. Do not confuse this
  divider with the existing Designer/XML split.
- Treat the new form as direct editing: starting a real edit to an inherited
  field starts a local draft from its raw expression, preserving provenance until
  that draft changes it. Focusing, tabbing through fields or resizing must not
  create local XML elements. Context-menu "Use inherited value" removes only the
  local element through the existing undoable edit path. Keep it accessible with
  the keyboard context-menu key / Shift+F10 as well as right-click.
- Preserve local presence and draft state internally; removing the checkboxes is
  not permission to materialize every field, normalize untouched XML, flatten
  expressions or change scalar inheritance rules. Retain the existing distinction
  between empty scalar elements and explicit-empty parameter assignments.
- The searchable reference control should filter by identifier and display name,
  support keyboard selection and show the defining file for disambiguation. Merge
  the current candidate picker with the ReferenceName editor; avoid two competing
  reference values. Retain literal reference entry and ordinary diagnostics for
  unresolved values. Exact-match selection, commit/cancel behavior, clearing the
  reference and no-results presentation still need a precise interaction contract.
- Keep search responsive for at least 1,000 candidates with bounded visible rows
  and virtualization; do not eagerly construct every candidate's WPF controls.
  Preserve current candidate checks for self/cyclic/ambiguous references and
  revalidate current analysis before applying a selected candidate.

The combobox currently immediately below "Filter alarms" is the **analysis
project context** selector (`Projects` / `SelectedProjectPath`), not a second alarm
picker. A linked ALMX file can belong to several projects with different parameters,
resources, lookups, references, culture/configuration and diagnostics. The selected
context controls analysis and preview; choosing it does not change file membership
or select the solution's build startup project. Accepted placement: a labelled
"Project context" control in the file-wide pane, with a compact static label for
one context and a dropdown when multiple contexts are available. Full project
paths can remain available in a tooltip.

## IntelliSense suggestion presentation (2026-10-10)

Confirmed owner direction: follow the supplied completion-popup example with
the format `par1 (additional information)`. Show the parameter/expression name
first, followed by supporting information in parentheses on the same row. For
example: `par1 (value pending; declaration: Project; assignment: Project)`.
Keep the existing value availability and provenance information; this decision
changes its presentation, not the compiler's meaning or completion behavior.

Apply the name/details format consistently to expression suggestions in property
and procedure editors. Supporting details are display-only: accepting the example
still inserts `$(par1)`. Preserve filtering, keyboard selection, insertion, draft
handling and stale-input validation. Use theme-aware styling with readable details
in selected and unselected rows, including dark mode. Validate the displayed and
accessible labels alongside existing insertion and metadata-preservation tests.

## Preview table and personal column layout (2026-10-10)

Owner direction: use the supplied alarm-table screenshot as the visual reference,
especially its multiline Test method cell. Allow users to hide/show and rearrange
columns. The owner also asked whether these choices can be saved in a VS `.user`
file; the supported project-specific destination is `<project>.almproj.user`.

The table should have visible column headers and cell boundaries, top-aligned
wrapped values and rows that grow to fit their contents. The earlier decision to
omit property-form column headers applies only to the editor form. Retain VS theme
colors and readable selection in light, dark and high-contrast modes.

Render a combined **Test method** preview column from the existing Instructions
and Reset sections: show `Instructions:` followed by the surviving entries, then
a separated `Reset alarm:` section. Render steps as bullet lines, retain explicit
Hint/Warning/Note labels, and preserve text, multiline content and compiler order.
This is a readable procedure rendering rather than a count or a one-line summary.
Use evaluated values and compiler-owned effective entries, including inherited
content; cleared/excluded entries and Clear directives do not appear as executable
steps in this resolved preview. The editor's separate cleared-row visibility
controls retain their agreed behavior.

The screenshot establishes presentation, not new source properties: Test method
combines existing procedures. Additional domain-specific headings such as Device
TAG or Machine Stop Type would need explicit mappings/scope before implementation;
do not introduce new built-in fields from the reference image alone.

Recommended interaction and persistence details:

- Provide a Columns chooser from the table toolbar/header menu with show/hide
  choices, Move left/right commands for keyboard use, and Reset layout. Support
  dragging headers to rearrange and dragging boundaries to resize. Keep the
  chooser reachable even when all columns are hidden.
- Save column visibility, display order and widths per project and user, shared
  across that project's ALMX previews and independent of Debug/Release. Restore
  the layout when reopening the project or switching preview project context.
  Use stable column identifiers so caption changes do not invalidate preferences.
- Use CPS-managed `UserFile` properties and preserve unrelated user settings.
  Existing evidence: `General.xaml` already stores ShowAllFiles with
  `Persistence="UserFile"`; `Alarmlist.DesignTime.targets` imports
  `$(MSBuildProjectFullPath).user`; `.gitignore` excludes `*.user`.
  Microsoft's [DataSource.Persistence documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.framework.xamltypes.datasource.persistence?view=msbuild-17)
  confirms this persistence location. Exact property/schema names remain to be
  chosen during implementation. No user settings file has been created here.
- Keep the layout independent of ALMX source and compiler output. Validate saved
  values, tolerate unknown/new column identifiers and reset invalid layouts to
  usable defaults. Standalone documents without a project can use session defaults.
  Report a failed settings save while retaining the usable in-memory layout.
- Preserve the existing filtering, sorting, source navigation, stale-input checks,
  preview lifetime and read-only behavior. Keep row virtualization enabled and
  measure long procedure cells with 1,000+ alarms; do not build an editor control
  tree per procedure entry. Proposed copy behavior: visible columns in display
  order, with multiline procedure text correctly quoted for spreadsheet paste.

Implementation validation must cover effective procedure rendering (including
multiple Clears, inheritance and unavailable values), multiline wrapping and
scrolling, hide/reorder/resize/reset, layout restoration and project isolation,
unrelated `.user` property preservation, source/draft stability, copy behavior,
keyboard access, theme contrast and large-table responsiveness on both VS hosts.

## Contextual tooltips and help (2026-10-10)

Owner direction: add tooltips where they help users understand or clarify the UI.
Apply this throughout the affected editor and preview controls as part of each
implementation iteration. Keep tooltips concise, contextual and useful beyond
repeating a visible label. Essential instructions, errors and state indicators
remain visible; tooltips supplement them.

Initial coverage and suggested wording:

| Control or state | Tooltip purpose/example |
| --- | --- |
| Inheritance indicator | Identify the defining alarm/file from current compiler provenance, e.g. "Inherited from Templates.Pump in Templates.almx." Attach help to the field/indicator container while keeping the adorner decorative. |
| Cleared entry | Explain exclusion and editability accurately: "Excluded by Clear. This local entry is still editable." Inherited cleared rows explain that their source is read-only here. |
| Group eye toggle | State the next action and count: "Hide 2 cleared entries. This only changes what is shown." Update when visibility changes. |
| Use inherited value | Explain the effect: "Remove this local override and use the inherited value." Reflect actual availability and never promise a value that does not exist. |
| Abstract template | "Available for other alarms to reference; excluded from compiled output." |
| Reference combobox | Explain search by identifier/name and the displayed defining file; document only the finalized keyboard behavior. |
| Project context | Explain which project's parameters, references and resources drive analysis/preview, and expose the full path when the label is shortened. |
| Procedure/pane icon actions | Name the action, its scope and any confirmed shortcut; explain disabled actions when the reason is otherwise unclear. |
| Preview Test method column | Explain that it shows resolved Instructions and Reset entries in execution order. |
| Preview column controls | Explain show/hide, move/resize and reset actions, including the scope of saved personal layout choices. |

Use VS-themed tooltips with readable wrapping and contrast. Keep help current
when provenance, selection or state changes. Expose equivalent concise help to
keyboard and assistive-technology users through the owning controls' accessible
names/help text; understanding a control must not require pointer hover. Opening
or dismissing help must not steal editor focus, commit a draft or trigger analysis
or source edits. Avoid tooltips that obscure active completion or ordinary typing.

Validate representative tooltip content and state changes alongside the controls
in iterations 2-6. Include keyboard/screen-reader discoverability, disabled-control
explanations, dark/light/high-contrast rendering and focus/draft preservation in
iteration 7's acceptance checks.

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
2-7 are authorized. Keep the compiler, SDK and extension usable at each boundary;
include tests/docs with the behavior and commit each successful iteration.

| Iteration | Deliverable and dependencies | Validation and completion criteria |
| --- | --- | --- |
| 1. UX review and milestone direction | Record confirmed scope, source findings, layout alternatives, constraints and open questions. No dependency. | Review documentation diff, paths/links and consistency. Distinguish observed source from inferred usability issues and concepts from implementation. |
| 2. Procedure layout | Implement the accepted inline structure with per-group headers and cleared-entry toggles after defining the draft/focus contract. Depends on iteration 1. | WPF/model and focused live checks on both hosts. Preserve XML, drafts and source selection during navigation; display local/inherited/excluded states correctly, including multiple Clear entries and editable local cleared rows. |
| 3. Procedure editing actions | Improve add/type/reorder/remove with immediate focus, stable selection and suitable keyboard commands; specify insertion/duplication if selected. Depends on iteration 2. | Regressions for source order, Clear boundaries, unsupported XML, completion, Enter/Escape/Tab, Undo/Redo and refresh while typing; live keyboard workflows on both hosts. No draft loss or unexpected edits. |
| 4. Procedure inheritance and expressions | Simplify provenance and local-copy presentation; clarify raw/resolved values without weakening source semantics. Present IntelliSense suggestions as name followed by parenthesized details across property/procedure editors. Depends on iteration 3 and any explicit expression-display decision. | Preserve inheritance/exclusions, expression-preserving section copies and stale-input rejection. Test drafts, copy/undo, linked contexts, culture changes, completion labels/details and unchanged insertion. Check selected/unselected completion contrast in light/dark themes. Inherited procedure content stays read-only unless the user invokes a defined operation. |
| 5. Property form, reference search and scope separation | Deliver labels-left/editors-right fields without presence checkboxes, automatic local edits, context-menu inheritance reset, readable Abstract theme states, one searchable reference control and distinct resizable/collapsible file/alarm areas. Depends on iterations 2-4 and the remaining commit/search contract. | Test unchanged-focus/no XML writes, local edit creation, raw expressions, inherited reset/Undo, invalid/literal references, 1,000+ candidates, linked contexts, light/dark themes, independent pane scrolling and draft-safe resize/collapse. Live keyboard and context-menu checks on both hosts. |
| 6. Preview table and column preferences | Deliver the reference-style wrapped table, combined Test method rendering, column chooser/reordering and project-user layout persistence described above. Depends on iterations 4-5. | Verify effective procedure text/order and exclusions, multiline rows, keyboard column controls, layout round-trip/project isolation, unrelated user properties, clipboard layout, drafts/stale-input guards and 1,000+ row virtualization. Run focused VS unit, SDK rule/package and live checks on both hosts. |
| 7. Integrated UX acceptance | Validate the agreed common workflows, responsive behavior and lifecycle after the redesign. Depends on iterations 2-6. | VS unit, VSIX/template and functional IDE checks on VS2022/2026; targeted layout/lifetime measurements where changed. Record manual keyboard, theme/high-contrast, screen-reader and DPI evidence separately from automated checks. Carry unrun gates explicitly. |

## Implementation decisions under delegated discretion

- Preserve current Enter-to-commit, Shift+Enter newline, Escape cancellation and
  raw-on-focus/resolved-on-blur behavior; completion retains keyboard precedence.
- Start with cleared entries visible. Remember visibility independently per group
  for the lifetime of the editor view, including alarm switches. Hiding rows is
  presentation-only and preserves drafts. Keep Clear boundaries visible.
- Keep the existing source-preserving add/type/move/delete operations, improve
  their focus and compact presentation, and leave duplicate/drag-and-drop commands
  outside this milestone unless implementation evidence makes them necessary.
- Search references with case-insensitive substring matching of identifier/name;
  show defining file and use bounded virtualized choices. Selecting a candidate
  uses its literal identifier through the existing commit path. Preserve ordinary
  literal entry, focus commits and Escape cancellation, with popup dismissal first.
- Persist the file/alarm divider and collapsed state in the existing per-user VS
  layout store. Persist preview columns per project in its `.almproj.user` file.
  Copy visible columns in display order, retaining quoted multiline cells.
- Validate ordinary and narrow panes with existing 440/650/900-pixel test sizes,
  light/dark/high-contrast palettes and 100/150/200% scale; keep actual IDE checks
  distinct from simulated WPF tests and manual screen-reader acceptance.

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

### Uncommitted discussion follow-up (2026-10-10)

Recorded commit deferral, retaining the inheritance adorner, and inline muted
cleared entries with a per-group hide/show control. Replaced the superseded
separate-exclusion-only proposal. Reviewed procedure presentation, exclusion state
and the existing multiple-Clear/adorner tests; no production files were changed.
Validation: documentation consistency and whitespace reviewed. Tests were not run
for these discussion-only edits. These notes remain uncommitted as requested.

Follow-up decision: local cleared entries stay editable. Revised the earlier
read-only proposal, preserving read-only inherited rows and independent adorner /
Cleared states. The owner requested a preview image to review this presentation.
The image is a design concept, not a screenshot of implemented WPF behavior.

Further discussion: the owner accepted the procedure preview, requested the
property/reference/theme/scope changes recorded above, and clarified that two
columns means labels left and editors right. Reviewed five frames across the
21.8-second supplied clip: it demonstrates two vertically arranged areas with
a draggable divider, each capable of using nearly the full height. It is a UI
interaction reference, not a recording of this repository's ALMX designer.
Checked the current project-context bindings, scalar edit/inheritance-reset code
and nearby source-preservation tests. Documentation-only updates remain uncommitted;
no production code or tests were changed and no IDE tests were run.

Preview follow-up: the owner accepted the proposed project-context placement and
requested a full-editor dark-mode preview with separate file/alarm areas, the
labels-left/editors-right form, and searchable reference combobox. Commit deferral
remains explicit. The preview is a design image; no live IDE validation is implied.

Adorner follow-up: recorded the owner's two placement alternatives, then the
accepted column refinement: no column headers and a right-aligned inheritance icon
between labels and editors. Updated the design decisions and removed the resolved
placement question.
Reviewed documentation consistency and whitespace; no tests were run for this
documentation-only update. Production code remains unchanged and commits deferred.

IntelliSense follow-up: recorded the supplied screenshot and requested
`par1 (additional information)` format. Reviewed the existing completion label,
separate insertion text, popup bindings and metadata-preservation tests. Reviewed
documentation consistency and whitespace; no tests were run for this design-only
update. No production changes or commits.

Preview-table follow-up: reviewed the supplied table image, current preview XAML,
row/copy models, effective procedure data and nearby table/accessibility tests.
The current table has six scalar/source columns, fixed 28-pixel rows and column
reordering disabled. Confirmed existing project-user persistence support from the
SDK rules/import and Microsoft documentation. Recorded the requested presentation
and recommended column controls/persistence. Added planned iteration 6 for this
scope and moved the still-unimplemented integrated acceptance to iteration 7;
completed iteration 1 is unchanged. Documentation consistency, links and whitespace
reviewed. No production code, settings file or tests changed; tests were not run.
Commit deferral remains in effect.

Tooltip follow-up: added the owner's contextual-help guideline, initial coverage
and suggested wording, and accessibility/focus constraints to the design plan.
Recorded the durable guideline in AGENTS.md. Reviewed documentation consistency,
relative links and whitespace. No production changes or commits; tests were not
run for these documentation-only edits.

Preview acceptance: the owner accepted the updated dark-mode editor and Preview
View images, then requested unattended Codex setup advice before implementation
starts. This confirms the visual direction, not every still-open interaction
detail. Implementation has not started, and the prior commit deferral remains.

### Iteration 1 follow-up: finalized execution plan (2026-10-10)

The owner authorized implementation and per-iteration commits, delegated minor
design decisions and specified the 12%-remaining usage checkpoint. Updated the
plan and retained all accepted preview, tooltip and persistence decisions.
Validation: documentation consistency, relative links and whitespace review.
No production tests required for this planning follow-up. The unrelated
`example/North/Alarms1.almx` edit remains excluded from commits.
