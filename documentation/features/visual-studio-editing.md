# Visual Studio alarm editing

Edit alarms through Designer, Split and XML modes backed by one document buffer,
with inherited-value navigation and live diagnostics.

## When to use it

Use the designer for structured changes and source navigation, and XML mode for
direct source edits or reviewing the representation of a designer change.

## Requirements and availability

Install the matching extension in Visual Studio 2022 17.9+ or 2026 and make its
SDK version available. Incompatible toolsets retain XML editing and drafts but
block designer changes until compatible analysis arrives.

## How it works

The editor separates file and alarm scopes. Inherited fields and entries show
their origins; equal text alone does not make a local value inherited. Use
reference selection and source navigation to inspect a reference chain.
Expression fields retain raw source while allowing effective-value display.

Enter or moving to another designer field commits; Escape cancels a draft.
Moving to a toolbar, switching view/context, and collapse actions preserve private
drafts. Undo reverses committed edits. Focus or analysis refresh does not write
inherited/default values into source. Literal helpers change drafts only when
explicitly selected. Document edits are source-preserving transactions.

Procedure/collection groups have collapse controls and bounded sticky headings
within the alarm scroll area. Procedure search and collapse are view state.
Inherited entries remain read-only; explicit override/copy actions create local
content. Drafts are distinct from unsaved committed document-buffer edits.

## Minimal example

Open the prepared [default playground](../../example/README.md), then
`North/Alarms.almx`. Select `Plant.Utility.AirPressureLow` and edit Code to
`U-002`, committing with Enter. Switch to Split mode.

### Expected result

XML shows `<Code>U-002</Code>` and live analysis uses the buffer without requiring
Save. Undo restores the prior value. Compiled files change only after Build.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Field cannot be edited | Inherited projection or incompatible analysis | Use explicit local override where available; resolve SDK compatibility. |
| Text not in XML yet | Still a private draft | Commit with Enter, or Escape to cancel. |
| Disk output unchanged | Live analysis does not write build output | Save intended edits and Build. |

## Related features and tutorials

Use [Explain this value](value-explanations.md) to inspect a designer value's
compiler contributors in Alarm Summary. Commit or cancel private drafts first;
the action never commits a draft.

Use [Find references and rename](references-and-rename.md) beside a supported value
or identity to inspect its consumers or preview coordinated unsaved changes.

See [edit and inspect alarms](../tutorials/edit-and-inspect-alarms.md),
[Alarm Summary](alarm-summary.md), and [completion](xml-completion.md).
