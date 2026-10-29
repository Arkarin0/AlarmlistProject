# Trace an inherited value to its sources

Use Explain this value to follow an inherited expression, its chosen parameter
assignment and a field dependency. Inspect the result without changing source,
then verify that private drafts and committed changes are handled separately.

## Prerequisites and starting files

Use a standard Alarmlist application project with matching Milestone 14 SDK and
Visual Studio extension builds. The standard definition includes Name and Code;
custom definitions can use different fields. Start with
[Build your first alarmlist](build-your-first-alarmlist.md) if you need a project.

Create `Explain.almx` in that project's directory. The default SDK includes ALMX
files in the project automatically. Save this complete source:

```xml
<Alarmlist>
  <Alarm abstract="true">
    <FullyQualifiedName>Explain.Base</FullyQualifiedName>
    <Name>Position $(Position), code $(AlarmlistAlarmCode)</Name>
    <Properties>
      <Property Name="Position" />
    </Properties>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Explain.Child</FullyQualifiedName>
    <ReferenceName>Explain.Base</ReferenceName>
    <Code>001</Code>
    <Properties>
      <Property Name="Position" Value="1" />
    </Properties>
  </Alarm>
</Alarmlist>
```

## Steps

### 1. Select the consuming alarm

Open `Explain.almx` in the designer. Filter alarms by `Explain.Child` and select
the matching alarm. If several projects own this file, choose the intended
consumer in Project context. Wait for current analysis.

Expected result: Name displays `Position 1, code 001` with inherited-source
information. The abstract Base supplies the expression; Child supplies the values.

### 2. Explain Name

Choose **Explain this value** beside Name. Alarm Summary opens its Selected alarm
view and expands Explanation. You can also use Tab to focus the action and
Space or Enter to activate it.

Expected result: Explanation shows the consuming project/configuration/language,
the raw inherited expression, and resolved/canonical `Position 1, code 001`.

### 3. Follow the contributors

Expand the Field, Inheritance and Parameter steps. Position's chosen declaration
and assignment of `1` are in Explain.Child. The Code field
contributes `001` through AlarmlistAlarmCode. Dependency IDs refer to shared steps;
the same contributor is not copied into a recursive history.

Choose **Go to source** for the Name field step.

Expected result: navigation selects the contributing Name source in Explain.Base,
using your existing designer/XML source-navigation preference. Nothing is saved.
Return to Explain.Child before continuing.

### 4. Check private draft handling

Focus Child's Code field and type `002` without committing it. Explain is disabled
while the designer has private drafts. Open Alarm Summary if it is hidden.

Expected result: the previous explanation clears; it does not incorporate or
commit `002`. Press Escape in Code to cancel the draft. Request Name again to
restore the `Position 1, code 001` explanation.

### 5. Inspect a committed change

Change Code to `002` and press Enter to commit it to the ALMX buffer. Wait for
current analysis and request Name again.

Expected result: the explanation shows `Position 1, code 002` and the Code step
uses the updated value. This committed buffer edit can still be unsaved on disk.
Explain does not build or write exported files. Save and Build only when you want
to persist the source and update build outputs.

## Verify the result

- Explain.Base remains abstract and has no Position assignment.
- Explain.Child has Position `1`; Name is still inherited, without a local Name
  element added by inspection.
- The final explanation resolves Name to `Position 1, code 002`.
- Go to source identifies contributing locations; collapsing steps changes no
  source values.

For a translated example, use the existing default playground after following
[Edit and inspect alarms](edit-and-inspect-alarms.md). Explain North's Name in
`en`, then choose South and explain its Name. Each consumer uses its own selected
resources and context. Keyword mode shows deferred localization instead of
inventing a translation source.

## Troubleshooting

| Problem | Likely cause | Action |
| --- | --- | --- |
| Explain is disabled | Private drafts, a language change or current analysis is pending | Commit/cancel drafts and wait for current analysis |
| The explanation clears | Source, consumer, culture or target changed | Select the current value and request it again |
| Wrong consumer's value | Shared file has a different project context | Choose the intended consumer before step 2 |
| No source link on a step | Context/default value has no captured source location | Inspect its scope and contributing steps |
| Invalid or unavailable result | Expression/binding or required input is invalid | Read the explanation's compiler diagnostics and correct the source |

## Cleanup

Keep the sample for later inspection, or remove only `Explain.almx` and rebuild
the project. Preserve other tutorial files and any changes you want to keep.

## Next steps and related features

- [Explain this value](../features/value-explanations.md)
- [Alarm Summary](../features/alarm-summary.md)
- [Inheritance and expressions](../features/alarm-inheritance-and-expressions.md)
- [Explore custom fields and collections](explore-custom-definitions.md)
