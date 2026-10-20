# Reusable alarm libraries

This independent playground contains `Common -> Equipment -> LineA / LineB`.
Arrows mean that the project on the right references the library on the left.
It uses the repository's built SDK and does not inherit the North/South
playground's shared source items or analyzer configuration.

## Prepare and build

Double-click either shortcut in `scripts/`:

- `start-library-project-references.bat`: live project references.
- `start-library-packages.bat`: locally packed NuGet libraries.

Both deploy the matching extension and open this same `Libraries.sln` in VS2026
(`AlarmlistExp`). Pass `-VisualStudioVersion 2022` to select VS2022, or
`-PrepareOnly` to deploy/prepare without opening the IDE. Close this solution before
switching modes or deploying again. These scripts do not prepare North/South.

From the repository root:

```powershell
dotnet build src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj
./eng/prepare-library-example.ps1
dotnet msbuild example/Libraries/LineA/LineA.almproj /t:Build
dotnet msbuild example/Libraries/LineB/LineB.almproj /t:Build
```

Preparation writes ignored SDK/feed configuration and `LibraryExample.local.props`
using the version from the built VSIX and an isolated package cache. It restores
all four projects without changing ALMX sources. In an experimental IDE with the
matching extension deployed,
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

Expand **Dependencies / Projects** in project mode to see Equipment in each
consumer and Common in Equipment. In package mode, **Dependencies / Packages**
shows Example.Equipment (1.0.0), with Example.Common (1.0.0) beneath it as a
read-only transitive dependency. Labels follow the IDE language (for example,
Abhängigkeiten / Projekte / Pakete). Empty groups are hidden; no target-framework
group is needed for these framework-independent projects.

Use the dependency root's native Add Reference or Manage NuGet Packages action.
Delete can remove a direct project reference; package changes go through NuGet
management. Missing or failed restore keeps direct package nodes visible as
unresolved and clears their old children until recovery. Browsing the tree does
not build or pack libraries. Configuration changes use the references evaluated
for the selected configuration.

Open a consumer's `Alarms.almx`, choose its inherited template in the reference picker,
and inspect the project preview table. Library labels show identity and version.
Go to source opens the actual referenced project source. Save an Equipment edit to
refresh consumers without Build; an unsaved Equipment edit remains private.

Use **Add New Project > Alarmlist Library** to create another library. Its Alarmlist
property page exposes output type, library filename, package ID and package version.
Use **Add Project Reference** for local libraries, or NuGet package management after
configuring a feed containing the library and all transitive dependency packages.

## Try NuGet instead of a project reference

From the repository root, prepare package mode:

```powershell
./eng/prepare-library-example.ps1 -ReferenceMode Package
dotnet msbuild example/Libraries/Libraries.sln /t:Build
```

The helper packs Common and Equipment into `.feed` using project references, then
sets `UseLibraryPackages=true` in the ignored local props file and restores the
solution projects. Equipment and both consumers condition their references on
this property; sources are shared unchanged. A fresh package-mode cache prevents
old contents of the fixed example versions from being reused after repacking.
Compare the three alarm values above. Equipment's package declares its
dependency on Example.Common; it does not embed that library. Package navigation
opens generated read-only definitions. The commands create local packages only.
Saved library-source edits reach package consumers only after packing/restoring
again. To return to live project references, run preparation with
`-ReferenceMode Project` or use the project-reference shortcut. For an individual
CLI build the property can also be overridden with `/p:UseLibraryPackages=false`
or `true`; restore after changing it. Package mode requires prepared local packages.
