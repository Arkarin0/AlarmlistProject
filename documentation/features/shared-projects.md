# Shared source projects

Include the same physical ALMX, RESX and lookup files in several consumers through
a native Visual Studio Shared Project.

## When to use it

Use shared projects for coordinated source editing with consumer-specific values
and references. Use libraries for separately built/distributed artifacts.

## Requirements and availability

Visual Studio 2022 17.9+ or 2026 needs the matching Alarmlist extension and native
Shared Project tooling. Command-line consumers need the imported `.projitems`
and files, without loading the `.shproj` container.

## How it works

The shared container owns membership and has no compiler/analyzer pipeline or
output artifact. Each consumer compiles its imported files in its own context.
Unsaved shared ALMX text reaches each consumer's live analysis; saved-only rules
still apply to RESX and lookups. Private designer drafts stay private.

An ambiguous shared document requires a consumer choice for semantic editing.
Designer and XML views share that choice; Alarm Summary has its own selection.
Removing/unloading the selected consumer clears the choice, and restoring it does
not silently reselect it. Removing a reference preserves physical shared files.

## Configuration or syntax

Use **Add New Project > Alarmlist Shared Project**, then the consumer's
**Dependencies > Add Reference > Shared Projects**. Saving records an import:

```xml
<Import Project="../Shared/Shared.projitems" Label="Shared" />
```

Shared item paths use `$(MSBuildThisFileDirectory)` relative to the import.
Build actions are Compile, AlarmlistResource and AlarmlistLookup. Edit membership
in the owning shared project. Remove excludes an item; Delete physically deletes it.

## Minimal example

The checked-in [Shared Projects sample](../../example/SharedProjects/README.md)
imports one alarm into North and South, each assigning its own Site parameter:

```xml
<Name>$(Site): $(loc:Ready)</Name>
```

### Expected result

English builds show `North: Ready` and `South: Ready`. Both descriptions resolve
to `READY`, and Shared itself produces no compiled artifact.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Semantic suggestions unavailable | No selected/current consumer | Choose a compatible consumer and allow analysis to refresh. |
| No input error after reference removal | Consumer had only shared inputs | Re-add the intended import or add local inputs. |
| Native shared project fails to load | Required tooling absent | Install native Shared Project tooling for that IDE. |

## Related features and tutorials

See [share sources](../tutorials/share-sources-between-projects.md),
[libraries](alarm-libraries.md), and [XML completion](xml-completion.md).
