# zenon keyword XML export

Export alarm text with COPA-DATA zenon language-switching keys surrounded by `@`,
while retaining resolved literal values between the keys.

## When to use it

Use this provider when a downstream integration consumes the existing Alarmlist
XML structure and needs zenon keyword text. It produces that XML with keyword
markers; it does not create a complete zenon alarm/variable import file or export
a zenon language table. Runtime import certification is outside this feature.

## Requirements and availability

Use a matching Alarmlist SDK and the `Alarmlist.Exporters.Zenon` NuGet package.
The package contains net472 and net8.0 provider assets, selected by the executing
MSBuild runtime. Reference the package in each exporting project; registration
does not propagate through project references or select the provider automatically.

The repository's prepared [example playground](../../example/README.md#try-per-project-exports)
includes a standalone Zenon project alongside North's XML and South's JSON exports.

## How it works

Provider ID `Zenon` uses compiler-owned localization segments in fields,
collections, procedure titles and entries. Inherited expressions use the consuming
alarm's evaluated values. Resource keys retain their original spelling and case,
including in concrete cultures. Literal text and parameter values stay literal.
Escaped token-looking text is never interpreted again.

The delimiter convention follows COPA-DATA's
[language-switching documentation](https://download.copadata.com/fileadmin/user_upload/Downloads/Dokumentation/820SP0/ENGLISH/Manual/Language_switch.pdf).
Configure corresponding language-table keys in zenon separately. An Alarmlist
RESX translation does not create or synchronize that table.

The provider rejects literal `@`, adjacent localization keys without a nonempty
literal separator, and keys requiring parameter or argument substitution. These
cases cannot be represented by this provider's plain keyword projection. Build
failure preserves all previous exports and the ownership manifest.

## Configuration or syntax

Select `Zenon` in `AlarmlistExportTypes`, alone or alongside `XML;JSON`.
The default filename is `$(MSBuildProjectName).Zenon.xml` in each culture directory.
`AlarmlistZenonOutputFileName` overrides the filename, without a directory.
No `Option_` metadata is supported.

Direct DLL registration uses `AlarmlistExportProvider Include="Zenon"` with
`AssemblyPath`, `TypeName=Alarmlist.Exporters.Zenon.ZenonExportProvider`, and
`OutputFileName`. Select the DLL matching the executing MSBuild runtime; the host
supplies the compiler contract. See [export providers](export-providers.md).

## Minimal example

With SDK/package version 1.0.0 available from the configured feed, use
`Plant.almproj` beside `Alarms.almx`:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK/1.0.0">
  <PropertyGroup>
    <OutputType>Exports</OutputType>
    <AlarmlistExportTypes>Zenon</AlarmlistExportTypes>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Alarmlist.Exporters.Zenon"
                      Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

```xml
<Alarmlist>
  <Alarm>
    <FullyQualifiedName>Plant.Position</FullyQualifiedName>
    <Properties><Property Name="Position" Value="1" /></Properties>
    <Name>$(loc:YouPlacedAtPositionKeyword) $(Position) $(loc:atLocationkeyword)</Name>
  </Alarm>
</Alarmlist>
```

Run `dotnet msbuild Plant.almproj /restore /t:Build`.

### Expected result

`bin/Debug/keyword/Plant.Zenon.xml` contains:

```xml
<Name>@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@</Name>
```

The source remains unchanged. Concrete cultures need valid resources for compiler
resolution and retain these same keyword markers in the zenon artifact.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| No zenon artifact | Package registration alone does not select exports | Add Zenon to AlarmlistExportTypes. |
| Literal delimiter failure | Literal text or a key contains @ | Remove the ambiguous delimiter from that text/key. |
| Adjacent keyword failure | Two keys have no literal separator | Add an intentional separator, such as a space. |
| Substitution failure | A resource expression or formatted key needs dynamic substitution | Author separate plain keys and evaluated literal values. |
| Package cannot restore | Matching exporter version is absent or excluded by source mapping | Add it to the configured feed and source mapping. |

## Related features and tutorials

See [export providers](export-providers.md), [localization](localization.md),
[JSON export](json-export-format.md), and
[export resolved alarms](../tutorials/export-resolved-alarms.md).
