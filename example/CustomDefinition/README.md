# Custom-definition playground

Run `scripts/start-custom-definition.bat` from the repository. It defaults to
Visual Studio 2026. For Visual Studio 2022:

```powershell
scripts/start-custom-definition.bat -VisualStudioVersion 2022
```

Add `-PrepareOnly` to build, verify and deploy without opening the IDE. The
optional `-DefinitionFile <path>` selects another definition; relative paths are
resolved from the calling directory. The companion sample alarms must still be
valid for that definition. The shortcut works from any working directory. It requires Git, the repository's
.NET tooling and the selected Visual Studio installation with MSBuild.

The launcher copies repository sources into a fresh directory under
`artifacts/def11/`, replaces that copy's compiler definition,
builds matching SDK/extension tools and compiles the sample using both task
runtimes. Build logs and `playground.json` identify each run. It deploys only to
`AlarmlistDefinitionExp`; close that playground before running the shortcut again.
First use of this separate experimental profile may require Visual Studio setup.

Open `Alarms.almx` in the generated solution. `Plant.Check` demonstrates inherited
fields, typed parameter expressions and procedures; `Plant.Defaults` demonstrates
an absent Code with a compiler-supplied default. Try changing Code to 11 for a
bound diagnostic, using boolean/enum helpers, and comparing the typed preview.
Ratio is deliberately hidden from preview. Source, Identity and TestMethod are
ordinary custom fields, distinct from the built-in preview columns.

To change the toolset, edit **this directory's original AlarmDefinition.xml** and
rerun the shortcut. Update the original Alarms.almx too if your changed definition
requires different sample values. These checked-in samples are the starting point
for each run; edits made inside an opened playground stay in that run. Copy wanted
edits back yourself. The copied definition is for reference: editing it in the
running playground does not rebuild tools or change the schema at runtime.

Previous runs and their edits are preserved. The standard compiler definition,
North/South examples and AlarmlistExp deployments are not changed. No package is
published. Currently this sample demonstrates scalar definitions; collection
examples will be added with the collection implementation.
