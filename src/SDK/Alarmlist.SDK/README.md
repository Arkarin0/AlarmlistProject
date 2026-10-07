# Alarmlist.MSBuild.SDK

An MSBuild SDK that compiles `.almx` source files into one XML alarmlist per selected culture.
It supports `dotnet msbuild` with .NET SDK 8 or later and Visual Studio MSBuild
with .NET Framework 4.7.2 or later. The package includes the compiler, task
assemblies, and their runtime dependencies.

## Use the SDK

For published releases, enable nuget.org in your NuGet sources, then create
`Plant.almproj`. A project-specific `NuGet.Config` is not necessary when that
source is already enabled. Unpublished development packages require a local
feed; repositories with package source mapping must allow this SDK from the
selected feed.

```xml
<Project Sdk="Alarmlist.MSBuild.SDK/1.0.0" />
```

Replace `1.0.0` with the version you built or published. Alternatively, omit
the version in the project and pin it in `global.json`:

```json
{
  "msbuild-sdks": {
    "Alarmlist.MSBuild.SDK": "1.0.0"
  }
}
```

Place your ALMX files beside the project or in subdirectories, then run:

```powershell
dotnet msbuild Plant.almproj /restore /t:Build
dotnet msbuild Plant.almproj /t:Clean
dotnet msbuild Plant.almproj /t:Rebuild /p:Configuration=Release
```

The default output is `bin/Debug/keyword/Plant.Alarmlist.xml`. `Restore` is a no-op
after SDK resolution; ALMX projects do not restore managed package references.
The SDK does not require a `TargetFramework`: it chooses `tasks/net8.0` for
.NET MSBuild and `tasks/net472` for desktop MSBuild based on the host runtime.

## Visual Studio integration

The [Alarmlist VSIX](../../VisualStudio/README.md) adds a CPS project type and
project/item templates for Visual Studio 2022 17.9+ and 2026. Released templates
pin a published SDK version, resolved through NuGet like command-line builds.
See the [SDK-first release workflow](../../VisualStudio/README.md#sdk-first-release).
The SDK supplies the `Alarmlist`
capability, file schemas, and project properties under `tools/Rules/`.

Configurations default to `Debug|AnyCPU` and `Release|AnyCPU`. A consumer can
replace these defaults by declaring its own `ProjectConfiguration` items with
`Configuration` and `Platform` metadata. The `.almproj.user` file is imported
for CPS user settings such as Show All Files. Design-time builds do not compile
or write output. Normal IDE builds always invoke MSBuild to reflect removed or
conditional inputs. ALMX files open in the XML editor in this first milestone.

## Customize a project

By default, `**/*.almx` files are compiled, excluding output, intermediate,
`bin`, `obj`, and hidden directories. Use `Compile Remove` to omit a file,
or disable default items and list your inputs explicitly:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK/1.0.0">
  <PropertyGroup>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <OutputPath>output/</OutputPath>
    <CompiledAlarmlistFileName>Plant.xml</CompiledAlarmlistFileName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="alarms/**/*.almx" />
    <Compile Include="optional/*.almx" Condition="'$(IncludeOptional)' == 'true'" />
  </ItemGroup>
</Project>
```

| Property | Default |
| --- | --- |
| `Configuration` | `Debug` |
| `AssemblyName` | Project filename without its extension |
| `BaseOutputPath` | `bin/` |
| `OutputPath` | `$(BaseOutputPath)$(Configuration)/` |
| `CompiledAlarmlistFileName` | `$(AssemblyName).Alarmlist.xml` |
| `EnableDefaultCompileItems` | `true` |
| `EnableDefaultAlarmlistResourceItems` | `true` |
| `AlarmlistCultures` | `keyword` (semicolon-separated build list) |
| `AlarmlistCulture` | `keyword` (independent shared preview selection) |
| `AlarmlistTasksAssembly` | Task assembly for the current MSBuild runtime |

`@(CompiledAlarmlistFiles)` exposes absolute output paths with normalized `Culture` metadata. Build and GetTargetPath return these items; GetTargetPath needs no prior build and writes no output. Projects can use
`BeforeTargets="Build"` or `AfterTargets="Build"` for custom targets;
`Directory.Build.props` and `Directory.Build.targets` are also imported.
Each build recompiles the selected inputs so changes to conditional items and
removed files are reflected. Compiler diagnostics fail the build. Clean removes
the configured and tracked compiled outputs and preserves unrelated files.

Reference errors ALM0001-ALM0004 include the source file and declaration line/column
when read from ALMX files. Missing, self, and circular references point to the
`ReferenceName` element; duplicate names point to `FullyQualifiedName`. A failed
compilation leaves any previous compiled output unchanged. Live analysis of
unsaved ALMX editor buffers is separate from saved RESX resource capture.

## Property expressions and templates

Expressions are always enabled in Name, Code, Category, Description and procedure
Step/Hint/Warning/Note text. `$(Name)` inserts a property; `$$` emits a literal
dollar, so `$$(Name)` remains literal. Inserted custom values are not expanded
again. FullyQualifiedName and ReferenceName remain literal identifiers.

Declare project parameters with evaluated items (MSBuild escaping rules apply):

```xml
<ItemGroup>
  <AlarmlistProperty Include="par1" Value="001" />
  <AlarmlistProperty Include="unassigned" />
  <AlarmlistProperty Include="empty" Value="" />
  <AlarmlistProperty Include="literalToken" Value="%24(par1)" />
</ItemGroup>
```

ALMX file and alarm scopes use the same declaration shape:

```xml
<Alarmlist>
  <Properties><Property Name="par1" Value="001" /></Properties>
  <Alarm abstract="true">
    <FullyQualifiedName>Template</FullyQualifiedName>
    <Properties><Property Name="par1" /></Properties>
    <Code>$(par1)</Code>
    <Description>Inspect $(AlarmlistAlarmCode)</Description>
  </Alarm>
  <Alarm>
    <FullyQualifiedName>Child</FullyQualifiedName>
    <ReferenceName>Template</ReferenceName>
  </Alarm>
</Alarmlist>
```

Only Child is emitted, with Code `001` and Description `Inspect 001`. References
inherit raw content and parameter declarations; base-alarm assignments and base-file
values do not flow to descendants. Value lookup uses the current alarm, its own
file, then the project. Missing Value falls through; `Value=""` stops fallback.
Unused unassigned parameters are allowed. Names are case-insensitive identifiers
(`A-Z`, `a-z`, `_`, followed by those characters or digits); `Alarmlist` is reserved.
Duplicates in one scope are errors. Abstract is local and defaults to false.

Well-known project values are `AlarmlistProjectName`, `AlarmlistProjectFile`,
`AlarmlistProjectFullPath`, `AlarmlistProjectDirectory`, `AlarmlistProjectExtension`,
`AlarmlistConfiguration`, and `AlarmlistPlatform`. File equivalents use
`AlarmlistThisFileName`, `AlarmlistThisFile`, `AlarmlistThisFileFullPath`,
`AlarmlistThisFileDirectory`, and `AlarmlistThisFileExtension`. Alarm properties use
`AlarmlistAlarmFullyQualifiedName`, `AlarmlistAlarmReferenceName`, `AlarmlistAlarmName`,
`AlarmlistAlarmCode`, `AlarmlistAlarmCategory`, and `AlarmlistAlarmDescription`.
Names omit the extension, File includes it, Extension includes the dot, and Directory
has no trailing separator except for roots. Inherited expressions use the consuming
alarm and file. Scalar alarm properties read evaluated values; cycles are errors.

The SDK passes captured project path, Configuration, Platform and AlarmlistProperty
items to the task. Direct compiler callers use `CompilationContext` overloads of
Compile, CompileFiles or CompileSnapshots; missing context diagnoses only on use.
No environment variables or ambient project properties are imported. ALM0005-ALM0012
cover invalid abstract/expression syntax, unknown or unassigned values, unavailable
context, scalar cycles, and invalid/duplicate declarations. Failed compilation
preserves previous output. Source serialization retains raw expressions.

## Localization and multiple outputs

Use `$(loc:Inspection)` in an ALMX text field and define `Inspection` in a saved
string-only RESX resource. For example, `Translations.en.resx` may contain:

```xml
<root>
  <data name="Inspection" xml:space="preserve">
    <value>Inspect $(par1)</value>
    <comment>Shown to the operator.</comment>
  </data>
</root>
```

The consuming alarm's parameter scope supplies `par1`; with `par1=001`, the result
is `Inspect 001`. Keys are case-sensitive, with grammar
`[A-Za-z_][A-Za-z0-9_.-]*`. The `loc:` prefix is case-sensitive. Nested localization
inside translations is initially unsupported. Escapes and substituted parameter
values remain literal; replacement text is never rescanned.

```xml
<PropertyGroup>
  <AlarmlistCultures>de-DE;en;keyword</AlarmlistCultures>
</PropertyGroup>
<ItemGroup>
  <AlarmlistResource Remove="obsolete/**/*.resx" />
  <AlarmlistResource Include="../shared/Operator.resx" Culture="en"
                     Link="Translations/Operator.en.resx" />
  <AlarmlistResource Update="My.Catalogue.resx" Culture="" />
</ItemGroup>
```

Default resource inclusion is `**/*.resx`, using the same output/intermediate and
hidden-directory exclusions as ALMX. Explicit Include, Remove, Update, linked
files, and conditions are supported. Disable the glob with
`EnableDefaultAlarmlistResourceItems=false`. Explicit `Culture` metadata wins,
including an explicitly empty value for an optional unqualified key catalogue.
Otherwise the filename's final dotted suffix is the culture: `Labels.de-DE.resx`
is German/Germany and `Labels.resx` is unqualified. Invalid dotted suffixes are
errors. Multiple resources per culture are allowed, but duplicate keys are errors.
String values and comments are supported; object/file-reference entries and DTDs
are rejected without deserializing or loading external content.

Culture lookup follows the requested culture and its .NET parents, stopping before
invariant: `de-DE` can use `de`. Empty values count as missing; whitespace values
are nonempty. There is no unqualified/default, unrelated-language, or keyword
fallback. A nonempty translation that evaluates to empty does not restart lookup.
Keyword mode displays the complete `$(loc:Inspection)` token, even for undeclared
keys. Resource structure and duplicate keys are still validated in keyword mode.

Missing, empty or whitespace-only AlarmlistCultures selects keyword. Other lists
trim and normalize entries, rejecting unsupported cultures, duplicates, and empty
entries within a list. Output paths are
`<OutputPath>/<culture>/<CompiledAlarmlistFileName>`, including `keyword`.
The filename must be a basename. `AlarmlistCulture` is reserved for the independent
IDE preview selection; it does not alter the build list. Designer localization
support follows in the milestone-five IDE iterations.

The task captures saved ALMX and RESX inputs once per batch, evaluates and serializes
all cultures, then stages the outputs. Validation/compilation failures leave every
previous output unchanged. Ordinary install failures and cancellation roll back
installed files; restoration failures are reported with retained backup paths.
This does not guarantee atomicity across process termination or power loss.
A project/configuration/platform ownership manifest under IntermediateOutputPath
tracks exact output and temporary paths. Successful builds retire outputs removed
from the selection or moved by output settings. Clean removes configured/tracked
files, without scanning or deleting culture folders. Unrelated files survive.
Rebuild explicitly runs Clean first, so a failed Rebuild cannot preserve files that
Clean already removed. ALM0013-ALM0018 report culture, resource, duplicate-key,
missing-translation, nested-localization and invalid-key errors.

Build and future localization consumers use saved RESX contents only. Pending
resource-editor drafts are never read or saved by Alarmlist. The native editor may
autosave a committed cell; that saved value is then eligible for capture.

## Compiler analysis API

The compiler in each SDK task-runtime directory exposes
`AlarmCompiler.AnalyzeSnapshots(IEnumerable<AlmxTextSnapshot>)` for host adapters.
It returns a detached, read-only `Alarmlist.Analysis.AnalysisResult` with contract
version 2, input versions, local/effective fields, defining alarms and locations,
reference chains, procedure origins, Clear exclusions, and located diagnostics.
It shares the compiler's resolution rules and never reads or writes snapshot paths.

The additive overload `AnalyzeSnapshots(snapshots, includeCompiledXml: true)` also
returns `AnalysisResult.CompiledXml` from those same bound inputs through the
normal XML output writer. It is null when not requested or compilation fails.
The original overload keeps its analysis-only behavior and returns contract 2.
`AnalyzeSnapshots(snapshots, context, includeCompiledXml)` captures the same
CompilationContext as compilation. EffectiveExpression and procedure RawText retain
source tokens; EffectiveValue and surviving procedure Text carry evaluated results.
Check IsValueAvailable and HasEvaluatedValues independently of IsResolved: an abstract
alarm or a field-dependency error can have a valid reference chain. IsAbstract marks
templates. Alarm and source Properties contain visible custom and well-known values,
local declaration/assignment presence, and separate declaration/value origins.
`XmlAlarmlistOutputWriter.WriteToString(AlarmList)` is the in-memory output sink;
its text matches file output, including the UTF-8 declaration and `AlarmList`
root. Neither API saves source files or changes existing build output.

Check each alarm's `IsResolved` before using effective fields or procedure lists.
Invalid or ambiguous reference chains retain local data but have no effective
projection; unrelated valid alarms remain available. Malformed XML still throws
`XmlException`. Result-local ordinals distinguish duplicate or unnamed alarms;
they are not persistent identities. Editor commands must validate source versions
and use source locations rather than treating procedure indexes as XML child indexes.

The SDK exposes `AlarmlistAnalysisCompilerAssembly` during project evaluation as
the absolute path to its `tasks/net472/Alarmlist.Core.dll`. It always selects the
desktop compiler for the in-process Visual Studio host, regardless of the build
runtime or an `AlarmlistTasksAssembly` override. Querying it requires no Build
target and writes no compiled output. Treat it as an SDK-provided location;
an absent property means the SDK does not provide this bridge integration.

The extension now has an isolated AppDomain bridge for contract 2 and a CPS
project service that captures this property and ordered Compile inputs from the
same evaluation, then analyzes current buffer/disk snapshots. The designer uses
these results for reference selection, inherited scalar/procedure values, Clear
exclusions, section copies, live designer/Error List diagnostics, and resolved
previews. The bridge requires context-aware analysis and compiled XML preview;
older SDK contracts report explicit update guidance. No alternate compiler is
loaded. Release the matching SDK before distributing a VSIX using these capabilities.

## Build and test this repository

From the repository root:

```powershell
dotnet build AlarmlistProject.slnx
dotnet test AlarmlistProject.slnx
```

The SDK package is generated during Build at
`artifacts/packages/<Configuration>/NonShipping/Alarmlist.MSBuild.SDK.<Version>.nupkg`.
To consume it locally, add that directory as a NuGet package source. When using
a `NuGet.Config` with package source mapping, map `Alarmlist.MSBuild.SDK` to
the local source as well.

The compiler, tasks, and tests target `net472;net8.0`. Running the `net472`
tests requires Windows and .NET Framework. The dedicated `AlarmList.MSBuild.SDK.Tests` project resolves
the generated package from an isolated local feed and cache, so they do not
use a previously installed SDK package.

The package tests follow [Arcade's SDK test structure](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.Tests):
checked-in sample projects, an xUnit collection fixture, isolated test apps,
and retained MSBuild logs. See the [test project guide](../Alarmlist.SDK.Tests/README.md).

To run the package tests with desktop MSBuild, set the executable path:

```powershell
$env:ALARMLIST_TEST_MSBUILD_PATH = '<Visual Studio installation>/MSBuild/Current/Bin/MSBuild.exe'
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj --framework net472
Remove-Item Env:ALARMLIST_TEST_MSBUILD_PATH
```

The package layout follows the [MSBuild custom task packaging guidance](https://learn.microsoft.com/en-us/visualstudio/msbuild/tutorial-custom-task-code-generation).
