# Future feature notes

Status reviewed on 2026-10-27 against milestone records and current source.
This file tracks implemented features and future ideas; it does not authorize
implementation or define a delivery schedule.

**Implemented** means the scoped feature exists, not that every manual, vendor,
or release gate has passed. Linked milestones retain the validation evidence and
remaining gates. Original proposals and dated planning updates below are design
history; current status summaries and milestone contracts supersede provisional
syntax, open questions and statements about future work in that history.

| Feature | Current status | Record |
| --- | --- | --- |
| Well-known and custom properties | Implemented | [Milestone 4](src/VisualStudio/MILESTONE-4.md) |
| Localization, RESX and XLIFF exchange | Implemented; vendor/manual/release gates remain | [Milestone 5](src/VisualStudio/MILESTONE-5.md) |
| NuGet alarm libraries and project references | Implemented | [Milestone 9](src/VisualStudio/MILESTONE-9.md) |
| Visual Studio Code | Proposed; unassigned | Proposal below |
| TwinCAT XAE Shell64 | Proposed; runtime compatibility unverified | Proposal below |
| Separate preview and alarm-list table | Implemented as Alarm Summary | [Milestone 5](src/VisualStudio/MILESTONE-5.md), [7](src/VisualStudio/MILESTONE-7.md), [11](src/VisualStudio/MILESTONE-11.md) |
| Procedure GroupBoxes, editable headings and splitter grip | Implemented, including collapse/search | [Milestone 10](src/VisualStudio/MILESTONE-10.md) |
| External alarm analyzers | Implemented, including NuGet analyzer packages | [Milestone 6](src/VisualStudio/MILESTONE-6.md), [9](src/VisualStudio/MILESTONE-9.md) |
| EditorConfig and diagnostic severity | Implemented; .globalconfig excluded | [Milestone 6](src/VisualStudio/MILESTONE-6.md) |
| Warnings as errors and NoWarn | Implemented for configurable diagnostics | [Milestone 6](src/VisualStudio/MILESTONE-6.md) |
| Visual Studio Shared Projects | Implemented; both-host native and packaged consumer acceptance passes | [Milestone 12](src/VisualStudio/MILESTONE-12.md) |
| Custom procedure groups and creation defaults | Implemented | [Milestone 8](src/VisualStudio/MILESTONE-8.md) |
| IntelliSense | Designer and scoped ALMX/lookup/project XML completion implemented and validated | [Milestone 4](src/VisualStudio/MILESTONE-4.md), [5](src/VisualStudio/MILESTONE-5.md), [7](src/VisualStudio/MILESTONE-7.md), [12](src/VisualStudio/MILESTONE-12.md) |
| External alarm-definition generation | Implemented for supported scalars and collections | [Milestone 11](src/VisualStudio/MILESTONE-11.md) |
| Explain this value | Milestone 14 authorized; implementation in progress | [Milestone 14](src/VisualStudio/MILESTONE-14.md) |
| Find All References and safe Rename | Proposed; high priority; unassigned | Proposal below |
| Semantic comparison and Git integration | Proposed; high priority; unassigned | Proposal below |
| Export-provider interface | Milestone 13 complete: XML/JSON/zenon, external providers and transactional artifacts | [Milestone 13](src/VisualStudio/MILESTONE-13.md) |
| Change-impact analysis | Proposed; unassigned | Proposal below |
| Analyzer quick fixes | Proposed; unassigned | Proposal below |
| Localization coverage dashboard | Proposed; unassigned | Proposal below |
| Alarm unit-style tests | Proposed; unassigned | Proposal below |
| Bulk editing | Proposed; unassigned | Proposal below |

## Priorities and scope

Priority decision (2026-10-27): Explain this value, Find All References and safe
Rename, semantic comparison with Git integration, and the export-provider
interface have equal high priority. Export providers were selected for Milestone 13
and Explain this value for Milestone 14; the remaining order is undecided.
The other new proposals below have no assigned priority or milestone.

Printable alarm documentation belongs to another project and is outside this
backlog. The export-provider proposal does not include printable documentation.

## Explain this value

Authorized for [Milestone 14](src/VisualStudio/MILESTONE-14.md) on 2026-10-29.
The confirmed scope covers chosen contributors for all supported value kinds,
invoked from the designer and Alarm Summary, with read-only source navigation.
The milestone plan supersedes the presentation/open-scope questions below.

Provide a navigable explanation of how a field obtains its resolved value:
inherited source, local overrides, parameter assignments, translation fallback
and the final result. Reuse compiler-owned provenance and resolution results;
let users navigate to the contributing sources. Presentation and handling of
unavailable or invalid values remain to be defined during milestone planning.

## Find All References and safe Rename

Proposed on 2026-10-27; high priority; no milestone assigned.

Discover usages of alarms, parameters, localization keys, lookup entries and
procedure groups. Preview coordinated rename changes across editable project
and shared sources, and identify references in read-only packages. Define the
search boundary, identity rules and conflict handling during milestone planning.

## Semantic comparison and Git integration

Proposed on 2026-10-27; high priority; no milestone assigned.

Compare alarm identities and resolved fields, translations and procedures across
builds or revisions without XML-formatting noise. Make changes caused by shared
templates, parameters or resources visible in the resulting alarms.

The milestone containing this feature must also include Git-aware diff,
revision comparison, three-way merge and conflict-resolution workflows.
Define source-versus-resolved comparison, culture/toolset context and the exact
Git/editor integration during milestone planning. Comparison and merge need
distinct behavior: resolved differences explain effects, while merge operations
must preserve authored source expressions and expose conflicts for review.

## Export-provider interface

Completed in Milestone 13 on 2026-10-28. XML/JSON/zenon and provider infrastructure
are implemented; scoped acceptance evidence is recorded in the milestone plan.

The [milestone plan](src/VisualStudio/MILESTONE-13.md) defines selectable XML/JSON
exports, an external zenon keyword-XML provider, NuGet/DLL distribution and
transactional multi-artifact publication. Printable alarm documentation remains
outside this project. CSV and full vendor import formats remain future work.

## Change-impact analysis

Proposed on 2026-10-27; no milestone assigned.

Identify alarms and known consumer projects affected by changes to shared
templates, parameters, translations or other shared inputs. Show the affected
sources and resolved values where available. Define discovery boundaries and
how unavailable consumers are reported; do not imply coverage of unknown users
of a published library.

## Analyzer quick fixes

Proposed on 2026-10-27; no milestone assigned.

Allow alarm analyzers to offer previewable corrections alongside diagnostics,
including a scoped "Fix all" action. Define the edit contract, supported scopes,
conflict/stale-input handling and Undo behavior during milestone planning.

## Localization coverage dashboard

Proposed on 2026-10-27; no milestone assigned.

Show missing keys, translations obtained only through fallback, and potentially
unused entries by culture. Navigate to resources and affected alarms. Reuse
compiler-owned lookup results and distinguish potentially unused entries from
proven unused entries within a defined project/dependency scope.

## Alarm unit-style tests

Proposed on 2026-10-27; no milestone assigned.

Provide unit-style tests for alarm definitions and reusable libraries. Supply
source/library inputs, parameters and cultures, then assert resolved values or
expected diagnostics. For example, verify a translated field's final text or
the error produced by a missing required parameter. Support automated regression
checks in CI. These tests cover compilation semantics, not PLC runtime behavior;
the test format, runner and IDE integration remain open.

## Bulk editing

Proposed on 2026-10-27; no milestone assigned.

Preview and apply common changes across selected alarms. Explicitly distinguish
editing a shared base from creating local overrides, preserve expressions and
support Undo. Selection scopes, supported operations and handling of conflicting
or read-only inputs remain to be defined during milestone planning.

## Historical implementation and planning updates

Implementation update (2026-10-18): [milestone nine](src/VisualStudio/MILESTONE-9.md)
implements reusable alarm libraries, project references and NuGet distribution,
including native Visual Studio management and saved-source dependency refresh.
Its [design contract](src/VisualStudio/MILESTONE-9-DESIGN.md) supersedes the open
library/project-reference questions below. All eight iterations and their scoped
automated acceptance are complete; the milestone records the actual evidence and
performance limitations. Shared Projects are now planned in
[milestone twelve](src/VisualStudio/MILESTONE-12.md); publication is separate.

Planning update (2026-10-10): the owner selected improvements to the existing ALMX
file editor's UI/UX for [milestone seven](src/VisualStudio/MILESTONE-7.md), with
procedure editing identified as the first priority. The procedure preview is
accepted; confirmed additions cover a labels-left/editors-right property form,
direct field editing, context-menu inheritance reset, searchable references,
dark-theme legibility, separate file/alarm areas, IntelliSense labels and a preview
table with multiline procedure rendering and configurable columns. Project-user
column layout persistence is specified in the milestone plan. Its iterations include
procedure layout, editing actions and inheritance/expressions. Some interaction details are
still open. Custom procedure groups and the other backlog features below are not
the selected milestone-seven scope.

Implementation update (2026-10-10): localization, generated lookups/named lists,
the dockable preview and alarm table are implemented in
[milestone five](src/VisualStudio/MILESTONE-5.md); its manual/vendor/release gates
remain explicitly tracked. The owner approved
[milestone six](src/VisualStudio/MILESTONE-6.md): EditorConfig, diagnostic severity,
project warning controls and explicitly referenced external analyzers. Its scoped
implementation and automated validation are complete: Release regressions, adapted
upstream EditorConfig cases, functional IDE checks and extended live analyzer
measurements pass. Compatibility limits and separate release gates are documented
in that milestone. The original proposal text below is retained as design history;
the milestone records describe current scope and actual validation.

Planning update (2026-10-06): the
[milestone-four plan](src/VisualStudio/MILESTONE-4.md) selects well-known and custom
string properties plus their designer completion as its scope. The owner confirmed
Project/File/Alarm declarations, resolved well-known alarm values, literal parameter substitution,
focus-dependent raw/resolved display, expression-preserving copies, and integrated
support without activation or backward-compatibility requirements for unreleased
versions. References inherit custom parameter declarations but not assigned values.
Inherited expressions evaluate with current-alarm -> current-file -> project value
lookup, preserving the selected child-context approach.
An `abstract` XML attribute identifies template-only alarms, excluded from compiled
output while remaining usable as reference targets with unresolved parameters.
The designer includes a parameter table for editing values and an Abstract checkbox.
Localization and NuGet alarm libraries remain for later milestones;
XML completion and syntax generation remain unassigned. The scoped expression
implementation and remaining acceptance gates are recorded in the plan's
[implementation record](src/VisualStudio/MILESTONE-4.md#implementation-record).

## Well-known properties and custom properties

**Implemented:** scoped custom parameters, well-known values, abstract alarms,
compiler evaluation and designer completion. See [milestone four](src/VisualStudio/MILESTONE-4.md)
for implementation evidence and its separately recorded acceptance gates.

### Original proposal and design history

Introduce `wellknownProperties`: predefined values that can be referenced in ALMX
input using an MSBuild-like property expression, such as `$(PropertyName)`.
The behavioral reference is
[MSBuild reserved and well-known properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=visualstudio).
MSBuild distinguishes reserved properties from well-known properties. The milestone
four implementation uses the Alarmlist catalog covering project/file identity and
alarm values documented in the [SDK README](src/SDK/Alarmlist.SDK/README.md).

Start with well-known properties, then extend the mechanism to support custom
properties defined by the programmer.

Confirmed direction (2026-10-06):

- Property declarations belong to Project, File, or Alarm scopes. PowerShell
  scope rules inform visibility/shadowing. References inherit parameter declarations
  through bases, without inheriting assignments. Local and inherited expressions
  find values through current Alarm -> current alarm's File -> Project. Neither
  base assignments nor base-file values enter that chain.
- An `abstract` XML attribute on Alarm marks a template-only definition. Such
  alarms stay available to references and analysis but produce no compiled alarm
  entry; parameter values are resolved when concrete alarms use the template.
- Expressions apply to Name, Code, Category, Description, and procedure text;
  FullyQualifiedName and ReferenceName keep literal contents, although their
  values can be exposed as well-known properties.
- Unassigned parameters fall back to file/project values; explicit empty stops
  lookup. Used but unresolved parameters fail concrete compilation, while unused
  parameters may remain unassigned.
- Raw scalar/procedure content, including literal values, remains inherited;
  the rule excluding inherited assignments applies only to custom parameters.
- Custom assignments are literal and inserted strings are never rescanned.
  Alarm-field properties expose resolved values: Code=`$(par1)` with par1=`001`
  makes Description=`Inspect $(AlarmlistAlarmCode)` resolve to `Inspect 001`.
  Field-dependency evaluation and cycle diagnostics belong to the compiler.
- Fields show resolved text until focused, then edit the raw expression. Local
  copy actions preserve expressions so property changes continue to flow.
- Include a designer parameter table and an Abstract checkbox, backed by the
  same source XML and Undo/Redo workflow as existing editing controls.
- The feature is integrated without explicit activation. Backward compatibility
  with earlier unreleased behavior is not required.

The confirmed [inherited-expression context](src/VisualStudio/MILESTONE-4.md#inherited-expression-context)
makes bases reusable templates: child properties and well-known alarm values
customize inherited text. A parameter can be inherited without an assignment and
obtain its value from the child's file or project; a base's own assignment applies
only to that base alarm. Raw-expression local copies preserve their result while
the reference/context stay unchanged. Raw alarm fields/procedure content still
form the inherited template. Implementation design must distinguish declarations,
assignment presence (including explicit empty), winning value origins, and abstract
state, and finalize syntax and diagnostics. The plan proposes a local, non-inherited
abstract flag that defaults to false, with source/reference validation retained for
templates. Localization remains later work.

## Localization

**Implemented:** RESX localization, language selection, raw/resolved editing,
multi-culture builds, missing-translation error `ALM0016`, designer completion
and XLIFF import/export. [Milestone five](src/VisualStudio/MILESTONE-5.md) also
records implemented HMI lookups, named lists and formatted arguments. Vendor
round trips and documented manual/release gates remain pending; CAB is cancelled.

### Original proposal and design history

Planning decisions recorded on 2026-10-07, before implementation:

The [milestone-five iteration plan](src/VisualStudio/MILESTONE-5.md) sequences
implementation and validation. Iterations 1-7 establish the RESX compiler/build
and designer workflow; iterations 8-9 add XLIFF exchange, followed by integrated
acceptance. Core behavior is confirmed; finalize implementation details in the
first iteration and exchange decisions before the XLIFF iterations.

- Add localization expressions such as `$(loc:MyKeyword)`, resolved for the selected
  culture. Editable fields retain raw source while focused and display evaluated
  text afterward. Language changes never rewrite ALMX expressions. Apply them to
  Name, Code, Category, Description, and procedure text; FullyQualifiedName and
  ReferenceName remain literal.
- The default displayed language is the special `keyword` mode. It displays the
  complete literal `$(loc:MyKeyword)`, not just `MyKeyword`. Keyword mode is also
  allowed as a build output, preserving localization tokens in the compiled text.
  Syntactically valid keys may be used in keyword mode before they are declared
  in any RESX file.
- Permit parent-culture fallback: for each key, `de-DE` may use `de` if no German
  (Germany) entry exists. Stop before the invariant/unqualified default resource.
  Never use another language or keyword mode as an automatic fallback. This
  supersedes the earlier exact-culture-only interpretation.
- Empty translations count as missing: continue permitted parent lookup and
  report a missing-translation error if no nonempty value is found. This does not
  change explicit-empty parameter assignments, which still stop scope lookup.
- Keys form one project-wide catalogue, including inherited alarm content.
  Duplicate keys for the same culture are errors; the same key in different
  cultures is expected. Keys are case-sensitive and may contain dots and hyphens.
- Translation values can contain parameter expressions. For example,
  `Inspection = "Inspect $(par1)"` evaluates using the consuming alarm's parameters.
  Custom parameter assignments retain milestone four's literal substitution rules.
  Nested localization expressions inside translations are initially disallowed.
- A shared `AlarmlistCultures` project property selects multiple build cultures,
  with a separate output folder for each. Keyword mode can be explicitly included.
  If any requested culture fails validation, the entire build fails without
  replacing any of the previous culture outputs. An absent or empty
  `AlarmlistCultures` defaults to `keyword`.
- Missing translations after permitted parent lookup produce a dedicated compiler
  error identifying the key, requested culture, and source location where available.
  Assign its stable diagnostic ID during implementation.
- `.resx` is the authoritative resource format for builds and designer previews.
  Use Visual Studio's built-in resource editor; verify its integration with the
  custom `.almproj` project type instead of assuming .NET-project behavior.
  An unqualified keyword catalogue such as `Translations.resx` is optional;
  language-specific files can define the catalogue, and unqualified resource
  values never provide translation fallback.
- Use only saved RESX contents for build, preview, completion, localization
  diagnostics, and XLIFF export. Unsaved resource edits remain private to the
  resource editor; saving refreshes consumers. Do not save buffers automatically.
  Existing unsaved ALMX analysis remains unchanged.
- XLIFF serves as an import/export format for exchanging translations. Export
  from `.resx` and import translated values back into `.resx`; XLIFF files are not
  direct build or preview inputs. Define conflict and outdated-export handling
  before implementing imports. The iteration plan places exchange after the core
  RESX workflow.
- CAB support is cancelled.

The proposed toolbar follows Visual Studio's Configuration selector and applies
to the active project. A singular `AlarmlistCulture` property holds the selected
designer language, initially `keyword`; the plural `AlarmlistCultures` defines the
build outputs independently. Build and preview use the same per-culture evaluation
rules. Designer completion and invalidation after saved-resource changes belong
in the planned workflow. A separate translation-table editor is not currently
proposed.

Remaining design details:

- Define how `.resx` files are included and associated with cultures. For XLIFF
  exchange, confirm the intended translation tool, source-language selection,
  supported versions/content forms, and whole-import versus per-entry conflict
  handling. These exchange decisions can wait until the XLIFF iterations.
- Freeze key delimiters, whitespace, and escaping rules within the confirmed
  case-sensitive syntax supporting dots and hyphens.
- Define standalone captured resource/context inputs and observation of saved
  resource changes without coupling compiler evaluation to disk or MSBuild.

## NuGet packages and alarm libraries

**Implemented:** `OutputType=Library`, deferred consumer resolution, packaged
resources/lookups, restore/pack and native reference management. The
[milestone-nine contract](src/VisualStudio/MILESTONE-9-DESIGN.md) resolves the
questions below; milestone eleven adds generated-definition compatibility rules.

### Original proposal and design history

Support distributing reusable alarm lists through NuGet packages. A package
contains a compiled alarm list with localization expressions still unresolved.
The consuming project translates these expressions using its selected language
when compiling the final alarm list.

Consider a new `OutputType=Library` option for producing this compiled,
untranslated form. The exact output format and which compilation stages run for
a library remain to be defined.

Investigate whether custom property expressions should also remain unresolved in
the packaged alarm list. If retained, the consuming project supplies their values
and resolves them during final compilation. Decide which values can be fixed by
the library author and which must be supplied by the consumer.

Open questions:

- How should the compiled library represent unresolved localization and custom
  property expressions, and which validation can happen when creating it?
- Should localization resources and custom property defaults or declarations be
  included in the package, supplied by the consumer, or combined?
- How are property scope, precedence, and name conflicts handled across the
  consuming project and its packages? Which context supplies well-known properties?
- How should package restore, library discovery, and packing integrate with
  `.almproj` projects, including dependencies between alarm libraries?
- How are packaged alarms combined with project alarms, including references and
  duplicate names? Missing localization keywords must still fail final compilation
  as described above; define when unresolved custom properties become errors.

## Visual Studio Code support

Proposed on 2026-10-21; no milestone assigned.

Add Visual Studio Code support for authoring `.almx` files and working with
`.almproj` projects. Reuse the Alarmlist compiler and SDK semantics for consistent
results across command-line builds, Visual Studio, and Visual Studio Code.

Open questions:

- Which capabilities belong in the first version: syntax highlighting,
  IntelliSense, live diagnostics, reference navigation, and build commands?
- Should designer editing and resolved/table previews be included or follow later?
- How should project loading, dependencies, localization, and analyzer settings
  integrate with the workspace and shared compiler analysis?
- Which operating systems and remote workspaces should be supported, and how
  should required tooling be discovered and the extension distributed?

## TwinCAT XAE Shell64 support

Proposed on 2026-10-23; no milestone assigned.

Investigate hosting the existing Alarmlist extension in the VS2022-based
TcXaeShell64. Beckhoff's [system requirements](https://infosys.beckhoff.com/content/1033/tc3_overview/6162419083.html)
distinguish this host from the older VS2017-based, 32-bit TcXaeShell, which is
excluded from this proposal.

Read-only inspection of a local TcXaeShell64 installation found CPS 17.10, a
VS17.10 XML editor, NuGet integration, and MSBuild 17.10. These components make
compatibility plausible, but runtime compatibility has not been verified. The
current VSIX targets Visual Studio without an explicit TwinCAT shell target.

Validate extension installation, registration, and loading; `.almproj` project
loading, SDK resolution, NuGet restore, and builds; designer/XML editing,
IntelliSense, diagnostics, previews, and localization; and coexistence with
TwinCAT projects in the same solution.

Supported shell versions and required packaging or installation changes remain
open until compatibility validation is complete. This proposal does not declare
TwinCAT shell support as implemented or assign it to a milestone.

## Separate preview view

**Implemented:** a dockable tool window, now named **Alarm Summary**, with
dedicated project-pinned windows. See [milestone five](src/VisualStudio/MILESTONE-5.md),
[milestone seven](src/VisualStudio/MILESTONE-7.md) and
[milestone eleven](src/VisualStudio/MILESTONE-11.md), iterations 27–29.
Existing internal Preview identifiers remain unchanged.

### Original proposal and design history

Proposed on 2026-10-08; no milestone assigned.

Extract the existing preview section from the designer UI into a separate view.
Expose it as a Visual Studio tool window that users can open from the **View
(Ansicht)** menu, following the menu shown in the reference screenshot. Keep the
resolved selected-alarm and compiled project XML previews available in this view.

Open questions:

- How should the view follow the active project, document, and selected alarm?
- How should docking, visibility, and layout be retained between sessions?
- How should pending drafts, stale analysis, and unavailable results be presented
  while preserving the existing preview validity rules?

## Alarm-list table preview

**Implemented in Alarm Summary:** resolved rows, scope selection, sorting,
persisted column preferences, multiline procedures and generated collection
presentation. See [milestone seven](src/VisualStudio/MILESTONE-7.md) and
[milestone eleven](src/VisualStudio/MILESTONE-11.md). Summary templates inspect
resolved data independently of document/PDF layout.

### Original proposal and design history

Proposed on 2026-10-08; no milestone assigned.

Add an Excel-style table preview of the alarm list, with one alarm per row and
alarm properties as columns. Present resolved values for the selected preview
culture in a read-only view.

Open questions:

- Should the table be a mode or tab in the separate preview view, or its own tool
  window accessible from the View menu?
- Which columns and project/file scopes should be available, and how should
  procedures and other collection-valued properties be displayed?
- Which sorting, filtering, column arrangement, and copy operations are useful?

## Procedure groups and splitter UI refinements

**Implemented:** GroupBoxes, one editable heading, distinct always-visible
read-only group IDs and a themed splitter grip, plus group collapse and procedure
search. [Milestone ten](src/VisualStudio/MILESTONE-10.md), iterations 1–5, records
scoped acceptance on both supported IDE hosts and separate historical gates.

### Original proposal and design history

Assigned on 2026-10-21 to [milestone ten](src/VisualStudio/MILESTONE-10.md).
Its confirmed contract supersedes the presentation questions below: title-like
editable headings, always-visible read-only group IDs, and a centered themed grip.
Implementation and acceptance evidence are tracked in that plan.

- Enclose each procedure group in its own GroupBox so the boundaries between
  groups are clear.
- Give the divider between file and alarm settings a visible drag handle so users
  can recognize that it resizes the two areas, using the selected centered themed grip.
- Combine the repeated display title and separate title/localization input into
  one editable GroupBox heading. Show the resolved title normally and the raw
  expression, such as `$(loc:GroupTitle)`, when focused. Preserve existing title
  inheritance, ID fallback, expression completion, and draft/commit behavior.

The stable group ID remains distinct from the editable title; editing the heading
does not rename the group. Show it as an always-visible read-only label below the
heading; remove the separate title field. These are presentation changes without
changing the existing group or localization semantics.

## External alarm analyzers

**Implemented:** explicit analyzer references, build/live analysis and diagnostic
policy in [milestone six](src/VisualStudio/MILESTONE-6.md), with NuGet analyzer
packages and native package presentation in [milestone nine](src/VisualStudio/MILESTONE-9.md).
Matching analyzer contracts are required; milestone eleven extends the contract
for generated fields and collections.

### Original proposal and design history

Proposed on 2026-10-08; no milestone assigned.

Provide an extension interface for external alarm analyzers, similar to adding
code analyzers to a C# project. Let users add analyzers that check domain-specific
or project-specific alarm rules and report diagnostics through Alarmlist's
existing diagnostic workflow.

Open questions:

- How are analyzers distributed and referenced, for example through NuGet packages
  or assembly references?
- Which inputs should the interface expose: source syntax, resolved alarms,
  localization/property context, provenance, or the complete project?
- When should analyzers run during compilation and live Visual Studio analysis,
  and how should their diagnostics appear in build output and the Error List?
- How should diagnostic IDs, severity, configuration, suppression, API versioning,
  and analyzer failures be handled?

## EditorConfig and diagnostic severity

**Implemented:** captured saved EditorConfig inputs, per-source settings,
per-rule severity and shared build/live-analysis policy in
[milestone six](src/VisualStudio/MILESTONE-6.md). Mandatory compiler errors cannot
be suppressed or demoted. Optional `.globalconfig` support remains outside scope.

### Original proposal and design history

Proposed on 2026-10-09; no milestone assigned. The examples below describe future
consumer behavior, not settings implemented by this document.

Support `.editorconfig` files for Alarmlist-specific settings and per-rule
diagnostic severity, including diagnostics from external alarm analyzers.
Requested option pattern: `Alarmlist_diagnostic.<rule-ID>.severity = none`.
Use a section matching ALMX inputs, adapting the C#/VB example:

```ini
[*.almx]
Alarmlist_diagnostic.<rule-ID>.severity = none
```

Here `<rule-ID>` is a placeholder. A section such as `[*.{cs,vb}]` matches C#/VB
files and would not apply to `.almx` files. Follow EditorConfig's directory
hierarchy, section matching, `root = true`, later/nearer overrides, case-insensitive
keys, and `unset` behavior. See the [specification](https://spec.editorconfig.org/).

Use the .NET severity vocabulary as the reference: `error`, `warning`,
`suggestion`, `silent`, `none`, and `default`. Define how these map to Alarmlist's
diagnostics and Visual Studio presentation, including suppression and build
success. Microsoft's [rule configuration documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options)
describes these values.

Open questions:

- Which compiler diagnostics are configurable, and which must remain errors
  because compilation cannot produce a valid result? Include external analyzers
  in the same documented policy.
- How are invalid values, unknown IDs, and IDs with different casing handled?
- Which file's settings govern inherited content, shared/linked inputs, and
  diagnostics with multiple locations or no source location?
- How do configuration changes invalidate build and live-analysis results?
  Should unsaved EditorConfig edits participate in live analysis?
- Is project-wide `.globalconfig` support also useful? This remains optional.

## Warnings as errors and NoWarn

**Implemented:** `TreatWarningsAsErrors`, `WarningsAsErrors`,
`WarningsNotAsErrors` and `NoWarn` in [milestone six](src/VisualStudio/MILESTONE-6.md).
Policy order is rule default, file severity, NoWarn, then warning promotion.
`ALM0001` remains mandatory: the historical example below cannot suppress it.

### Original proposal and design history

Proposed on 2026-10-09; no milestone assigned.

Add warning controls to consumer `.almproj` projects, following the C# project
properties: `TreatWarningsAsErrors` for all warnings and `WarningsAsErrors` for
selected IDs. Consider matching `WarningsNotAsErrors` for exceptions. Support
`NoWarn` for suppressing selected ALM diagnostics. Requested syntax, with existing
values preserved:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <NoWarn>$(NoWarn);ALM0001</NoWarn>
</PropertyGroup>
```

`ALM0001` currently represents a duplicate alarm name error. This records the
requested example; its suppressibility and the effect on compilation must be
decided with the diagnostic policy above. Hiding a diagnostic and obtaining a
valid resolved alarm list are separate concerns.

Specify precedence between per-file severity, project-level suppression, and
warning promotion. Promoted warnings must affect compilation success before
output installation. The C# reference distinguishes compiler
`TreatWarningsAsErrors` from MSBuild's task-wide `-warnaserror` switch; adopting
the property requires explicit Alarmlist compiler/task integration. See
[C# errors and warnings options](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/errors-warnings)
and [configuration precedence](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files#precedence).

## Project references

**Implemented:** ProjectReference library consumption, transitive dependencies,
consumer resolution, native reference management and saved-dependency analysis
refresh in [milestone nine](src/VisualStudio/MILESTONE-9.md). Shared Projects
remain a separate, unimplemented proposal.

### Original proposal and design history

Proposed on 2026-10-09; no milestone assigned.

Support references between consumer projects, including one `.almproj` consuming
another project's alarms. Coordinate the consumed artifact with the proposed
`Library` output type and NuGet alarm libraries. Use MSBuild `ProjectReference`
conventions for dependency ordering and output discovery, adapting them to alarm
artifacts and projects without a `TargetFramework`.

Open questions include the referenced output contract, transitive references,
cycles and duplicate alarms, configuration/culture propagation, restore and clean
behavior, and Visual Studio reference management and live analysis across projects.
The [MSBuild ProjectReference protocol](https://github.com/dotnet/msbuild/blob/main/documentation/ProjectReference-Protocol.md)
is the implementation starting point; the
[item metadata reference](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items#projectreference)
documents controls such as `ReferenceOutputAssembly` and `OutputItemType`.

## Visual Studio shared projects

Proposed on 2026-10-09; assigned to
[milestone twelve](src/VisualStudio/MILESTONE-12.md) on 2026-10-26.
Native `.shproj`/`.projitems` support is confirmed scope; implementation was
authorized on 2026-10-27. Native creation/reference/item management, independent
consumer builds, shared document selection and lifecycle are implemented and
validated. Combined acceptance passes on both supported IDE hosts and task
frameworks. The milestone records scope, actual evidence and the persistent
Shared Projects example.

### Original proposal and design history

Support Visual Studio Shared Projects so several `.almproj` consumers can share
ALMX sources and associated resources/lookups. Follow the shared-source model:
each consuming project compiles the shared inputs in its own context. Microsoft's
[shared-project reference documentation](https://learn.microsoft.com/en-us/visualstudio/ide/managing-references-in-a-project#shared-project-references)
describes this model, which has no separate binary output.

Investigate `.shproj` ownership, imported `.projitems`, Add Reference integration,
and Solution Explorer using the
[`IVsSharedProjectReference` contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivssharedprojectreference?view=visualstudiosdk-2022).
Confirm how Alarmlist item types participate, how shared documents select the
active consumer for parameters/culture/EditorConfig, and how duplicate inclusion
and source navigation behave. Validate command-line builds as well as the IDE.

## Custom procedure groups

**Implemented:** case-insensitive stable identities, expression-capable titles,
inheritance/Clear semantics and creation-only EditorConfig defaults in
[milestone eight](src/VisualStudio/MILESTONE-8.md). Instructions and Reset are
defaults rather than fixed compiler sections; settings do not rewrite existing alarms.

### Original proposal and design history

Accepted on 2026-10-13 for [milestone eight](src/VisualStudio/MILESTONE-8.md).
That plan governs the named Group XML transition, case-insensitive identities,
expression-capable titles, inheritance, dynamic designer/preview and EditorConfig
creation defaults. The proposal below is retained as design history.

Proposed on 2026-10-09; no milestone assigned.

Replace the hardcoded `Instructions` and `Reset` procedure groups with support for
custom named groups. Add an EditorConfig option containing an ordered list of
groups to create when a new alarm is added. Illustrative spelling and syntax,
still to be finalized:

```ini
[*.almx]
alarmlist_procedure.default_groups = Instructions, Reset, Verification
```

Resolve the setting for the destination ALMX file when creating the alarm. This
list supplies creation defaults; changing it should not rewrite existing alarms.
The names in the example are configurable values rather than a fixed group set.

Open questions include group identifiers/display labels, ordering, list escaping,
empty/duplicate entries, and defaults when the option is absent. Define source and
compiled XML representation, migration of existing Instructions/Reset data,
per-group inheritance and `Clear`, and dynamic designer/preview support. Coordinate
the model with future syntax generation and collection-valued alarm properties.

## EditorConfig implementation references

Research notes (2026-10-09): these are implementation references and candidates,
not a decision to add Roslyn dependencies.

| Reference | What to inspect |
| --- | --- |
| [EditorConfig specification](https://spec.editorconfig.org/) | Parsing, glob matching, directory traversal, precedence, custom keys, and `unset`. |
| [EditorConfig core tests](https://github.com/editorconfig/editorconfig-core-test) | Parser, glob, and file-hierarchy conformance cases for evaluating a parser. |
| [Roslyn AnalyzerConfig.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/CommandLine/AnalyzerConfig.cs) | `AnalyzerConfig.Parse(text, path)` parses captured configuration text. |
| [Roslyn AnalyzerConfigSet.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/CommandLine/AnalyzerConfigSet.cs) | `Create(...)` and `GetOptionsForSourcePath(...)` combine configs and calculate per-file options. Inspect severity handling too. |
| [Roslyn AnalyzerConfigOptionsResult.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/CommandLine/AnalyzerConfigOptionsResult.cs) | Separate diagnostic settings (`TreeOptions`) from custom key/value settings (`AnalyzerOptions`). |
| [AnalyzerConfigOptionsProvider API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.analyzerconfigoptionsprovider?view=roslyn-dotnet-5.0.0) | A model for exposing global and per-file options to external analyzers; this API consumes options rather than discovering/parsing files. |
| [.NET configuration files](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files) | EditorConfig/global-config scoping and precedence, including severity overrides. |

Suggested direction to evaluate: hosts discover and capture applicable configs;
a shared configuration layer resolves options for each source path; compilation
applies the diagnostic policy before determining success, and the designer uses
the resolved creation defaults. Pass captured data through the existing analysis
context/bridge so build and editor agree without compiler-side filesystem reads.

Roslyn treats `dotnet_diagnostic.*.severity` specially. The proposed
`Alarmlist_diagnostic.*.severity` prefix would arrive as custom analyzer options
and needs explicit Alarmlist interpretation. Likewise, the procedure-group key
needs Alarmlist logic. Evaluate direct API reuse versus a smaller compatible
parser against net472/net8.0 support, dependencies, and the isolated SDK bridge;
the Roslyn options-provider API alone does not add support to `.almproj`.

## IntelliSense

**Implemented in the documented scopes:** applicable designer fields support reference names,
well-known/custom properties, localization keywords, lookup/named-list expressions
and argument suggestions. See [milestone four](src/VisualStudio/MILESTONE-4.md),
[five](src/VisualStudio/MILESTONE-5.md) and [seven](src/VisualStudio/MILESTONE-7.md).
Generated scalar/collection fields reuse expression controls and completion in
[milestone eleven](src/VisualStudio/MILESTONE-11.md). XML-editor
IntelliSense is implemented and validated in
[milestone twelve](src/VisualStudio/MILESTONE-12.md), covering `.almx`,
`.almlookup` and `.almproj` files.
ALMX structural/semantic and automatic lookup/project XML completion are
implemented within the milestone's documented contexts. See its validation
evidence, measurement interpretation and separately retained release gates.

### Original proposal and design history

Provide IntelliSense in input fields wherever it is applicable, including:

- Suggestions appropriate to the field being edited.
- Completion for well-known properties and, later, custom properties.
- Completion for localization keywords inside expressions such as
  `$(loc:MyKeyword)`.

Define which designer fields and XML editing contexts participate, how suggestions
are discovered for the current project, and how they stay current after edits to
property definitions or localization files.

Coordinate this work with the reference-name completion already planned in
[Visual Studio milestone three](src/VisualStudio/MILESTONE-3.md). The broader
property completion is proposed for designer fields in
[milestone four](src/VisualStudio/MILESTONE-4.md). Localization completion and
broader XML IntelliSense were unassigned in the original proposal. Designer
localization completion is now implemented; XML completion is planned in
[milestone twelve](src/VisualStudio/MILESTONE-12.md).

## Generate syntax nodes from an external definition

**Implemented in [milestone eleven](src/VisualStudio/MILESTONE-11.md), including
the completed collection and designer/Summary follow-ups.** A versioned XML
definition drives compiler models, parsing/output, typed expressions,
analysis/analyzer metadata, SDK integration and designer/Summary fields.
Supported shapes include typed scalars, simple scalar collections and flat
scalar-field record collections, with optional TestProcedure capability.
Follow-ups add collection Summary templates, localized enum captions and an
isolated definition playground. See the
[definition authoring guide](eng/tools/AlarmDefinitionGenerator/README.md).

Runtime per-project schemas, nested collections, keyed inherited-record overrides
and inherited-entry reordering remain excluded. Matching rebuilt tools and
libraries are required under the plan's compatibility contracts. Its evidence
and separate release/manual gates remain authoritative.

### Original proposal and design history

Introduce a source-file generator that creates syntax nodes from an external
definition, following the approach used by Roslyn. Useful references are Roslyn's
[`Syntax.xml`](https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Syntax/Syntax.xml)
and its
[C# syntax generator](https://github.com/dotnet/roslyn/tree/main/src/Tools/Source/CompilerGeneratorTools/Source/CSharpSyntaxGenerator).

The goal is to let forked repositories quickly adapt alarm properties to their
own requirements by changing the definition and regenerating the relevant code.
The definition format and generator integration remain to be chosen.

Open questions:

- What belongs in the definition: property names, types, defaults, XML names,
  inheritance rules, validation, or editor metadata?
- Beyond syntax nodes, which parsing, binding, resolved-model, and serialization
  code needs to be generated or adapted to support a changed alarm definition?
- How can this capability extend to the UI and Visual Studio integration? Could
  shared metadata drive fields, labels, validation, and IntelliSense, or will
  custom view models and controls still be required?
- How should collection-valued alarm properties be represented, generated,
  serialized, and edited? Define item types, ordering, inheritance, local
  overrides, and clearing behavior. Existing procedure collections and `Clear`
  semantics are relevant cases to preserve.
- How can generated and handwritten extension points coexist so forks can
  regenerate safely and continue taking upstream changes?
