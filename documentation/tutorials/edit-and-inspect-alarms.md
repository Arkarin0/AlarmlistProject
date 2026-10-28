# Edit and inspect alarms in Visual Studio

Use the designer and XML together, inspect inheritance and compare resolved values
in Alarm Summary.

## Prerequisites and starting files

Use a Windows checkout with repository tooling and Visual Studio 2022 17.9+ or
2026. Preserve any existing example edits. Close the AlarmlistExp instance before
redeploying to that profile; do not close unrelated IDE work. Commands run from
the repository root. Use the existing `example/Example.sln` playground.

## Steps

### 1. Prepare and open the playground

Run `scripts/start-vs2026.bat`, or `scripts/start-vs2022.bat` for that host.

Expected result: the launcher builds/deploys matching tools, prepares the SDK and
sample analyzer and zenon exporter, and opens Example.sln in AlarmlistExp. Preparation preserves
source edits, so a previously modified playground may show different values.

### 2. Edit a local scalar field

Open **North > Alarms.almx**, select `Plant.Utility.AirPressureLow`, and note its
current Code. Change Code to `U-002`, then press Enter. Switch to Split.

Expected result: the XML contains the new Code. Ctrl+Z restores the original
value. A typed draft cancelled with Escape never becomes a source edit.

### 3. Inspect the reference chain and completion

Select `Plant.Pump.PressureLow.Panel`. Inspect its inherited Category/Description
and use **Go to reference** or **Go to source**. In a Name expression field,
invoke Ctrl+Space to inspect localization candidates; dismiss with Escape.

Expected result: navigation reaches the owning declaration. Candidates reflect
the selected project's current saved resources. Viewing/dismissing suggestions
does not change source or materialize inherited values.

### 4. Compare summary languages and projects

Open **View > Other Windows > Alarm Summary**. Choose North and `en` in
**Summary language**. Locate `Plant.Pump.PressureLow`; then choose South and the
same alarm. Inspect **Selected alarm** and **Compiled XML**. Try sorting, column
preferences, Ctrl+C on selected rows and double-click navigation.

Expected result: on unchanged sample inputs the Names are `North pump pressure
low` and `South pump pressure low`. A de-DE selection uses German resources;
keyword retains localization expressions. Layout changes do not edit ALMX.

## Verify the result

Return the test Code edit to its original value with Undo. Check that XML and
Designer agree and that summary project/language selections explain the displayed
values. Live inspection alone does not rewrite compiled files on disk.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Designer disabled | Incompatible/stale SDK analysis | Prepare matching tools and allow project reevaluation. |
| Summary differs from editor | Different project/language selection | Align both selections before comparing. |
| Sample differs from expected text | Playground was edited previously | Inspect current saved sample rather than overwriting user edits. |

## Next steps and related features

See [editing](../features/visual-studio-editing.md),
[completion](../features/xml-completion.md), [Alarm Summary](../features/alarm-summary.md)
and [shared source contexts](share-sources-between-projects.md).
