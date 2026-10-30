# Find and rename an inherited alarm

Find an alarm's declaration and consumers, review a coordinated rename, and apply
it as one unsaved edit. Verify that literal text stays unchanged and that Undo
restores both files.

## Prerequisites and starting files

Use a standard Alarmlist application with matching Milestone 14 SDK and extension
builds in Visual Studio 2022 or 2026. See
[Build your first alarmlist](build-your-first-alarmlist.md) for project preparation.
The standard definition supplies Name, Code and Description.

Create and save `ReferenceBase.almx` in the application's directory:

```xml
<Alarmlist>
  <Alarm abstract="true">
    <FullyQualifiedName>ReferenceDemo.Base</FullyQualifiedName>
    <Name>Reference sample</Name>
    <Code>001</Code>
  </Alarm>
</Alarmlist>
```

Create and save `ReferenceChild.almx` beside it:

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>ReferenceDemo.Child</FullyQualifiedName>
    <ReferenceName>ReferenceDemo.Base</ReferenceName>
    <Description>Keep literal ReferenceDemo.Base</Description>
  </Alarm>
</Alarmlist>
```

The default SDK includes both files automatically. Keep any existing application
sources; these tutorial identities must be unique. Build once before continuing.
Apply or cancel private designer drafts. For shared files, select the intended
consumer and language before invoking a semantic action.

## Steps

### 1. Find the base alarm's consumers

Open `ReferenceBase.almx` in Designer mode and select `ReferenceDemo.Base`.
Choose **Find alarm references** beside its identity. Its accessible name is
**Find references to this alarm**. You can focus the button with Tab and activate
it with Space or Enter.

Expected result: Alarmlist References shows an Alarm definition for
`ReferenceDemo.Base`, grouped by physical file and consumer context. Its findings
include the declaration and `ReferenceChild.almx`'s inheritance reference.
Search coverage reports unavailable projects and configurations that were not
validated. Equal text in Description is absent from the reference findings.

### 2. Navigate to a contributing source

Expand `ReferenceChild.almx` and its consumer context, then use **Go to source**
for the inheritance finding.

Expected result: Visual Studio opens that physical source using your configured
navigation preference. No text changes. Opening a previously closed buffer can
invalidate the old search snapshot. Return to the base alarm and invoke
**Find alarm references** again before continuing.

### 3. Review the rename

Expand **Rename** in Alarmlist References. Enter `ReferenceDemo.Template` as
the new name and choose **Preview changes**.

Expected result: the preview proposes two token changes: the base declaration
and the child's ReferenceName. It opens affected files into buffers before the
final review. Inspect before/after text, search coverage, skipped findings and
diagnostic differences. The child's identity and literal Description stay intact.

If the preview requires acknowledgement, read its findings and select the
acknowledgement checkbox. For this complete two-file exercise, retain both changes.
Excluding a change requires **Validate selection** and can produce expected
diagnostics that need explicit acknowledgement before Apply.

### 4. Apply and inspect the unsaved result

Choose **Apply**.

Expected result: the base FullyQualifiedName and child ReferenceName both become
`ReferenceDemo.Template`. The child's FullyQualifiedName remains
`ReferenceDemo.Child`, and Description remains `Keep literal ReferenceDemo.Base`.
Both edited files remain unsaved. Their filenames stay unchanged.

### 5. Verify linked Undo and Redo

Activate either affected source and use **Edit > Undo** once, then **Edit > Redo**
once.

Expected result: one Undo restores both original tokens; one Redo restores both
renamed tokens. Literal text stays unchanged throughout. Changes are still unsaved.

### 6. Save and build

Save both affected files and Build the application.

Expected result: the tutorial's concrete `ReferenceDemo.Child` still inherits
Name `Reference sample` and Code `001`. Its reference now resolves through
`ReferenceDemo.Template`. Other application alarms are unaffected.

## Verify the result

Inspect both sources, then use Alarm Summary to inspect `ReferenceDemo.Child`.
Its inherited values should match the original build. Find references on
`ReferenceDemo.Template` again to inspect the new identity. The old name should
remain only in the deliberately preserved Description literal.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Results or preview become obsolete | Inputs, context or toolsets changed | Wait for current analysis and invoke the action again |
| Shared XML requests a consumer | Several projects own the source | Choose the intended project in the XML context selector |
| Apply is disabled | Preview is stale, a selection needs validation, acknowledgement is missing, or a conflict blocks it | Review findings and validate the intended selection |
| A resource rename is not reflected in ordinary analysis | Resource/dependency inputs use saved files | Save those affected buffers before downstream analysis |

## Next steps and related features

- [Find references and rename](../features/references-and-rename.md)
- [Shared projects](../features/shared-projects.md)
- [Trace an inherited value to its sources](explain-a-value.md)
