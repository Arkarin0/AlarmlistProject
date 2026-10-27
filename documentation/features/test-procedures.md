# Test procedures and named groups

Attach ordered operator instructions to an alarm, with inherited groups and local
additions or replacements.

## When to use it

Use procedures for verification, reset and other repeatable operator actions.

## Requirements and availability

The standard definition enables TestProcedure. A custom toolset can omit it;
procedure XML is then invalid for that toolset.

## How it works

Groups have stable case-insensitive identities and optional expression-capable
titles. Inherited groups come first; matching local groups append entries. New
local groups follow inherited groups. An absent title inherits; an explicitly
empty title displays the ID and stops title inheritance. Otherwise the ID is the
fallback. Empty groups retain their headings.

`Clear` excludes earlier inherited and local entries in that group. Entries after
Clear remain. The designer displays excluded entries separately. View actions
such as collapse and search preserve drafts and do not modify source.

## Configuration or syntax

Use `Group Name`, optional `Title`, and ordered `Step`, `Hint`, `Warning`, `Note`
and `Clear` elements. IDs start with a letter/underscore and then permit ASCII
letters, digits, underscores, dots and hyphens. Old direct Instructions/Reset
elements are rejected; use named groups instead.

Saved `.editorconfig` may set creation-only defaults:

```ini
[*.almx]
alarmlist_procedure.default_groups = Instructions, Reset, Verification
alarmlist_procedure.group.verification.title = Verification
```

Missing/unset lists create Instructions and Reset; an empty list creates none.
Invalid settings block designer alarm creation. Changes do not rewrite existing
or duplicated alarms, and builds do not inject these defaults.

## Minimal example

Inside an alarm in an SDK application project:

```xml
<TestProcedure>
  <Group Name="Verification">
    <Title>Verify $(AlarmlistAlarmCode)</Title>
    <Step>Check the indicator.</Step>
    <Warning>Follow the site's safety procedure.</Warning>
  </Group>
</TestProcedure>
```

### Expected result

For an alarm with Code `001`, the resolved group title is `Verify 001`. The step
precedes the warning. Resolved XML uses Group and TestProcedureItem elements;
it is not the same serialization as source XML.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Earlier instructions disappear | Clear excludes preceding entries | Inspect Excluded by Clear and move/remove the local directive as intended. |
| New defaults do not alter existing alarms | Defaults apply only during creation | Edit existing groups explicitly. |
| Procedure elements rejected | Definition omits capability or uses legacy syntax | Use a compatible toolset and named Group syntax. |

## Related features and tutorials

See [inheritance](alarm-inheritance-and-expressions.md),
[designer editing](visual-studio-editing.md), and
[author reusable alarms](../tutorials/author-reusable-alarms.md).
