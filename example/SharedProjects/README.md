# Native Shared Projects playground

Build the extension, then run from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/prepare-shared-example.ps1
dotnet msbuild example/SharedProjects/North/North.almproj /t:Build
dotnet msbuild example/SharedProjects/South/South.almproj /t:Build
```

Open `SharedProjects.sln` in Visual Studio 2022 or 2026 with the matching extension
and native Shared Project tooling installed. Preparation selects the SDK version
from the built VSIX and uses a package-content-specific local cache. It restores
the two consumers without building them or replacing edited sources, imports,
projects or the solution. `-Configuration Release` selects a Release-built VSIX;
`-MSBuildPath` selects desktop MSBuild. Generated `global.json`, `NuGet.Config`,
packages and build outputs remain local.

Shared contains one ALMX alarm, English/German translations and a named lookup
list. North and South import the same `.projitems`; they compile independently.
Their output cultures are keyword, English and German. English names are
`North: Ready` and `South: Ready`; German names end in `Bereit`. Both resolve
Description to the literal lookup value `READY`. Shared produces no alarm artifact.

Open `Shared/Alarms.almx` from Shared. Choose North or South using the designer's
project selector or XML's Project context selector. The displayed Summary culture
and parameter candidates differ (`NorthOnly`/`SouthOnly`). An unsaved ALMX buffer
updates both consumers; private designer drafts remain private through view/context
changes. Alarm Summary keeps its own selected project. Resources and lookup files
are analyzed from saved text, including when their XML editor has unsaved changes.

Use Ctrl+Space in ALMX, `Devices.almlookup`, or either `.almproj` for completion.
Tab/Enter commits an Alarmlist suggestion; Escape dismisses it. ALMX completion
also works in Open With > XML Editor. Structure remains available without a
consumer; semantic candidates require current compatible analysis of a selected
consumer. Lookup literal values and MSBuild conditions/property values are not
expression completion contexts. Existing general MSBuild completion remains.

Use Dependencies > Add Reference > Shared Projects to remove/re-add Shared for a
consumer. Removing the reference preserves shared files. These consumers have no
local ALMX, so removing their only shared import produces
the existing no-input build error and preserves previous output. Re-add Shared to
build again. Edit membership in Shared;
consumer projections open the same physical files. Save before unloading projects
when Visual Studio asks about related dirty documents. The extension never saves
or discards shared text automatically. After unload, reactivate the existing shared
tab; a removed selected context requires a new choice even after reload.
