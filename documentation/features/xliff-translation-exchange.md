# XLIFF translation exchange

Export saved RESX strings as XLIFF 2.0 and review returned translations before
applying them with conflict checks.

## When to use it

Use exchange files when a translator works outside the project's RESX editor.

## Requirements and availability

Use the SDK targets from a terminal and saved source/target RESX files. Vendor
round trips with Trados Studio, memoQ and Phrase TMS remain unverified; the
supported exchange profile is narrower than arbitrary XLIFF 2.0.

## How it works

Export creates an XLIFF and adjacent `.xlf.baseline.xml` per language pair. Send
only XLIFF to translators and keep the baseline unchanged. Every key needs a
nonempty exact-source-culture translation. Exchange does not use culture fallback.
Protected expressions and named placeholders may reorder but cannot change,
disappear or duplicate. Existing exchange files are protected by default.

Import defaults to a dry run. Apply repeats validation against current saved
inputs; conflicts reject the batch. Missing targets leave values unchanged;
explicit empty targets supply empty values. Existing keys stay in their resource
files. New keys need an existing target resource, with an explicit destination if
ambiguous. Reimporting applied values is a no-op. Normal installation failures
roll back, but this is not a crash-atomic transaction. Build/Clean do not exchange
translations or delete the exchange files.

## Configuration or syntax

| Property | Purpose | Default |
| --- | --- | --- |
| `AlarmlistXliffSourceCulture` | Source language | `en` |
| `AlarmlistXliffTargetCultures` | Export destinations | Required for export |
| `AlarmlistXliffOutputPath` | Exchange directory | Project's `xliff` directory |
| `AlarmlistXliffOverwrite` | Replace existing exchange | `false` |
| `AlarmlistXliffImportFiles` | Returned exchanges | Explicit selection |
| `AlarmlistXliffApply` | Write import results | `false` |

## Minimal example

For `Plant.almproj` with saved English strings and a German resource file, run
from the project directory:

```powershell
dotnet msbuild Plant.almproj /t:ExportAlarmlistXliff /p:AlarmlistXliffTargetCultures=de
dotnet msbuild Plant.almproj /t:ImportAlarmlistXliff /p:AlarmlistXliffImportFiles=xliff/Alarmlist.en.de.xlf
```

### Expected result

Export writes the exchange and baseline. Import reports changes without writing
RESX. Applying requires a separate invocation with `/p:AlarmlistXliffApply=true`.
Save/discard pending changes and close all affected RESX editors first. Open or
inaccessible Visual Studio resource editors block apply; close other editors too.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Export refuses existing files | Overwrite protection | Use a new job directory, preserving existing baselines. |
| Import rejects a conflict | Source/target changed since export | Review changes and prepare a fresh exchange rather than editing the baseline. |
| Apply blocked | Destination editor open or IDE inaccessible | Close affected editors and resolve inaccessible instances, then rerun preflight. |

## Related features and tutorials

See [exchange translations](../tutorials/exchange-translations.md),
[localization](localization.md), and the
[detailed exchange contract](../../src/SDK/Alarmlist.SDK/README.md#xliff-translation-import).
