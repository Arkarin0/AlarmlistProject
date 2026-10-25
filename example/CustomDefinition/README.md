# Custom-definition playground

Run `scripts/start-custom-definition.bat` from the repository. It defaults to
Visual Studio 2026. For Visual Studio 2022:

```powershell
scripts/start-custom-definition.bat -VisualStudioVersion 2022
```

Add `-PrepareOnly` to build, verify and deploy without opening the IDE. The
optional `-DefinitionFile <path>` selects another definition and `-SampleFile <path>`
selects the alarm source copied into the playground as Alarms.almx. Relative paths
are resolved from the calling directory. The companion sample alarms must be
valid for that definition. The shortcut works from any working directory. It requires Git, the repository's
.NET tooling and the selected Visual Studio installation with MSBuild.

The launcher copies repository sources into a fresh directory under
`artifacts/def11/`, replaces that copy's compiler definition,
builds matching SDK/extension tools and compiles the sample using both task
runtimes. Build logs and `playground.json` identify each run. It deploys only to
`AlarmlistDefinitionExp`; close that playground before running the shortcut again.
First use of this separate experimental profile may require Visual Studio setup.

Open `Alarms.almx` in the generated solution. `Plant.Check` demonstrates inherited
fields, typed parameter expressions, collections and procedures; `Plant.Defaults`
demonstrates absent Code and record fields with compiler-supplied defaults.
Tags allow duplicate strings and show inherited-first ordering. Numbers contain
typed parameter expressions. Checks contain flat records: Copy uses
`$(item:Count)` and inherits the consuming alarm's parameter scope. The Defaults
alarm demonstrates Clear and an empty record receiving literal field defaults.
Try changing Code or Count to 11 for a bound diagnostic, adding a fourth effective
tag for a count diagnostic, moving local entries, using boolean/enum helpers, and
comparing the separate Tags, Measurements and Inspection checks Preview columns.
Tags use Concatenated with a comma separator; Measurements use a numbered List.
Inspection checks use the compiled template in `Preview/Checks.xaml`, with the
shared heading style in `Preview/Palette.xaml`. Edit those original files and
rerun the launcher to rebuild matching tools. Both procedure variants use these
formats. Each cell initially shows up to ten entries and offers expansion;
copying includes the complete collection even while collapsed.
Inherited entries remain read-only; collapse and analysis refresh preserve drafts.
Use an entry arrow or the collection's Collapse entries/Expand entries commands
to save space. Collapsed headers show one field per tooltip line and retain
status and actions. New entries open expanded with editing focus. The inherited
chain marker occupies the same reserved gutter as local entries, and record
labels align across the collection. Clear remains a directive: earlier effective
entries are excluded and subsequent entries remain.
Ratio is deliberately hidden from preview. Source, Identity and TestMethod are
ordinary custom fields, distinct from the built-in preview columns.

To try the same feature **without procedures**, run from the repository root:

```powershell
scripts/start-custom-definition.bat -DefinitionFile example/CustomDefinition/AlarmDefinition.NoProcedures.xml -SampleFile example/CustomDefinition/Alarms.NoProcedures.xml
```

Add `-VisualStudioVersion 2022` or `-PrepareOnly` as needed. This second definition
omits TestProcedure; its companion alarm source contains no procedure elements.
The procedure editor and built-in Test method column disappear. The ordinary
TestMethod field remains. Supplying procedure content to this definition is a
compiler error. The two sample XML files are explicit alternatives; the launcher
does not strip content from your chosen source to make it compile.

To change the toolset, edit **the original definition selected for your run**
(AlarmDefinition.xml by default) and rerun the shortcut with the same options.
Update the chosen sample source too if your changed definition requires different
sample values. These checked-in samples are the starting point
for each run; edits made inside an opened playground stay in that run. Copy wanted
edits back yourself. The copied definition is for reference: editing it in the
running playground does not rebuild tools or change the schema at runtime.
The launcher captures declared template dependencies relative to the selected
original definition, copies them into the isolated toolset and playground, and
compiles them into the extension. This also works for definitions outside the
repository. Runtime Preview uses installed resources, not these copied files.
See the [template binding contract](../../eng/tools/AlarmDefinitionGenerator/README.md)
for named fields, states, origins and resource restrictions.

Editable Boolean and Enum collection fields have a dropdown beside their
expression editor. Enabled offers true/false; Priority offers Low/High. Tab to
the dropdown and press Enter to open its choices. Opening or dismissing it keeps
the current expression; choosing a value changes the draft. Inherited entries
remain read-only.

Enum captions use the project's selected language and the supplied
Translations.en.resx / Translations.de.resx files. Choose en for English, or
de-DE to see parent-culture fallback to German. Priority.Low is deliberately
missing in German: the fallback label appears and ALM0031 warns once for the key.
Add that entry to remove the warning. Keyword mode uses the definition labels.
The shortcut copies these resources with the playground and builds keyword, en
and de-DE outputs; warnings do not fail builds unless promoted to errors.
Stored enum values remain Low/High in every output. The custom record template
shows the translated Priority caption. Existing XLIFF export/import includes the
caption RESX keys even without a corresponding alarm expression.

Previous runs and their edits are preserved. The standard compiler definition,
North/South examples and AlarmlistExp deployments are not changed. No package is
published. The standard version-2 definition keeps the four original scalar fields
and explicitly enables TestProcedure; collections are opt-in fork definitions.
