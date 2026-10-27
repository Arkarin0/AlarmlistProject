# Build your first alarmlist

Create a minimal application and produce resolved XML containing one alarm.

## Prerequisites and starting files

Use the repository checkout, its .NET tooling and an available matching SDK
package. Run preparation commands from the repository root. This development
workflow builds local tools; it does not require a published SDK version.

## Steps

### 1. Prepare the repository SDK

```powershell
dotnet build src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/prepare-example.ps1
```

Expected result: the extension/SDK build succeeds, and preparation selects the
matching SDK/feed for `example/`. It preserves edited example sources/projects.
See the [playground guide](../../example/README.md) for experimental IDE launchers.

### 2. Create an isolated application under the prepared example root

Create a new `example/FirstAlarm/` directory. Add `Plant.almproj`:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK" />
```

Add `Alarms.almx` beside it:

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Plant.Ready</FullyQualifiedName>
    <Name>Ready</Name>
    <Code>001</Code>
    <Category>Status</Category>
    <Description>The plant is ready.</Description>
  </Alarm>
</Alarmlist>
```

Expected result: the new project inherits the prepared example SDK selection.
It is independent of North/South and does not need to be added to their solution.
It also inherits the example's analyzer registration and saved warning policy;
the general sample rule is suppressed for these exercise files.

### 3. Build the project

From the repository root:

```powershell
dotnet msbuild example/FirstAlarm/Plant.almproj /restore /t:Build
```

Expected result: `example/FirstAlarm/bin/Debug/keyword/Plant.Alarmlist.xml` exists.

## Verify the result

Open that XML file. Its root is `AlarmList`, with one Alarm identified as
`Plant.Ready`, Name `Ready`, Code `001`, Category `Status`, and the description
entered above. The authored file remains unchanged.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| SDK cannot resolve | Preparation failed or project is outside prepared hierarchy | Complete step 1 and keep FirstAlarm under example. |
| Error from an unexpected ALMX file | Default recursive inclusion | Keep only intended inputs inside FirstAlarm, or use explicit Compile membership. |
| Build fails but old output remains | Output preservation on failure | Correct the reported problem and rebuild before inspecting results. |

## Cleanup

When finished with the following tutorials, remove only the `example/FirstAlarm`
directory you created. Keep the existing playground projects and preparation files.

## Next steps and related features

Continue with [reusable alarms](author-reusable-alarms.md) or
[translations](translate-alarms.md). See [project builds](../features/project-builds.md)
for output and membership settings.
