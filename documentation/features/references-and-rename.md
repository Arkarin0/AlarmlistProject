# Find references and rename

Find All References shows the declarations and uses of an Alarmlist symbol across
the known solution and its reachable dependencies. Rename previews coordinated
changes before applying them to unsaved editor buffers.

## When to use it

Use references to inspect an alarm's consumers, parameter assignments, translated
keys, lookup identities or procedure groups. Use Rename when related declarations,
translations and overrides should keep their binding after a name changes.

## Requirements and availability

Use matching Milestone 14 SDK and extension builds in Visual Studio 2022 or 2026.
Apply or cancel private designer drafts before invoking either action. Choose an
explicit consumer for shared XML when its ownership is ambiguous.

## How it works

Designer and Alarm Summary values offer Find references and Rename actions.
Alarm and procedure-group headings also offer actions for their identities.
A value containing several referenced symbols presents a compiler-resolved choice.
In XML, place the caret within or immediately after a supported token and use the
standard Find All References or Rename command. Other tokens retain native XML
command behavior. Moving the caret during discovery cancels that request; after
publication, the results retain the resolved symbol until inputs or context change.

References groups results by definition, physical source and consumer context.
Source links open editable files or read-only package content. Coverage findings
identify unavailable projects, excluded configurations, ambiguous bindings and
immutable definitions. Search validates active configurations and all configured
cultures; it cannot discover unknown external consumers.

Enter the new name in Rename and choose **Preview rename changes**. The preview
shows before/after text, skipped findings, conflicts and diagnostic differences.
You can exclude editable changes and revalidate the resulting selection. Partial
coverage, exclusions, remaining references and expected new diagnostics require
explicit acknowledgement. Invalid names, collisions, overlapping replacements and
incompatible shared-consumer meanings cannot apply as approved changes.

Apply opens affected files, checks editability and freshness, and changes the
approved set as one linked Undo operation. A failure restores touched buffers.
Files remain unsaved. One Undo or Redo covers the complete applied transaction.
Save resource, lookup and dependency changes before ordinary downstream analysis
can reflect them; preview validation temporarily includes those proposed changes.
Shared committed unsaved ALMX buffers already participate in search.

Comments and expression-like literals remain unchanged. Built-in names, generated
definition members and package definitions are searchable but immutable. File,
project and schema-definition renaming are outside this feature.

## Minimal example

Put these two alarms in an application's ALMX source:

```xml
<Alarmlist>
  <Alarm abstract="true">
    <FullyQualifiedName>Base</FullyQualifiedName>
    <Name>Reference sample</Name>
    <Code>001</Code>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Child</FullyQualifiedName>
    <ReferenceName>Base</ReferenceName>
    <Description>keep literal Base</Description>
  </Alarm>
</Alarmlist>
```

### Expected result

Find references on the `Base` identity includes its declaration and the child's
inheritance reference. Previewing `RenamedBase` changes those two tokens and keeps
`keep literal Base` intact. Apply leaves the changes unsaved; one Undo restores both.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Results or preview become obsolete | Inputs, context or ownership changed | Invoke the action again on current analysis |
| XML requests an explicit consumer | Shared ownership is ambiguous | Select the consuming project in the XML context selector |
| A resource edit is absent from ordinary results | Resources use saved inputs | Save the resource and wait for analysis |
| Apply is disabled | Preview needs validation or acknowledgement, or contains a blocking conflict | Review findings, adjust selected changes and preview again |
| A known token is unavailable | Parsing or binding prevented a reliable identity | Resolve the reported diagnostics; no replacement is guessed |

## Related features and tutorials

- [Inheritance and expressions](alarm-inheritance-and-expressions.md)
- [Shared projects](shared-projects.md)
- [Visual Studio editing](visual-studio-editing.md)
- [Explain this value](value-explanations.md)
