# Milestone four: property expressions across compilation and the designer

Status: compiler, SDK, captured-context analysis and designer implementation are
present. Validation results and remaining interactive-desktop/release gates are
recorded below; full acceptance is not claimed. The planning baseline is `de01a69`;
the initial proposal was recorded on 2026-09-25 against `1ac450a`.
The owner selected child-context evaluation (approach A) for inherited
expressions, refined so references inherit parameter declarations but not their
assigned values. An `abstract` XML attribute identifies template-only alarms.
Exact remaining public names, declaration/assignment syntax, and analysis
API shape are frozen by the [iteration-one implementation contract](MILESTONE-4-DESIGN.md),
which takes precedence over provisional spellings below. Iteration results are
recorded at the end of this document.

The outcome is an author who can write `$(PlantName)` in an alarm value, build
with a project-supplied value, and see the same resolved text in the designer's
preview. The source retains the expression, diagnostics explain invalid uses,
and completion offers the properties visible to the selected alarm. Editable
fields show resolved values normally and switch to raw source when focused.

## Owner decisions (2026-10-06)

These replace the earlier proposal where it differs:

| Question | Decision |
| --- | --- |
| Well-known properties | Include alarm values as well as project context; consult MSBuild's well-known/reserved properties for further useful entries. |
| Custom-property scope | Use Project -> File -> Alarm scopes, with PowerShell as the scope reference. Bases supply parameter declarations, not assigned parameter values. Local and inherited expressions resolve values through current Alarm -> current alarm's File -> Project. |
| Empty and missing values | Unassigned parameters fall through to File, then Project. Explicitly empty assignments stop lookup. A concrete alarm using a parameter with no available value fails compilation; unused parameters may remain unassigned. |
| Abstract alarms | Add an `abstract` XML attribute on Alarm to identify definitions used purely as templates. Abstract alarms remain referenceable but are excluded from compiled output and may leave parameter values unresolved until a concrete alarm uses them. |
| Expression targets | Name, Code, Category, Description, and procedure text only; FullyQualifiedName and ReferenceName remain literal. |
| Recursive expansion | Do not rescan substituted strings or recursively expand custom parameter assignments. Alarm-field properties read resolved field values through field dependencies. |
| Alarm-field property reads | Expose the current alarm's resolved field values. With par1=`001`, Code=`$(par1)` and Description=`Inspect $(AlarmlistAlarmCode)` produce Code=`001` and Description=`Inspect 001`. |
| Literal template content | Declaration-only inheritance applies to custom parameters. Ordinary raw alarm fields and procedure content, including literal values, remain inherited. |
| Designer display | Show resolved values when unfocused and the raw expression when focused. |
| Designer parameter editing | Include a parameter table for editing values and an Abstract checkbox in milestone four, so normal parameter/template editing does not require switching to XML. |
| Local copies | Preserve the expression rather than freeze its current resolved text. |
| Activation and compatibility | Integrate expressions into normal compilation and editing. No activation flag and no backward-compatibility requirement: the software has not been released. |
| Later milestones | Localization and NuGet alarm libraries remain for another milestone. |

The earlier opt-in flag, project-only scope, permanently raw editing controls,
resolved-text copy behavior, and old SDK/VSIX compatibility gate are superseded.
Child-context evaluation (approach A) is selected for reusable alarm templates;
the current alarm's values customize inherited expressions. The owner's refinement
replaces base-value fallback with inheritance of parameter declarations only. The
owner confirmed that this applies to custom parameters; existing raw alarm fields
and procedure content still form the inherited template. All four follow-up
questions are settled in the decision table above. Public spelling and API design
remain implementation work.

## Baseline and release dependency

[Milestone three](MILESTONE-3.md) implements reference-aware editing, provenance,
live diagnostics, and resolved previews through iteration seventeen. Compared
with the original plan's baseline, the current checkout includes:

| Completed baseline work | Evidence and limits to preserve |
| --- | --- |
| WPF binding retention fix, iteration fifteen | Read-only notifying item sources and notifying presentation rows remove the observed WPF retention paths. The recorded VS2026 100-close run found no reachable closed designer models or disposed analysis adapters; whole-process private memory still grew. |
| Accessible status events, iteration sixteen | Visible status/error changes raise LiveRegionChanged. Automated event delivery passed; actual spoken screen-reader output remains unaccepted. |
| Weak document observation, iteration seventeen | The observer no longer strongly owns VS documents. The recorded VS2022 20-close run found no reachable closed designer models or disposed analysis adapters. The final VS2026 observer/long-session repeat remains open. |
| Dedicated functional UI suite, `de01a69` | The [UI migration record](Alarmlist.VisualStudio.UIIntegrationTests/README.md#migration-validation-2026-09-26) records 14 passing VS2022/VS2026 functional cases and default-run skipping. New property workflows still need their own cases. |
| Dedicated performance and diagnostic tooling | xUnit hosts isolated BenchmarkDotNet measurements and live latency/memory wrappers. The migration's one-sample live runs validate the harness/reporting, not long-session memory or performance acceptance. The reusable heap tool replaces the temporary milestone-three probe. |

Release acceptance remains open for spoken screen-reader output, native high
contrast, mixed-DPI transitions, precise keystroke-to-paint measurement, and the
final VS2026 observer/long-session process-memory repeat. The two identified
extension retention causes are fixed; remaining whole-IDE allocation growth is
not fully attributed. Keep these gates and their evidence in milestone three;
neither the test migration nor this plan marks them passed.

Feature development can proceed while those checks are completed. Distribution
of a resulting VSIX requires the outstanding release gates to be resolved and
recorded, new controls to receive equivalent acceptance, and the matching SDK to
be published and verified first. There is no publication action in this plan.

Relevant existing boundaries are:

| Area | Starting point and implication |
| --- | --- |
| Compiler | `AlarmCompiler` accepts files, source objects, syntax trees, or snapshots. It has no evaluation-context parameter. Its API can change as needed for the new integrated semantics. |
| Resolution | `Binder` resolves alarm references; `AlarmSyntaxNode` chooses scalar origins using null/empty fallback; procedure collections implement ordered Clear. Property evaluation must reuse those choices. |
| Analysis | `SnapshotAnalyzer` and `AnalysisResult` currently expose contract 1 with raw local fields, effective values, origins, and optional compiled XML. The new contract must also carry effective raw expressions, parameter declarations, assignment presence, and scoped-property data; expansion cannot overwrite raw source data. |
| Build | `AlarmlistBuildTask` currently receives Compile inputs and output settings. The SDK selects net472/net8.0 task payloads; both need identical property behavior. |
| Project context | `AlarmlistProjectSnapshot` captures evaluated Compile items and the SDK compiler path. Project properties must be captured there; file/alarm declarations belong to captured ALMX text. |
| VS boundary | `SdkAnalysisWorker` invokes the selected SDK in an AppDomain, and `AnalysisContractReader` currently requires contract 1. Update compiler and reader together, keeping SDK isolation and explicit failure for an incompatible installed SDK. |
| Designer | Local drafts and semantic previews are separate. Focus-dependent display must never overwrite draft/source text; local-copy actions must obtain effective raw expressions from the compiler. |
| UI lifetime/accessibility | `BindingCollection`, notifying presentation rows, `LiveStatusTextBlock`, and weak `VsAnalysisDocuments` registrations are established patterns. New completion and property displays must preserve their lifetime and event guarantees. |
| Test ownership | Functional IDE cases live in UIIntegrationTests; measurements and report/ownership tests live in PerformanceTests; shared automation lives in `src/shared/VisualStudioTesting/`. Test dependencies remain outside the VSIX. |

## Scope

Include:

- Built-in property expressions in Name, Code, Category, Description,
  and Step/Hint/Warning/Note text in Instructions and Reset.
- Well-known project, file, and alarm properties, followed by custom string
  parameters declared at Project, File, and Alarm scopes. Bases pass parameter
  declarations through references; value lookup uses current Alarm -> current
  File -> Project and excludes base assignments.
- Template-only alarms marked with the `abstract` XML attribute, retained in
  binding/analysis but excluded from resolved compilation output.
- One compiler-owned evaluator used by normal compilation and snapshot analysis,
  with resolved alarm-field dependencies, cycle diagnostics, literal parameter
  substitution, and unchanged failure/output preservation behavior.
- Captured project properties and unsaved file/alarm declarations in live analysis,
  including configuration changes and independent contexts for linked files.
- Resolved displays that switch to raw-expression editing on focus, property-name
  completion, and expression-preserving copies with the existing draft/Undo behavior.
- A designer parameter table for value editing and an Abstract checkbox, with
  source-preserving edits and the same analysis/Undo behavior as existing controls.
- Package-consumer, build/analysis parity, live-host, and performance coverage.

Defer localization, its language toolbar/project setting, culture/fallback
selection, `.resx`/XLIFF/CAB support, NuGet alarm libraries and `OutputType=Library`,
syntax generation, dynamically generated designer fields, collection-valued properties,
reference renaming,
and XML language-service completion/squiggles. Keep the current XML editor.
This milestone provides designer completion only for its property expressions.

### Boundaries for the newer localization and library proposals

The expanded [future feature notes](../../FUTURE-FEATURES.md) describe a language
toolbar synchronized with a compile-time project setting, raw localization text
while editing and translated text afterward, and packaged alarm libraries whose
localization expressions remain unresolved until final compilation. They also
leave consumer-supplied custom properties in those libraries as an open decision.
These are follow-up features, with these design consequences for milestone four:

- Keep raw source, selected reference origin, and evaluated text distinct in the
  compiler contract and designer. Property expansion must not destructively
  replace the only representation of an expression with its resolved string.
- Keep the captured evaluation context explicit and versioned so a later language
  setting can participate in the same build/preview invalidation boundary. Do not
  add a culture property, toolbar, resource loader, or localization fallback now.
- Keep `loc:` outside custom property names. Direct `$(loc:Key)` use remains an
  unsupported-expression error in milestone-four expression fields; single-pass
  replacement strings remain literal. This is not localization support, and the
  future missing-key diagnostic must have its own stable ID and language context.
- Treat milestone-four output as a final resolved alarm list. Its XML serializer
  does not become a library format. A future library stage must explicitly define
  how it preserves expressions and when errors occur; library scope, defaults,
  precedence, restore, and packing are not settled by this plan.

Review these boundaries in iteration one. The focus-dependent raw/resolved display
is now part of milestone four and can later serve localization too. Language
selection, translation resources, and library output remain separate work.

## Proposed expression and scope contract

The owner's decisions above are fixed, including child-context evaluation. The
remaining grammar details and public spellings below are implementation proposals
to finalize in iteration one.

| Subject | Proposed behavior |
| --- | --- |
| Activation | Expressions are built into every supported compilation/analysis entry point. There is no activation flag or legacy literal mode. Literal expression-looking text uses the documented escape. |
| Grammar | An expression is `$(Name)` with a name matching `[A-Za-z_][A-Za-z0-9_]*`. Text can contain multiple expressions. Property lookup uses ordinal case-insensitive comparison; alarm identity/reference comparison remains ordinal case-sensitive. |
| Escaping | In expression fields, `$$` emits one literal dollar and its result is not rescanned. Thus `$$(PlantName)` emits literal `$(PlantName)`. Ordinary dollar signs remain text. |
| Replacement | Scan each selected field's source expression once. Insert custom assignments literally; resolve referenced alarm fields before inserting their values. Never rescan inserted strings, including escaped or expression-looking results from another field. XML escaping remains the normal output writer's responsibility. |
| Field dependencies | Alarm-value properties read resolved scalar fields of the current concrete alarm. Evaluate dependencies independently of source order, sharing results per alarm/context. Proposed error handling: direct self-dependencies and cycles between fields report located errors and suppress output; never substitute raw text or empty to break a cycle. |
| Invalid expressions | When evaluated for a concrete alarm, unknown names and used parameters with no value in any applicable scope report errors. Distinguish unknown from known but unassigned; never substitute empty for either. Abstract templates defer context-dependent value resolution. Malformed expressions beginning with `$(` and unsupported namespaces such as `$(loc:Key)` remain grammar errors. |
| Custom definitions | Project definitions come from the host/SDK; file and alarm definitions come from ALMX. Separate parameter declaration from optional value assignment, including assignment presence. An unassigned declaration permits outer-scope fallback; an explicitly empty assignment is valid and shadows outer values. Reject invalid names, duplicates within the same scope, and collisions with the reserved `Alarmlist` prefix. Assigning an inherited parameter locally is not a duplicate declaration. |
| Scope | References inherit parameter declarations through the base chain but never inherit parameter assignments. Value lookup searches current Alarm -> current alarm's File -> Project, stopping at the first assignment. Local and inherited expressions use this same context. Lookup never changes declarations or exposes unrelated sibling scopes. |
| Abstract marker | Use the literal Boolean source attribute `abstract` on Alarm. Proposed defaults: omitted or `false` is concrete; `true` is template-only; the marker belongs to each declaration and is not inherited. Invalid attribute values report a located diagnostic. |
| Well-known properties | The initial proposed catalog below includes project/configuration values, source-file identity, and the selected alarm's identity/scalar values. It is frozen for each analysis/compilation input. |
| Excluded contexts | FullyQualifiedName, ReferenceName, element/attribute names, file paths, unknown XML, and Clear are not expression contexts and retain their existing treatment. |

Compiler code receives captured data; it does not load MSBuild or import the whole
process-environment property bag. The initial catalog avoids ambient clock/user
values. Custom parameter assignments hold literal string values: a value containing
`$(Other)` is inserted unchanged. There is no recursive evaluation of parameter
values, dynamic property name, or function call. Alarm-field references require a
separate dependency evaluator and cycle detection; they do not rescan replacement
strings or turn parameter assignments into expressions.

This intentionally uses only the familiar notation from
[MSBuild properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties?view=visualstudio).
MSBuild's broader evaluation model is not the ALMX expression contract. The
[reserved/well-known distinction](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=visualstudio)
informs the reserved prefix and initial catalog. The property names below are
Alarmlist proposals, not automatic imports of MSBuild's properties.

### Initial well-known property catalog

| Context | Proposed names | Meaning |
| --- | --- | --- |
| Project | `AlarmlistProjectName`, `AlarmlistProjectFile`, `AlarmlistProjectFullPath`, `AlarmlistProjectDirectory`, `AlarmlistProjectExtension` | Project filename without extension, filename with extension, absolute path, directory, and extension respectively. |
| Project configuration | `AlarmlistConfiguration`, `AlarmlistPlatform` | Values captured from the evaluated project, or explicitly supplied by a standalone caller. |
| File | `AlarmlistThisFileName`, `AlarmlistThisFile`, `AlarmlistThisFileFullPath`, `AlarmlistThisFileDirectory`, `AlarmlistThisFileExtension` | The current alarm's ALMX file identity using the same name/file/full-path distinctions, including when an expression is inherited from another file. This is not an imported MSBuild props/targets file. |
| Alarm | `AlarmlistAlarmFullyQualifiedName`, `AlarmlistAlarmReferenceName`, `AlarmlistAlarmName`, `AlarmlistAlarmCode`, `AlarmlistAlarmCategory`, `AlarmlistAlarmDescription` | Literal identity/reference and resolved scalar values of the current alarm being compiled/previewed. Select raw fields through reference inheritance, then evaluate field dependencies in that alarm's context. Inherited expressions therefore see the child's resolved values. |

Alarm-field properties expose resolved values. With par1 assigned `001` in the
current alarm, file, or project, the confirmed example is:

| Field | Effective raw source | Resolved value |
| --- | --- | --- |
| Code | `$(par1)` | `001` |
| Description | `Inspect $(AlarmlistAlarmCode)` | `Inspect 001` |

Resolve Code before consuming its value in Description, regardless of XML field
order. A longer acyclic field chain works the same way. Keep the raw Code and
Description independently available for editing, copying, and provenance.

This does not recursively expand assignments: if par1 is the literal string
`$(Other)`, Code resolves to literal `$(Other)` and Description becomes
`Inspect $(Other)`. Neither insertion is rescanned. Likewise, escaped tokens
are literal output, not field dependencies. A field such as Code containing
`$(AlarmlistAlarmCode)`, or a Code/Description dependency cycle, is an evaluation
error under the proposed cycle rule; it does not return its own raw expression.

Finalize path separators/trailing-separator rules and missing-value behavior in
iteration one. Proposed defaults: extensions include the dot; directories retain
their root but otherwise omit a trailing separator; missing optional alarm strings
are empty; unavailable project/file context produces a diagnostic when referenced.
Use explicit input identity and captured project paths, never the process working
directory. Snapshot analysis must not access disk merely to expose path properties.
FullyQualifiedName and ReferenceName may be read as property values even though
their own XML contents remain outside expression evaluation.

### Scope and declarations

PowerShell searches the current scope before its parents, and a child declaration
can hide an outer declaration without changing it. Adapt that behavior to the
three ALMX scopes. [PowerShell scope rules](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_scopes?view=powershell-7.5).

Project declarations are visible throughout that project. File declarations supply
parameters and values for alarms in that file. Alarm parameter declarations are
also inherited through references, but assignments belong only to the alarm that
makes them. Build a visible parameter set from the current alarm and its bases
separately from the value lookup context. Then resolve each use through assignments
on the current alarm, its containing file, and the project, in that order. A base
assignment never supplies a value to a descendant, including through multiple hops.

The confirmed value-presence rule distinguishes three states: unknown parameter,
known parameter without an assignment, and assigned parameter (possibly empty).
An unassigned declaration does not hide an outer assignment. An explicitly empty
assignment wins and stops fallback. For a concrete alarm, report a used parameter
that remains unassigned after all scopes have been checked. An unused unassigned
declaration alone does not fail compilation; abstract alarms can also leave used
parameters unresolved. Exact diagnostic IDs/messages remain implementation work.

Validate duplicate declarations/assignments within each scope, but allow a child
to assign an inherited parameter without redeclaring it. Finalize XML spelling
and same-name declaration merging in iteration one. Do not infer assignment
presence from string emptiness. Retain separate declaration and winning-assignment
locations, and show shadowed assignments for diagnostics/completion explanations.
Declaration order within a scope does not affect lookup; collect inputs before
evaluating fields.

Keep lookup views immutable; do not copy assignments from bases into child value
maps or mutate dictionaries on other alarms. Missing, ambiguous, or cyclic
reference chains follow the compiler's existing invalid-reference handling,
rather than being hidden by property fallback. Inheriting declarations is not
recursive expansion of substituted strings.

Do not implement PowerShell execution, parent-scope mutation, Private/AllScope
options, or module scopes. Scope-qualified reads are not required by the initial
proposed grammar.

### Inherited-expression context

The owner selected approach A on 2026-10-06: evaluate inherited raw expressions
for the consuming child, then refined references to inherit parameter declarations
without assigned values. Bases provide reusable text and parameter names. Each
child obtains values through Child -> Child's File -> Project; no base assignment
acts as a default. Well-known alarm-field values expose the consuming child's
resolved fields, evaluated from its effective raw template content.
Use this context for both scalar fields and every surviving procedure entry,
even when a section combines entries from several bases.

For the owner's example, A declares par1 and assigns `001`. B references A and
inherits par1 without an alarm-level assignment. If B's file assigns `002`, B
uses `002`; otherwise it checks the project. If A is concrete, it uses its own
`001`; if marked abstract, it has no compiled alarm entry. A's assignment is never
B's fallback. A concrete B using par1 reports an error when neither B, its file,
nor the project assigns it.

Base alarms can reside in other files, but neither their assignments nor their
file-scope values enter the child's fallback chain. File well-known values describe
the child's file. Retain three independent origins: the raw expression's source,
the parameter declaration (which may come from a base), and the winning assignment
(current alarm, current file, or project).

This means one source expression can produce different results for different
children. Context changes must invalidate affected results, and property
explanations must distinguish declaration visibility from the winning assignment.
The benefit is independent configuration of similar alarms; the tradeoff is that
reusable value defaults must live at file/project scope or be assigned by each
alarm. No separate template-default mechanism is added in this milestone.
Copying an expression locally preserves its result while the reference/context
stay unchanged. Linked projects remain independent, and substituted strings are
never expanded again.

This parameter rule does not remove raw scalar/procedure inheritance: for example,
a literal Code in a base remains template content under the existing resolver.
Parameterizing that field allows each child to supply its own value. The explicit
`abstract` attribute controls whether an alarm is used purely as a template.

### Abstract template alarms

The owner chose `abstract` as an XML attribute on Alarm. An alarm marked
`abstract="true"` contributes raw template content and parameter declarations to
referencing alarms, but produces no entry in the compiled AlarmList. It may leave
parameters unresolved; concrete consumers resolve their surviving expressions
using their own Alarm -> File -> Project assignments. Assigned parameter values
on an abstract alarm still do not become defaults for descendants.

Proposed details to finalize in iteration one:

- Omitted `abstract` or `abstract="false"` means concrete. The flag is local to
  each alarm and is not inherited. Thus an ordinary Alarm referencing an abstract
  base is concrete; an intermediate template explicitly marks itself abstract.
- Both abstract and concrete alarms can be reference targets. Abstract alarms
  remain in the bound reference graph, name checks, detached analysis, reference
  picker, provenance, and source navigation. Do not remove them before binding.
- Validate XML, declarations, literal Boolean values, expression grammar, and
  references on templates, including unused ones. Abstract does not hide duplicate
  identities, missing/self/cyclic references, or malformed source. Defer
  context-dependent unknown/unassigned-value checks to surviving effective uses
  in concrete consumers; retain the existing Clear/override selection rules.
- Preserve the marker in source models, serialization, and unrelated designer
  edits. It is not an expression or a compiled-output attribute. Carry abstract
  state separately from invalid-reference or failed-evaluation state in analysis.
- Keep template source and effective raw inheritance visible in the designer,
  with a template-only status and declared/unassigned parameter information.
  Do not present an abstract alarm as a resolved compiled alarm or treat its
  intentionally unresolved parameters as a failed concrete preview. The project
  XML preview contains only concrete alarms, using the same compiler path as Build.
- Author the attribute through the designer's Abstract checkbox or directly in
  XML; both paths edit the same source state. A valid project containing only abstract
  alarms produces an empty compiled AlarmList, with no missing-value errors from
  template-only expressions. Toggling the attribute must invalidate output,
  diagnostics, and preview availability through the normal snapshot pipeline.

### Proposed project and source example

```xml
<!-- Inside an SDK-based .almproj; project-scope definitions. -->
<ItemGroup>
  <AlarmlistProperty Include="PlantName" Value="North" />
  <AlarmlistProperty Include="AreaLabel" Value="$(Configuration) area" />
</ItemGroup>
```

```xml
<Alarmlist>
  <Properties>
    <Property Name="PlantName" Value="South" />
  </Properties>
  <Alarm abstract="true">
    <FullyQualifiedName>Pump.Base</FullyQualifiedName>
    <Properties>
      <Property Name="PlantName" Value="East" />
    </Properties>
    <Code>P00</Code>
    <Name>$(PlantName) pump temperature</Name>
    <Description>Inspect $(AlarmlistAlarmCode) in the $(AreaLabel).</Description>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Pump.HighTemperature</FullyQualifiedName>
    <ReferenceName>Pump.Base</ReferenceName>
    <Properties>
      <Property Name="PlantName" Value="West" />
    </Properties>
    <Code>P42</Code>
  </Alarm>
</Alarmlist>
```

The iteration-one contract fixes `<Property Name="..." Value="..." />` as the
declaration/assignment form. Omitting Value leaves it unassigned; `Value=""`
assigns empty. A child may assign an inherited parameter using the same form.
Pump.Base is an abstract template and produces no output entry. Pump.HighTemperature
is concrete because it omits the attribute; its reference does not make it abstract.
For Pump.HighTemperature in Debug, the inherited expressions produce
`West pump temperature` and `Inspect P42 in the Debug area.` Removing its PlantName
assignment exposes South from the child's file, then North from the project if
the file assignment is absent. The parameter remains visible through Pump.Base,
but its East assignment never supplies the child's value.

If Pump.Base moves to another file, its parameter declaration still reaches the
child; neither its East assignment nor its containing file's values are inherited.
For a multi-hop Child -> Base -> Root chain, a Root declaration reaches Child, but
assignments on Root and Base remain local to those alarms. Child still looks to
its own file/project when it has no assignment, even for expressions from Root.

MSBuild evaluates `$(Configuration)` in the project;
the compiler receives the resulting string. Expressions in the ALMX file are
evaluated by Alarmlist. SDK documentation must explain these two boundaries and
show how to pass literal dollar/semicolon/XML characters without double expansion.

Define project items during project evaluation, so the build task and CPS
can capture the same values without running Build. Target-time mutation is outside
the live-preview guarantee and must be documented. Unsaved ALMX buffers participate
immediately; project-file edits participate after CPS reevaluation, not through a
second project-file parser in the extension. File/alarm definitions participate
through the same captured ALMX text, including unsaved XML-only buffers. Include
the confirmed parameter table and Abstract checkbox in the designer. Their edits
use the existing source-buffer editing pipeline, so XML and designer views agree;
completion must explain the active scope and declaration origin.

## Evaluation order and provenance

1. Capture project definitions and parse raw ALMX, including the located abstract
   attribute, file/alarm parameter declarations, and assignments with explicit
   assignment presence, using the existing XML restrictions.
2. Bind all literal alarm identities and references, including abstract alarms,
   preserving ALM0001-ALM0004 and template source/grammar validation.
3. Select effective scalar origins using raw null/empty fallback, and resolve
   procedure order and Clear exclusions with the current resolver.
4. Resolve visible parameter declarations through the bound base chain without
   inheriting assignments. Construct immutable value lookup contexts from the
   current alarm, its file, and the project, with captured identity/configuration
   properties. For each concrete alarm, evaluate selected scalar expressions in
   field-dependency order, exposing resolved field values to their dependents and
   to surviving procedure text. Scan each source expression once and never rescan
   substitutions. Keep abstract template expressions raw, without requiring
   their parameter values or field dependencies to be resolved independently.
   Do not change syntax or raw local/excluded entries.
5. Produce detached analysis for all alarms and compiled values for concrete
   alarms from the same bound data. Filter abstract alarms in compilation, before
   serializing through the existing `AlarmList` output writer, so all APIs agree.

Resolve field dependencies in the compiler, keyed by current concrete alarm and
captured context; never cache an inherited expression's resolved value solely by
its base/source origin. Dependencies come from parsed tokens in selected effective
raw fields, not from inserted strings. Child overrides can break cycles present
in template content; analyze the concrete child's effective graph. Procedure text
can read resolved scalar fields, while overridden fields and Clear-excluded
procedure entries do not contribute evaluation dependencies.

Use explicit evaluating/resolved/failed states or an equivalent dependency traversal
to detect direct and indirect field cycles and propagate unavailable values to
dependents. Add dedicated expression diagnostics with use locations and concrete
alarm context; retain ALM0001-ALM0004 for alarm identity/reference failures. Exact
diagnostic design remains iteration-one work. Build, detached analysis, and XML
preview must use the same field evaluation results and failure behavior.

A nonempty raw value such as `$(EmptyValue)` is still a local override when its
expansion is empty. It does not fall back to the referenced alarm. Existing
`<Name/>` fallback continues unchanged. Keep an inherited expression's defining
alarm/source location separate from its evaluation context. A linked source can
resolve differently in North and South without sharing semantic state.

Apply the confirmed [inherited-expression context](#inherited-expression-context)
consistently to scalar values, surviving procedure text, well-known values, and
property explanations. Retain the defining source for provenance. Completion for
local drafts and copy commands must use the same current-alarm lookup and expose
both the inherited parameter declaration's origin and the winning assignment's
origin where applicable.

Do not expand entries removed by Clear for that alarm's effective section. A
surviving use in another concrete alarm can still produce an error. Abstract
templates do not require value resolution on their own, but source/reference
validation still applies. Retain raw excluded entries and their existing origins.

Expansion failures suppress compiled XML/output. The proposed conservative
analysis behavior marks the affected concrete alarm's resolved projection unavailable,
retains its raw local data and diagnostics, and leaves unrelated valid alarms
inspectable. Never expose a partially substituted value as successfully resolved.
Property diagnostics use new stable IDs; choose them from the actual descriptor
table in iteration one. Locate a failure at the ALMX use, with its source version;
use a containing declaration when decoded XML has no reliable token span.
ALMX declaration errors point to the declaration, with related locations for
same-scope duplicates. Host-supplied configuration failures may be location-free.
Do not invent source locations for evaluated MSBuild items or fabricate ALM codes
in the VS adapter.

## Compiler, SDK, and extension design

Add immutable compiler context objects and update the public entry points together.
All supported entry points perform expression evaluation; a context-free call can
still use available ALMX/alarm data and reports unavailable host properties when
referenced by a concrete alarm. All entry points retain abstract definitions for
binding/analysis and omit them from compiled results. Include syntax-tree
compilation: it currently bypasses the ordinary
source-binding entry point. Centralize resolved-value evaluation so
file compilation, snapshot compilation, and optional XML preview cannot diverge.
Keep the compiler independent of Microsoft.Build and Visual Studio assemblies.

Keep parser/source information, reference binding, expression evaluation, and
output responsibilities separate. This adapts Roslyn's explicit
[compilation options](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Compilation/CompilationOptions.cs)
to this smaller compiler; it does not require Roslyn dependencies, a full syntax
tree redesign, or a new solution-wide workspace.

The task and SDK own transport of project definitions and well-known project
values. File/alarm definitions are compiler inputs from ALMX, not MSBuild-expanded
text. Extend the syntax, parser, source serializer, and analysis to retain them;
preserve them during unrelated designer edits, along with unknown XML and comments.
Source serialization writes raw local expressions/declarations and the abstract
attribute, never evaluated display values.
Retain each alarm's containing source scope when Binder combines input trees;
do not flatten file declarations into a project-wide dictionary or change Compile
order/duplicate handling. Only resolved concrete alarm values belong in final compiled
output. Extend `tools/Alarmlist.props`,
`tools/Alarmlist.targets`, and CPS rules only as required to expose evaluated data.
Keep shared engineering policy in the root/`eng` configuration and preserve the
pinned `Arkarin0.DotNet.Arcade.Sdk` 1.0.0-Preview imports. Do not add a required
TargetFramework, change Compile ordering, or change Clean/build lifecycle behavior.

The analysis bridge must copy new data into host-owned immutable DTOs without
passing SDK objects across AppDomain boundaries. Define one expression-aware
contract and update the compiler, `AnalysisContractReader`, worker, and consumers
together. Its exact API/version is an iteration-one deliverable. It must distinguish
local raw text, inherited effective raw text, evaluated text, scope/declaration
origins, and reference provenance for fields and surviving procedure entries.
Parameter data must separate inherited declaration identity/origin, local assignment
presence (including explicit empty), and winning value/assignment origin; a visible
parameter does not imply that the current alarm has assigned a value.
Carry the source-local abstract state and distinguish template-only projection
availability from evaluation failure. Abstract definitions remain available for
provenance and navigation while CompiledXml excludes their alarm entries.
Do not reconstruct raw expressions from expanded strings in the extension.

Capture project property names/values and well-known project values in
`AnalysisProject`; include file/alarm declarations through captured input snapshots.
Include scope/context data in equality, cache identity, scheduling, publication,
and command validation, including abstract-state edits. A value-only change must invalidate current diagnostics,
completion, and previews even when Compile inputs and SDK path stay the same.
Retain immediate stale-result clearing, cancellation/generation checks, buffer
precedence, linked-project isolation, off-UI-thread calls, and disposal guarantees.

Preserve weak document observation when extending property refresh subscriptions;
the analysis service must never become an owner of a VS document or buffer.
Convert SDK metadata into presentation rows at the UI boundary. Bind completion
choices and property rows through `BindingCollection` and the existing notification
base, while keeping analysis DTOs independent of WPF. Dispose per-view completion
subscriptions and release obsolete rows/contexts on close. Extend the existing
document-observer and designer collectibility regressions to cover these new
objects and configuration changes.

### Coordinated unreleased update

Backward compatibility with earlier unreleased APIs, literal interpretation, or
SDK/VSIX combinations is not an implementation requirement, per the owner's
2026-10-06 decision. Remove the old opt-in and legacy-bridge proof from this plan.
Update affected tests, templates, samples, and documentation with the new contract;
do not build a second evaluator or fallback mode for the old behavior.

The installed extension must still validate its selected SDK's contract and report
an unsupported/mismatched package clearly. Never substitute another compiler or
silently publish incorrect results. Preserve isolation, source/output protection,
and the SDK-first distribution workflow. Update AGENTS.md's contract-specific
guidance when the implementation changes the checked-in API and supported workflow.

## Designer behavior

Include a parameter table and an Abstract checkbox. The table presents parameter
names, editable local assignments, and compiler-supplied declaration/value origins.
Distinguish inherited declarations and effective outer-scope values from local
assignments: displaying a value does not create an assignment or copy a base value.
Edit the chosen local source scope explicitly, preserving other scopes and alarms.
Design table layout and assignment-state controls around the confirmed distinction
between an unassigned value and an explicitly empty assignment.
Provide an explicit way to remove a local assignment and resume file/project
fallback; clearing an assigned value to empty retains an explicit assignment.

The Abstract checkbox edits the selected alarm's `abstract` XML attribute. Toggling
it updates diagnostics, template status, and preview availability through normal
analysis. Both controls preserve surrounding XML/comments and participate in the
existing draft, validation, Undo/Redo, keyboard, and accessibility workflows.
XML edits refresh the controls without overwriting active drafts. Bind table rows
through the established notifying collection/presentation patterns and dispose
subscriptions on close.

Abstract alarms remain selectable/editable and eligible reference targets, with
visible template-only status. Their raw template and parameter information remain
available even when parameters have no assignments; their resolved compiled-alarm
preview is unavailable by design. Preserve this distinction from stale or invalid
analysis, and preserve the abstract attribute during source edits and local copies.

Unfocused expression fields show the current evaluated value. On focus, switch
editable controls to raw source/draft text; on successful commit and focus loss,
return to the evaluated display when current analysis is available. Procedure
text follows the same rule. Merely focusing, selecting, copying, or leaving an
unchanged field must not dirty the document or create an undo transaction.

Keep presentation text separate from draft/source bindings. Never commit the
displayed expansion as source, overwrite an active draft during a scope refresh,
or manufacture an override when focusing an inherited read-only value. Inherited
values remain read-only until **Override locally** starts a raw-expression draft.
Commit conflicts/invalid edits retain the raw draft and error; pending or unavailable
analysis shows raw text with a status instead of a stale resolved display. Existing
preview panels still clear during drafts and stale analysis.

Inheritance adorners describe reference provenance; property explanations identify
the declaration origin and the winning assignment/scope separately, including
declared-but-unassigned state. Use SDK-supplied metadata/results rather than a
WPF evaluator. Show ALMX declaration origins where available; do not invent
navigation spans for evaluated project properties. Preserve focus/caret/selection
through raw/resolved transitions and keep selection/copy behavior appropriate to
the displayed text, without source edits.

Provide keyboard-operable suggestions after `$(` and through an explicit completion
command in the supported scalar and procedure controls. Offer well-known names and
custom names visible through declarations and outer scopes, including inherited
parameters without a value. Show declaration origin, winning assignment scope and
shadowing, or unassigned status. Insert only the expression token into the raw draft at the caret.
Completion changes the active draft; it does not commit or save independently.
Escape dismisses completion first, then retains the existing draft-cancel behavior;
Enter accepts a selected suggestion before the normal commit behavior resumes.
Treat a completion popup as part of the field's editing session so moving focus
into it does not prematurely commit or switch back to resolved text.
Preserve selection, focus, multiline input, Undo/Redo, and contextual action names.
Pending/unsupported analysis must not offer stale values or stale property names.

Use the existing live-status event behavior for visible property-analysis errors
and availability changes. Test event delivery without stealing focus or announcing
hidden/closed content. UI Automation events and accessible names are required
automated evidence; actual spoken completion/status announcements remain a
separate live accessibility check.

**Override locally** copies the defining effective raw value, including expression
tokens and existing dollar escapes. **Make local copy** copies the surviving
procedure entries' raw expressions in execution order, with a leading Clear and
each entry once. The copied structure stops following the reference, while its
expressions continue to respond to applicable properties. Do not freeze expanded
text, double-escape dollars, or silently copy property declarations from other scopes.
Reevaluate copied expressions with current-alarm -> current-file -> project value
lookup. Copying within the same alarm preserves the result while its
reference/context remain unchanged. Current-alarm, current-file, and project
assignment changes flow according to precedence; base custom-parameter assignments
do not supply descendant values. Changing ReferenceName or base declarations can
change the visible parameter set. Keep current-input validation,
unsupported-content refusal, and one document edit. Undo restores the original
source; unrelated XML remains intact.

## Implementation sequence and acceptance

Each iteration should leave the existing suites passing and append its actual
validation evidence here. These are dependency-ordered slices, not delivery dates.
Add functional xUnit cases with each user-facing slice; the last iteration performs
the full host acceptance rather than postponing all live coverage until the end.

| Iteration | Work | Acceptance before proceeding |
| --- | --- | --- |
| 1. Finalize scoped-property design | Apply the confirmed child-context, parameter/value, resolved field-read, designer, and abstract-template decisions; finalize the attribute's Boolean/default rules, catalog/names, declaration/assignment syntax, same-name declaration merging, grammar, field-dependency diagnostics, and one compiler/analysis contract. Specify focus/copy examples and record the current test/host baseline. | Examples cover abstract-to-concrete references, unresolved template parameters, cross-file/multi-hop declarations without inherited assignments, unknown/unassigned/explicit-empty states, resolved field reads, cycles, and value-preserving local copies; the contract separates template state, raw expressions, declaration origins, assignment presence, and winning value origins. No opt-in or legacy compatibility work is required. |
| 2. Add integrated evaluation, abstract alarms, and well-known properties | Parse/preserve the abstract attribute and evaluate expressions after reference/Clear resolution. Add per-alarm resolved scalar dependencies and cycle detection while keeping custom assignments literal and replacements unrescanned. Retain templates for binding and omit them from compiled results; connect all supported compiler entry points and diagnostics. | Both compiler frameworks pass; expressions evaluate without activation; resolved field chains are independent of XML order; direct/indirect cycles fail without output replacement; concrete descendants compile while abstract entries are omitted; template source/reference errors still fail; raw text/provenance and literal identity/reference fields survive. |
| 3. Add scoped custom declarations and SDK transport | Parse/serialize located declarations and optional assignments; inherit alarm parameter declarations without values through binding; pass project definitions through the public API/task/SDK. Add source-preservation tests, isolated consumer assets, and documentation. | Alarm -> current File -> Project values, inherited unassigned parameters, no base-assignment fallback, explicit-empty shadowing, sibling isolation, and same-scope duplicate errors agree on net8.0 and desktop net472; unsaved inputs work; failures preserve previous output. |
| 4. Extend detached analysis and the isolated bridge | Update the contract/reader/worker together with abstract state, effective raw expressions, evaluated values, visible declarations, assignment presence, and separate origins. Share evaluated values with XML preview and update packaged contract tests. | Analysis retains abstract templates while CompileSnapshots and built XML emit only concrete alarms; intentionally unresolved templates remain distinct from failures; unassigned and explicit-empty remain distinct; raw expressions needed by copy actions survive the bridge; an incompatible SDK is reported explicitly; no compiler assembly appears in the VS host. |
| 5. Capture and invalidate scope context | Extend CPS project capture, input/state identity, linked contexts, and asynchronous validation; observe declarations, assignments, and raw field changes through ALMX buffers. Add functional xUnit cases for declaration edits and context changes. | Declaration/reference, applicable assignment, and referenced field changes invalidate affected results and dependent displays; changing only a base custom-parameter assignment never supplies a descendant value; old results cannot publish; linked files use the right project; buffer precedence, weak ownership, and cleanup remain intact. |
| 6. Deliver parameter editing, focus-dependent fields, and expression copies | Add the parameter table and Abstract checkbox with source-preserving edits; switch resolved/raw display without changing source; add scope-aware completion, template-only status/preview handling, and raw-expression copies for scalar/procedure actions. Extend FlaUI keyboard, live-status, and collectibility tests. | Parameter assignments and abstract state can be edited without switching to XML; XML/designer updates and Undo/Redo agree without mutating base or unrelated scope values; abstract templates remain editable/referenceable with honest preview availability; concrete project XML excludes them; focus-only interaction never dirties source; popup/Enter/Escape/Undo behavior works; copies preserve tokens/escapes; obsolete views/contexts are collectible. |
| 7. Validate both hosts and prepare release evidence | Run functional UI xUnit on VS2022 and VS2026, extend the interactive linked fixture, collect isolated/live performance evidence, and document the new syntax/catalog/API. | Fresh-cache package checks and all selected functional cases pass; measurement reports are complete; milestone-three release gates and new UI acceptance are resolved before distribution. |

## Validation matrix

| Layer | Required scenarios |
| --- | --- |
| Compiler grammar | Evaluation without activation, mixed/multiple expressions, case rules, unknown/malformed names, unsupported `loc:`, empty values, dollar escaping, inserted custom/field-result expressions remaining literal, escaped field tokens creating no dependencies, XML special characters, repeated calls with immutable prior results. |
| Alarm-field dependencies | Code=`$(par1)` with par1=`001` makes Description=`Inspect $(AlarmlistAlarmCode)` resolve to `Inspect 001`; multi-field chains and XML-order independence; direct self-read and indirect cycles; dependency failure propagation and output preservation; child overrides breaking template cycles; inherited fields evaluated per concrete child; procedure reads of resolved fields; raw expressions retained; custom assignments and escaped dependency-looking strings never rescanned. |
| Abstract templates | True/false/omitted/invalid attribute values; non-inherited marker; abstract-to-abstract-to-concrete chains across files; concrete bases still supported; used but unresolved parameters permitted in templates and diagnosed in concrete consumers; unused invalid templates still receive source/reference diagnostics; concrete-only output and empty output for all-abstract projects; round-trip attribute preservation, unsaved toggles, designer/reference navigation, and compiled-preview parity. |
| Scopes and declarations | Current Alarm -> current File -> Project value precedence; declarations inherited across files/multiple hops without assignments; no base/base-file value fallback; inherited unassigned parameters permit outer-scope lookup; explicit-empty assignments stop it; unknown versus unassigned diagnostics; unused unassigned declarations; same-scope duplicates versus child assignment of inherited parameters; sibling isolation, declaration order independence, unsaved/removed inputs, and source preservation. |
| Resolution | Local and inherited expressions use the current alarm's context; A's par1=001 does not supply B's value; well-known values describe the current alarm/file; raw-expression local copies preserve values while reference/context stay unchanged; raw scalar/procedure inheritance remains intact; raw-empty scalar fallback versus explicit-empty parameter assignment; invalid chains, independent sections/Clear, and separate expression/declaration/assignment origins and versions. |
| API and output | All supported source/snapshot/syntax-tree paths, explicit/context-free host data, detached local/effective-raw/evaluated values, unavailable context diagnostics, unrelated valid alarms, analysis/compiled XML parity, no snapshot disk I/O, no output replacement on failure. |
| Task and SDK | Integrated project/file/alarm built-ins, custom/conditional/removed project definitions, scoped ALMX inputs, item metadata escaping, both MSBuild hosts, no TargetFramework, unchanged Build/Clean/Rebuild/design-time guards, package payload/dependencies. |
| Bridge and CPS | Coordinated new contract, explicit incompatible-SDK failure, package-selected compiler isolation, changes at every scope, linked contexts, reevaluation during analysis, navigation/copy revalidation, weak ownership, unload/disposal. |
| Designer | Parameter-table editing and Abstract checkbox synchronization with XML; local assignments separated from inherited/effective displays; unset resumes fallback while explicit empty stops it; dependent-field refresh and cycle diagnostics; no base/sibling mutation; unfocused resolved/focused raw display, no source mutation on focus-only interaction, drafts surviving semantic refresh, completion popup focus/Enter/Escape, expression-preserving copies and later property changes, exact dollar escapes, one Undo action, surrounding XML/preview selection, live-status events, and collectibility. |
| Functional IDE xUnit cases | Per-host parameter-table keyboard editing, Abstract checkbox toggles, XML/designer round trips and Undo/Redo; scope/configuration changes, linked North/South values, unsaved file/alarm definitions and inherited expressions, no-Build diagnostics/preview, matching saved build output, real focus transitions/completion/Undo, expression copies, and close/discard/reload/unload. Retain source/output hashes and failure artifacts. |
| Performance and diagnostics | Property-heavy bridge analysis, designer publication/refresh, completion queries, and context changes; separate live latency/close-memory scenarios; complete report/ownership validation; targeted heap/root captures if lifetime regressions require attribution. |
| Interactive acceptance | Actual spoken completion/status output, native high contrast, mixed-DPI transitions, precise typing/paint response, and the remaining milestone-three host/memory checks. Automated keyboard functionality or UIA events do not substitute for these measurements/observations. |

Extend existing tests and helpers rather than introduce another test framework:
compiler `Text`/`Binding`/`Analysis`/`Output` tests, task tests, SDK `testassets` with
`TestProjectCollection`/`CreateTestApp`, VS `Analysis`/`Editing` tests, and packaged
VSIX/template consumers. The package-test approach follows Arcade's
[TestProjectFixture](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestProjectFixture.cs)
and [TestApp](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestApp.cs),
adapted through this repository's existing isolated feed/cache and retained logs.

### Test ownership

| Project or tool | Milestone-four responsibility |
| --- | --- |
| Compiler, MSBuild, and SDK tests | Grammar, resolution, context transport, failure paths, output parity, package contents, and consumer lifecycle on both frameworks and the appropriate MSBuild hosts. |
| `Alarmlist.VisualStudio.UnitTests` (net472) | Bridge/CPS/editor tests, real BAML, live-region event checks, and document/designer collectibility. Extend `EditorTests.Lifetime`, `LiveStatusTests`, and `VsAnalysisDocumentsTests`; keep test-host assets aligned with the pinned VS SDK. |
| `Alarmlist.VisualStudio.IntegrationTests` (net472/net8.0) | Packaged VSIX/template consumers and package boundaries; these tests do not launch the IDE. |
| `Alarmlist.VisualStudio.UIIntegrationTests` (net8.0-windows/x64) | All functional IDE assertions as individual xUnit cases, using shared C# DTE/shell automation and FlaUI 5 for real completion/keyboard input. Extend the existing workspace/session helpers and traits; keep functional assertions out of PowerShell. |
| `Alarmlist.VisualStudio.PerformanceTests` (net472) | Opt-in Release/x64 BenchmarkDotNet bridge/WPF scenarios, opt-in live latency/memory wrappers, and ordinary report/ownership validation tests. Reuse `src/shared/VisualStudioTesting/LiveVisualStudioRun.cs` and the dedicated live driver. |
| `eng/tools/VisualStudioHeapProbe` (net8.0/x64) | Optional heap/root/thread diagnostics for an explicitly selected test process, separately from timing and normal tests. It remains outside the VSIX. |

### Ordinary build and package validation

Run affected suites first, then downstream suites for shared compiler/SDK changes.
For final implementation validation, from the repository root:

```powershell
dotnet build AlarmlistProject.slnx
dotnet test AlarmlistProject.slnx
```

Omitting `--framework` covers both repository frameworks where applicable; the
VS host/unit/performance projects remain net472 and the functional UI runner is
net8.0-windows. Ordinary solution tests skip live IDE cases and measurements, so
a green solution run alone does not establish IDE or performance acceptance.
Additionally run the SDK suite against desktop
MSBuild as documented in the [SDK test README](../SDK/Alarmlist.SDK.Tests/README.md),
restoring the prior `ALARMLIST_TEST_MSBUILD_PATH` value afterward. Do not use
`--no-build` against stale SDK packages.

### Functional IDE validation

Build and deploy the matching extension/SDK configuration to the chosen experimental
profile, complete first-run screens, and use an unlocked desktop. The test launcher
builds test assets but does not deploy or verify the installed DLL against the
checkout; record the deployed build and configuration with the results.

```powershell
powershell.exe -NoProfile -File eng/test-visualstudio.ps1 -VisualStudioPath '<VS2022 installation>' -Configuration Debug -RootSuffix AlarmlistExp
powershell.exe -NoProfile -File eng/test-visualstudio.ps1 -VisualStudioPath '<VS2026 installation>' -Configuration Debug -RootSuffix AlarmlistExp
```

Run these sequentially. The launcher enables `ALARMLIST_RUN_UI_VS_TESTS`, selects
the host, restores environment values, and rejects empty/skipped selections.
Direct xUnit invocation requires that opt-in plus explicit host paths/profile, as
documented in the [UI suite README](Alarmlist.VisualStudio.UIIntegrationTests/README.md).
Traits alone do not enable live tests. Use the same configuration/profile as the
deployment; these commands illustrate Debug on the default experimental profile.

Each case owns a fresh IDE and isolated fixture/feed/cache. Preserve the shared
live-test mutex, foreground/target-field checks before input, retained failure
artifacts, and cleanup of only the owned process. Functional tests close their
IDEs; `-KeepOpen` is not supported for that suite. Retain xUnit/TRX and scenario
artifacts under `artifacts/log/<Configuration>/VisualStudioUI/`.

For interactive work, extend the linked fixture prepared by
`eng/prepare-visualstudio-acceptance.ps1`. Preserve the
[milestone-three acceptance record](ACCEPTANCE.md); add milestone-four scenarios
and record actual/not-run outcomes separately. Fixture preparation does not
constitute either automated or interactive acceptance.

### Performance and heap evidence

Use the [performance project workflow](Alarmlist.VisualStudio.PerformanceTests/README.md)
for property-heavy scenarios and context changes:

```powershell
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1 -Job Short
```

This runs xUnit-hosted BenchmarkDotNet in Release/x64: Short uses two warmups and
five measurements; Dry validates the harness only; Full uses default sampling.
Collect a matching current baseline with the same method and configuration.
Milestone-three Stopwatch results remain historical evidence and are not directly
comparable to the new benchmark method. Keep correctness checks outside measured
intervals and retain raw CSV/JSON, summaries, TRX, and revision/runtime metadata
under `artifacts/log/Release/AnalysisPerformance/`.

Run live wrappers separately in PerformanceTests with `ALARMLIST_RUN_LIVE_VS_TESTS=1`
and explicit VS2022/VS2026 paths, configuration, profile, and sample counts. Follow
the [live wrapper instructions](Alarmlist.VisualStudio.PerformanceTests/README.md#live-visual-studio-wrappers)
and restore prior environment values. The wrappers invoke
`eng/measure-visualstudio-live.ps1`; `test-visualstudio.ps1 -MeasureAnalysis` remains
a compatibility route to that driver, not part of functional UI coverage. Require
complete reports, both warmups per mode, source/output preservation, and owned-IDE
cleanup. Retain wrapper evidence under `LiveVisualStudioTests/` and linked raw
latency/memory samples under `VisualStudioSmoke/` in the configuration's log root.
Do not run builds, functional live tests, or other benchmarks alongside measurements.

Use the separate [heap diagnostic tool](../../eng/tools/VisualStudioHeapProbe/README.md)
through `eng/capture-visualstudio-heap.ps1` when attribution is needed. Preserve
PID/start-time validation and unique output paths; compare equivalent lifecycle
points and inspect roots. Do not force collection in the inspected IDE or equate
reachable objects, allocated benchmark bytes, or process-memory totals with a leak.
Keep diagnostic captures outside timing runs. No machine-dependent performance
or memory threshold is added; polling latency still does not measure typing/paint.

## Completion criteria and planning validation

Milestone four is implementation-complete when the compiler, packaged SDK, and
designer deliver the scoped workflow with build/analysis parity and regression
checks satisfied. Release acceptance additionally requires the carried-forward
host gates, fresh-cache verification, and SDK-first distribution described in
the [Visual Studio README](README.md#sdk-first-release).

The original planning-only refresh updated documentation and roadmap links, using
existing evidence and a diff/path/command review. Newly implemented behavior and
executed checks are recorded in the iterations below. No publication is part of
this work.

## Implementation record

### Iteration one: contract and baseline

Completed the [implementation contract](MILESTONE-4-DESIGN.md): source/project
spelling, explicit assignment presence, scope/template rules, resolved field
dependencies, diagnostic IDs, context API, contract-2 bridge direction, and
designer editing requirements. Inspected compiler/binder/parser/analysis, SDK
transport, bridge, and existing tests. Consulted Roslyn compilation options and
the repository's pinned Arcade SDK imports/testing conventions.

Corrected the shared UnitTests.props import condition to compare explicitly with
true, so an unset IsUnitTestProject no longer produces MSB4113 during evaluation.
The first sandboxed baseline exposed that error; the next attempt needed installed
Windows SDK access. The authorized unrestricted test run completed successfully.

Validation: `dotnet test AlarmlistProject.slnx --nologo -v:minimal` using .NET SDK
10.0.401 passed: compiler 101 per framework, task units 6 per framework, task
integration 4 per framework, SDK 19 per framework, VS units 220, VS package tests
10 per framework, performance infrastructure 26. Total: 526 passed, 18 expected
opt-in skips (14 functional IDE cases and 4 measurements). No live IDE, interactive
acceptance, or performance measurement was executed. Existing host acceptance
remains the milestone-three record. Documentation XML/link checks and
`git diff --check` also passed. Unrelated playground/launcher work is excluded
from this iteration's commit.

### Iteration two: compiler expressions and abstract templates

Implemented the compiler-owned Evaluation layer, immutable captured context,
single-pass token/escape parsing, well-known project/file/alarm properties,
resolved scalar dependencies and cycle diagnostics. Abstract alarms remain bound
but are omitted from output; concrete children evaluate inherited raw expressions
in their own context. Clear-excluded procedure values are not evaluated. Source
serialization retains local raw expressions and the abstract flag.

All compiler entry points use the evaluator; direct syntax-tree compilation now
binds references as well. Invalid reference chains no longer contribute partial
compiled alarms (the prior failure-result assertion was updated); diagnostics and
the no-output-on-errors build behavior remain. Analysis now includes evaluator
diagnostics and suppresses failed previews; detached evaluated field/provenance
data and the context-aware bridge remain iteration four.

Validation: compiler tests pass on net472 and net8.0, 129 per framework, including
the final invalid-path/direct-tree checks. The ordinary solution regression run
passed with 578 tests and 18 expected opt-in skips before those last two tests and
the path guard; the final focused compiler run passed afterward. No live IDE or
performance measurement was run. Existing CA1416 shared-test-helper warnings remain.

### Iteration three: scoped parameters and SDK transport

Added file/alarm Properties parsing and source serialization, immutable parameter
definitions with assignment presence, same-scope validation, and source-file scope
retention through binding. Resolution uses current alarm -> current file -> project;
references contribute declarations only. Explicit empty assignments stop fallback.
The task and SDK now pass evaluated AlarmlistProperty items and captured project,
configuration, and platform context. The SDK README documents syntax, well-known
values, escaping, template behavior, and diagnostics.

Validation: `dotnet test AlarmlistProject.slnx --nologo -v:minimal` passed with 620
tests and 18 expected opt-in skips. Compiler coverage is 147 per framework; SDK
coverage is 20 per framework. The new packaged Properties scenario verifies both
configurations, literal inserted values, empty-versus-absent metadata, template
exclusion, and preservation of prior output on ALM0008. That scenario also passed
with desktop MSBuild from VS2022 and VS2026, using net472 tests and the already
rebuilt package. The temporary host-selection environment variable was restored.
No live IDE or performance measurement was run.

### Iteration four: detached contract 2 and isolated bridge

Compiler analysis now returns raw and evaluated fields/procedure text, local
abstract state, per-value availability, and visible custom/well-known properties.
Declaration and assignment origins remain distinct; source-level properties are
available even in files without alarms. Context-aware analysis uses the same
evaluator and scoped resolver as compilation. The AppDomain worker constructs SDK
context objects by reflection and returns only host-owned serializable data.
Contract 2 and compiled preview support are required together; contract 1 receives
explicit update guidance. Updated the repository/SDK documentation accordingly.

Validation: 22 focused bridge cases passed, including captured context, raw/value
separation, origins, domain unload, and rejection of older contracts. The first
solution run exposed an outdated package-test log assertion; after correcting it,
`dotnet test AlarmlistProject.slnx --nologo -v:minimal` passed with 627 tests and 18
expected opt-in skips (compiler 150 per framework, VS units 221). The expanded
packaged AnalysisContract scenario also passed through desktop MSBuild from both
VS2022 and VS2026 using the rebuilt assets. No live IDE or measurement was run.

### Iteration five: captured CPS context and invalidation

CPS captures evaluated Configuration, Platform and ordered AlarmlistProperty items
from the same ProjectInstance as Compile and the SDK path, preserving empty versus
absent Value metadata. Project snapshots own immutable context data; equality and
bridge replacement include its values and assignment presence. Analysis forwards
the captured context through the bridge and retains the existing generation,
buffer-identity and input-revalidation gates.

Validation: 38 focused project-analysis cases passed, then all 225 VS unit tests
and 10 VS package-consumer tests per framework passed. New cases cover conditional
items, duplicates/order, detachment from mutable MSBuild items, linked unsaved
North/South context isolation, and property-only changes during a non-cancellable
analysis call. Functional IDE scope cases are grouped with iteration six's completed
designer workflow; they have not run yet. No live IDE or measurement was run.

### Iteration six: scoped editing, focus display and completion

Implemented the alarm/file parameter table, explicit assignment presence and local
declaration controls, and the Abstract checkbox. Edits preserve surrounding XML,
reject ambiguous/unsupported declarations and concurrent changes, and use the
shared buffer's atomic Undo transaction. Project/template rows show separate
declaration and assignment origins. Scalar and procedure controls keep raw draft
bindings separate from evaluated displays. Focus alone does not change source;
completion supports automatic tokens, Ctrl+Space, arrows, Enter/Tab and Escape.
Inherited scalar/procedure copies retain raw expressions and escapes. Stale/draft
states suppress old evaluated values and invalid completion context.

Validation: 246 VS unit tests passed, including 21 new property/source-editing,
focus/completion, draft, preview and Undo cases. Existing BAML, live-region and
designer collectibility tests remain green. Packaged VSIX/template consumer tests
also passed: 10 per framework. Added four opt-in functional cases
(two per host) covering the integrated property workflow and linked project scopes.
The first UI compile found a test-only ComboBoxItem type mismatch; it was corrected.
VS2022's matching Debug extension was deployed to the idle AlarmlistExp profile.
Both new live scenarios built their isolated projects, but stopped at the foreground
safety check before keyboard input. Failure screenshots were black; no interactive
workflow pass is claimed. Both owned IDEs exited normally. These cases remain an
interactive-desktop validation gate, not a reason to weaken focus checks.

Evidence: `artifacts/log/Milestone4/iteration6-unit-final.trx` and
`artifacts/log/Debug/VisualStudioUI/TestResults/053503e9073c436ca66a0a70d3255179/`.

### Iteration seven: validation workloads and acceptance record

Extended the isolated benchmark fixture with 1,000 alarms across ten files, 32
project parameters, file shadowing, abstract templates, escapes and field
dependencies. Added changed-context bridge/UI publication and production
completion-filter measurements. Extended live measurement inputs with file
properties and field dependencies, retaining source/output hashes, owned-process
cleanup and the existing no-threshold/no-forced-IDE-collection rules. The linked
acceptance fixture now includes a property template and consumer, conditional
Debug/Release values, and a separate milestone-four interactive checklist.

Ordinary validation: `dotnet test AlarmlistProject.slnx --nologo -v:minimal` passed
652 tests on all supported target frameworks, with 24 expected opt-in skips.
The full packaged SDK suite also passed through VS2022 and VS2026 desktop MSBuild,
20 tests each. Logs are under `artifacts/log/Milestone4/Final/`. Fixture preparation
passed both project builds and output checks; its sources, hashes and solution are
under `artifacts/log/Debug/VisualStudioAcceptance/a47bce54827b4ef2aa4f84a3ea3cea39/`.
Preparation does not accept the interactive checklist.

Release/x64 Short benchmarks passed both xUnit workload cases (eight measurements
each), with two warmups and five iterations before BenchmarkDotNet outlier handling.
The comparison uses the same current implementation and ten-file/1,000-alarm size;
the property workload also adds template and parameter metadata, so it is not an
isolated estimate of token substitution cost. Means from this machine:

| Operation | Literal workload | Property workload |
| --- | ---: | ---: |
| SDK analysis | 271.9 ms | 475.4 ms |
| SDK analysis with alternate context | 264.0 ms | 481.3 ms |
| Designer publication/layout | 18.3 ms | 19.4 ms |
| Unchanged refresh/layout | 4.1 ms | 4.8 ms |
| Context-change publication/layout | 19.0 ms | 115.7 ms |
| 100 completion prefix queries | 0.087 ms | 0.142 ms |
| Draft/layout | 8.5 ms | 36.5 ms |

Raw CSV/JSON and reports are under `artifacts/log/Release/AnalysisPerformance/`:
property workload `3cc62a543bd745f2b911ee3607b5cb51`, literal comparison
`38158141994b45afa89bc6340dddbfb2`. These are warm isolated operations; context-change
layout includes refreshing changed compiled XML. Query batches do not measure
popup rendering, speech or physical typing/paint. No performance budget is asserted.

Both live wrappers passed against matching Release deployments in AlarmlistExp,
using 1,000 additional property alarms, two warmups and 20 measured samples per
mode. VS2022 medians were 450.6 ms (XML buffer) and 447.4 ms (designer reopen);
VS2026 medians were 449.6 ms and 452.9 ms. These include 50 ms polling/COM and live
IDE overhead. Both reports confirm preserved sources/output and owned-IDE cleanup:
`artifacts/log/Milestone4/Final/iteration7-live.trx`. Detailed wrapper evidence is
under `artifacts/log/Release/LiveVisualStudioTests/e031f1bfc60c453e8b2a13d4bb089cb5/`
and `85d793fbfe7c431c840e11bf97f74196/`.

The 20 designer-close samples ended with zero open documents. Private bytes grew
from 917.1 to 1,063.5 MB on VS2022 and 1,032.4 to 1,097.3 MB on VS2026; Gen2 counts
were 3 -> 3 and 3 -> 4. These short whole-process runs do not establish leak freedom
or replace the carried-forward memory/lifetime acceptance gate.

The six existing API-driven functional cases passed on each host (12 total),
covering templates/items, shared editors, both Save As paths, live diagnostics,
failure-output preservation and Clean/Rebuild. Their Release TRX runs are under
`artifacts/log/Release/VisualStudioUI/TestResults/405637b771704878a835cad642be8ed9/`
and `67ef533d46b54c59b6b248f0cc0b778e/`.

The linked-property scenario then used UIA selection instead of physical clicks.
It exposed an existing mode-selector accessibility defect: selecting a radio button
through UIA changed its checked state without executing its command. Mode changes
now use Checked for mouse, keyboard and assistive selection; failed draft commits
restore the selected mode. A regression verifies actual SelectionItem-provider
behavior. The scenario also now reselects the project after its XML-only helper
deliberately closes all designer views, preserving the per-view context contract.
VS2022's linked scenario passed after both changes, including North/South values,
configuration reevaluation, unsaved file precedence, diagnostics, discard and reload.
Evidence: `artifacts/log/Release/VisualStudioUI/TestResults/8d37db03a60e4d7aa2c4eef8522d87c5/`.
VS2026's linked case and shared-editor regression also passed with the selector fix:
`artifacts/log/Release/VisualStudioUI/TestResults/1fc2eaf39ab547ad8f91cf876407d627/`.
There are 14 distinct passing functional IDE scenarios across both hosts; the
remaining four physical-keyboard cases are tracked separately below.

Final validation after the mode-selector fix: the full solution build succeeded,
and the full solution test run passed 653 tests with 24 expected opt-in skips,
including all 247 VS unit tests. Final TRX results are under
`artifacts/log/Milestone4/FinalAfterAccessibility/`.

Each final host deployment is built with that host's desktop MSBuild. Immediate
build/deployed DLL hashes match and are retained in `deployment2022.json` and
`deployment2026.json` under the final log directory. Compiler toolchains produce
different DLL hashes, so comparing a later dotnet build against both deployments
is not the deployment identity check.

Remaining gates: the two physical property/completion keyboard cases and the two
existing physical scalar-keyboard cases require an unlocked interactive desktop.
The VS2022 foreground failure remains retained; no forced input or relaxed keyboard
focus guard was used. Actual speech, native high contrast, mixed DPI, typing/paint,
longer host-memory acceptance and the carried-forward release checks remain open.
No SDK/VSIX publication was performed. The implementation is committed in iterations;
full milestone/release acceptance remains conditional on these checks.
