# Milestone four implementation contract

Iteration one freezes the remaining implementation choices for
[milestone four](MILESTONE-4.md). This contract implements the owner's decisions;
where the earlier plan calls spelling or API shape a proposal, this document is
the implementation target. It does not claim that later iterations are complete.

## Source and project declarations

ALMX retains its `Alarmlist` root and literal alarm identities/references.
`abstract` is a local, non-inherited Boolean attribute on `Alarm`. Omitted means
false. Accept XML Boolean values `true`, `false`, `1`, and `0` after trimming;
serialize true/false. Invalid values are diagnostics, not implicit false.

A `Properties` container may occur directly under `Alarmlist` or `Alarm`.
Its entries use `Property`, required `Name`, and optional `Value` attributes:

```xml
<Alarmlist>
  <Properties>
    <Property Name="par1" Value="001" />
  </Properties>
  <Alarm abstract="true">
    <FullyQualifiedName>A</FullyQualifiedName>
    <Properties>
      <Property Name="par1" />
    </Properties>
    <Code>$(par1)</Code>
    <Description>Inspect $(AlarmlistAlarmCode)</Description>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>B</FullyQualifiedName>
    <ReferenceName>A</ReferenceName>
  </Alarm>
</Alarmlist>
```

Only B is emitted, with Code `001` and Description `Inspect 001`.

| Source | Meaning |
| --- | --- |
| `<Property Name="par1" />` | Declare without a local assignment; use outer-scope fallback. |
| `<Property Name="par1" Value="002" />` | Declare/assign locally, or assign an inherited parameter. |
| `<Property Name="par1" Value="" />` | Explicit empty assignment; stop fallback. |
| Remove only `Value` | Keep the declaration and resume fallback. |
| Remove the local Property | Remove the local declaration/assignment; inherited or outer declarations remain. |

Property values are attribute strings; do not introduce a second text-content
assignment form. XML handles escaping of ampersands/quotes and attribute whitespace.
Unknown elements retain the existing skip/preserve behavior; non-whitespace content
inside a Property is invalid declaration syntax. Duplicate containers contribute to
one containing scope, so duplicate names still diagnose rather than selecting an
arbitrary declaration. Designer editing refuses ambiguous/unsupported local XML.

Names match `[A-Za-z_][A-Za-z0-9_]*` and compare ordinally ignoring case. Reserve
the entire `Alarmlist` prefix ignoring case. Reject duplicates within one scope;
the same name in a child and a base is allowed and identifies one visible parameter.
The nearest declaration supplies declaration provenance, never inherited assignment
values. Different-file bases do not introduce their file values into a child's scope.

Project definitions use evaluated `AlarmlistProperty` items:

```xml
<ItemGroup>
  <AlarmlistProperty Include="par1" Value="001" />
  <AlarmlistProperty Include="Unassigned" />
  <AlarmlistProperty Include="Empty" Value="" />
</ItemGroup>
```

Preserve metadata presence separately from evaluated text, so absent and empty
Value remain distinct. The task and CPS capture the same evaluated items. Project
definitions are normally maintained through project XML; the ALMX designer edits
file/alarm declarations, showing project values as read-only context. No implicit
environment-variable import or target-time property mutation is supported.

## Evaluation

Bind identities/references first, then select raw scalar and surviving procedure
origins using the existing resolver. Templates inherit raw content and parameter
names. They never pass parameter assignments to descendants. Value precedence is
current Alarm -> its File -> Project. A declared but unassigned parameter falls
through; an assigned empty string stops. Only used unresolved parameters on a
concrete alarm fail compilation. Abstract alarms remain in binding and detached
analysis but do not require value evaluation and are excluded from compiled results.

Scan each selected source expression once. `$(Name)` reads a property, `$$` emits
one literal dollar, and a lone dollar remains literal. `$$(Name)` emits literal
`$(Name)`. Missing close parentheses, invalid names, unsupported namespaces, and
empty tokens are grammar errors. Parse/validate local expression grammar on every
alarm, including abstract templates; evaluate only surviving concrete expressions.
Do not parse substituted values again.

The well-known names in the plan's catalog are fixed. Project/file Name excludes
the extension; File includes it; Extension includes the dot; FullPath requires a
captured absolute path. Directory retains a filesystem root and otherwise omits
the trailing separator. Use supplied paths, never current working directory or
disk reads. Missing project/configuration/file context diagnoses only when used.
Missing optional alarm identity/reference/fields read as empty. Identity/reference
remain literal even if their text resembles an expression.

`AlarmlistAlarmName`, `AlarmlistAlarmCode`, `AlarmlistAlarmCategory`, and
`AlarmlistAlarmDescription` read resolved scalar values of the consuming alarm.
Cache evaluation states per alarm/field/context and detect direct/indirect cycles.
Dependencies come only from parsed raw tokens, after escaping. Overrides may break
template cycles. Procedure expressions may read scalar results. Failed dependencies
remain unavailable; never substitute raw text or empty as a recovery value.
Custom values containing `$(Other)` are literal even when inserted into a field
that is then read by another field. Keep all raw expressions for edits and copies.

Syntax/declaration/reference errors on unused templates still fail compilation.
Value availability and scalar dependency cycles are checked for concrete consumers.
Errors suppress compiled XML and replacement of build output. A valid project with
only abstract definitions emits an empty AlarmList.

## Diagnostics

Keep ALM0001-ALM0004 unchanged. Reserve these new stable error IDs:

| ID | Meaning |
| --- | --- |
| ALM0005 | Invalid abstract attribute. |
| ALM0006 | Malformed or unsupported property expression. |
| ALM0007 | Unknown property name. |
| ALM0008 | Declared parameter has no available assignment. |
| ALM0009 | Required well-known project/file context is unavailable. |
| ALM0010 | Circular alarm-field dependency, including self-reference. |
| ALM0011 | Invalid parameter declaration/name/reserved prefix. |
| ALM0012 | Duplicate parameter in the same scope. |

Use existing descriptors and bags. Report ALMX declaration/use locations with
snapshot versions; decoded expression spans may use the enclosing element location.
Include the consuming alarm in expression messages and dependency traces. Related
declaration/cycle locations remain source locations; host-provided declarations
may be location-free. Retain structural XML exceptions at the existing input boundary.

## Compiler and detached analysis boundary

Introduce `Alarmlist.Compiler.CompilationContext`, capturing project path,
configuration, platform, and a defensively copied read-only list of immutable
`PropertyDefinition` values. A definition has Name and optional Value; null means
unassigned and empty is assigned. Compiler entry points accept a context overload;
the parameterless compiler and existing convenience overloads use an empty context.
There is one evaluation path and no legacy-literal mode.

Keep expression parsing/evaluation in the compiler's `Evaluation` layer, between
reference binding and resolved output. Preserve file identity and file declarations
when Binder combines sources. Syntax remains raw and fresh per parse. Source
serialization must use local raw fields rather than reference-resolved getters.

Analysis contract 2, introduced together with the bridge in iteration four, adds:

- Alarm-local abstract state and evaluated-value availability, separate from
  reference-chain validity (`IsResolved`).
- Effective raw scalar expressions alongside evaluated EffectiveValue and existing
  local values/locations. Procedure rows preserve raw text independently of evaluated
  surviving text; Clear/excluded/local entries retain their source representation.
- Visible parameter names, local declaration/assignment presence, explicit-empty
  state, effective value availability, and separate declaration/assignment scope
  and origins. Include immutable well-known property values for completion.
- Existing source versions, reference origins, diagnostics, and CompiledXml.

Expose context-aware `AnalyzeSnapshots(snapshots, context, includeCompiledXml)`.
The AppDomain worker constructs SDK context objects by reflection and copies only
host-owned serializable results back. Contract 2 and compiled-preview support are
required together; unsupported installed SDKs receive an explicit failure, never
a substitute compiler. Include captured context in project-state equality and
generation checks. Cache values per consuming alarm, not defining template.

This follows Roslyn's explicit captured
[compilation options](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Compilation/CompilationOptions.cs)
and separated compiler layers at this repository's scale. Build transport stays
in the task/SDK, adapting the existing pinned Arcade SDK's props/targets and
package-consumer testing rather than importing new upstream infrastructure.

## Designer and iteration checks

The parameter table edits the selected alarm or current file scope explicitly,
with local name/value controls, assignment-presence control, effective value,
and origin. Inherited/project rows are context, not edits to other sources.
Add/remove local declarations and remove-assignment operations are explicit.
Clearing an assigned editor writes empty; removing the assignment restores fallback.
The Abstract checkbox updates only the selected alarm's source attribute.

Normal fields show evaluated text when unfocused and raw source/draft when focused.
Read-only inherited fields stay read-only until Override locally starts a draft.
Focus-only changes never save or dirty source. Copies preserve raw tokens/escapes.
Use the existing one-edit Undo, validation, notification, lifetime, and accessibility
patterns. Completion inserts tokens and shows scope/value availability without
committing. An incomplete expression draft remains editable until commit/cancel.

Each iteration records commands/results in the milestone plan and is committed
before the next begins. Normal tests and live IDE/acceptance results are distinct;
skipped host tests are not claimed as passes. Preserve the outstanding milestone-three
release gates. No package publication is part of these implementation iterations.
