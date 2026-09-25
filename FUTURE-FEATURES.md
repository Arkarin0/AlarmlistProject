# Future feature notes

Status: ideas for future development, recorded on 2026-09-24. These notes capture
intent and open design questions; they do not define implemented behavior,
committed milestones, or a delivery schedule.

Planning update (2026-09-25): the proposed
[milestone-four plan](src/VisualStudio/MILESTONE-4.md) selects well-known and custom
string properties plus their designer completion as its scope. It supplies
recommended answers to the property questions below; these notes retain the
broader ideas. Localization, NuGet alarm libraries, XML completion, and syntax
generation remain unassigned. The proposal does not describe implemented behavior.

## Well-known properties and custom properties

Introduce `wellknownProperties`: predefined values that can be referenced in ALMX
input using an MSBuild-like property expression, such as `$(PropertyName)`.
The behavioral reference is
[MSBuild reserved and well-known properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-reserved-and-well-known-properties?view=visualstudio).
MSBuild distinguishes reserved properties from well-known properties; Alarmlist's
property names and override rules remain to be defined.

Start with well-known properties, then extend the mechanism to support custom
properties defined by the programmer.

Open questions:

- Which predefined properties should exist, and where do their values come from?
- Which alarm fields and procedure values allow property expressions?
- Where are custom properties declared, and what are their scope and precedence?
- When does expansion happen relative to reference inheritance and localization?
- How should escaping, unknown names, nested expressions, and cycles behave?
- How is the same evaluation context supplied to standalone compilation, MSBuild,
  and the Visual Studio editor?

## Localization

Add localization expressions such as `$(loc:MyKeyword)`, replaced with the
localized value for the selected language or culture.

In Visual Studio, provide a language dropdown combobox on a toolbar, following
the behavior of Visual Studio's `Configuration` combobox for C# projects.
Selecting a language updates both the resolved translations displayed in the
designer and the language used for subsequent compilation.

When an editable field receives focus, display its raw source string, such as
`$(loc:MyKeyword)`, instead of the resolved translation. After editing ends and
focus leaves the field, display the translation for the selected language again.
Edits apply to the raw source value. Changing the selected language updates the
active language setting while preserving the localization expressions in ALMX
source files.

Supply the language used for translation during compilation as a compile-time
setting (compiler constant), exposed through a project property in the same way
that C# projects expose `Configuration`. The property name and default remain to
be defined. The toolbar combobox reflects and changes this same project setting,
keeping the designer's displayed translations and compiled translations aligned.

When compiling the final translated alarm list, if a localization keyword cannot
be found in the localization source, the compiler must report an error and fail
compilation without writing a new compiled output. As part of the future
implementation, add a dedicated
missing-localization-keyword error to the compiler's diagnostic classes, with a
stable diagnostic ID and a message identifying the keyword and compilation
language. Report the source location of the expression where available. The
diagnostic ID will be assigned when this feature is implemented.

The supported localization file formats remain undecided. Candidates are:

- CAB-based files; the contained localization format still needs clarification.
- Resource files supported by Visual Studio, such as `.resx`.
- XLIFF files.

Open questions:

- Which formats should be supported first, and how are localization files included
  in a project?
- What is the compilation-language property's name and default, and how is it
  passed to standalone compiler callers?
- How should language fallback behave during display and compilation?
- What happens when a localization key is duplicated?
- Which fields can be localized, and how do localization expressions interact with
  property expansion and inherited values?
- Does compilation produce one output per culture, or use another output model?

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
