# Future feature notes

Status: ideas for future development, recorded on 2026-09-24. These notes capture
intent and open design questions; they do not define implemented behavior,
committed milestones, or a delivery schedule.

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

The supported localization file formats remain undecided. Candidates are:

- CAB-based files; the contained localization format still needs clarification.
- Resource files supported by Visual Studio, such as `.resx`.
- XLIFF files.

Open questions:

- Which formats should be supported first, and how are localization files included
  in a project?
- How are the target culture, default language, and fallback behavior selected?
- What happens when a key is missing or duplicated?
- Which fields can be localized, and how do localization expressions interact with
  property expansion and inherited values?
- Does compilation produce one output per culture, or use another output model?

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
property and localization completion described here has no assigned milestone.

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
