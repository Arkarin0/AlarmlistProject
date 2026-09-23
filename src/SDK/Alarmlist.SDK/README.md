# Alarmlist.MSBuild.SDK

An MSBuild SDK that compiles `.almx` source files into a single XML alarmlist.
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

The default output is `bin/Debug/Plant.Alarmlist.xml`. `Restore` is a no-op
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
| `AlarmlistTasksAssembly` | Task assembly for the current MSBuild runtime |

`CompiledAlarmlistFile` exposes the absolute output path. Projects can use
`BeforeTargets="Build"` or `AfterTargets="Build"` for custom targets;
`Directory.Build.props` and `Directory.Build.targets` are also imported.
Each build recompiles the selected inputs so changes to conditional items and
removed files are reflected. Compiler diagnostics fail the build. Clean removes
the configured compiled output and preserves unrelated files.

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
