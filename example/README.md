# Alarmlist example playground

For fork-time custom fields, use the separate
[custom-definition playground](CustomDefinition/README.md) and
`scripts/start-custom-definition.bat`.

For reusable ProjectReference/NuGet libraries, see the independent
[library playground](Libraries/README.md) with two consumers and a transitive
dependency graph. Its preparation does not change this North/South playground.

Run `scripts/start-vs2022.bat` or `scripts/start-vs2026.bat` from the repository.
The launcher builds and deploys the current extension, prepares its matching SDK
and sample analyzer,
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
4. Open **View > Other Windows > Alarm Summary**. Its **Alarm table** shows
   the current project's concrete alarms; filter or sort the columns, select a
   row for **Selected alarm** details, and use **Compiled XML** for the whole
   project. Ctrl+C or **Copy selected rows** copies a table selection. Double-click
   a row to go to source. Resize or float the window for a larger table.
   In the designer, expand **Test procedure**. Try **Override locally** on an
   inherited field or **Make local copy** on Instructions, inspect the XML, and Undo.
5. Choose **en** in **Summary language** on the native **Alarmlist** toolbar
   beside the standard VS controls. The default **keyword** displays literal
   `$(loc:Key)` expressions. Try **de-DE** to see German translations.
6. Build the solution. Each project produces **keyword**, **en**, and **de-DE**
   folders under **bin/Debug**, containing **North.Alarmlist.xml** or
   **South.Alarmlist.xml**. Try Clean/Rebuild and the project output properties.
   Live previews reflect unsaved ALMX edits without writing these files.

The initial solution has no compiler errors: North compiles twelve alarms and South
compiles eight in each build culture. Each project has two intentional **SAMPLE001**
warnings per culture from the diagnostics exercise below; builds still succeed.
With preview language **en**, the shared
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

For translation exchange, use the SDK's [export and import workflow](../src/SDK/Alarmlist.SDK/README.md#xliff-translation-import).
Export from the desired project, retain the local baseline, and review an import
dry run before applying the returned translation. Close affected RESX editors
after saving or discarding their drafts. The designer refreshes after a successful
import; Build regenerates each language output. Shared resource changes affect
both North and South, while their project-local resources remain independent.

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

You can also place the caret inside an empty quoted slot and invoke Ctrl+Space,
or type a supported expression prefix inside it, for example `path="$(path:Service."`.
Choosing a path replaces the entire quoted slot with an unquoted `$(path:...)`
expression. Quoted values otherwise remain literal; nested `$(loc:...)` arguments
are not evaluated. Use path/property/button/meta/list or parameter expressions for
resolved argument values instead.

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

## Try supplied lists and electrical instructions

Open **ElectricalInstructions.almx** in either project. Both projects link
**Shared/Electrical.almlookup**, representing lists supplied by electrical and PLC
tools. Choose **en** to see:

> Disconnect wire =EC-120W1 from =EC-120K1:12.

The next instruction selects two independent connection points from the same
list. The third uses **Main.Outlets[1].VacuumReady**, retaining its punctuation.
The Hint reuses the same CheckSignal sentence with a free literal argument,
**Auxiliary.Ready[0]**, which need not appear in a list.

Choose **de-DE**. The German sentence reverses the order of wire and connection,
and the suggestion descriptions translate through **de**. The identifiers remain
unchanged. **keyword** displays each complete expression.

To try authoring, edit the first local Step:

1. Type `$(loc:Instruction.DisconnectWire` and choose the **template** suggestion.
2. With the first `""` selected, press **Ctrl+Space**. Only **Wires** are offered,
   together with descriptions, drawing pages and cross-section metadata. Select
   an entry and press Enter to insert `$(list:Wires#EC120W1)` without quotes.
3. Place the caret inside the connection argument's quoted value and press
   **Ctrl+Space**. Only **ConnectionPoints** are offered. You can type part of an
   identifier or description to narrow the choices. Selecting replaces the whole
   argument value. Enter again commits the ALMX edit; Ctrl+Z undoes it.

Direct `$(list:PlcSignals#` completion is also available in scalar fields and
procedure rows. Explicit Template/Argument mappings in the catalogue choose the
lists; the editor does not infer them from argument names. Entries are suggestions,
without membership or relationship validation. No DeviceTag field is added.

Keep an entry's **Key** stable and change its **Value**, then save the catalogue.
References resolve to the new value in both projects. Unsaved catalogue edits stay
private. Multiple files may contribute to a list; duplicate entry keys fail the
build with **ALM0023**. Optional **DescriptionKey** uses RESX; **Description** supplies
a literal alternative. Missing translated descriptions do not invalidate literal
identifiers. Removing an explicitly referenced entry fails with **ALM0021** and
preserves previous outputs. Restore exploratory edits afterward.

## Try configurable diagnostics and external analyzers

Both projects explicitly reference the [sample code-prefix analyzer](../src/Analyzers/Alarmlist.SampleAnalyzer/README.md).
Preparation builds its **net472** and **net8.0** DLLs into ignored `.analyzers/`
folders. `Directory.Build.targets` selects net472 for Visual Studio/desktop
MSBuild and net8.0 for `dotnet msbuild`. The SDK package supplies the compiler.

Open **Diagnostics.almx** in North or South and inspect **Error List > Build +
IntelliSense**. The `Diagnostics.Valid` alarm has a valid local code. The
`Diagnostics.PrefixWarning` alarm deliberately inherits an invalid legacy code
from an abstract template. **SAMPLE001** points to the consuming alarm and retains
the template's code as a related location. The abstract template itself is not
checked or emitted. Set the warning alarm's local Code to **N-002** in North or
**S-002** in South to clear it, then Undo.

Open **Diagnostics.Shared.almx**, linked into both projects from
**Shared/Diagnostics.almx**. Its valid code is **SH-001**; the intentional warning
uses **LEGACY-SHARED**. Change that code to **SH-002** to clear the warning in both
contexts, then Undo. The shared file uses its physical source path for settings,
so North's and South's local prefixes do not apply to it.

Open **Configuration > Example.editorconfig** and **Project.editorconfig** in
either project (the links point to the actual `.editorconfig` files). The root
file is also under **Solution Items**. The configured rules are:

| Physical source | Expected code prefix | Rule severity |
| --- | --- | --- |
| North/Diagnostics.almx | N-, from North/.editorconfig | warning |
| South/Diagnostics.almx | S-, from South/.editorconfig | warning |
| Shared/Diagnostics.almx | SH-, from the root .editorconfig | warning |
| Earlier playground samples | Not checked visibly | none |

The example's `root = true` stops ancestor configuration. Later matching sections
override the initial suppression; nearer project files override the prefix for
local files. In North's `[Diagnostics.almx]` section, add a severity setting and
**save** to try:

```ini
alarmlist_diagnostic.SAMPLE001.severity = error
```

Use **warning**, **suggestion**, **silent**, **none**, or **default** instead to
compare behavior. `error` blocks previews/build output; `suggestion` is nonfatal;
`silent` and `none` have no live Error List row. `default` restores the rule's
warning default. The shared warning is unaffected by this project-local section.
Remove the added line to restore the starting state. Setting
`alarmlist_sample.code_prefix = unset` removes the custom prefix, so this analyzer
uses its **AL-** default; restore **N-** or **S-** afterward.

Unsaved `.editorconfig` drafts do not change analysis. Saving refreshes live
diagnostics without saving ALMX drafts or rebuilding output. ALMX buffer edits
still participate in live analysis; command-line builds use saved sources.

Set project warning controls in an individual `.almproj`, or use the shared
**Solution Items > Directory.Build.props** default. From the repository root,
these commands demonstrate their effects using saved inputs:

```powershell
# Succeeds with the two intentional warnings per culture.
dotnet msbuild example/North/North.almproj /t:Build /nr:false
# Fails and preserves all three previous outputs and their ownership manifest.
dotnet msbuild example/North/North.almproj /t:Build /p:TreatWarningsAsErrors=true /nr:false
# Exempts SAMPLE001 from blanket promotion; warnings remain visible.
dotnet msbuild example/North/North.almproj /t:Build /p:TreatWarningsAsErrors=true /p:WarningsNotAsErrors=SAMPLE001 /nr:false
# Explicitly promotes just this rule.
dotnet msbuild example/North/North.almproj /t:Build /p:WarningsAsErrors=SAMPLE001 /nr:false
# Suppresses the rule, including when warnings are treated as errors.
dotnet msbuild example/North/North.almproj /t:Build /p:TreatWarningsAsErrors=true /p:NoWarn=SAMPLE001 /nr:false
```

Restore the starting configuration before moving on. Compiler errors
**ALM0001–ALM0025** remain mandatory even with `NoWarn` or `severity = none`.
For example, the missing-reference exercise below still fails the build.

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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/prepare-example.ps1
dotnet msbuild example/Example.sln /t:Build
```

Run these commands from the repository root. Use `-Configuration Release` when
preparing from Release artifacts, and deploy that same extension configuration.
This selects the package build; the example still opens in Debug by default.
`-MSBuildPath '<VS installation>/MSBuild/Current/Bin/MSBuild.exe'` optionally uses
desktop MSBuild for preparation, as the launchers do.

The helper builds both sample analyzer runtimes, reads the SDK version from the
built VSIX template, and generates
ignored `global.json` and `NuGet.Config` files here. Only the repository's matching
local package feed is used. Its cache is under `.packages/<package SHA256>/`, so
rebuilding the SDK with the same version still loads fresh contents. Local
`Directory.Build.props/targets` keep the repository's Arcade configuration out of
these consumer projects, following the existing SDK test/acceptance fixtures.
The targets file also links the common HMI, electrical and diagnostic samples,
the saved EditorConfig files, and the prepared analyzer. Analyzer DLLs live under
ignored `.analyzers/net472/` and `.analyzers/net8.0/`; rerunning preparation updates
them without replacing ALMX, RESX, lookup or EditorConfig inputs. Preparation
restores the projects but does not compile their output. After analyzer replacement,
the IDE renews its isolated analysis domain; use a fresh desktop MSBuild process
(`/nr:false`) for command-line builds.
The launchers clear `NUGET_PACKAGES` only in their child process environment so
the IDE honors this isolated cache; they do not change user NuGet settings.

This is a persistent playground for implemented features. For a fresh fixture with
baseline hashes and the full interactive checklist, use
[the acceptance workflow](../src/VisualStudio/ACCEPTANCE.md).
