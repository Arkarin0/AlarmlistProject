# Edit and navigate expression tokens

Color, inspect and rename a parameter inside an alarm expression. Keep a private
draft separate from committed XML, then save and build the coordinated result in
three cultures.

## Prerequisites and starting files

Use the matching Alarmlist SDK and extension in Visual Studio 2022 17.9+ or 2026.
Create a fresh **Alarmlist** project named **Plant**. Work in that project directory
and replace its sample `Alarms.almx`; keep existing work in another project.

Use **Open With > XML (Text) Editor** for the RESX files in this tutorial. Direct
RESX XML commands are supported; commands in the standard resource table remain
unavailable.

## Steps

### 1. Create the alarm and translations

Put this exact sample in `Alarms.almx`:

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>A</FullyQualifiedName>
    <Properties>
      <Property Name='P' Value='one'/>
      <Property Name='Q' Value='two'/>
    </Properties>
    <Name>plain $(P) $(loc:Message, X=$(Q)) $$(Escaped)</Name>
    <Code>001</Code>
    <Description>Line $(P)</Description>
  </Alarm>
</Alarmlist>
```

Create `Labels.en.resx`:

```xml
<root>
  <data name='Message'><value>Value {X}</value></data>
</root>
```

Create `Labels.de-DE.resx`:

```xml
<root>
  <data name='Message'><value>Wert {X}</value></data>
</root>
```

Add this property group before the closing `Project` element in `Plant.almproj`:

```xml
<PropertyGroup>
  <AlarmlistCultures>keyword;en;de-DE</AlarmlistCultures>
</PropertyGroup>
```

Save the files and build. Reopen the solution if project changes have not finished
reevaluating. The SDK includes these ALMX and RESX files automatically.

Expected result: one alarm is built into each of `bin/Debug/keyword`,
`bin/Debug/en` and `bin/Debug/de-DE` as `Plant.Alarmlist.xml`.

### 2. Inspect an authored token

Open `Alarms.almx` in **Designer**, select alarm **A** and focus **Name**.
The native text editor shows the authored expression. Parameters, localization
and punctuation have separate formats; `plain` and `$$(Escaped)` stay literal.
Move focus to **Filter alarms** to see the resolved value, then return to Name.

Place the caret in the first `$(P)` and press F12, or open its context menu and
choose **Go to Definition**. Navigation follows **Tools > Options > Alarmlist >
Navigation**: Designer selects parameter P; XML selects its declaration. Return to
Name and open the menu at the nested `$(Q)`. Choose **Find All References**.

Expected result: **Alarmlist References** identifies parameter **Q**. The nested
reference has its own identity. No source or build output changes. **Explain this
value** in the value menu inspects the entire containing Name.

### 3. Try a private draft

Hide References, return to Name and replace its text with
`draft $(P) $$(Escaped)`. Open the token menu at P.

Expected result: lexical coloring still works, while semantic actions require
commit or cancellation. The XML buffer and disk source are unchanged. Close the
menu and press Escape to cancel the draft and restore the authored sample.

### 4. Review a coordinated parameter rename

Right-click the first `$(P)` and choose **Rename…**. In the separate dockable
**Alarmlist Rename** window, enter **RenamedP**, then **Preview changes**.
Review P's declaration and its uses in Name and Description.
Acknowledge any displayed partial-coverage or diagnostic warning only after
reviewing it, then **Apply**.

Expected result: the three occurrences change in the unsaved ALMX buffer. The
literal value `one`, parameter Q, resource key Message and escaped text are
unchanged. Files on disk and existing output remain unchanged.

Activate the ALMX document and Undo once to restore all three occurrences. Redo
reapplies the coordinated changes. Undo again, return to Name and repeat Rename
with the final name **Position**.

### 5. Save and build

Save All and build the project. The declaration now uses `Name='Position'`, and
both expression uses are `$(Position)`. The translations remain unchanged.

## Verify the result

Read Name in each `Plant.Alarmlist.xml`:

| Culture directory | Expected Name text |
| --- | --- |
| `keyword` | `plain one $(loc:Message, X=$(Q)) $(Escaped)` |
| `en` | `plain one Value two $(Escaped)` |
| `de-DE` | `plain one Wert two $(Escaped)` |

Description is `Line one` in all three outputs. Renaming changes the bound
identity and preserves the resolved value. The escape emits literal `$(Escaped)`.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Commands are disabled | A draft or analysis/context change is pending | Commit or cancel the draft, wait for current analysis, then reopen the token menu. |
| The field shows resolved text | It is unfocused | Focus Name to edit and target authored tokens. |
| Preview becomes obsolete | Captured inputs or the project catalogue changed | Find the same token again and create a fresh preview; stale Apply remains disabled. |
| A translated value has not changed | Resources use saved inputs | Save the intended RESX changes before downstream analysis/build. |

## Next steps and related features

Read [Visual Studio editing](../features/visual-studio-editing.md),
[Find references and rename](../features/references-and-rename.md) and
[Trace an inherited value](explain-a-value.md). For localization-key references,
open a key in RESX XML and use the standard semantic commands described in the
[references guide](../features/references-and-rename.md).
