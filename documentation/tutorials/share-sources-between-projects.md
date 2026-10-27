# Share sources between projects

Build shared files under two consumer contexts and choose the correct context
when editing their common physical source.

## Prerequisites and starting files

Use `example/SharedProjects`, the matching extension/SDK build, and Visual Studio
with native Shared Project tooling. Run terminal commands from the repository
root. Preparation preserves edited inputs; expected values describe the unchanged
sample. Deploy matching tools using the normal playground launcher before opening
this solution if needed; never redeploy into an experimental profile still in use.

## Steps

### 1. Prepare and build both consumers

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/prepare-shared-example.ps1
dotnet msbuild example/SharedProjects/North/North.almproj /t:Build
dotnet msbuild example/SharedProjects/South/South.almproj /t:Build
```

Expected result: North and South each produce keyword, English and German outputs.
Shared has no artifact of its own.

### 2. Open the shared document and choose a consumer

Open `example/SharedProjects/SharedProjects.sln` in the prepared experimental IDE.
Open **Shared > Alarms.almx**. Choose North, then South in the designer project
selector. In XML use **Project context** for the same choice.

Expected result: semantic candidates follow the chosen consumer. NorthOnly and
SouthOnly parameter candidates differ. Changing modes preserves the selected
physical document's context and any private drafts.

### 3. Inspect language-specific results

Choose English for summary inspection, selecting the corresponding project in
Alarm Summary as well.

Expected result: Name is `North: Ready` or `South: Ready`; both descriptions are
READY. German names end in Bereit. The shared source still uses Site and loc
expressions rather than consumer-specific literal text.

### 4. Inspect reference ownership

Inspect each consumer's **Dependencies > Shared projects** and the shared
`.projitems` import. Use **Add Reference > Shared Projects** to inspect the
checked reference, then cancel without changing it. Open projected files to see
that they refer to the same physical files owned by Shared.

Expected result: membership belongs to Shared and each consumer has its own
reference. Removing one would preserve files but leave this sample consumer with
no ALMX inputs; it is not necessary to remove it for this walkthrough.

## Verify the result

Compare English consumer XML under their respective `bin/Debug/en` directories.
Both contain Shared.PumpReady, with the consumer-specific names above. Confirm
that Summary's selected project is independent from the editor consumer choice.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Shared project fails to load | Native shared tooling missing | Install that tooling for the chosen IDE host. |
| Completion lacks consumer values | Context not selected/current | Choose North or South and wait for compatible analysis. |
| Selected context vanished after unload | Membership was removed | Reload/re-add the consumer and explicitly choose it again. |

## Next steps and related features

See [shared projects](../features/shared-projects.md),
[completion](../features/xml-completion.md), and [libraries](../features/alarm-libraries.md)
for the separately built distribution alternative.
