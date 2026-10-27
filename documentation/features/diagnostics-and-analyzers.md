# Diagnostic policy and external analyzers

Control configurable diagnostics per physical source file and opt into additional
alarm rules through compatible analyzer packages or explicit assemblies.

## When to use it

Use diagnostic policy to enforce project conventions while retaining mandatory
compiler validation.

## Requirements and availability

Saved `.editorconfig` settings are supported; `.globalconfig` is not. External
Alarmlist analyzers must match the compiler contract and MSBuild runtime. They are
explicit trusted code; ordinary Roslyn analyzers are not Alarmlist analyzers.

## How it works

EditorConfig follows physical source directories, including linked sources, until
`root=true`. Nearer files/later matching sections win; unset removes an inherited
option. Keys/IDs ignore case; globs and custom values preserve case. Inline `#` and
`;` stay literal. Unsaved configuration is ignored.

Policy order is rule default, file severity, NoWarn, then warning promotion.
WarningsAsErrors takes precedence over WarningsNotAsErrors, which only exempts
blanket TreatWarningsAsErrors. ALM0001–ALM0030 remain mandatory errors. ALM0031
missing enum-caption warnings are configurable. Any effective error prevents
installing new outputs across the culture batch.

Analyzers run on valid evaluated models per culture. Analyzer load/execution
failures report ALM0025. DLL updates in reused desktop MSBuild processes can require
a new process; the IDE refreshes its isolated SDK context when inputs change.

## Configuration or syntax

`alarmlist_diagnostic.<ID>.severity` accepts error, warning, suggestion, silent,
none and default. Suggestion is information; silent remains in analysis without
a build log/Error List row. Use project NoWarn, TreatWarningsAsErrors,
WarningsAsErrors and WarningsNotAsErrors for project policy.

## Minimal example

With the explicitly installed
[sample analyzer](../../src/Analyzers/Alarmlist.SampleAnalyzer/README.md), save this
`.editorconfig` alongside the project's ALMX files:

```ini
root = true

[*.almx]
alarmlist_sample.code_prefix = PLANT-
alarmlist_diagnostic.SAMPLE001.severity = warning
```

### Expected result

A concrete alarm with string Code `001` reports SAMPLE001; `PLANT-001` satisfies
the rule. Without installing the analyzer these settings do not create a rule.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| No warning | Analyzer not opted in or pattern does not match | Verify package/DLL registration and physical source path. |
| Cannot suppress compiler error | Mandatory validation | Correct the source instead of changing severity. |
| ALM0025 after update | Incompatible contract or stale DLL | Rebuild against matching tools; restart a reused desktop build process. |

## Related features and tutorials

See [configure diagnostics](../tutorials/configure-diagnostics.md),
[project builds](project-builds.md), and [designer editing](visual-studio-editing.md).
