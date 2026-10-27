# Alarm Summary

Inspect resolved alarms, selected-alarm details and compiled XML in a Visual
Studio tool window without writing build outputs.

## When to use it

Use the summary to compare inherited/effective values, translations and collection
contents before saving or building.

## Requirements and availability

Use the matching Visual Studio extension and a project with current compatible
analysis. Custom collection presentation requires a rebuilt definition/toolset.

## How it works

Open **View > Other Windows > Alarm Summary**, or the project's **Show Alarm
Summary** action. Select the project explicitly; its choice is independent of a
shared editor's consumer. The Alarm table shows concrete alarms; Selected alarm
shows details, and Compiled XML shows resolved project XML. Live results can
include unsaved ALMX buffer edits; resources/lookups remain saved-only.

Filter/sort rows, double-click to navigate to source, and use Ctrl+C or
**Copy selected rows** to copy selection. Column visibility/order/width persist
in `.almproj.user`, without changing source or compiled output. Summary language
is independent of build culture selection.

Custom scalar columns use their declared labels and visibility. Collections have
separate columns with List/Concatenated presentation or rebuilt complex-record
templates. Cells initially show up to ten entries with expansion; copy includes
the complete collection. Summary templates inspect data and are independent of
document/PDF layout. There is no document/PDF export claim here.

## Minimal example

Open the prepared default playground, choose North in Alarm Summary and `en` in
Summary language. Select `Plant.Pump.PressureLow`.

### Expected result

The summary shows Name `North pump pressure low`. Choose South and the corresponding
alarm to see `South pump pressure low`. Source XML keeps its localization expression.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Summary differs from open shared document | Different selected project | Select matching project/context and language. |
| Localization tokens displayed | Keyword language selected | Choose a real culture with saved translations. |
| Columns return on another machine | Preferences are local user state | Set the layout in that checkout's user settings. |

## Related features and tutorials

See [edit and inspect](../tutorials/edit-and-inspect-alarms.md),
[localization](localization.md), and [custom definitions](custom-fields-and-collections.md).
