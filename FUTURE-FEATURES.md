# Future feature notes

Status: ideas for future development, recorded on 2026-09-24. These notes capture
intent and open design questions; they do not define implemented behavior,
committed milestones, or a delivery schedule.

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

Planning decisions (2026-10-07; not implemented):

The [milestone-five iteration plan](src/VisualStudio/MILESTONE-5.md) sequences
implementation and validation. Iterations 1-7 establish the RESX compiler/build
and designer workflow; iterations 8-9 add XLIFF exchange, followed by integrated
acceptance. Remaining defaults are proposals to finalize in its first iteration.

- Add localization expressions such as `$(loc:MyKeyword)`, resolved for the selected
  culture. Editable fields retain raw source while focused and display evaluated
  text afterward. Language changes never rewrite ALMX expressions.
- The default displayed language is the special `keyword` mode. It displays the
  complete literal `$(loc:MyKeyword)`, not just `MyKeyword`. Keyword mode is also
  allowed as a build output, preserving localization tokens in the compiled text.
- Permit parent-culture fallback: for each key, `de-DE` may use `de` if no German
  (Germany) entry exists. Stop before the invariant/unqualified default resource.
  Never use another language or keyword mode as an automatic fallback. This
  supersedes the earlier exact-culture-only interpretation.
- Keys form one project-wide catalogue, including inherited alarm content.
  Duplicate keys for the same culture are errors; the same key in different
  cultures is expected.
- Translation values can contain parameter expressions. For example,
  `Inspection = "Inspect $(par1)"` evaluates using the consuming alarm's parameters.
  Custom parameter assignments retain milestone four's literal substitution rules.
- A shared `AlarmlistCultures` project property selects multiple build cultures,
  with a separate output folder for each. Keyword mode can be explicitly included.
  If any requested culture fails validation, the entire build fails without
  replacing any of the previous culture outputs.
- Missing translations after permitted parent lookup produce a dedicated compiler
  error identifying the key, requested culture, and source location where available.
  Assign its stable diagnostic ID during implementation.
- `.resx` is the authoritative resource format for builds and designer previews.
  Use Visual Studio's built-in resource editor; verify its integration with the
  custom `.almproj` project type instead of assuming .NET-project behavior.
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
rules. Designer completion and resource-change invalidation belong in the planned
workflow. A separate translation-table editor is not currently proposed.

Remaining design details:

- Define how `.resx` files are included and associated with cultures. For XLIFF
  exchange, choose supported versions/content forms and import conflict handling.
- Define behavior when the build-culture list is absent, key validation in keyword
  mode, and the difference between missing and intentionally empty translations.
- Freeze key syntax/case rules, nested localization-expression behavior, and the
  localizable field set (proposed: the same fields as milestone-four expressions).
- Define standalone captured resource/context inputs and live observation of
  unsaved resource edits without coupling compiler evaluation to disk or MSBuild.

## NuGet packages and alarm libraries

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

## IntelliSense

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
broader XML IntelliSense have no assigned milestone.

## Generate syntax nodes from an external definition

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
