# Alarmlist.Exporters.Zenon

Opt-in provider ID Zenon renders compiler-owned localization references as @Key@
in the existing alarm XML shape, preserving resolved literal values. It does not
produce a full zenon alarm/variable import file or manage zenon language tables.

Reference this package privately and select Zenon in AlarmlistExportTypes.

```xml
<ItemGroup>
  <PackageReference Include="Alarmlist.Exporters.Zenon" Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
<PropertyGroup>
  <OutputType>Exports</OutputType>
  <AlarmlistExportTypes>XML;JSON;Zenon</AlarmlistExportTypes>
</PropertyGroup>
```

Use the matching version from your configured feed. This fragment belongs inside
an Alarmlist SDK project.

Ordinary package build assets choose net472 for desktop MSBuild and net8.0 for
.NET MSBuild. The consumer TargetFramework does not choose the provider runtime.
The matching SDK supplies the compiler contract; this package ships no compiler
copy, lib assets, dependencies or buildTransitive activation.

The default artifact is $(MSBuildProjectName).Zenon.xml per culture. Set
AlarmlistZenonOutputFileName to override it. Direct DLL registration uses
AlarmlistExportProvider metadata AssemblyPath, TypeName and OutputFileName instead.
For example, after copying the matching DLL to the project's exporters directory:

```xml
<ItemGroup>
  <AlarmlistExportProvider Include="Zenon">
    <AssemblyPath>exporters/Alarmlist.Exporters.Zenon.dll</AssemblyPath>
    <TypeName>Alarmlist.Exporters.Zenon.ZenonExportProvider</TypeName>
    <OutputFileName>keywords.xml</OutputFileName>
  </AlarmlistExportProvider>
</ItemGroup>
```

Select Zenon as above and use either package or direct registration. Filename
overrides cannot contain directories. This provider has no Option_ settings.

Literal @, adjacent keywords without literal separators, and localization keys
requiring parameter/argument substitution fail explicitly; no source is rewritten.
For example, separate key/literal/key segments produce:

@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@

Provider authoring and user guides are in documentation/features/ and
src/Compiler/Output/README.md in the source repository. Runtime vendor import
certification is outside this provider's keyword-XML scope.
