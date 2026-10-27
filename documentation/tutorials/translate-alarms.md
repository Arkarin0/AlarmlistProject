# Translate alarms into English and German

Build keyword, English and German outputs from a shared localization key.

## Prerequisites and starting files

Complete [your first build](build-your-first-alarmlist.md). Use FirstAlarm as an
exercise project with the original single Plant.Ready alarm. If you completed the
inheritance tutorial, restore the first tutorial's ALMX before starting. Run
commands from the repository root.

## Steps

### 1. Select output cultures

Replace `example/FirstAlarm/Plant.almproj` with:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK">
  <PropertyGroup>
    <AlarmlistCultures>keyword;en;de-DE</AlarmlistCultures>
  </PropertyGroup>
</Project>
```

Expected result: the next build requests three independent culture outputs.

### 2. Add the resource strings

In Alarms.almx change Name to `<Name>$(loc:Ready)</Name>`. Save
`Translations.en.resx` beside the project:

```xml
<root>
  <data name="Ready" xml:space="preserve"><value>Ready</value></data>
</root>
```

Save `Translations.de.resx`:

```xml
<root>
  <data name="Ready" xml:space="preserve"><value>Bereit</value></data>
</root>
```

These are minimal compiler-readable string resources. For editing in Visual
Studio's resource grid, create files with **Add > New Item > Alarmlist >
Translations (RESX)** and enter the same key/value; that template adds the standard
resource-editor headers.

Expected result: Ready exists once per language. No de-DE resource is needed
because de-DE can use its de parent.

### 3. Build all languages

```powershell
dotnet msbuild example/FirstAlarm/Plant.almproj /t:Build
```

Expected result: three Plant.Alarmlist.xml files appear under
`example/FirstAlarm/bin/Debug/keyword`, `en` and `de-DE`.

## Verify the result

Name is `$(loc:Ready)` in keyword, `Ready` in English and `Bereit` in de-DE.
The ALMX retains `$(loc:Ready)`. No English fallback was needed for German.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Missing key in de-DE | Key absent/empty in de and de-DE | Save a nonempty Ready value in the German resource. |
| Duplicate key | Two files define Ready in the same culture | Keep exactly one definition per culture. |
| Resource editor cannot open minimal XML | Standard headers absent | Use the Translations item template as explained in step 2. |

## Next steps and related features

Continue with [translation exchange](exchange-translations.md).
See [localization](../features/localization.md) for fallback and saved-input rules.
