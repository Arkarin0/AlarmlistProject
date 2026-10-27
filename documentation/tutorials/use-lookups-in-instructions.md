# Use lookups in translated instructions

Insert a named-list value into an English instruction without duplicating the
wire identifier in every procedure.

## Prerequisites and starting files

Complete [translate alarms](translate-alarms.md). FirstAlarm must have its
Plant.Ready alarm, standard procedure support and English/German resource files.
Commands run from the repository root.

## Steps

### 1. Add a saved lookup list

Create `example/FirstAlarm/Wires.almlookup`:

```xml
<Lookup xmlns="urn:alarmlist:lookup:1">
  <List Key="Wires">
    <Entry Key="Outlet" Value="=EC-120W1" Description="Outlet wire" />
  </List>
  <Template Key="Disconnect">
    <Argument Name="wire" List="Wires" />
  </Template>
</Lookup>
```

Expected result: the project includes the catalogue through its default lookup
glob. The argument binding offers list guidance to compatible editor completion.

### 2. Add the sentence in both resources

Inside the English RESX root add:

```xml
<data name="Disconnect" xml:space="preserve">
  <value>Disconnect wire {wire}.</value>
</data>
```

Inside the German RESX root add:

```xml
<data name="Disconnect" xml:space="preserve">
  <value>Leitung {wire} trennen.</value>
</data>
```

Expected result: both translations declare the same wire placeholder.

### 3. Use the template in the alarm

Add this TestProcedure inside Plant.Ready, then save all exercise inputs:

```xml
<TestProcedure>
  <Group Name="Verification">
    <Step>$(loc:Disconnect, wire=$(list:Wires#Outlet))</Step>
  </Group>
</TestProcedure>
```

Run `dotnet msbuild example/FirstAlarm/Plant.almproj /t:Build`.

Expected result: the build resolves the list entry and passes it as literal text
to each translated sentence.

## Verify the result

The English step reads `Disconnect wire =EC-120W1.` The de-DE step reads
`Leitung =EC-120W1 trennen.` Keyword output preserves the expression. The wire's
punctuation is unchanged; its Description is guidance rather than substituted text.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| ALM0021 | Wrong list/entry identity | Match Wires and Outlet in the saved catalogue. |
| ALM0022 | Placeholder mismatch | Use wire consistently in RESX and the localization call. |
| Duplicate procedure element | Alarm already has a procedure | Add the Group inside its existing TestProcedure instead. |

## Next steps and related features

See [lookups and templates](../features/lookups-and-instruction-templates.md) and
the [HMI catalogue specification](../../src/VisualStudio/HMI-LOOKUPS.md) for paths,
properties, buttons and metadata.
