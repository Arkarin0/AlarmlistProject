# Milestone seven: ALMX editor user experience

Status: initial implementation and scoped automated acceptance completed on
2026-10-12 on `dev/milestone7`. The [authorized follow-up](#authorized-follow-up-2026-10-13)
is in progress: iterations 8–9 are complete; 10–14 remain. The current pause
boundary is 15% account allowance remaining, superseding the original run's 12%.
Manual accessibility/display and release gates remain visible in the closeout
below. Implementation was authorized on 2026-10-10; the owner accepted the latest
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

Checkpoint (2026-10-11): implementation iterations through 6b were validated and
committed; work paused at the requested usage boundary before iteration 7.
Resumed on 2026-10-12 with renewed instruction and 99% account allowance remaining.
Iteration 7's implementation/automated validation is complete; this does not close
the separately carried interactive/release acceptance gates.

## Intended outcome and confirmed scope

Make everyday ALMX authoring easier: find an alarm, understand its values, edit
fields and procedures, and see the result without excessive scrolling or confusing
focus changes. Scope covers the existing Visual Studio designer and its interaction
with the XML editor, preview, references, parameters and diagnostics.

Procedure editing remains the first priority; the inline procedure preview is
accepted as a design direction. Property fields, reference selection, dark-theme
legibility, file/alarm scope separation, IntelliSense suggestion formatting and
the preview table's procedure rendering/column customization are also confirmed
scope. The proposals below retain the design discussion; the delegated decisions
and actual iteration records state the final choices. Interpret the
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
  confirms this persistence location. Iteration 6b chose
  `AlarmlistPreviewColumnLayout` in the existing ConfigurationGeneral rule and
  verified CPS-managed user-file persistence in isolated IDE fixtures.
- Keep the layout independent of ALMX source and compiler output. Validate saved
  values, tolerate unknown/new column identifiers and reset invalid layouts to
  usable defaults. Standalone documents without a project can use session defaults.
  Report a failed settings save while retaining the usable in-memory layout.
- Preserve the existing filtering, sorting, source navigation, stale-input checks,
  preview lifetime and read-only behavior. Keep row virtualization enabled and
  measure long procedure cells with 1,000+ alarms; do not build an editor control
  tree per procedure entry. Accepted copy behavior: visible columns in display
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
  This UI redesign adds no compiler architecture or Roslyn dependency.
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
| 5a. Direct property form | Deliver labels-left/editors-right fields with the adorner gutter, automatic local edits, context-menu inheritance reset and readable Abstract theme states. Depends on iteration 4. | Test unchanged-focus/no XML writes, absent/local/inherited editing, raw expressions, reset/Undo and light/dark states; live keyboard/context-menu checks on both hosts. |
| 5b. Searchable reference | Merge literal editing and candidate selection into one searchable reference combobox. Depends on 5a. | Test invalid/literal references, current-input validation, keyboard/popup behavior and 1,000+ candidates; live selection on both hosts. |
| 5c. File/alarm scopes | Separate resizable/collapsible file and alarm panes, move labelled project context, persist layout in the existing user settings store. Depends on 5b. | Test linked contexts, independent scrolling, draft-safe resize/collapse, keyboard divider and layout restoration; live checks on both hosts. |
| 6a. Preview report table | Deliver the reference-style wrapped table and combined Test method rendering. Depends on iterations 4-5. | Verify effective procedure text/order and exclusions, multiline rows, clipboard quoting, drafts/stale-input guards and 1,000+ row virtualization. Run VS unit and live checks on both hosts. |
| 6b. Preview column preferences | Add column chooser, keyboard/drag reordering, width/reset and project-user layout persistence. Depends on 6a. | Verify layout round-trip/project isolation, unrelated user properties, displayed-column clipboard order and stale/lifetime guards. Run VS unit, SDK rule/package and live checks on both hosts. |
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

### Iteration 2: inline procedure layout (2026-10-11)

Implemented one inline sequence per Instructions/Reset group with decorated,
read-only inherited rows followed by local source rows. Compiler-owned exclusion
history supplies order and provenance; cleared rows are muted and local rows stay
editable. Per-group eye controls retain view-local visibility across alarm changes,
keep Clear boundaries visible, and preserve text drafts and their controls while
hiding/showing. Kind/reference changes still invalidate structural analysis.
Origin, exclusion and command explanations are available as contextual help.

The first live dark-theme screenshot exposed low contrast in the new toggle;
its template now uses the existing editor theme brushes and visible focus border.
The new `Area=Procedures` live scenario types a cleared draft, hides/restores it,
commits with Enter and undoes through the shared buffer, verifying saved source
and build output remain unchanged. Fresh VS2026 profile checks initially failed
because a startup notification was mistaken for the main window and release notes
activated over the editor. The keyboard/procedure harness now locates the owned
designer tree and reactivates its frame during bounded startup readiness checks.

Validation evidence:

- Full VS unit suite: 352 passed, zero skipped. Focused procedure/WPF suite:
  26 passed, including a repeat after the theme correction.
- The test builds regenerated the net472 extension/VSIX and both SDK runtimes.
- VS2026 18.10, isolated `AlarmlistM7` profile: final `Area=Procedures|Area=Keyboard`
  run passed both cases, zero skipped; TRX directory
  `artifacts/log/Debug/VisualStudioUI/TestResults/693cdc89257d44b0bba68d5c5ed5b896/`.
- VS2022 17.14, `AlarmlistExp`: final two focused live cases passed, zero skipped;
  TRX directory `artifacts/log/Debug/VisualStudioUI/TestResults/cb17971bf0e7433d9292cc0ca8b64512/`.
- Reviewed the actual VS2026 dark-theme procedure screenshot. This does not replace
  integrated light/high-contrast, DPI, screen-reader or final manual acceptance.

No compiler/source-format changes. The unrelated sample edit remains untouched.
Remaining work: iterations 3-7; the current property/reference layout and global
button/combobox theme styling will be replaced in their planned iterations.

### Iteration 3: procedure editing actions (2026-10-11)

Add focuses the appended text editor (or the type selector for Clear); move follows
the entry and carries its selection; remove focuses the following/previous entry
or the group's Add button when empty. Requests are published only after successful
source edits and discarded on source/selection refresh, view unload or later user
input. Existing Enter, Shift+Enter, Escape, Tab, completion and shared Undo/Redo
commands remain unchanged. Compact remove buttons retain accessible names and
tooltips. Action buttons use the same theme/focus pattern as the parameter editor.

Decision: when moving an active entry across Clear makes it excluded while hidden
rows are enabled, retain that editor until focus leaves; do not silently change
the group's visibility preference or remove the editor while typing.

Validation: full VS unit suite passed 358 cases, zero skipped, after the native
text-insertion fix. New tests cover added/moved/deleted focus, selection, failed
source edits, cleared-row refresh while focused, reentrant focus input and native
text composition into an empty resolved entry. After the final delete-focus fix,
the focused procedure/keyboard unit selection passed all 29 cases, zero skipped.

The live typing check found an existing empty-resolved-field defect: the first
character triggered analysis invalidation and a reentrant Text refresh before WPF
finished insertion, moving that character to the end of subsequent typing. A new
WPF text-composition test reproduced it without Add/focus commands and passed after
blocking display refresh during the raw source update. Focus handling additionally
sets selection before exposing keyboard focus, avoiding post-input caret changes.

The expanded live scenario also exposed Shift+Enter being translated by VS into
VSStd2K RETURN and forwarded to the hidden XML editor, inserting a newline there
instead of in the private draft. Return now routes to the focused designer field,
with completion precedence, multiline insertion and commit semantics shared with
the WPF key path. XML focus still uses the native command route. A WPF regression
test verifies status queries do not edit, Shift+Enter stays private, Enter commits,
and read-only fields/source-pane focus do not alter another editor.

Deleting a focused row also lets WPF briefly focus the native XML pane. Focus
restoration now tolerates that removed control while observing subsequent physical
input across the shell; later user input cancels the deferred request. The temporary
input observation is always detached after the dispatcher yield.

Final live validation rebuilt/deployed the VSIX and passed `Area=Procedures|Area=Keyboard`
on both hosts, two cases per host, zero skipped: VS2022 TRX directory
`artifacts/log/Debug/VisualStudioUI/TestResults/ae837e52796b47df97e13d500f8a820a/`
and VS2026 `artifacts/log/Debug/VisualStudioUI/TestResults/620c017773e04ede92a7f96e1174751e/`.
The extended scenario verifies cleared drafts, Undo, Add/immediate typing,
Shift+Enter, multiline commit, move/remove source order and resulting focus, with
saved files/build output unchanged. Earlier failures above are resolved by these
final runs. Reviewed the diff and whitespace; updated the editor usage guide.
Remaining work: iterations 4-7. No genuine blocker at this boundary.

### Iteration 4: inheritance and expression presentation (2026-10-11)

Replaced repeated section-action rows with each group's keyboard-accessible
Inheritance menu. Opening it preserves private drafts; Make local copy retains
the existing current-analysis, draft and unsupported-source guards. Popups release
their target/data context on unload. Inherited rows stay read-only with their
chain adorners and a compact source action; tooltips explain origin, exclusion,
whole-section copying and raw/resolved keyboard behavior.

All expression suggestions now display `name (details)`, with semicolon-separated
value/declaration/assignment information. Multiline values are flattened only for
the one-line label; raw metadata and insertion tokens remain intact. Selected
suggestions use system highlight/text brushes even though keyboard focus remains
in the editor; unselected rows use VS theme resources. Full labels are exposed to
automation and tooltips. Reviewed generated light, dark and high-contrast palette
renders; these are simulated WPF palettes, not OS theme-switch evidence.

Validation: focused WPF/model selection passed 53 cases, full VS unit suite passed
362, and the final multiline-label change passed four focused checks (one newly
added case); all zero skipped. VSIX/SDK assets rebuilt. Live `Area=Procedures|Area=Properties`
passed all three cases per host, zero skipped: procedure actions/menu/copy/Undo,
parameter/completion/abstract/raw-expression workflows and linked project/configuration
switching. VS2022 TRX directory:
`artifacts/log/Debug/VisualStudioUI/TestResults/99b670931f564b7d8dc941b906674169/`;
VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/895f026cc6284e9ea7b68895acea3bbc/`.
Reviewed actual VS2022 dark-menu/completion screenshots as well as WPF palette
renders. OS light/high-contrast switching remains part of final acceptance.
Reviewed diff/whitespace; no compiler or source-format changes, no current blocker.

Planning refinement: split the still-unimplemented iteration 5 into 5a/5b/5c so
each independent behavior has its own validation and commit boundary. The agreed
scope, ordering, final gates and completed iteration identifiers are unchanged.

### Iteration 5a: direct property form (2026-10-11)

Implemented aligned labels/editors with a reserved, right-aligned adorner gutter
and no form headers or presence checkboxes. A dedicated edit value presents the
inherited raw expression without changing source presence. Actual typing starts a
local draft; focus alone never writes. Empty drafts remain stable until commit,
including clearing an already-empty local scalar that inherits (no extra XML edit).
The field context menu retains clipboard commands and offers inheritance reset and
source navigation. The Abstract template checkbox now uses VS/system theme brushes
for its text, check mark, background and focus border.

Removed the obsolete override-button focus callback/tests; direct-edit tests cover
the replacement's focus, selection, cancellation, raw expressions and Undo. The
existing guarded model copy/navigation commands and source edit rules are retained.
Validation: initial focused selection passed 14 cases and the full VS unit suite
passed 365, all zero skipped. Reviewed simulated light/dark/high-contrast form
renders at 900/650/440 widths and the actual VS2022 dark form/context menu; the
Abstract label/check border are readable. The full OS theme/DPI matrix remains
iteration 7's gate. VSIX/SDK assets rebuilt.

Live `Area=Procedures|Area=Properties|Area=Keyboard` passed all four cases per host,
zero skipped. The new workflow verifies inherited focus produces no write, typing
creates a local raw expression, Shift+F10 opens the field menu, reset removes the
element, Undo restores it, and the subsequent saved build evaluates correctly.
VS2022 TRX directory: `artifacts/log/Debug/VisualStudioUI/TestResults/a976a67c12764528ba90c1fcd598bc13/`;
VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/829744ef9fe643dfa9d142cbb76661ff/`.
Reviewed diff/whitespace and updated usage documentation. Remaining: 5b, 5c, 6, 7;
no current blocker. Account allowance at the last checkpoint: 34% remaining.

### Iteration 5b: searchable references (2026-10-11)

Replaced the separate candidate picker/button and ReferenceName editor with one
editable control exposing ComboBox/Value/ExpandCollapse automation. Its text stays
private while keyboard arrows highlight a candidate. Identifier/name filtering is
case-insensitive substring matching, including raw names on abstract templates;
rows show defining files and use bounded-height virtualization. Shared suggestion
styles keep selection readable without moving keyboard focus out of the editor.

Decisions: F4/Alt+Down opens choices; Enter applies the highlighted nonempty-query
match after saved-input validation. Escape closes choices first, preserving the
literal draft; Enter with no popup/no matches commits that literal. An empty query
has no automatic selection, so Enter clears the reference. Down explicitly selects
a candidate from an empty query. Late validation is rejected after input, Escape,
focus loss, unload, source or project changes. Existing unambiguous/self/cycle
candidate checks remain authoritative; typing an invalid literal remains allowed.

Validation: initial focused checks passed 10 cases, including 1,201 candidates with
fewer than 40 realized WPF rows, name/identifier search, unchanged text during arrow
navigation, selection/Undo, literals and stale-result rejection. Full VS unit suite
passed 373 cases; the final abstract-name and compact-label refinements passed eight
reference checks and four reference/completion presentation checks, all zero skipped.
Suggestion rows show filenames; tooltips and automation help retain full paths.

Live `Area=References|Area=Properties|Area=Keyboard` passed four cases on each host,
zero skipped, including 1,200 abstract targets, private queries, selection, Undo,
invalid literals and clearing. VS2022 TRX directory:
`artifacts/log/Debug/VisualStudioUI/TestResults/13c0042a182d4fe4ac8192a6c53a9dc2/`;
VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/f2a893c49bbc48f4aedbb356f8add0f6/`.
The final compact-label reference scenario passed again on both hosts: VS2022
`2afbde853efc46cf99394e5e6cdb7c8e`, VS2026 `c800c747a5d64f82bc0f8b0eaeb795c2`
under the same TestResults directory. Reviewed the actual dark-theme popup.
One VS2022 query took 564 ms including physical typing and UIA result observation;
this is functional timing evidence, not a latency benchmark. The tests rebuilt and
deployed SDK/VSIX assets. Reviewed diff/whitespace and updated both usage guides.
No compiler or format changes; no current blocker. Remaining: 5c, 6 and 7.

### Iteration 5c: file and alarm scopes (2026-10-11)

Added separate labelled File settings and Alarm settings areas with independent
scrolling, collapsible headers and a keyboard/mouse divider. File settings owns
the project context, file parameters and diagnostics; alarm filtering/actions stay
with alarm navigation. A single project is a static label; linked contexts retain
the guarded project selector with full-path accessibility information.

Decision: default file area proportion is 25%, bounded to 10-80% when resizing.
Both areas start expanded. Collapse and divider preferences use separate keys in
the existing VS user settings store, never ALMX or shared project content. Headers
remain available when both areas are collapsed. Focus moves to the matching header
before collapsing a focused field, keeping its private draft intact. Layout event
subscriptions are removed on unload.

Validation: full VS unit suite passed 378 cases, zero skipped. The new checks cover
independent scrolling and private drafts at 440/650/900 widths, keyboard resizing,
focus before collapse, both collapsed headers, settings round-trip/invalid values,
unload detachment and linked-context selection. The initial exact-double assertion
was corrected to a tolerance for text serialization; no functional failure remained.
All five focused scope/context cases passed again after adding simulated dark
palette renders; reviewed the 440px layout. This is not OS theme/DPI evidence.
Updated usage documentation, including removal of superseded presence-checkbox text.

Live `Area=Scopes|Area=Properties|Area=References|Area=Keyboard|Area=Procedures`
passed six cases on each host, zero skipped, including private typing, keyboard
divider/collapse, editor-reopen layout persistence, linked contexts and existing
procedure/reference workflows. VS2022 TRX directory:
`artifacts/log/Debug/VisualStudioUI/TestResults/2a344a2174784149bc43ed513164ea6e/`;
VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/2fb3601e4d9040ffb4c1ab3cc5f6b371/`.
The tests rebuilt/deployed the SDK/VSIX; reviewed the actual VS2022 dark layout.
Saved sample source/output remained unchanged. Final manual/theme/DPI gates remain
in iteration 7; no current blocker. The unrelated example edit is preserved.

Planning refinement: split the remaining iteration 6 into report rendering (6a)
and column preferences/persistence (6b), with separate validation/commit boundaries.
The approved scope and final acceptance gates remain unchanged.

### Iteration 6a: preview report table (2026-10-11)

Added wrapped, top-aligned report cells with both gridline directions, automatic
row heights and a combined Test method column. Instructions and Reset retain
compiler order/evaluated text; steps have bullets and Hint/Warning/Note keep their
labels. Only compiler-effective entries are included. Filtering, sorting and
quoted multiline clipboard output include Test method.

Decision: retain recycling row virtualization with pixel scrolling so tall rows
remain readable. Disable column virtualization for the seven fixed columns: row
height must include procedure content even when that column is horizontally out
of view. This bounds realized rows while avoiding height changes from horizontal
scrolling. Column preferences remain the separate 6b deliverable.

Validation: 17 focused report/table checks and the full 380-case VS unit suite
passed, zero skipped. The new compiler-backed checks verify inherited reset text,
local/inherited exclusions, evaluated expressions, all entry labels, multiline
clipboard quoting, wrapping/resize row heights and fewer than 30 realized rows
while scrolling through 1,200 alarms. Reviewed the simulated dark report render.

The expanded live `Area=PreviewTable` case passed on both hosts, zero skipped,
including automatic row height, localized values, quoted procedure copy, excluded
text absence, multi-selection/Ctrl+C, sorting/filtering, source navigation and draft
invalidation. SDK/VSIX assets rebuilt/deployed. VS2022 TRX directory:
`artifacts/log/Debug/VisualStudioUI/TestResults/d06aeb8567b340b6ac2bcdaaf127b6a4/`;
VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/33669173e7bd4e6da77c56f6380834bb/`.
Reviewed the actual VS2022 dark report screenshot and diff/whitespace. No current
blocker; remaining implementation is 6b followed by integrated acceptance 7.

### Iteration 6b: personal preview columns (2026-10-11)

Added an always-reachable Columns chooser with visibility, numeric width,
keyboard Move left/right and Reset layout. Headers support drag reorder/resize;
copy uses only visible columns in display order and cancels late copies after a
layout change. All columns may be hidden; the chooser remains available.

Persistence uses hidden `AlarmlistPreviewColumnLayout` in the existing CPS
ConfigurationGeneral rule with `Persistence="UserFile"` and no configuration
condition. The adapter uses the pinned CPS rule/property API and SaveUserFileAsync,
never direct project XML mutation. Each loaded project owns one shared setting;
views detach on context change/disposal. Coalesced sequential writes preserve the
latest value, late initial reads cannot replace edits, and read/write errors are
shown while retaining a usable session layout. The versioned format uses stable
column IDs, validates widths/order/visibility and ignores unknown future columns.

Initial checks: extension/SDK builds passed with zero warnings/errors; five new
column/settings tests and the full 386-case VS unit suite passed, zero skipped.
SDK package checks passed three cases per framework; the full SDK suite then
passed 53 cases on net472 and 53 on net8.0, zero skipped. Seven final focused
column/schema/lifetime cases passed, including handling an already-handled header
drag event and releasing disposed models from the shared project setting.

Live validation initially found a test assumption (UIA header collection order is
not display order), corrected by checking screen position. It then found a real
resize-persistence defect: WPF marks the header drag event handled before the grid
receives it. The listener now includes handled events, saves only visible column
widths and ignores unrelated scrollbar drags. The subsequent mouse-reorder failure
was traced to stale screen coordinates: saving the user preference triggered an
analysis-status layout transition, so the test clicked a data row instead of its
header. It now waits for fresh results and stable table geometry before dragging.
Temporary input tracing was removed. The readiness check uses fresh rows rather
than requiring a selection, which analysis invalidation legitimately clears.

Final live `Area=PreviewTable` passed on VS2022 and VS2026, one case per host,
zero skipped. It verifies native header resize/reorder, keyboard move/width/hide,
visible-column clipboard order, shared choices across documents, fresh CPS reads
after solution reopen, reset, preserved unrelated user properties, and unchanged
source/shared project files. SDK/VSIX assets rebuilt/deployed on both hosts.
VS2022 TRX directory:
`artifacts/log/Debug/VisualStudioUI/TestResults/388ef4a42ad0453887f07abca97e19c4/`;
VS2026: `artifacts/log/Debug/VisualStudioUI/TestResults/624728ef722648bd95b1389b5d549ba6/`.
Reviewed the actual VS2022 dark report with restored custom columns, the code diff,
documentation and whitespace. No current 6b blocker. Integrated acceptance and
the separately listed manual checks remain iteration 7 work.

### Usage checkpoint (2026-10-11)

The account reached 12% remaining during iteration 6b. Per the owner's instruction,
finished this focused column-preferences task and its validation/commit boundary,
then paused for feedback. The last reading while finishing was 7% remaining.
Do not start iteration 7 without renewed instruction. Its integrated solution build,
full downstream/packaged-extension and functional IDE suites, final performance
and lifetime checks, and remaining manual theme/DPI/accessibility gates remain
pending. Earlier scoped passes do not replace that acceptance work. The unrelated
`example/North/Alarms1.almx` edit remains preserved and excluded from commits.

### Iteration 7: integrated UX acceptance (2026-10-12)

Resumed at `bc33c11` on `dev/milestone7`. The worktree is now clean; the earlier
unrelated example edit is no longer an outstanding change, and this iteration
will not restore or otherwise alter it. The renewed goal authorizes the remaining
acceptance work; the 12% remaining-allowance stopping instruction still applies.

Run the full solution build and ordinary tests, then the opted-in functional IDE
suite on each installed host. Review coverage of the new controls at narrow and
ordinary widths, themed/scaled WPF rendering, keyboard help, draft preservation
and disposal. Run targeted Release designer and live close/lifetime measurements
separately from other builds/tests. Add focused regressions for any uncovered
behavior and fix failures before committing this iteration. Record actual native
theme/DPI/screen-reader evidence separately; simulated palettes and UIA assertions
do not establish physical monitor movement or spoken-output acceptance.

Initial evidence: `dotnet build AlarmlistProject.slnx --verbosity minimal` passed
with zero errors and two existing CA1416 warnings from the shared net8.0 test helper
(`Thread.SetApartmentState`). The built solution's ordinary test run passed 1,672
cases; 44 functional IDE and 11 performance opt-in cases were intentionally skipped.
This includes both frameworks for compiler/MSBuild/SDK/VSIX consumers. Build and
TRX evidence is under `artifacts/log/Debug/Milestone7/Iteration7/`.

The narrow high-contrast simulation exposed a collapsed preview filter: the count
and action buttons consumed its width. The toolbar now moves those actions below
the filter in narrow panes, retaining the same text/focus controls. Nine focused
layout/automation checks passed, then all 390 VS unit cases passed. New coverage
checks filter usability across resize, column-chooser bounds/help, draft stability
and palette/scale rendering. These bitmap scales are not actual monitor DPI tests.

Live scope inspection preserved the private draft while showing help but exposed
an unwrapped tooltip extending across the screen. Shared editor/preview help now
uses bounded wrapping and theme colors. Thirteen focused layout/help/lifetime
checks passed after that change, including focus/draft preservation and released
designer, preview and shared-setting subscriptions.
The full VS2022 run also exposed a lookup-test readiness race immediately after
returning from an XML lookup buffer. The field was visible in the later failure
capture. Lookup/list scenarios now wait for the reopened editable field before
typing; they retain real keyboard input and all saved-input/source assertions.
The next full VS2022 run passed all 22 functional cases with zero skips:
`artifacts/log/Debug/VisualStudioUI/TestResults/54d7e2d0d80548f58cfcbb24ed81f94c/`.
It includes native tooltip wrapping, narrow preview filter/focus, keyboard editing,
lookup/list saved-input behavior and column user-file round trips. Tooltip captures
now allow the native fade animation to finish before visual contrast review;
the functional focus/source assertions still run immediately.

The first full VS2026 run found another test readiness issue: the native language
dropdown was opened while the initial resource analysis was still pending and
retained its keyword-only list. The source confirms that the shell queries a
snapshot of choices on opening. The shared test helper now reopens the list
periodically within the existing bounded wait for the requested culture. It still
fails if that culture never becomes available; saved-catalogue and draft assertions
are unchanged. All callers are included in the subsequent host validation.
The full VS2026 run had 21 passes and that one failure (`21df3c048a7647a2b0d240b01cc1aeff`).
The follow-up passed seven affected cases, including lookup completion, but the
report test's width-save predicate threw when CPS temporarily held the user file.
It now treats the unavailable intermediate value as not ready within the same
bounded wait, retaining the expected saved width. That report case passed again
(`fa195702ea46498888594f56b11fa2d6`); the seven other passes are in
`034c546fac4548e092ce67bd162b2a4b`. These IDs are under
`artifacts/log/Debug/VisualStudioUI/TestResults/`. Reviewed the settled native
VS2026 dark tooltip: it wraps, remains readable and leaves the Code draft focused.
All eight affected workflows then passed on VS2022, zero skipped, with the final
helpers (`b4d7fa5b11df45608085b68e2dcabd42`). Across the full runs and focused
rechecks, all 22 distinct functional scenarios passed on each installed host:
VS2022 17.14.36930.0 / AlarmlistExp and VS2026 18.10.12224.181 / AlarmlistM7.
The initial failed runs remain recorded; no failed case is treated as a pass.

Added a separate Release `Area=Preview` benchmark for 1,200 long-procedure rows,
report resize/scroll and the searchable reference popup. It reuses the established
SDK-isolated fixture/harness; analysis and correctness checks stay outside measured
intervals. The Release performance project builds without warnings/errors.

Release isolated measurements passed both selected xUnit wrappers and all nine
BenchmarkDotNet operations (Short: two warmups/five measurement iterations).
Command: `eng/measure-visualstudio-analysis.ps1 -Job Short -Filter
'Category=Performance&Host=Isolated&Area=Preview|Category=Performance&Host=Isolated&FullyQualifiedName~PropertyDesigner'`.
Raw CSV/JSON, runtime/revision metadata and summaries:
`artifacts/log/Release/AnalysisPerformance/85f2e0f3f98d481f9270dd342536e48c/`.

| Workload/operation | Mean on this machine |
| --- | --- |
| 1,200 long-procedure rows: column resize/layout | 18.58 ms |
| 1,200 rows: scroll first to last/layout | 16.77 ms |
| More than 1,000 reference choices: popup/layout | 17.83 ms |
| 1,000 property alarms: publication/layout | 39.86 ms |
| Property designer: unchanged refresh/layout | 13.26 ms |
| Property designer: context change/layout | 42.69 ms |
| Property designer: private draft/layout | 17.77 ms |
| Property designer: 100 completion/command queries | 0.147 / 0.099 ms |

The report checks retained fewer than 40 realized rows, resolved long procedure
content and source preservation. These are isolated WPF layout measurements,
with compilation outside timing, not physical typing/paint or release thresholds.
They establish a workload baseline, not a before/after improvement claim.

Release live measurement wrappers passed on both IDEs, one case per host with
zero skips. Each used `Host=VisualStudio&Area=LiveAnalysis&VSVersion=<host>`, the
explicit installed host/profile, 1,000 additional alarms, two warmups and 20
measured samples in each mode. Source/output preservation and owned-IDE cleanup
passed; all post-close samples reported zero open documents. Wrapper reports:
`artifacts/log/Release/LiveVisualStudioTests/e6f4c835514d4f3194794c757e94de74/`
(2022), `ada0d4a6531c43d3beb21f738db21eaf/` (2026). Raw fixtures/CSVs are under
`artifacts/log/Release/VisualStudioSmoke/d93344d8ad2f41c1b22be5ff7eca5411/`
and `a6aafc889bb7427fa0bfc81d8b0441b3/`, respectively.

| Live host | XML buffer diagnostic median | Reopened designer diagnostic median | First → last measured private memory |
| --- | --- | --- | --- |
| VS2022 | 458.6 ms | 452.5 ms | 1,012.1 → 1,087.6 MiB |
| VS2026 | 451.3 ms | 455.8 ms | 1,026.6 → 1,166.5 MiB |

These diagnostic times include debounce, COM and polling, and exclude opening
the editor. Memory grew during these short whole-IDE samples; natural Gen2 counts
increased from 3 to 4 on each host. No collection was forced, and this evidence
does not establish a plateau or leak freedom. Automated WPF collectibility checks
passed separately. Long-session attribution remains an interactive/profiler gate.

Final verification after the product and harness fixes:

- `dotnet build AlarmlistProject.slnx --verbosity minimal` passed with zero
  warnings/errors; `final-solution-build.binlog` retains the final incremental
  build. The initial full build's two shared-test-helper warnings remain recorded
  above rather than being described as fixed.
- `dotnet test AlarmlistProject.slnx --no-build --no-restore --verbosity minimal
  --logger trx --results-directory artifacts/log/Debug/Milestone7/Iteration7/FinalTestResults`
  passed 1,675 cases, zero failures. All 13 result files are retained. The 56
  ordinary-run skips are the 44 opt-in functional IDE cases and 12 performance
  wrappers; required IDE and targeted measurement runs are recorded separately.
- This includes all 390 VS unit cases, both compiler/task/SDK frameworks, and 11
  packaged VSIX/template consumer cases per framework. The SDK consumer suites
  passed 53 cases on each framework.
- Aggregating the latest result for each distinct functional scenario across the
  full runs and focused rechecks gives 44 passes: 22 per installed host, with no
  unresolved failed case. The native keyboard/mouse assertions were retained.
- Reviewed actual dark IDE tooltip/report captures and representative light,
  dark and high-contrast WPF renders, including 100/150/200% bitmap scales. Render
  artifacts are in `artifacts/log/Debug/Milestone7/Iteration7/Rendering/`.
- Reviewed the implementation/test/documentation diff and whitespace. The work
  remains on `dev/milestone7`; no unrelated source or generated artifact is part
  of this iteration.

Iteration 7's scoped implementation and automated checks are successful. No
essential automated check remains blocked. The final changes keep narrow preview
filtering usable, wrap/theme help without changing drafts, and harden readiness
in the live tests. No compiler, XML-format or keyboard-commit contract changed.
The last account reading was 85% remaining, above the requested pause boundary.

### Carried interactive and release gates

Per iteration 7's criteria, the following checks remain distinct from automated
acceptance and were not performed in this run:

- Actual screen-reader speech, selection and live announcements. UIA names,
  patterns/help and keyboard checks passed; they do not verify spoken output.
- Native light/Windows high-contrast theme switching and physical 100/150/200%
  or mixed-monitor DPI movement. Actual IDE captures use dark mode; the other
  palettes/scales are WPF simulations. No user OS display settings were changed.
- Human workflow/typing-to-paint assessment and long-session memory attribution.
  The short live samples record memory growth and do not establish leak freedom.
- Older minimum-supported VS2022/resource-editor acceptance, prior milestone
  vendor XLIFF round trips, and SDK/VSIX publication gates remain governed by
  their existing plans. Nothing was published as part of milestone seven.

The updated [interactive workflow](ACCEPTANCE.md#milestone-seven-editor-and-report-workflow)
gives the remaining checks. These are explicit carried gates, not claimed passes
or additional implementation iterations awaiting owner review.

## Authorized follow-up (2026-10-13)

The owner authorized autonomous implementation on `dev/milestone7`, with a
focused commit and short summary after each successful iteration. Preserve
unrelated changes, existing editing/keyboard semantics and the accepted inline
procedure design. Check account allowance at iteration boundaries; at 15% or
less remaining, finish the current iteration and pause for feedback. The initial
reading is 80% remaining. No follow-up implementation was present at start.

### Confirmed decisions and defaults

- Localization completion searches any part of the key, case-insensitively,
  while inserting the exact compiler-supplied token. Prefix matches rank first;
  catalogue order breaks ties. Search only keys, not translated text or template
  argument names. Other expression namespaces retain their existing matching.
- Move parameter Row actions to row context menus, for both alarm and file
  parameters because they share one control. Preserve text Cut/Copy/Paste/Select
  all, keyboard context-menu access, command availability and draft/focus rules.
  Keep Add and Remove visible. Explain actual alarm A/D access keys in tooltips
  and accessible help; Ctrl+A remains Select all.
- Use custom bell/list and language artwork for the two item templates, and
  native VS symbols for menu commands. Use native icons for Properties,
  Resources, Translations and Lookups project folders, matching names without
  case sensitivity; ordinary folders retain ordinary icons. Preserve special
  linked/excluded/missing folder semantics and theme/DPI support.
- Investigate the reported ALM0001 delay in the VS Error List. Live duplicate
  diagnostics must work before Build; the Build-only source filter is a possible
  cause, not a confirmed diagnosis. Preserve separation from MSBuild errors.
- A personal Tools > Options > Alarmlist preference selects Designer (default)
  or XML navigation, including Alarmlist diagnostic links. Select the exact
  source alarm/field/entry using source positions, including duplicate identities.
  Use XML for non-ALMX or locations the designer cannot represent. Keep search
  navigation, source validation, private drafts and undo semantics intact.
- Extend the existing preview window with Follow active editor (existing default)
  or an explicit project. Project > Preview alarms opens and selects that project;
  project mode survives editor switches/closure. Reuse project analysis, all
  three preview tabs, language settings, virtualization and `.almproj.user`
  column preferences. Include shared unsaved ALMX buffers, never auto-apply
  private designer drafts. Project mode describes the analyzed buffer state and
  makes the exclusion of unapplied field edits clear. Unload invalidates pending
  actions and releases subscriptions. No new compiler, output or file format.

### Follow-up iterations and planned acceptance

| Iteration | Deliverable / dependencies | Required validation and completion criteria |
| --- | --- | --- |
| 8 | Localization key substring search; independent. | Matching/ranking/case/template-key unit cases, stale input and keyboard insertion/Undo regressions, build and native saved-localization completion on both installed VS hosts. |
| 9 | Parameter row menus and discoverable alarm access keys; independent. | WPF row targeting, text commands, keyboard menu, draft/focus/command tests; native parameter editing and access keys on both hosts. |
| 10 | Template/menu symbols and known project-folder icons; follows row menu work. | VSIX/template icon content checks on both frameworks, icon mapping/theme tests, native Add New Item/menu/folder creation/rename/reload on both hosts. |
| 11 | Live duplicate diagnostic investigation and regression; independent. | Reproduce duplicate creation before any build in designer and XML; assert live Error List appearance/removal, linked contexts and source filters on both hosts; fix discovered product faults and document actual cause/evidence. |
| 12 | Configurable Designer/XML navigation; follows diagnostic coverage. | Mapping duplicate identities and field/procedure targets, settings persistence and stale/draft guards; native same/cross-file, preview and live/build diagnostic navigation on both hosts, with XML fallback. |
| 13 | Independent whole-project preview in the existing window; follows navigation. | Context/selection/culture/layout lifetime unit cases, no-editor/multi-project/linked-file/update/unload native scenarios on both hosts, unchanged source/output and virtualized large tables. |
| 14 | Follow-up integration acceptance and documentation; follows 8–13. | Full solution build/tests, complete opted-in functional IDE suites on VS2022/2026, targeted performance/lifecycle checks where changed, final diff and requirement audit. Retain the earlier explicit manual/release gates separately. |

Implementation evidence is recorded below per iteration. A planned check is not
a passing result; an essential failure prevents calling that iteration successful.

### Iteration 8 — localization key search (complete)

Completion now matches any case-insensitive substring of a localization key.
Key-prefix choices rank first, with stable catalogue order inside each group;
ordinary and named-argument template choices keep their exact insertion text.
The search does not include argument names, translation values or comments.
Other expression namespaces retain their existing prefix/case rules. Saved-input
revalidation and asynchronous cancellation still run before display/insertion.
The README and repository guidance document the new search contract.

Validation evidence:

- The initial focused run passed 35 tests and failed one older count assertion:
  `Set` now also matches `Hmi.Reset`. Updated that expected count and retained
  the keyboard assertion that selects the second, prefix-ranked template entry.
- `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
  --verbosity minimal --logger trx --results-directory artifacts/log/Debug/Milestone7/Iteration8/AllUnitTests`
  passed all 399 tests, no failures/skips. This built the extension and VSIX;
  no build warnings/errors were reported. Nine additional cases cover matching,
  stable ranking, exact-case insertion, template arguments and namespace scope.
- Ran `eng/test-visualstudio.ps1` with `-Filter 'FullyQualifiedName~LanguageAndSavedCompletion'`
  against VS2022 17.14.36930.0 / AlarmlistExp and VS2026 18.10.12224.181 /
  AlarmlistM7. Both passed one native scenario, no failures/skips. The actual
  keyboard tests search `PEC` inside Inspection and `conta` inside
  Electrical.ContactorTerminal, preserving raw spelling, private draft
  cancellation, saved RESX boundaries, one-edit commit/Undo and unchanged output.
  TRX directories under `artifacts/log/Debug/VisualStudioUI/TestResults/`:
  `8f2b9b4d78904d74b312ad32c42aca15` (2022) and
  `49115e32103b417abd4ec841a9fc78d8` (2026).
- Reviewed the focused diff and `git diff --check`; no whitespace errors.
  No compiler or file-format change, unrelated change or generated output is
  included. No iteration-8 blocker remains. Iteration 9 is next.

### Iteration 9 — parameter menus and alarm access-key help (complete)

Both parameter scopes now expose assignment, empty, fallback and declaration
commands directly on rows/cells via right-click or Shift+F10. Cell menus retain
Cut/Copy/Paste/Select all; row-edge menus omit text commands. Add/Remove remain
visible. Opening a menu selects its target through the existing commit/conflict
guard. It preserves a current-row draft and text selection, closes on a selection
change, restores editing focus after assignment, and releases popup context on
unload. Tooltips and accessible row help explain the actions. The current README,
acceptance checklist and parameter design contract describe these menus.

Alarm button help documents bare A/D while the designer has focus outside text
fields. The native VS2022 test confirmed those keys and Undo work. An attempted
Alt+A hint was disproved: this installed German VS routes Alt+A to View. Its
failure capture and TRX (`e07347c1827c4a6aafb9877a2816d64c`) remain under the usual
VisualStudioUI logs. Removed the incorrect Alt hints without changing keyboard
handling. The corrected native scenario passed (`52b1cee3a5db4bd38d0c319d4b374f0f`)
before the final selection-change guard. Earlier compile checks also caught an
unsupported WPF cue property and the test's incorrect Alt enum name; both were
removed/corrected before functional validation.

`dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
--verbosity minimal --logger trx --results-directory artifacts/log/Debug/Milestone7/Iteration9/AllUnitTests`
passed all 403 tests, no failures/skips, including the final menu selection-change
guard. Extension/VSIX build emitted no warnings/errors.

Final native validation used `eng/test-visualstudio.ps1 -Filter
'FullyQualifiedName~PropertiesAndCompletion'` with the installed host paths and
experimental suffixes. Both passed one scenario, with no failures/skips:
VS2022 / AlarmlistExp (`2f4cde1f1ea84473a20d3719cae8db74`) and VS2026 /
AlarmlistM7 (`9964c29666f94c1eb608df22fe935e9a`). The earlier VS2026 run
(`7238e71e9bf84cdd91ff7fc88408ea98`) timed out because a restored floating
preview covered the mouse target. Its screenshot confirms the obstruction. The
test now hides that tool window only in its owned IDE before mouse input,
following the existing resource-editor test pattern. It still performs actual
Shift+F10/right-click menu actions, verifies fallback/empty assignments, edits
and Undo/Redo, checks native A/D with Undo and accessible shortcut hints, and
preserves saved source/output until the explicit final save/build assertion.

Reviewed the final diff and whitespace; no unrelated or generated files are
included. No iteration-9 blocker remains. Template/menu/folder icons are next.
