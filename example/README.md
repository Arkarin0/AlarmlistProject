# Alarmlist example playground

Run `scripts/start-vs2022.bat` or `scripts/start-vs2026.bat` from the repository.
The launcher builds and deploys the current extension, prepares its matching SDK,
and opens **example/Example.sln** in the `AlarmlistExp` instance. Close that
experimental instance before running the launcher again. No manual NuGet feed or
SDK version setup is needed. `--deploy-only` prepares everything without opening
the IDE; use **File > Open > Project/Solution** to open `Example.sln` yourself.

## Start here

1. Open **North > Alarms.almx**. Try Designer, Split, and XML modes. Select
   `Plant.Utility.AirPressureLow` to edit local fields and test-procedure rows.
   Enter or moving to another designer field commits; Escape cancels a draft;
   Ctrl+Z undoes edits. Moving to the VS toolbar keeps a draft private.
2. Select `Plant.Pump.PressureLow.Panel` in the same file. Name and Code are local;
   Category and Description come through two references from **North/Base.almx**.
   Try the reference picker, **Go to reference**, and **Go to source**.
3. Open **North > PressureLow.almx**, the linked file also included by South.
   Choose North, then South in **Analysis project context** to compare the same
   source against different bases. This demonstrates project-context selection.
4. Expand **Resolved alarm preview**, **Test procedure**, and **Compiled project
   XML preview**. Try **Override locally** on an inherited field or **Make local
   copy** on Instructions, inspect the XML, and Undo.
5. Choose **en** in **Preview language** on the native **Alarmlist** toolbar
   beside the standard VS controls. The default **keyword** displays literal
   `$(loc:Key)` expressions. Try **de-DE** to see German translations.
6. Build the solution. Each project produces **keyword**, **en**, and **de-DE**
   folders under **bin/Debug**, containing **North.Alarmlist.xml** or
   **South.Alarmlist.xml**. Try Clean/Rebuild and the project output properties.
   Live previews reflect unsaved ALMX edits without writing these files.

The initial solution has no compiler errors: North compiles seven alarms and South
compiles three in each build culture. With preview language **en**, the shared
`Plant.Pump.PressureLow` alarm shows:

| Value | North context | South context |
| --- | --- | --- |
| Name | North pump pressure low | South pump pressure low |
| Category | North utilities | South utilities |
| Code | P-001, local even though it equals Base | P-001, local even though it equals Base |
| Instructions | North Step + Warning, then the shared Note | South Step + Warning, then the shared Note |
| Reset | Only the shared control-panel reset | Only the shared control-panel reset |

The empty local Name inherits. Reset's **Clear** excludes both the inherited reset
and the preceding local step; inspect **Excluded by Clear**. The panel alarm in
North adds a Hint and Step after the inherited instructions. Comments and escaped
ampersands in the resources also provide examples for source-preserving editing.

## Try localization

Each project has its own **Translations.en.resx** and **Translations.de.resx**.
They use the same `Pump.*` keys with different North/South translations. Both
projects also link the common catalogues under **Shared**, containing
`Shared.LogPressure` and `Shared.Reset`. These are saved RESX build inputs,
editable in Visual Studio's built-in resource editor.

- **Keyword display:** the toolbar starts at **keyword**, so Name shows
  `$(loc:Pump.Name)`. Select **en** or **de-DE** for a translated preview. The
  shared project property remembers the choice without changing build cultures.
- **Parent lookup:** **Translations.de-DE.resx** intentionally contains only
  `Shared.Reset`. That key uses its regional wording; every other key comes from
  **de**. Select **de** to compare the reset wording. There is no fallback to
  English or an invariant catalogue. An empty translation also counts as missing.
- **Parameters inside translations:** `Pump.Inspect` contains `$(par1)`.
  The shared alarm uses project value **001** in North and **002** in South.
  North's panel alarm overrides it with **003**. `NewAlarm` has no assignment,
  so it uses **010** from its file. For example, the English panel instruction is
  **Inspect 003: check the North pump and inlet valve.**
- **Focus and completion:** a translated field shows its original expression on
  focus. Type `$(loc:` or press Ctrl+Space for saved keys, translations, comments,
  and parent-culture origins. Escape cancels the draft. Selecting a toolbar
  language keeps unfinished ALMX edits private.
- **Saved resources:** change a translation in the resource editor and commit the
  cell to refresh previews. The current resource editor saves committed cells.
  An unfinished cell edit or unsaved RESX XML buffer is not exposed to previews
  or builds.
- **New resources:** use **Add > New Item > Alarmlist > Translations (RESX)**.
  Choose a new culture suffix, such as `Translations.fr.resx`, and add its keys.
  Add `fr` to **AlarmlistCultures** when it should also be built. Keep keys unique
  within each project and culture, including linked catalogues.

To inspect a missing translation diagnostic, temporarily remove `Pump.Name` from
North's German resource and select **de-DE**. The English entry is not used as a
fallback. Undo or restore the German entry before building again.

## Try reusable instructions and HMI lookups

Open **North > HmiInstructions.almx** and select **Plant.Outlet.Vacuum**. South
links the same file. The abstract **Templates.Hmi.Vacuum** supplies procedure
expressions and a parameter declaration, and is excluded from compiled output.
Both projects link **Shared/Hmi.almlookup** through `Directory.Build.targets`.
This file represents output from another tool; its German code identifiers stay
stable while RESX translates each ordered segment and property/button label.

Choose **en** in the Alarmlist toolbar. The first instruction reads:

> In "Service -> Transport -> Outlet -> Parameter 1 Countercheck -> Camwindow" set property "ON" to "60".

The next instruction reuses **Instruction.Set**, with `value=$(par1)`: North
displays **001**, South **002**. The Hint explicitly references stored metadata
**Default**, displaying **060** without numeric formatting. No default is inserted
automatically, and **Minimum/Maximum** are selection guidance only. The following
Step and Reset reuse **Instruction.Press** with the **ON/OFF** buttons under
**Service -> Transport -> Outlet -> Manual 1 -> Vacuum**.

Choose **de-DE** to translate the sentence, all path segments, and the member
labels together through parent **de**. The German Set template changes the order
of `{property}` and `{path}`. Choose **keyword** to see each complete expression
with its named argument bindings.

For a local procedure row, type `$(loc:Instruction.Set` and choose the completion
row marked **template**. It inserts `path="", property="", value=""` and selects
the first quoted slot. Type `$(path:` to choose a path. Select another quoted slot
(including its quotes) and type `$(property:` or `$(button:`; include a path key
and `#` to narrow the member choices. Use `$(meta:` to explicitly insert stored
metadata. Enter inserts into the draft; the next Enter commits one undoable edit.
For inherited rows, use **Override locally** or **Make local copy** first.

Change the **Value** attribute of the **Default** metadata entry from **060** to
**061**, then save the catalogue. Both project previews refresh. An unsaved lookup
XML draft stays private. Restore **060** afterward. Removing a referenced member
produces **ALM0021** and fails the whole multi-culture build without replacing any
previously compiled output.

The authoritative XSD ships in the SDK; query its installed path with:

```powershell
dotnet msbuild example/North/North.almproj /getProperty:AlarmlistLookupSchema
```

See the [lookup format and expression grammar](../src/VisualStudio/HMI-LOOKUPS.md)
for external generators. **Instruction.Set** and **Instruction.Press** are
translated once per language and reused across instructions; adding another step
does not require another sentence key. XLIFF export protects their named
placeholders while allowing translators to reorder them.

## Try live diagnostics

In North's `Alarms.almx`, temporarily change the panel's ReferenceName to
`Does.Not.Exist`. Commit the edit and inspect **Live project diagnostics** and
**Error List > Build + IntelliSense** for **ALM0002**. Follow the source link,
then Undo to restore the preview. Building with the error should fail and preserve
the previous compiled output. Other useful temporary edits are a duplicate
FullyQualifiedName (ALM0001) or a self-reference (ALM0003).

Opening the linked file from both projects uses the same source buffer. Editing a
base Name without saving should update that project's inherited preview while the
other context keeps its own value. Undo or discard exploratory changes when done.
The launchers preserve your example sources; they do not reset them on startup.

## SDK preparation details

After building the VSIX separately, prepare the example without deploying or
starting Visual Studio:

```powershell
powershell.exe -NoProfile -File eng/prepare-example.ps1
dotnet msbuild example/Example.sln /t:Build
```

Run these commands from the repository root. Use `-Configuration Release` when
preparing from Release artifacts, and deploy that same extension configuration.
This selects the package build; the example still opens in Debug by default.
`-MSBuildPath '<VS installation>/MSBuild/Current/Bin/MSBuild.exe'` optionally uses
desktop MSBuild for preparation, as the launchers do.

The helper reads the SDK version from the built VSIX template and generates
ignored `global.json` and `NuGet.Config` files here. Only the repository's matching
local package feed is used. Its cache is under `.packages/<package SHA256>/`, so
rebuilding the SDK with the same version still loads fresh contents. Local
`Directory.Build.props/targets` keep the repository's Arcade configuration out of
these consumer projects, following the existing SDK test/acceptance fixtures.
The targets file also links the common HMI catalogue and instruction sample.
The launchers clear `NUGET_PACKAGES` only in their child process environment so
the IDE honors this isolated cache; they do not change user NuGet settings.

This is a persistent playground for implemented features. For a fresh fixture with
baseline hashes and the full interactive checklist, use
[the acceptance workflow](../src/VisualStudio/ACCEPTANCE.md).
