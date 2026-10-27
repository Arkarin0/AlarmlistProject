# Exchange translations with XLIFF

Export a translation job, review a returned change and explicitly apply it to RESX.

## Prerequisites and starting files

Complete [translate alarms](translate-alarms.md), keeping the saved English Ready
and German Bereit resource entries. Commands run from the repository root in a
terminal. Use a fresh `example/FirstAlarm/xliff` directory, or preserve earlier
jobs and select a new output directory. No vendor-tool compatibility is assumed.

## Steps

### 1. Export the German job

```powershell
dotnet msbuild example/FirstAlarm/Plant.almproj /t:ExportAlarmlistXliff /p:AlarmlistXliffTargetCultures=de
```

Expected result: `xliff/Alarmlist.en.de.xlf` and
`xliff/Alarmlist.en.de.xlf.baseline.xml` appear under FirstAlarm.
Keep the baseline locally and unchanged; give the translator only the XLIFF.

### 2. Prepare a returned translation

For this exercise, open the XLIFF in a text editor and find the unit whose source
text is Ready. Change only its existing target text from Bereit to Einsatzbereit.
Preserve unit IDs, namespace, structure and any protected content. Save the XLIFF.

Expected result: the returned file differs in one target value; the baseline and
RESX remain unchanged.

### 3. Review a dry run

```powershell
dotnet msbuild example/FirstAlarm/Plant.almproj /t:ImportAlarmlistXliff /p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf
```

Expected result: the import reports the Ready change from Bereit to Einsatzbereit
without writing the resource file. Review every proposed change before continuing.

### 4. Apply and rebuild

Save or discard pending edits and close all affected RESX editors, including XML
views, in every Visual Studio instance and any other editor. Then run:

```powershell
dotnet msbuild example/FirstAlarm/Plant.almproj /t:ImportAlarmlistXliff /p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf /p:AlarmlistXliffApply=true
dotnet msbuild example/FirstAlarm/Plant.almproj /t:Build
```

Expected result: a fresh import preflight passes, the saved German Ready value
changes, and the subsequent build updates the de-DE output.

## Verify the result

The German RESX and de-DE compiled Name contain Einsatzbereit. English still shows
Ready. Repeating apply without intervening changes is a no-op. Reopen resource
editors only after apply finishes.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Export refuses existing job | Overwrite protection | Preserve old baseline and export to a fresh job directory. |
| Import conflicts | Source/target changed after export | Review local edits and export a fresh baseline/job. |
| Apply refuses an open/inaccessible IDE | Resource editor guard | Close affected resource editors and resolve inaccessible IDE instances. |

## Next steps and related features

See [XLIFF exchange](../features/xliff-translation-exchange.md) for batch imports,
destination selection and failure behavior.
