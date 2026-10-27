# Custom fields and collections

Rebuild a coordinated toolset from an alarm definition to support typed scalar
fields, ordered scalar/record collections and optional test procedures.

## When to use it

Use a fork definition when the standard Name, Code, Category and Description
fields do not cover your domain. This is a toolset authoring workflow, not a
per-project runtime schema setting.

## Requirements and availability

Build matching compiler, SDK and extension tools together. Rebuild dependent
libraries and analyzers for the matching contracts. The standard version-2
definition enables TestProcedure and the original four string fields, with no
custom collections. Use the isolated playground to explore alternate definitions.

## How it works

Definition order drives ordinary field presentation/output. Typed fields validate
required values and bounds; output is canonical. Literal defaults supply effective
values without being written into authored source. Enum captions may use project
RESX LabelKey translations, while stored enum tokens remain unchanged. Missing
configured captions warn with ALM0031 in concrete application cultures; keyword
and library builds defer these warnings.

Simple collections hold ordered scalars. Complex collections hold flat scalar-field
records. Inherited entries come first, local entries append, and Clear excludes
earlier entries. Record siblings use `$(item:FieldName)` in record context, including
translated expressions. Defaults remain literal; records inherit whole. Nested
collections and keyed overrides of inherited records are excluded. ALM0029 scalar
and ALM0030 cardinality errors are mandatory; concrete consumers enforce deferred
library/abstract cardinality constraints.

The designer edits local entries transactionally and preserves local identities
through moves/refresh. Inherited/excluded projections are read-only. Collapse
preserves drafts. Compiled WPF summary templates ship with the rebuilt extension;
they are not loaded from arbitrary project/library files at runtime.

## Configuration or syntax

Author the definition using the
[generator guide](../../eng/tools/AlarmDefinitionGenerator/README.md) and its XSD.
Use version-2 SimpleCollection/ComplexCollection descriptors and explicitly include
TestProcedure when wanted. Semantic definition changes invalidate libraries;
presentation changes still require matching extension/SDK tooling fingerprints.

## Minimal example

From the repository root, with Git, repository tooling and Visual Studio installed:

```powershell
scripts/start-custom-definition.bat
```

### Expected result

An isolated rebuilt toolset opens a sample containing Tags, Numbers and Checks.
`Plant.Check` demonstrates typed expressions and inheritance; `Plant.Defaults`
demonstrates effective defaults and Clear. The standard checkout's compiler
definition and default experimental deployment are preserved.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Designer blocks changes | Tooling fingerprint mismatch | Deploy/use matching SDK and VSIX. |
| Collection XML rejected by normal project | Standard definition has no collections | Use the rebuilt custom toolset. |
| Procedure XML rejected | Chosen definition omits TestProcedure | Choose the matching sample or explicitly adapt your source. |

## Related features and tutorials

See [explore custom definitions](../tutorials/explore-custom-definitions.md),
[playground guide](../../example/CustomDefinition/README.md), and [Alarm Summary](alarm-summary.md).
