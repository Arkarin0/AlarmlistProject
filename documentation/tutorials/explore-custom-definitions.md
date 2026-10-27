# Explore custom fields and collections

Open an isolated rebuilt toolset with typed fields and collections, then compare
an explicit procedure-disabled definition.

## Prerequisites and starting files

Use Git, repository .NET tooling and Visual Studio with MSBuild. Start from the
checked-in `example/CustomDefinition` definition and samples. Commands run from
the repository root. The launcher defaults to Visual Studio 2026; add
`-VisualStudioVersion 2022` for that host. Close an existing AlarmlistDefinitionExp
playground before deploying another run to that profile.

## Steps

### 1. Build and open the isolated playground

```powershell
scripts/start-custom-definition.bat
```

Expected result: a fresh isolated copy under `artifacts/def11` has matching compiler,
SDK and extension tools and sample outputs. The launcher opens the generated
solution in AlarmlistDefinitionExp. The standard definition and AlarmlistExp
deployment are preserved. Use `-PrepareOnly` to prepare without opening the IDE.

### 2. Inspect typed values and collection inheritance

Open the generated Alarms.almx and select Plant.Check. Inspect Tags, Numbers and
Checks; then inspect Plant.Defaults. Try changing a bounded Code or Count to 11,
committing the draft, and then Undo. Collapse/expand an entry while drafting text.

Expected result: invalid bounds produce diagnostics, inherited entries stay
read-only, and collapsing preserves drafts. Defaults supply effective values
without appearing as newly authored source. Clear excludes preceding entries.

### 3. Inspect summary presentation and captions

Open Alarm Summary and compare Tags, Measurements and Inspection checks. Expand
a collection cell if needed and copy its contents. Choose en, then de-DE.

Expected result: Tags are concatenated, Measurements form a numbered list, and
Checks use the rebuilt record template. Full collection copying includes entries
hidden by cell collapse. The supplied German catalogue intentionally lacks
Priority.Low, so its fallback caption and ALM0031 warning demonstrate missing-key
behavior. Stored enum tokens remain Low/High.

### 4. Open the procedure-disabled variant

Save wanted edits in the existing playground, close that experimental IDE, then run:

```powershell
scripts/start-custom-definition.bat -DefinitionFile example/CustomDefinition/AlarmDefinition.NoProcedures.xml -SampleFile example/CustomDefinition/Alarms.NoProcedures.xml
```

Expected result: a new isolated run has no procedure editor or built-in Test
method column. The ordinary custom TestMethod field remains. The launcher uses
the explicit companion source and does not strip a user's content automatically.

## Verify the result

Use the run's `playground.json` and build logs to locate the selected definition,
generated workspace and outputs. Compare both capability variants. Older runs
and their edits remain intact; edits made in a generated run are not automatically
copied back to the original definition/sample.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Editing copied definition does not change tools | Schema is compiled at build time | Edit the original selected definition and rerun with matching sample inputs. |
| Procedure content fails with disabled variant | Definition/source mismatch | Select the supplied NoProcedures pair or explicitly adapt your source. |
| Custom schema not recognized in default playground | Different toolset | Use the generated isolated solution and matching extension/SDK. |

## Next steps and related features

See [custom fields and collections](../features/custom-fields-and-collections.md),
[the playground guide](../../example/CustomDefinition/README.md), and
[definition authoring](../../eng/tools/AlarmDefinitionGenerator/README.md).
