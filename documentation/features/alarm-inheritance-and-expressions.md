# Alarm inheritance and expressions

Reuse raw alarm content through references and resolve parameter expressions in
each consuming alarm's context.

## When to use it

Use abstract alarms for common templates and parameters for site-specific values.

## Requirements and availability

Available in the compiler, SDK and matching designer. The standard definition
provides Name, Code, Category and Description as string fields.

## How it works

`ReferenceName` identifies a base alarm; `FullyQualifiedName` identifies the alarm
itself. Identifiers stay literal. Missing references, duplicates and cycles are
errors. Abstract alarms remain referenceable and are omitted from application XML;
`abstract` is local and defaults to false.

References inherit raw content and parameter declarations. Resolve parameter
values from the current alarm, then its own file, then the project. Base-alarm and
base-file assignments do not become child assignments. An omitted Value falls
through; `Value=""` stops fallback. In standard string fields an empty local value
inherits; it is not an explicit clearing operation.

## Configuration or syntax

`$(Parameter)` inserts a parameter. `$$` emits a literal dollar. Inserted custom
values are not evaluated again. Parameter names ignore case, use letters/digits
and underscores with a letter/underscore first, and cannot use the reserved
`Alarmlist` prefix. Duplicate declarations in one scope are errors.
Project parameters use `AlarmlistProperty` items with Value metadata. File/alarm
parameters use `Properties/Property`.

Built-ins include `$(AlarmlistAlarmCode)`, `$(AlarmlistAlarmFullyQualifiedName)` and
project/file context values listed in the
[SDK expression reference](../../src/SDK/Alarmlist.SDK/README.md#property-expressions-and-templates).
Scalar cycles are errors.

## Minimal example

Place this complete `Alarms.almx` in an SDK application project:

```xml
<Alarmlist>
  <Properties><Property Name="Site" Value="North" /></Properties>
  <Alarm abstract="true">
    <FullyQualifiedName>Template.Ready</FullyQualifiedName>
    <Name>$(Site) ready</Name>
    <Code>001</Code>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Plant.Ready</FullyQualifiedName>
    <ReferenceName>Template.Ready</ReferenceName>
  </Alarm>
</Alarmlist>
```

### Expected result

Build emits only `Plant.Ready`, with Name `North ready` and Code `001`.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Unassigned parameter | Child has no usable assignment | Assign it in the child, its file or the project. |
| Literal token expected but evaluated | Dollar was not escaped | Write `$$(Site)` to retain `$(Site)` literally. |
| Template is emitted | Abstract flag absent on that alarm | Set `abstract="true"` on the template itself. |

## Related features and tutorials

See [procedures](test-procedures.md), [author reusable alarms](../tutorials/author-reusable-alarms.md)
and [libraries](alarm-libraries.md).
