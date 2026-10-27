# Author reusable alarms and procedures

Create an abstract template, resolve it in a concrete alarm and replace inherited
procedure entries with Clear.

## Prerequisites and starting files

Complete [your first build](build-your-first-alarmlist.md). Use its
`example/FirstAlarm/Plant.almproj` and the standard definition with procedures.
Save a copy of Alarms.almx if you want to retain the original exercise.
Commands below run from the repository root.

## Steps

### 1. Define a template and child

Replace this exercise's `Alarms.almx` with:

```xml
<Alarmlist>
  <Properties><Property Name="Site" Value="North" /></Properties>
  <Alarm abstract="true">
    <FullyQualifiedName>Template.Ready</FullyQualifiedName>
    <Name>$(Site) ready</Name>
    <Code>001</Code>
    <TestProcedure>
      <Group Name="Verification">
        <Title>Verify $(AlarmlistAlarmCode)</Title>
        <Step>Check the indicator.</Step>
      </Group>
    </TestProcedure>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Plant.Ready</FullyQualifiedName>
    <ReferenceName>Template.Ready</ReferenceName>
    <TestProcedure>
      <Group Name="Verification"><Note>Record the result.</Note></Group>
    </TestProcedure>
  </Alarm>
</Alarmlist>
```

Expected result: the child inherits Name/Code and the Verification step; its Note
follows the inherited step.

### 2. Build and inspect inheritance

```powershell
dotnet msbuild example/FirstAlarm/Plant.almproj /t:Build
```

Expected result: the keyword output contains one concrete alarm, `Plant.Ready`,
with Name `North ready`, Code `001`, and group title `Verify 001`.

### 3. Replace the inherited procedure body

Replace the child's Group with:

```xml
<Group Name="Verification">
  <Clear />
  <Step>Check the local panel.</Step>
</Group>
```

Save and repeat the build command.

Expected result: the inherited heading remains, but the effective group contains
only the local step. Clear is a source directive, not an operator instruction.

## Verify the result

The output has one alarm and one effective Verification item, with text
`Check the local panel.` The template itself is not emitted. Source still contains
the reference and raw expressions rather than copied resolved values.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Two alarms emitted | Template not abstract | Restore `abstract="true"` on Template.Ready. |
| No inherited heading | Group identity changed | Use the same Verification identity. |
| Parameter unassigned after moving files | Assignments use the consuming file | Supply Site in the child's file or project. |

## Next steps and related features

See [inheritance and expressions](../features/alarm-inheritance-and-expressions.md),
[procedures](../features/test-procedures.md), and [library reuse](reuse-alarm-libraries.md).
