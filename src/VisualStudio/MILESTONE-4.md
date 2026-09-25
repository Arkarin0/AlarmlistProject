# Milestone four: property expressions across compilation and the designer

Status: proposed plan, recorded on 2026-09-25 against `1ac450a`. No milestone-four
behavior is implemented by this document. The proposed scope selects well-known
and custom properties from the [future feature notes](../../FUTURE-FEATURES.md);
the other feature areas remain unassigned. Names and examples below are proposed
public contracts, to be finalized in iteration one.

The outcome is an author who can write `$(PlantName)` in an alarm value, build
with a project-supplied value, and see the same resolved text in the designer's
preview. The source retains the expression, diagnostics explain invalid uses,
and completion offers the properties available to the selected project.

## Baseline and release dependency

[Milestone three](MILESTONE-3.md) implements reference-aware editing, provenance,
live diagnostics, and resolved previews through iteration fourteen. Its release
acceptance remains open for spoken screen-reader output, native high contrast,
mixed-DPI transitions, keystroke-to-paint responsiveness, and attribution of
measured memory growth. Those remain milestone-three work with their original
evidence; planning or implementing milestone four does not mark them passed.

Feature development can proceed while those checks are completed. Distribution
of a resulting VSIX requires the outstanding release gates to be resolved and
recorded, new controls to receive equivalent acceptance, and the matching SDK to
be published and verified first. There is no publication action in this plan.

The working tree was clean at planning start. Relevant existing boundaries are:

| Area | Starting point and implication |
| --- | --- |
| Compiler | `AlarmCompiler` accepts files, source objects, syntax trees, or snapshots. It has no evaluation-context parameter. Existing overloads must remain callable. |
| Resolution | `Binder` resolves alarm references; `AlarmSyntaxNode` chooses scalar origins using null/empty fallback; procedure collections implement ordered Clear. Property evaluation must reuse those choices. |
| Analysis | `SnapshotAnalyzer` and `AnalysisResult` expose contract 1 with raw local fields, effective values, origins, and optional compiled XML. Expansion cannot overwrite raw source data. |
| Build | `AlarmlistBuildTask` currently receives Compile inputs and output settings. The SDK selects net472/net8.0 task payloads; both need identical property behavior. |
| Project context | `AlarmlistProjectSnapshot` captures evaluated Compile items and the SDK compiler path. `AnalysisProject` has no property context yet. |
| VS boundary | `SdkAnalysisWorker` invokes the selected SDK in an AppDomain, and `AnalysisContractReader` requires contract 1. New capabilities need explicit negotiation. |
| Designer | Local drafts and semantic previews are separate. Existing local-copy actions copy effective text and must account for expression escaping. |

## Scope

Include:

- Explicitly enabled property expressions in Name, Code, Category, Description,
  and Step/Hint/Warning/Note text in Instructions and Reset.
- A small, documented set of project-context properties, followed by custom
  string properties supplied through the public compiler API and SDK project.
- One compiler-owned evaluator used by normal compilation and snapshot analysis,
  with located diagnostics and unchanged failure/output preservation behavior.
- Captured evaluated project properties in live analysis, including configuration
  changes and independent contexts for linked files.
- Raw-expression editing, resolved displays, and property-name completion in the
  supported designer fields, with the existing draft and Undo behavior.
- Package-consumer, compatibility, live-host, and performance regression coverage.

Defer localization, culture/fallback selection, `.resx`/XLIFF/CAB support, syntax
generation, dynamically generated designer fields, collection-valued properties,
property declarations inside ALMX, per-alarm property scopes, reference renaming,
and XML language-service completion/squiggles. Keep the current XML editor.
This milestone provides designer completion only for its property expressions.

## Proposed expression and scope contract

Use the following defaults to make the first implementation bounded. Iteration
one must record any changes to them before subsequent implementation depends on
the spelling or behavior.

| Subject | Proposed behavior |
| --- | --- |
| Activation | `AlarmlistEnablePropertyExpressions` defaults to false in the SDK. An explicit compiler option enables the same behavior. Existing API overloads and projects remain literal by default, including dollar escaping. |
| Grammar | An expression is `$(Name)` with a name matching `[A-Za-z_][A-Za-z0-9_]*`. Text can contain multiple expressions. Property lookup uses ordinal case-insensitive comparison; alarm identity/reference comparison remains ordinal case-sensitive. |
| Escaping | In enabled fields, `$$` emits one literal dollar and its result is not rescanned. Thus `$$(PlantName)` emits literal `$(PlantName)`. Ordinary dollar signs remain text. |
| Replacement | Insert property values as literal strings in one pass. Do not recursively expand replacement text. XML escaping remains the normal output writer's responsibility. |
| Invalid expressions | Unknown names, malformed expressions beginning with `$(`, and unsupported namespaces such as `$(loc:Key)` report errors when enabled. Never silently substitute an empty string for an unknown property. |
| Custom definitions | The host supplies a frozen set of name/string-value pairs. Empty is a valid value. Reject invalid names, case-insensitive duplicates, and collisions with the reserved `Alarmlist` prefix. There is no last-definition-wins rule in the compiler. |
| Scope | One context per compilation/project configuration. There are no file- or alarm-local overrides in this milestone. All sources, including linked files, use the selected project's context. |
| First well-known properties | `AlarmlistProjectName`, `AlarmlistConfiguration`, and `AlarmlistPlatform`. SDK hosts capture their evaluated values. Standalone callers supply them explicitly; referencing an unavailable value is an error. |
| Excluded contexts | FullyQualifiedName, ReferenceName, element/attribute names, file paths, unknown XML, and Clear are not expression contexts and retain their existing treatment. |

The initial well-known list avoids ambient machine/user/time values. Do not import
the entire MSBuild or environment-property bag into ALMX. Compiler code receives
data and does not load MSBuild or inspect the process environment. Custom property
values are terminal strings: recursive definitions, dynamic property names,
functions, and property dependency cycles are outside this grammar. A value that
contains `$(Other)` remains that literal text.

This intentionally uses only the familiar notation from
[MSBuild properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties?view=visualstudio).
MSBuild's broader evaluation model is not the ALMX expression contract. The
[reserved/well-known distinction](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=visualstudio)
informs the reserved prefix; the three names above are Alarmlist proposals.

### Proposed project and source example

```xml
<!-- Inside an SDK-based .almproj; SDK version must support this feature. -->
<PropertyGroup>
  <AlarmlistEnablePropertyExpressions>true</AlarmlistEnablePropertyExpressions>
</PropertyGroup>
<ItemGroup>
  <AlarmlistProperty Include="PlantName" Value="North" />
  <AlarmlistProperty Include="AreaLabel" Value="$(Configuration) area" />
</ItemGroup>
```

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Pump.HighTemperature</FullyQualifiedName>
    <Name>$(PlantName) pump temperature</Name>
    <Description>Inspect the $(AreaLabel).</Description>
  </Alarm>
</Alarmlist>
```

For Debug, the proposed outputs are `North pump temperature` and
`Inspect the Debug area.` MSBuild evaluates `$(Configuration)` in the project;
the compiler receives the resulting string. Expressions in the ALMX file are
evaluated by Alarmlist. SDK documentation must explain these two boundaries and
show how to pass literal dollar/semicolon/XML characters without double expansion.

Initially define these items during project evaluation, so the build task and CPS
can capture the same values without running Build. Target-time mutation is outside
the live-preview guarantee and must be documented. Unsaved ALMX buffers participate
immediately; project-file edits participate after CPS reevaluation, not through a
second project-file parser in the extension.

## Evaluation order and provenance

1. Parse raw ALMX with existing XML restrictions and source locations.
2. Bind literal alarm identities and references, preserving ALM0001-ALM0004.
3. Select effective scalar origins using raw null/empty fallback, and resolve
   procedure order and Clear exclusions with the current resolver.
4. Expand selected scalar text and surviving procedure text using the captured
   property context. Do not change syntax nodes or raw local/excluded entries.
5. Produce detached analysis and compiled values from the same resolved data;
   serialize through the existing `AlarmList` output writer.

A nonempty raw value such as `$(EmptyValue)` is still a local override when its
expansion is empty. It does not fall back to the referenced alarm. Existing
`<Name/>` fallback continues unchanged. An inherited expression retains its
defining alarm/source location while its resolved text uses the current project's
property context. A linked source can therefore resolve differently in North and
South without sharing semantic state.

Do not expand entries removed by Clear for that alarm's effective section. A
surviving use elsewhere can still produce an error because compilation includes
every alarm. Retain raw excluded entries and their existing exclusion origins.

Expansion failures suppress compiled XML/output. The proposed conservative
analysis behavior marks the affected alarm's resolved projection unavailable,
retains its raw local data and diagnostics, and leaves unrelated valid alarms
inspectable. Never expose a partially substituted value as successfully resolved.
Property diagnostics use new stable IDs; choose them from the actual descriptor
table in iteration one. Locate a failure at the ALMX use, with its source version;
use a containing declaration when decoded XML has no reliable token span.
Host-supplied configuration failures may be location-free. Do not invent source
locations for evaluated MSBuild items or fabricate ALM codes in the VS adapter.

## Compiler, SDK, and extension design

Add immutable compiler options/context objects and explicit context-taking API
entry points while preserving all existing signatures and literal defaults.
Include syntax-tree compilation in the compatibility audit: it currently bypasses
the ordinary source-binding entry point. Centralize resolved-value evaluation so
file compilation, snapshot compilation, and optional XML preview cannot diverge.
Keep the compiler independent of Microsoft.Build and Visual Studio assemblies.

Keep parser/source information, reference binding, expression evaluation, and
output responsibilities separate. This adapts Roslyn's explicit
[compilation options](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/Compilation/CompilationOptions.cs)
to this smaller compiler; it does not require Roslyn dependencies, a full syntax
tree redesign, or a new solution-wide workspace.

The task and SDK own transport: the opt-in flag, explicitly exported custom items,
and well-known project values. Extend `tools/Alarmlist.props`,
`tools/Alarmlist.targets`, and CPS rules only as required to expose evaluated data.
Keep shared engineering policy in the root/`eng` configuration and preserve the
pinned `Arkarin0.DotNet.Arcade.Sdk` 1.0.0-Preview imports. Do not add a required
TargetFramework, change Compile ordering, or change Clean/build lifecycle behavior.

The analysis bridge must copy new data into host-owned immutable DTOs without
passing SDK objects across AppDomain boundaries. Preserve the required contract-1
shape and old analysis-only/preview overloads. Negotiate expression-aware inputs
and additional raw/resolved procedure/property metadata as a separate capability;
do not make optional properties mandatory in `AnalysisContractReader`.
The exact API and capability shape are an iteration-one design deliverable.

Capture expression enablement, property names/values, and well-known values in
`AnalysisProject`. Include them in equality, cache identity, scheduling, publication,
and command validation. A value-only change must invalidate current diagnostics,
completion, and previews even when Compile inputs and SDK path stay the same.
Retain immediate stale-result clearing, cancellation/generation checks, buffer
precedence, linked-project isolation, off-UI-thread calls, and disposal guarantees.

### Compatibility gate

| Consumer/project combination | Required result |
| --- | --- |
| New SDK, feature disabled | Existing literal compilation and contract-1 analysis continue unchanged. |
| New VSIX with old SDK | Existing reference features remain usable. Property features report unsupported capability when requested. |
| New SDK and new VSIX, feature enabled | Build and analysis use equivalent captured contexts and resolved values. |
| Old VSIX with new SDK, feature enabled | Must not display a successful literal preview while Build expands expressions. Establish and test an explicit compatibility guard before exposing the feature. |

The last row is a design spike, not an already-solved compatibility claim. The
current VSIX does not send an evaluation context or inspect an expression
capability, so adding an optional overload alone cannot protect it. Iteration one
must demonstrate a guard with the actual old bridge/package or record a necessary
versioned integration change and its migration rules. A minimum-version note alone
does not meet this gate. Do not enable/release property-aware project support until
this behavior is established; do not silently use a different SDK compiler.

## Designer behavior

Local controls continue to edit raw expressions. Resolved previews show evaluated
text; inheritance adorners still describe reference provenance. Add a separate,
accessible explanation of property substitution so a local expression is not
mistaken for inheritance. Use SDK-supplied property metadata/results rather than
implementing an expression evaluator in WPF. Configuration-origin values can be
identified without promising navigation to unavailable project declaration spans.

Provide keyboard-operable suggestions after `$(` and through an explicit completion
command in the supported scalar and procedure controls. Offer property names from
the current selected context and insert only the expression token at the caret.
Completion changes the active draft; it does not commit or save independently.
Escape dismisses completion first, then retains the existing draft-cancel behavior;
Enter accepts a selected suggestion before the normal commit behavior resumes.
Preserve selection, focus, multiline input, Undo/Redo, and contextual action names.
Pending/unsupported analysis must not offer stale values or stale property names.

Existing **Override locally** and **Make local copy** actions copy the effective
value/sequence as a snapshot. In expression mode, escape literal dollar characters
before inserting that snapshot into source: copying an evaluated `$(Literal)` must
not turn it into a new expression. Keep one document edit, current-input validation,
unsupported-content refusal, and the leading Clear for procedure section copies.
Ordinary raw-expression edits preserve the expressions and surrounding XML.

## Implementation sequence and acceptance

Each iteration should leave the existing suites passing and append its actual
validation evidence here. These are dependency-ordered slices, not delivery dates.

| Iteration | Work | Acceptance before proceeding |
| --- | --- | --- |
| 1. Freeze contracts and prove compatibility | Confirm activation, grammar, scopes, built-ins, diagnostics, API/capability transport, and the old-VSIX guard. Specify representative literal/expression examples and review milestone-three release follow-up. | Record decisions and an executable compatibility proof; preserve old public callers and identify any unavoidable versioned change before broad integration. |
| 2. Add compiler evaluation with well-known properties | Add captured options/context and one evaluator after existing reference/Clear resolution. Connect all supported compilation entry points and diagnostics. | Both compiler frameworks pass; raw source and origins survive; successful XML is correct; invalid expressions fail; legacy literal cases remain identical. |
| 3. Add explicit custom properties and SDK transport | Accept custom strings in compiler options; transport evaluated items/context through task and SDK. Add isolated consumer assets and documentation. | net8.0 and desktop net472 task loading agree; duplicates/reserved names fail; conditional/removed definitions and escaped values work; failure preserves previous output. |
| 4. Extend detached analysis and the isolated bridge | Share resolved values with preview generation; copy capability/metadata through the SDK worker; extend package contract tests. | Analysis, CompileSnapshots, and built XML agree for equal inputs/context; old SDKs retain baseline features; no compiler assembly appears in the VS host. |
| 5. Capture project property context | Extend CPS capture, state identity, invalidation, linked contexts, and asynchronous command validation. | Property-only/configuration changes clear stale data; queued old results cannot publish; XML-only unsaved buffers work; unload and SDK changes clean up owned state. |
| 6. Deliver designer expression workflow | Show raw versus expanded values, implement property completion, and make local-copy actions expression-safe. | Keyboard/draft/Undo/focus and source-preservation tests pass, including literal dollar copies, narrow layout, and accessible descriptions. |
| 7. Validate both hosts and prepare release evidence | Extend the isolated acceptance fixture; test VS2022 and VS2026, run relevant performance measurements, document usage/compatibility, and prepare release artifacts. | Fresh-cache package checks and live IDE workflows pass; outstanding milestone-three release gates and new UI acceptance are resolved and recorded before distribution. |

## Validation matrix

| Layer | Required scenarios |
| --- | --- |
| Compiler grammar | Disabled literal mode, mixed/multiple expressions, case rules, unknown/malformed names, unsupported `loc:`, empty values, dollar escaping, replacement text containing expressions, XML special characters, repeated calls with different contexts and immutable prior results. |
| Resolution | Multi-file/multi-hop inheritance, equal local/inherited text, raw-empty fallback versus expansion-to-empty, invalid reference chains, independent Instructions/Reset, multiple Clears, excluded entries, local/origin locations and snapshot versions. |
| API and output | Existing overloads, explicit context overloads, source/snapshot/syntax-tree paths, detached raw/effective data, unrelated valid alarms, analysis/compiled XML parity, no snapshot disk I/O, no output replacement on failure. |
| Task and SDK | Default opt-out, project built-ins, custom/conditional/removed properties, reserved/duplicate names, item metadata escaping, both MSBuild hosts, no TargetFramework, unchanged Build/Clean/Rebuild/design-time guards, package payload/dependencies. |
| Bridge and CPS | Old/new capability matrix, actual legacy bridge guard, package-selected compiler isolation, property-only invalidation, linked contexts with different values, reevaluation while analysis is running, navigation/copy revalidation, unload/disposal. |
| Designer | Raw drafts survive semantic refresh, correct expanded/provenance displays, completion replacement at caret, keyboard arbitration, cancelled/committed drafts, literal snapshot copies, one Undo action, unchanged surrounding source and preview selection. |
| Live IDE | Linked North/South contexts; edit unsaved referenced expressions; change evaluated property/configuration; observe Error List/preview without Build; build matching saved inputs; Undo, close/discard, reload/unload, source/output hash preservation. |

Extend existing tests and helpers rather than introduce another test framework:
compiler `Text`/`Binding`/`Analysis`/`Output` tests, task tests, SDK `testassets` with
`TestProjectCollection`/`CreateTestApp`, VS `Analysis`/`Editing` tests, and packaged
VSIX/template consumers. The package-test approach follows Arcade's
[TestProjectFixture](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestProjectFixture.cs)
and [TestApp](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestApp.cs),
adapted through this repository's existing isolated feed/cache and retained logs.

Run affected suites first, then downstream suites for shared compiler/SDK changes.
For final implementation validation, from the repository root:

```powershell
dotnet build AlarmlistProject.slnx
dotnet test AlarmlistProject.slnx
```

Omitting `--framework` covers both repository frameworks where applicable; the
VS host/unit suite remains net472. Additionally run the SDK suite against desktop
MSBuild as documented in the [SDK test README](../SDK/Alarmlist.SDK.Tests/README.md),
restoring the prior `ALARMLIST_TEST_MSBUILD_PATH` value afterward. Do not use
`--no-build` against stale SDK packages.

Use `eng/test-visualstudio.ps1` on both supported IDE hosts and extend the linked
fixture prepared by `eng/prepare-visualstudio-acceptance.ps1`. Preserve the
[milestone-three acceptance record](ACCEPTANCE.md); add milestone-four scenarios
with explicit actual/not-run outcomes. Reuse `eng/measure-visualstudio-analysis.ps1`
and opt-in live analysis measurements to compare property-heavy fixtures and
context changes with the existing baseline. Retain raw samples and host/revision
details; do not introduce machine-dependent timing assertions or infer leak
freedom from whole-process memory totals.

## Completion criteria and planning validation

Milestone four is implementation-complete when the compiler, packaged SDK, and
designer deliver the scoped workflow with the compatibility matrix and regression
checks satisfied. Release acceptance additionally requires the carried-forward
host gates, fresh-cache verification, and SDK-first distribution described in
the [Visual Studio README](README.md#sdk-first-release).

This planning change only adds documentation and roadmap links. Validation is a
diff, relative-link/path, and command review; no build, unit test, IDE acceptance,
performance measurement, or publication is claimed for this document.
