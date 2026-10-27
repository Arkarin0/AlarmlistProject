# Reuse alarm libraries through projects and packages

Build two consumers of a transitive library graph, then switch the same sources
to local NuGet packages.

## Prerequisites and starting files

Use the checked-in `example/Libraries` playground with Common, Equipment, LineA
and LineB. Build the matching extension/SDK first as in
[your first build](build-your-first-alarmlist.md). Run commands from the repository
root. Keep any source edits; the expected values below describe unchanged samples.
Close this example in an experimental IDE before switching its reference mode.

## Steps

### 1. Prepare project-reference mode

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/prepare-library-example.ps1 -ReferenceMode Project
dotnet msbuild example/Libraries/LineA/LineA.almproj /t:Build
dotnet msbuild example/Libraries/LineB/LineB.almproj /t:Build
```

Expected result: preparation selects matching SDK/feed settings and restores the
four projects. Builds produce Common/Equipment libraries and consumer XML.

### 2. Inspect consumer-specific resolution

Open each consumer's `bin/Debug/en/<project>.Alarmlist.xml`.

Expected result: each has Common.Stop, Equipment.Pump and its own local alarm.
All three LineA Codes are A-STOP and descriptions are `Inspect line LineA`.
LineB Codes are B-STOP and descriptions are `Check LineB`. Abstract templates
remain referenceable without appearing in the application list.

### 3. Switch to package mode

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/prepare-library-example.ps1 -ReferenceMode Package
dotnet msbuild example/Libraries/Libraries.sln /t:Build
```

Expected result: preparation packs Common/Equipment into its private feed, selects
package references through ignored local settings and restores. No packages are
published; transitive dependencies are separate packages.

### 4. Compare and return to project mode

Compare the same consumer outputs, then run preparation with
`-ReferenceMode Project` again if you want live project references.

Expected result: the resolved alarm values are the same in both modes. Package
consumers need repacking/restoring for subsequent library-source edits.

## Verify the result

Confirm three concrete alarms per English consumer output and the values in step
2. In an IDE with matching tools, Dependencies shows project references in project
mode and Example.Equipment with Example.Common beneath it in package mode.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Package mode cannot resolve | Local packages/feed not prepared | Run package preparation before Build. |
| Different output | Existing playground edits | Compare source/parameter/resource overrides with the sample guide. |
| Library toolset mismatch | Old artifact or package | Rebuild/pack with matching tools and restore. |

## Next steps and related features

See [alarm libraries](../features/alarm-libraries.md) and the
[library playground guide](../../example/Libraries/README.md) for creating your
own Library project and reference/package configuration.
