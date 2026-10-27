# Lookups and reusable instruction templates

Use captured lookup catalogues for stable HMI identities and named-list values,
then insert them into translated instructions.

## When to use it

Use lookups to keep device names, wires, PLC symbols and reusable sentence
arguments consistent without copying their displayed text into every alarm.

## Requirements and availability

Save `.almlookup` files in an SDK project. Default inclusion is enabled. HMI
catalogues must be supplied by you or another tool; Alarmlist does not scan HMI code.

## How it works

Lookup values and metadata are literal, preserving punctuation, empty text and
leading zeros. HMI path segments and members use RESX LabelKey translations;
translated paths join segments with ` -> `. Metadata has no automatic default or
numeric validation behavior. Lists merge across files; duplicate/ambiguous entry
identities are errors. Saved lookup inputs feed both builds and completion.

Named localization arguments match the template's placeholders. Quoted arguments
are literal; expression arguments resolve explicitly. Inserted text is never
expanded again. Lookup Template/Argument list bindings guide editor selection but
do not forbid literals or parameters. Keyword output preserves localization and
lookup tokens while still checking referenced catalogue identities.

## Configuration or syntax

| Expression | Meaning |
| --- | --- |
| `$(list:List#Entry)` | Literal named-list entry |
| `$(path:Path)` | Translated HMI path |
| `$(property:Path#Member)` | Translated property label |
| `$(button:Path#Member)` | Translated button label |
| `$(meta:Path#Member#Name)` | Literal member metadata |
| `$(loc:Key, value="001")` | Formatted translation with a literal argument |

For named formatting use `{value}` in RESX; `{{` and `}}` escape literal braces.
See the [full lookup format](../../src/VisualStudio/HMI-LOOKUPS.md) for HMI
Path/Segment/Property/Button and argument-binding declarations.

## Minimal example

Save `Devices.almlookup` beside a project's ALMX file:

```xml
<Lookup xmlns="urn:alarmlist:lookup:1">
  <List Key="Devices">
    <Entry Key="Ready" Value="READY" Description="Ready state" />
  </List>
</Lookup>
```

Use `<Description>$(list:Devices#Ready)</Description>` inside an alarm and build
with a real culture such as `en`.

### Expected result

The resolved description is `READY`. Changing Description guidance on the entry
does not change that Value. Keyword output retains the list expression.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Catalogue rejected | Wrong namespace or invalid XML | Use `urn:alarmlist:lookup:1` and the supported schema. |
| ALM0021 | Referenced identity unavailable | Check exact catalogue keys and included saved files. |
| ALM0022 | Template arguments mismatch | Supply exactly the placeholder names required by RESX. |

## Related features and tutorials

See [use lookups](../tutorials/use-lookups-in-instructions.md),
[localization](localization.md), and [XML completion](xml-completion.md).
