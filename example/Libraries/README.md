# Reusable alarm libraries

This independent playground contains `Common -> Equipment -> LineA / LineB`.
Arrows mean that the project on the right references the library on the left.
It uses the repository's built SDK and does not inherit the North/South
playground's shared source items or analyzer configuration.

## Prepare and build

From the repository root:

```powershell
dotnet build src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj
./eng/prepare-library-example.ps1
dotnet msbuild example/Libraries/LineA/LineA.almproj /t:Build
dotnet msbuild example/Libraries/LineB/LineB.almproj /t:Build
```

Preparation writes ignored SDK/feed configuration using the version from the built
VSIX and a content-specific package cache. It restores both consumers without
changing ALMX sources. In an experimental IDE with the matching extension deployed,
open `Libraries.sln`. Both VS2022 and VS2026 support the `.sln` format.

Each consumer's `bin/Debug/en/<project>.Alarmlist.xml` contains three alarms:
`Common.Stop`, `Equipment.Pump`, and its own `<project>.Local`. Abstract templates
remain referenceable but are not emitted.

| Value | LineA | LineB |
| --- | --- | --- |
| Site parameter | LineA | LineB |
| Every alarm's code | A-STOP | B-STOP |
| Every description | Inspect line LineA | Check LineB |

`Common` supplies template fields, a concrete alarm, a translation and a named-list
entry. `Equipment` overrides the translation/list entry and adds a template and
concrete alarm. Both consumers override Site and the list entry; LineA also overrides
the translation. LineB uses Equipment's translation, evaluated in its own consumer
scope. Library project defaults do not follow template inheritance into local alarms.

## Explore in Visual Studio

Open a consumer's `Alarms.almx`, choose its inherited template in the reference picker,
and inspect the project preview table. Library labels show identity and version.
Go to source opens the actual referenced project source. Save an Equipment edit to
refresh consumers without Build; an unsaved Equipment edit remains private.

Use **Add New Project > Alarmlist Library** to create another library. Its Alarmlist
property page exposes output type, library filename, package ID and package version.
Use **Add Project Reference** for local libraries, or NuGet package management after
configuring a feed containing the library and all transitive dependency packages.

## Try NuGet instead of a project reference

Pack both libraries from this directory:

```powershell
dotnet msbuild Common/Common.almproj /t:Pack /p:PackageOutputPath=../.feed
dotnet msbuild Equipment/Equipment.almproj /t:Pack /p:PackageOutputPath=../.feed
```

In a copy of LineB, replace its ProjectReference with:

```xml
<PackageReference Include="Example.Equipment" Version="1.0.0" />
```

Keep the prepared NuGet configuration in an ancestor directory, run Restore then
Build, and compare the three alarm values above. Equipment's package declares its
dependency on Example.Common; it does not embed that library. Package navigation
opens generated read-only definitions. The commands create local packages only.
When changing package contents, use a new package version to avoid cached payloads.
