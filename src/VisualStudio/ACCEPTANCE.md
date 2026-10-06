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

## Milestone-four property workflow

The fixture also links `Shared/Properties.almx` into both projects. The original
milestone-three scenarios and their historical results below remain unchanged.
These new checks have **not been accepted interactively**.

1. Select `Property.Child`. North shows Name `North 001`, South shows `South 001`,
   and both show Code `001` and Description `Inspect 001 - Shared`. The base's
   assigned `par1` value must not appear. Release changes the project value to `101`.
2. In the two-column Alarm parameters list, use Row actions to assign `par1` to
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

1. Open `North/Child.almx` in the designer. This is a link to `Shared/Child.almx`,
   also included by South. Wait for both projects to finish loading. With no
   context selected, the designer should ask for a project context instead of
   guessing which project's `Base` to use.
2. Choose North in **Analysis project context**. Select `Shared.Child`, expand
   **Resolved alarm preview**, and check the following values.

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
2. Resolved Reset contains only `Shared reset only`. The procedure editor's
   **Excluded by Clear** section explains both the inherited reset and
   `Excluded local reset`. No Clear directive appears in compiled output.
3. On Instructions, choose **Make local copy**. XML should contain a leading
   Clear and exactly the three current effective entries. Reset is unchanged.
   Undo once restores the original section and inheritance. Redo/Undo should
   restore the same result without duplicate entries.
4. Leave **Resolved alarm preview** expanded and choose **Override locally** on
   inherited Name. Both previews clear during the uncommitted draft. The local
   Name editor must stay in view and receive keyboard focus, with the caret after
   the copied value. Escape restores inheritance without changing source.
   Commit a different local Name, verify the unsaved preview, then Undo.
5. Edit a base Name in the XML editor without saving. Return to Child and verify
   the corresponding context updates. The other context keeps its own Base.
   Undo the edit; disk source and compiled output remain at their baselines.

## Keyboard, copy, themes, and layout

1. Navigate the expanded previews with Tab/Shift+Tab. Text and source links must
   expose descriptive accessible names and origin help. Chain adorners are
   decorative: they must neither take focus nor intercept text selection.
   Scalar action names must identify their field (for example, **Override Name
   locally**); section copy/replacement actions must identify Instructions or Reset.
2. Select text in a resolved field using Shift+Arrow. Ctrl+C and paste into a
   temporary unsaved text document should preserve the selected text. Attempted
   typing in the preview must not change source. Do not save the scratch document.
3. Expand **Compiled project XML preview**, focus its text, and use Ctrl+A/Ctrl+C.
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

## Closeout and evidence

Discard test edits and compare all files in `baseline-hashes.json` to the recorded
SHA256 values. A preview or context change must never write compiled output.
Record host version, deployed build, fixture path, scenarios, observed results,
failures, and any checks not run in [MILESTONE-3](MILESTONE-3.md).

Use `eng/test-visualstudio.ps1 -MeasureAnalysis` for running-IDE buffer-to-diagnostic
latency and repeated designer close/process-memory samples. See the README for
parameters and measurement limits. Real keystroke-to-paint responsiveness and
long-session memory attribution still need interactive/profiler evidence. The
separate packaged-bridge benchmark remains a useful baseline. Keep remaining
checks explicit before release acceptance. Publish the authoritative matching SDK before distributing
the VSIX through the existing release workflow.
