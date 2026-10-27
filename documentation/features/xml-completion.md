# XML and expression completion

Use native XML suggestions for ALMX structure, lookup catalogues and project
settings, plus analysis-aware candidates for alarm expressions and references.

## When to use it

Use completion to discover valid element/attribute names and select identifiers
from the current project's saved resources, lookups and alarm definitions.

## Requirements and availability

Available in the matching Visual Studio extension, in embedded ALMX XML and
standalone XML Editor. Semantic candidates need compatible current analysis and
a selected consumer when a file has multiple owners.

## How it works

Structural suggestions follow the installed alarm definition, including enabled
collections/procedures and enum/boolean values. Lookup hints use the shipped XSD;
project hints use SDK rules. These do not modify machine-wide schema catalogs or
replace user-selected native schemas.

Expression fields offer parameters, localization/lookup/list expressions and
record siblings where supported. ReferenceName offers eligible alarms. Identity
fields and parameter assignment values remain literal. Comments, CDATA and foreign
namespaces do not receive ALMX structural hints. Lookup literal values and MSBuild
conditions/property values are not expression contexts. Native XML/MSBuild fallback
remains available. Saved input/context changes invalidate stale semantic insertion.

## Configuration or syntax

Press Ctrl+Space, invoke List Members/Complete Word, or type the appropriate tag,
attribute, quote or `$(` trigger. Tab/Enter accepts, Escape dismisses and Undo
restores text without saving. In an empty localization argument slot invoke
completion, or select its quotes first; quoted text otherwise remains literal.

## Minimal example

In a compatible ALMX document with an included saved English `Ready` key, place
the caret in Name and type `$(loc:`. Invoke Ctrl+Space and choose Ready.

### Expected result

The field contains `$(loc:Ready)`. In English analysis its resolved text comes from
the resource. Accepting a suggestion does not save the document or insert defaults.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Structure only, no semantic candidates | Consumer absent or analysis unavailable | Select the consumer and resolve errors/compatibility. |
| Old suggestion is rejected | Source/context/saved inputs changed | Invoke completion again against current inputs. |
| Lookup hints absent | Wrong namespace | Use `urn:alarmlist:lookup:1`. |

## Related features and tutorials

See [editor workflow](../tutorials/edit-and-inspect-alarms.md),
[shared contexts](../tutorials/share-sources-between-projects.md), and
[lookups](lookups-and-instruction-templates.md).
