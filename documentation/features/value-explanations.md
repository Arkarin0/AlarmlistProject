# Explain this value

Explain this value shows how the compiler obtained a value and links its chosen
contributors to their sources. Explanations appear in the read-only, expandable
Explanation section of Alarm Summary.

## When to use it

Use an explanation to find an inherited assignment, understand a translated
instruction, inspect a default or typed conversion, or investigate an invalid or
unavailable value.

## Requirements and availability

Use matching Milestone 14 SDK and extension builds in Visual Studio 2022 or 2026.
Choose the consuming project when a shared file belongs to several projects.
Commit or cancel private designer drafts before requesting an explanation.
Shared unsaved ALMX text participates; resources and dependency projects use their
existing saved-input rules. The selected summary language applies.

## How it works

Choose Explain this value beside a designer field, parameter, procedure title or
entry, or collection field. The same actions appear beside values in the selected
alarm's summary. Parameters and context includes custom and well-known values.

Explanation shows the target, project/configuration/language, raw expression,
resolved and canonical values, state, diagnostics and contributing steps. Expand
a step to inspect its values, origin and dependency IDs. Go to source navigates
to a captured physical source or a read-only package definition. Context values
and definition defaults can have no source location.

Only chosen contributors appear. Inheritance, expression dependencies, selected
translation fallback, lookup definitions/labels, formatted arguments and typed
conversion can contribute. Repeated dependencies share IDs. Cycles terminate in
an explicit cycle step. Keyword localization stays deferred. An excluded entry
points to its controlling Clear; its discarded expression is not evaluated.

Invalid and deferred values retain available steps and relevant compiler
diagnostics. When parsing, binding or target identity prevents a reliable result,
Explanation displays an unavailable reason. No explanation edits source, applies
a private draft or writes build output.

Changing inputs, the selected value, consumer or culture clears an obsolete
explanation and cancels its request. Request the value again after analysis
finishes. Entries are identified by their defining source/version, so moved entries
are not silently selected by an old index. Navigation rechecks captured inputs.
Steps are shown in pages of 50; Show more steps reveals the next page without
duplicating shared dependencies.

## Minimal example

In an application project's `Alarms.almx`:

```xml
<Alarmlist>
  <Alarm abstract="true">
    <FullyQualifiedName>Base</FullyQualifiedName>
    <Name>Position $(P)</Name>
    <Properties><Property Name="P" /></Properties>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Child</FullyQualifiedName>
    <ReferenceName>Base</ReferenceName>
    <Properties><Property Name="P" Value="1" /></Properties>
  </Alarm>
</Alarmlist>
```

### Expected result

Select Child and choose Explain this value for Name. Explanation shows
`Position 1`, the inherited Name expression, and the chosen Child declaration and
assignment of P. Go to source opens the contributing ALMX location.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Action disabled | Drafts or current analysis are pending | Commit/cancel drafts and wait for current analysis |
| Result cleared | Inputs, context or selection changed | Request the value again |
| Unavailable target | Parsing/binding failed or its defining source changed | Inspect diagnostics, then select the current value |
| No source button | A context/default value has no captured location | Inspect its scope and value in the step |
| SDK incompatible | Extension and SDK contracts/definitions differ | Restore/rebuild matching tools; XML editing remains available |

## Related features and tutorials

- [Trace an inherited value to its sources](../tutorials/explain-a-value.md)
- [Alarm Summary](alarm-summary.md)
- [Inheritance and expressions](alarm-inheritance-and-expressions.md)
- [Localization](localization.md)
- [Custom fields and collections](custom-fields-and-collections.md)
