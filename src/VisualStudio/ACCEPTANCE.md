# Visual Studio interactive acceptance

Use an experimental Visual Studio instance with the matching VSIX deployed, as
described in [README](README.md#experimental-ide-smoke-test). Repeat on VS2022 and
VS2026. Preparation uses the SDK version pinned in the built VSIX template and
never publishes packages or changes the user's NuGet configuration.

```powershell
powershell.exe -NoProfile -File eng/prepare-visualstudio-acceptance.ps1
& '<VS installation>/Common7/IDE/devenv.exe' /RootSuffix AlarmlistM3 '<printed Acceptance.sln path>'
```

`-Configuration Release` selects Release build artifacts; the generated consumer
starts in Debug. Build the VSIX first when those artifacts are missing or stale.
The helper creates a fresh local feed/cache, builds both projects, checks their
resolved output, and retains package hashes, revision, build logs, and baseline
source/output hashes under `artifacts/log/<Configuration>/VisualStudioAcceptance/`.
The helper passing does not establish that any IDE checks below passed.

## Milestone-seven editor and report workflow

Use the matching milestone-seven VSIX and repeat on both supported IDE hosts.
The automated suites and simulated WPF renders are recorded in
[MILESTONE-7](MILESTONE-7.md#actual-iteration-record). The following interactive
observations remain distinct from those results; do not infer speech, native
high contrast or mixed-monitor DPI behavior from UIA/bitmap checks.

1. In the File settings and Alarm settings areas, collapse/expand each header and
   resize their divider with mouse and keyboard while a property has a private
   draft. Both headers must remain reachable, scrolling stays independent, and
   resizing/collapse must not commit the draft. Reopen to check personal layout.
2. Select a referenced alarm. Verify labels, the right-aligned inheritance gutter
   and editors. Focus alone must keep XML unchanged. Type an override, commit and
   Undo; use Shift+F10/right-click **Use inherited value** and Undo again. Verify
   the Abstract checkbox is readable and has a visible focus/checked state.
3. Edit a local procedure, including a cleared entry. Hide/show cleared entries
   independently in Instructions/Reset; local cleared text remains editable and
   Clear boundaries remain visible. Add/move/remove with keyboard and mouse, then
   Undo. Enter commits, Shift+Enter inserts a newline, and Escape dismisses a
   suggestion popup before cancelling the draft.
4. Type a reference query against a large catalogue. Verify filtering by literal
   identity and name, filename disambiguation, arrows/Enter selection, F4 opening,
   clearing and an unresolved literal. Arrow navigation must not replace typed
   text. Expression completion uses `name (details)` but inserts only its token.
5. Inspect the report: Test method preserves surviving Instructions/Reset order,
   labels and multiline text. At narrow widths the filter stays usable. Sort,
   filter, select/copy and navigate to source. Hide every column, reopen the chooser,
   reset, reorder/resize by mouse and keyboard, then reopen the solution to confirm
   `.almproj.user` restoration without changes to shared project/source/output.
6. Hover representative help while editing. It must be readable, explain the
   current state, and preserve keyboard focus/drafts. Verify equivalent control
   names/help and popup selection/status with an actual screen reader; successful
   automation-peer assertions alone do not establish spoken output.
7. Repeat representative controls at 440/650/900-pixel pane widths in native
   light, dark and Windows high-contrast modes, and at 100/150/200% monitor scale.
   Move the IDE between monitors with different scale settings. Check clipped
   labels, focused/disabled controls, selected/unselected suggestions, tooltip
   placement and long report rows. Discard test edits and compare fixture hashes.

## Milestone-four property workflow

The fixture also links `Shared/Properties.almx` into both projects. The original
milestone-three scenarios remain applicable with the milestone-seven controls
described below; historical results stay in their milestone records. These
property checks have **not been accepted interactively**.

1. Select `Property.Child`. North shows Name `North 001`, South shows `South 001`,
   and both show Code `001` and Description `Inspect 001 - Shared`. The base's
   assigned `par1` value must not appear. Release changes the project value to `101`.
2. In the two-column Alarm parameters list, right-click `par1` or use Shift+F10 to assign it to
   `002`. The dependent fields and procedure update without Build. Undo/Redo must
   restore the exact shared XML.
   An assigned empty string stops fallback; **Use file/project value** resumes it.
   Repeat in the File parameters list at the top of the editor and confirm both
   linked project contexts share that source edit. Both lists must expose exactly
   Name/Value columns, selected-row origins, Add/Remove and keyboard-accessible row
   actions. File editing must also work when no alarm matches the filter.
3. Override Code locally: the editable copy is `$(par1)`. Focus and unfocus it;
   focus-only interactions leave the document clean. Repeat with procedure text.
   Make local copy of Instructions and verify `$(par1)` and `$$(literal)` survive.
4. Type `$(` or press Ctrl+Space. Verify property names, values and origins; use
   arrows, Enter/Tab, Escape, mouse selection and a subsequent draft cancellation.
   Observe spoken status and selection with a screen reader, including initial
   opening and stale-context closure. UIA events alone do not verify speech.
5. Toggle Abstract. The selected alarm stays editable/referenceable and disappears
   from compiled preview/output; its unresolved parameters are allowed. Undo the
   toggle. Check a malformed expression and a scalar cycle through the live Error
   List without overwriting the prior build output.
6. Verify native high contrast, narrow layout, mixed-DPI monitor movement and
   typing/paint response. Close/discard/reopen and unload/reload both projects;
   compare baseline source/output hashes before accepting the run.

The automated property/linked-context cases are in the functional xUnit suite.
They complement these observations; skipped or foreground-blocked cases are not
accepted passes. See the [milestone implementation record](MILESTONE-4.md#implementation-record)
for the current validation results and outstanding gates.

## Linked contexts and provenance

For localization, use the isolated fixtures retained by the `Area=Localization`
functional cases. Choose **Preview language** on the native **Alarmlist** toolbar:
keyword displays complete `$(loc:Key)` tokens; de-DE may use a saved de translation.
Verify that each project's choice survives solution reopen without altering build
cultures or compiled output. Change the language while an ALMX field has a draft;
the draft and Undo history must remain intact. A read-only project must report a
failure and keep its evaluated language; a global AlarmlistCulture disables selection.

Edit a resource value in the native editor, undo/discard the unfinished cell and
then commit it. Only the saved value should reach preview. In a dirty RESX XML
buffer, keep an unsaved value while invoking `$(loc:` completion in the designer;
its label must still describe the saved value. Save and repeat to see the update.
Use arrows and Enter/Tab to insert a full token, then Escape/Undo. With a screen
reader, verify the suggestion name, translated text, parent language and status.
Check a resource diagnostic opens the XML location without saving a dirty buffer.
Retain the separate minimum-VS2022, theme, DPI and speech verification limits.

1. Open `North/Child.almx` in the designer. This is a link to `Shared/Child.almx`,
   also included by South. Wait for both projects to finish loading. With no
   context selected, the designer should ask for a project context instead of
   guessing which project's `Base` to use.
2. Choose North in **Analysis project context**. Select `Shared.Child`, open
   **View > Other Windows > Alarmlist Preview**, select **Selected alarm**, and
   check the following values.

   | Field | North | South | Origin |
   | --- | --- | --- | --- |
   | Name | North inherited name | South inherited name | Inherited from that project's Base; local element is empty |
   | Code | LOCAL | LOCAL | Local, even though Base has equal text |
   | Category | North category | South category | Inherited from Base |
   | Description | North description & Unicode: Ω | South description & Unicode: Ω | Inherited from Base |

3. Switch to South and back. Preview values, origin paths, reference choices, and
   project XML must follow the selected context. The shared source must stay clean.
4. Use **Go to source for resolved Name**. It must reach the selected project's
   `Base.almx` declaration. Return to Child and check the other context as well.
5. Open the linked Child from the other project. Edits must still share the same
   document buffer. Close/reopen a designer view and verify that analysis returns.

## Procedures, drafts, and unsaved preview

1. The Child's resolved Instructions are, in order, the selected project's
   `inspect` step, its `caution` warning, and `Shared local note`. Only the first
   two entries have chain adorners. Their source links go to that project's Base.
2. Resolved Reset contains only `Shared reset only`. The procedure editor shows
   the inherited reset and `Excluded local reset` inline with **Cleared** labels.
   Its group eye toggle hides/shows these entries, and cleared local text remains
   editable. No Clear directive appears in compiled output.
3. On Instructions, open **Inheritance**, then choose **Make local copy**. XML
   should contain a leading Clear and exactly the three current effective entries.
   Reset is unchanged.
   Undo once restores the original section and inheritance. Redo/Undo should
   restore the same result without duplicate entries.
4. Leave **Selected alarm** visible and focus inherited Name in the designer.
   Focus alone must preserve source and the resolved preview. Type a different
   Name: both previews clear during the uncommitted draft, and the editor keeps
   focus. Escape restores inheritance without changing source. Type and commit
   a local Name, verify the unsaved preview, then Undo. Repeat with the field's
   **Use inherited value** context action and Undo.
5. Edit a base Name in the XML editor without saving. Return to Child and verify
   the corresponding context updates. The other context keeps its own Base.
   Undo the edit; disk source and compiled output remain at their baselines.

## Keyboard, copy, themes, and layout

1. Navigate the Preview window tabs with Tab/Shift+Tab. Text and source links must
   expose descriptive accessible names and origin help. Chain adorners are
   decorative: they must neither take focus nor intercept text selection.
   Scalar action names must identify their field (for example, **Use inherited
   Name**); group action controls must identify Instructions or Reset.
2. Select text in a resolved field using Shift+Arrow. Ctrl+C and paste into a
   temporary unsaved text document should preserve the selected text. Attempted
   typing in the preview must not change source. Do not save the scratch document.
3. Select **Compiled XML**, focus its text, and use Ctrl+A/Ctrl+C.
   Paste into a scratch XML document. It should contain the whole selected
   project's resolved output, including current unsaved edits. ReferenceName and
   Clear directives must not appear. Close the scratch document without saving.
4. Change the experimental IDE's theme while the panels remain open, then restore
   the original theme. Check headings, text, selection contrast, source links, and
   chain icons. Test high contrast where available. Record actual themes used;
   offscreen palette tests do not substitute for this check.
5. Resize the designer until the alarm list becomes the compact selector. Check
   wrapped origins, nested scrolling, and keyboard access to the XML box. Move
   between monitors with different scale factors where available; record actual
   DPI values and check clipped text and adorner alignment. Do not call resizing
   or rasterizing an image a per-monitor DPI test.
6. With an actual screen reader, verify names, origins, read-only status, and
   status announcements. UI Automation inspection alone is a separate check.

## Translation exchange

The persistent example also covers reusable instruction templates and generated
HMI lookups. Follow [its walkthrough](../../example/README.md#try-reusable-instructions-and-hmi-lookups)
to compare keyword/en/de-DE, project-scoped arguments, reordered named placeholders,
individual path/member translations and explicitly selected metadata. The
LookupCompletion xUnit scenario (`Area=Lookups`) checks saved-only catalogue capture,
completion/Undo and regeneration in an isolated fixture on each supported IDE.
These UI checks passed on VS2022 and VS2026 for iteration H4; see MILESTONE-5.

The ListCompletion scenario (`Area=Lists`) covers explicitly mapped wire and
terminal suggestions, literal identifiers, descriptions/metadata, replacement of
quoted slots, direct reference insertion, Undo and saved-only regeneration. It
passed in both IDE versions for L2. The persistent ElectricalInstructions.almx
sample also covers two independent selections from one list, PLC punctuation,
and a free literal argument. Follow its walkthrough in the example README.

1. Export a synthetic fixture with the SDK's ExportAlarmlistXliff target. Include
   Unicode, leading/trailing whitespace, comments, repeated parameter expressions,
   a missing target and an explicitly empty target. Keep the generated baseline
   unchanged. The source culture defaults to en; also verify a configured override.
2. In each available target tool (Trados Studio, memoQ, Phrase TMS), open the XLIFF
   2.0 file using the documented profile settings. Verify the key IDs, notes,
   whitespace and protected tags. Edit ordinary target text, preserve parameter
   tags, and export the translated XLIFF. Record the exact tool version/settings
   and retain both exported files. Opening XML in a text editor or validating its
   schema is not evidence of vendor interoperability.
3. Keep vendor round trips pending when no target tool is available. The owner
   explicitly accepted that deferral on 2026-10-08.
4. Run `ImportAlarmlistXliff` first as a dry run, with the retained baseline and
   returned file selected. Verify per-key changes and unchanged resource/output
   bytes. Change a saved source or target after export and verify that the whole
   batch is rejected with conflicts and no partial writes.
5. Try apply with the destination open as dirty XML and with an unfinished native
   resource cell. Both must be refused without saving/discarding the draft. Save
   or discard intentionally, close the resource editor, and apply. Check saved
   RESX, designer refresh, native-editor reopening and a subsequent multi-culture
   Build. Repeating the same import must not rewrite resources. Native-cell checks
   require an idle unlocked desktop; foreground-blocked cases remain pending.

## Closeout and evidence

Discard test edits and compare all files in `baseline-hashes.json` to the recorded
SHA256 values. A preview or context change must never write compiled output.
Record host version, deployed build, fixture path, scenarios, observed results,
failures, and any checks not run in the applicable milestone record, currently
[MILESTONE-7](MILESTONE-7.md).

Use `eng/test-visualstudio.ps1 -MeasureAnalysis` for running-IDE buffer-to-diagnostic
latency and repeated designer close/process-memory samples. See the README for
parameters and measurement limits. Real keystroke-to-paint responsiveness and
long-session memory attribution still need interactive/profiler evidence. The
separate packaged-bridge benchmark remains a useful baseline. Keep remaining
checks explicit before release acceptance. Publish the authoritative matching SDK before distributing
the VSIX through the existing release workflow.
