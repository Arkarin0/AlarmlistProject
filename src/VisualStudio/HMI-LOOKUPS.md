# Reusable instruction templates and HMI lookups

This is the implementation specification for the confirmed milestone-five
extension. The iteration log in [milestone five](MILESTONE-5.md) records which
parts are implemented. Format examples below describe the complete target feature.

## Generated catalogue

Other tools produce `.almlookup` XML files. The version-one namespace is
`urn:alarmlist:lookup:1`. The authoritative
[AlarmlistLookup.xsd](../Compiler/Lookups/AlarmlistLookup.xsd) is embedded in the
compiler and shipped in the SDK at `tools/Schemas/AlarmlistLookup.xsd`.
The compiler validates captured XML against this schema without accessing external
schema locations or allowing DTDs. Unknown elements/attributes and duplicate
keys are errors. An empty catalogue is valid.

```xml
<Lookup xmlns="urn:alarmlist:lookup:1">
  <Path Key="Service.Transport.Auslauf.Nockenfenster">
    <Segment LabelKey="Hmi.Service" />
    <Segment LabelKey="Hmi.Transport" />
    <Segment LabelKey="Hmi.Auslauf" />
    <Segment LabelKey="Hmi.Nockenfenster" />
    <Property Key="Ein" LabelKey="Hmi.Nockenfenster.Ein">
      <Metadata Name="Minimum" Value="0" />
      <Metadata Name="Maximum" Value="100" />
      <Metadata Name="Default" Value="060" />
      <Metadata Name="Unit" Value="degrees" />
    </Property>
    <Button Key="Reset" LabelKey="Hmi.Nockenfenster.Reset" />
  </Path>
</Lookup>
```

Paths have project-wide, case-sensitive keys. A path contains one or more ordered
segments, followed by zero or more properties/buttons. Member keys are unique
within their path, including across properties and buttons. Metadata names are
unique within a member. Technical keys start with a Unicode letter or underscore;
subsequent characters may also be Unicode letters, combining marks, numbers,
periods, or hyphens. Resource LabelKey values follow existing RESX key rules.
The generator must map unsupported code-language punctuation to stable keys.

Segment order is explicit; the compiler never splits a technical path key to
guess navigation labels. Each LabelKey resolves through the existing saved RESX
catalogue and selected culture. Reuse a label key where the HMI label has the same
meaning; use distinct keys where context requires different translations.
Class renames require explicit updates to references; no heuristic rename matching
or regeneration of translator-maintained RESX files takes place.

Metadata Value is required but may be empty. It is text, not a number or executable
expression. Minimum, Maximum and Default are conventions, not compiler primitives.
No numeric coercion, bounds validation, automatic default, or units conversion
occurs. An explicit reference to Default above inserts `060`.

## Expressions

Named arguments extend the existing localization token:

```text
$(loc:Instruction.SetProperty,
  path=$(path:Service.Transport.Auslauf.Nockenfenster),
  property=$(property:Service.Transport.Auslauf.Nockenfenster#Ein),
  value=$(meta:Service.Transport.Auslauf.Nockenfenster#Ein#Default))
```

The RESX template is `In "{path}", set property "{property}" to "{value}".`
One key serves many steps. Plain `$(loc:Key)` retains its existing behavior;
supplying arguments enables named-placeholder formatting. Arguments use the
existing ASCII parameter identifier grammar, compare case-sensitively, and may be
reordered. Every supplied argument must occur in the template and every placeholder
must have an argument. Repeated placeholders are allowed. Indexed placeholders,
alignment, numeric format strings, and nested localization arguments are unsupported.

Argument values are double-quoted literal strings or one explicit expression:
`$(Parameter)`, `$(path:PathKey)`, `$(property:PathKey#MemberKey)`,
`$(button:PathKey#MemberKey)`, `$(meta:PathKey#MemberKey#MetadataName)`, or
`$(list:ListKey#EntryKey)`.
Whitespace/newlines are allowed around argument separators and equals signs, not
inside identifiers. Quoted strings support `\"`, `\\`, `\n`, `\r`, and `\t` escapes;
dollar tokens and braces inside those strings remain literal. All values are text,
so use `value="001"` to preserve leading zeros. XML escaping still applies when
the expression is stored inside XML.

Literal braces in a formatted template are `{{` and `}}`. Parse the template
before inserting argument or parameter values; inserted text is never parsed
again. Existing parameter expressions in translations retain the consuming
alarm/file/project scope and field-dependency diagnostics. Localization inside
a translation remains unsupported. HMI label entries are plain text; they do not
introduce another expression-evaluation context.

Path expressions join translated segments with ` -> `. Property/button expressions
resolve the named member of the requested kind and translate its label. Metadata
expressions read the named member's stored value without translating it. Missing
paths, members or metadata, ambiguous paths, and kind mismatches are diagnostics.
Do not validate unused abstract-template or cleared-step contextual references.
Malformed catalogue XML is always an error, including in keyword mode.

One selected culture applies to the sentence and every HMI label. Keep
`AlarmlistCulture`, `AlarmlistCultures`, parent-only translation lookup and current
output paths. In keyword mode retain each complete localization/lookup expression
literally, including arguments, while still checking referenced catalogue entities.
Ordinary parameter expressions outside these tokens continue evaluating normally.

## Named lists and argument suggestions

Generated catalogues also support independent named lists for wires, terminals,
PLC symbols, spare parts, and other supplied identifiers. There is no built-in
DeviceTag field. List entries contain stable keys and literal values:

```xml
<Lookup xmlns="urn:alarmlist:lookup:1">
  <List Key="Wires">
    <Entry Key="EC120W1" Value="=EC-120W1" DescriptionKey="Wire.Outlet">
      <Metadata Name="DrawingPage" Value="023" />
    </Entry>
  </List>
  <List Key="ConnectionPoints">
    <Entry Key="EC120K1_12" Value="=EC-120K1:12" Description="Contactor terminal" />
  </List>
  <Template Key="Instruction.DisconnectWire">
    <Argument Name="wire" List="Wires" />
    <Argument Name="connection" List="ConnectionPoints" />
  </Template>
</Lookup>
```

Store `Disconnect wire {wire} from {connection}.` once in RESX, and use:

```text
$(loc:Instruction.DisconnectWire, wire=$(list:Wires#EC120W1), connection=$(list:ConnectionPoints#EC120K1_12))
```

The English result is `Disconnect wire =EC-120W1 from =EC-120K1:12.`
Values preserve punctuation, case, leading zeros, and empty strings. Values and
metadata are never translated or rescanned as expressions. Optional Description
is literal; DescriptionKey uses the selected culture and existing parent lookup.
These attributes are mutually exclusive. Missing translated descriptions leave
the description unavailable without making the identifier itself unavailable.

Files can contribute different entries to the same list. Duplicate list keys
within one file are malformed; duplicate entry keys across contributions report
ALM0023. Different keys may have the same displayed value. Ambiguous entries are
unavailable. Duplicate template/argument bindings or bindings to unknown lists
report ALM0024. Keys and argument names compare case-sensitively.

Bindings supply editor suggestions only. A user can still enter a literal or a
parameter expression. The RESX template owns its placeholder signature; an unused
binding does not add a placeholder. Selecting a list entry inserts an explicit
reference, so regenerating its value updates subsequent compilation and preview.
Missing explicit references fail compilation. Keyword mode retains complete tokens
while checking identities. Relationship filtering and multi-selection formatting
are deferred; two arguments can independently select from the same list.

In the designer, choose the reusable localization template, then press Ctrl+Space
in its selected empty argument. Only the explicitly bound list is offered. For
later arguments, place the caret inside the quoted value or select that value and
press Ctrl+Space. Typing part of a value, description, key, or metadata narrows the
list; this search ignores case, while stored identities remain case-sensitive.
Selecting an entry replaces the entire value, including any enclosing quotes,
with its list expression. This works in scalar fields and procedure rows. Use
`$(list:Wires#` for direct expression completion outside a mapped template.
Empty or invalid bound lists never offer entries from unrelated lists.

## Host integration

The SDK includes `**/*.almlookup` as `AlarmlistLookup` items, with the same output,
intermediate and hidden-directory exclusions as existing inputs. Explicit items,
linked files, conditions, and disabling default lookup items are supported.
Hosts capture saved lookup bytes once with each resource batch. Compiler code
receives immutable text snapshots; it never discovers project files. Preview
observes saved lookup changes without saving pending editor buffers.

Compiler-owned analysis exposes captured catalogue identities, entries, labels,
metadata, diagnostics and locations. The VS bridge copies detached data from the
SDK; it does not parse or evaluate catalogue contents independently. The designer
offers template/argument and path/member/metadata insertion with current-input
validation and preservation of local drafts. XLIFF protects named placeholders
and explicit expressions against accidental changes while allowing reordering.

In an expression-enabled designer field or procedure row, type `$(loc:` or use
Ctrl+Space. A template choice lists its named arguments and inserts, for example,
`$(loc:Set, path="", property="", value="")`. The first quoted empty value is
selected. Replace it with a literal, or type `$(path:` and select a path. Select
each subsequent quoted value (including the quotes) to insert an expression there.
Type `$(property:Service.Transport.Auslauf.Nockenfenster#` to narrow choices to
that path's properties; use `button:` for buttons and `meta:` for stored metadata.
Metadata values appear as selection guidance. Selecting `Default` inserts a
reference to that stored string; no default is applied automatically.

Completion is case-sensitive for resource and lookup keys. It preserves the
existing case-insensitive parameter completion. It rechecks saved inputs both
before showing choices and before insertion. Escape closes completion first;
Enter inserts into the draft, and the normal next Enter commits one undoable ALMX
edit. Nested localization choices are excluded inside argument values. In keyword
mode, distinct template signatures from saved RESX entries are offered explicitly
without choosing a fallback language; real cultures use the compiler's supplying
entry. Plain `$(loc:Key)` remains available alongside formatted template choices.

The design follows this repository's captured-input compiler boundary and
[Roslyn compilation responsibilities](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Compilation/Compilation.cs).
Schema packaging uses the existing SDK content layout, following
[Arcade's SDK organization](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk).
Neither reference introduces a dependency or changes the pinned Arcade SDK.
