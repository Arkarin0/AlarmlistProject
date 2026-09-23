# Visual Studio project integration

Milestone one supplies a CPS project type for `.almproj`, project and ALMX item
templates, Solution Explorer file management, project properties, and
Build/Rebuild/Clean through `Alarmlist.MSBuild.SDK`. Compiler errors are reported
through the standard MSBuild logger and Visual Studio Error List. ALMX files
open in Visual Studio's XML editor. The WPF designer, shared document buffer,
and live semantic editing belong to later milestones.

The [milestone-two plan](MILESTONE-2.md) describes the proposed MVVM designer,
dependency injection, shared document handling, and its acceptance criteria.

## Requirements and installation

- Visual Studio **2022 17.9+ or 2026**, x64, with the core editor and NuGet package
  manager. Both hosts use the same VSIX and the Visual Studio 17.x API baseline.
- Released VSIX versions require their matching `Alarmlist.MSBuild.SDK` version
  on NuGet.org. With nuget.org enabled, Visual Studio downloads it automatically
  on first project creation. Internet access is needed until the SDK is cached.
  No project-specific `NuGet.Config` is needed for this normal installation.
- Building the extension requires Windows and the .NET Framework 4.7.2 targeting
  pack. The VSSDK build tools are restored from NuGet.

Build from the repository root:

```powershell
dotnet build src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj
```

This produces both:

- `artifacts/bin/Alarmlist.VisualStudio/Debug/net472/Alarmlist.VisualStudio.vsix`
- `artifacts/packages/Debug/NonShipping/Alarmlist.MSBuild.SDK.<Version>.nupkg`

For **unpublished development builds**, add the package directory as a NuGet source in Visual Studio's NuGet settings
(or in the consumer repository's `NuGet.Config`). If using package source
mapping, map `Alarmlist.MSBuild.SDK` to that source. Install the VSIX and restart
Visual Studio. Search for **Alarmlist Project** in New Project; **Alarm List** is
available in Add New Item. Existing SDK projects can be opened directly or added
to a solution. Projects do not need `TargetFramework`.

Templates pin the SDK version obtained from the SDK packaging project at build
time. The VSIX does not install an SDK resolver, modify NuGet settings, or bundle
a private compiler. When developing an unpublished package with the same version,
use a fresh consumer package cache to avoid loading an older SDK. The automated
tests use isolated feeds and caches for this reason.

## SDK-first release

The SDK package is the authoritative build tool for both Visual Studio and CI.
The VSIX supplies IDE integration only; it never installs a private compiler,
adds a package feed, or writes machine-specific SDK paths into projects.
Repositories that restrict NuGet sources or use source mapping must allow
`Alarmlist.MSBuild.SDK` from nuget.org explicitly.

Prepare a stable release with `eng/release-visualstudio.ps1`. First set the VSIX
`Identity Version` in `source.extension.vsixmanifest` to the release version.
The script passes that version to the existing Arcade/MSBuild build, runs the
entire solution test suite, checks the template's SDK pin, and stages a matching
SDK/VSIX pair with checksums. It does not publish during the build.

```powershell
powershell.exe -NoProfile -File eng/release-visualstudio.ps1 -Version 1.0.0 -Stage Prepare
```

Review `artifacts/release-candidates/1.0.0/`. To publish, use a NuGet.org account
authorized to own `Alarmlist.MSBuild.SDK`. Set `ALARMLIST_NUGET_API_KEY` securely
in the process environment to a key scoped to that package, then run:

```powershell
powershell.exe -NoProfile -File eng/release-visualstudio.ps1 -Version 1.0.0 -Stage PublishSdk
```

Alternatively upload the exact staged `.nupkg` through NuGet.org. After its
validation/indexing completes, run:

```powershell
powershell.exe -NoProfile -File eng/release-visualstudio.ps1 -Version 1.0.0 -Stage VerifyPublished
```

Verification creates a fresh consumer and isolated caches with only nuget.org
enabled, compiles the actual VSIX project template, and compares the downloaded
SDK payload to the tested candidate (allowing NuGet.org's added signature).
Only success copies the VSIX into `artifacts/releases/1.0.0/` for distribution.
If indexing is pending, rerun verification; do not publish the VSIX first or
silently substitute another SDK version. Retain the original candidate between
stages. Package versions are immutable: use a new version for changed contents.

This uses Arcade's separation of build and publication, with a small explicit
NuGet.org release script instead of its organizational Maestro infrastructure.
The repository's pinned Arcade SDK and ordinary build behavior are preserved.
Public publishing requires account setup and is not completed by preparing a
local candidate.

## Architecture

`Alarmlist.VisualStudio` is a `net472` in-process VSSDK/CPS MEF component. Its
package registers the project type and existing XML editor. The compiler and
MSBuild task remain independent of Visual Studio, targeting `net472;net8.0`.

The SDK ships the capabilities, Debug/Release configurations, and XAML item and
property schemas under `tools/`. Solution Explorer uses evaluated `Compile`,
`None`, and `Folder` items. CPS handles glob-aware add, remove, rename, and reload
operations. The scoped `IBuildUpToDateCheckProvider` always requests an MSBuild
build so removed or conditional inputs cannot leave stale compiled output.

This follows Microsoft's [CPS project registration sample](https://github.com/microsoft/VSProjectSystem/blob/master/samples/WindowsScript/WindowsScript/WindowsScript.ProjectType/MyUnconfiguredProject.cs)
and [MSBuild rule registration guidance](https://github.com/microsoft/VSProjectSystem/blob/master/doc/extensibility/adding_xaml_rules.md).
Unlike the older sample's machine-wide MSBuild installation, rules travel in our
existing NuGet SDK so command-line consumers and the IDE use identical versions.
Repository build policy remains in `eng/`, with the existing Arcade imports.

## Automated validation

```powershell
dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
```

The unit tests run on `net472`, exercising the CPS build check and deserializing
the rules with MSBuild's XAML types. Integration tests run on both repository
frameworks, inspect the built VSIX, instantiate its actual templates, and compile
against the matching SDK in an isolated consumer directory. They verify file
lifecycle, diagnostic failure, and output preservation. They do not launch the
IDE. Logs are retained in `artifacts/log/<Configuration>/VisualStudioTests/`.

## Experimental IDE smoke test

For manual testing, run `scripts/start-vs2022.bat` or `scripts/start-vs2026.bat`.
Each launcher finds the matching installed Visual Studio, builds and deploys the
extension, then opens its `AlarmlistExp` instance. Close that experimental instance
before rerunning the launcher. Add `--deploy-only` to build and deploy without
opening the IDE. The launcher prints the SDK package feed to configure for your
test solution as described above.

For F5 debugging, open the solution in **Visual Studio 2022 or 2026**, set
`Alarmlist.VisualStudio` as the startup project, and build/start it. IDE builds
deploy the extension to `AlarmlistExp`, and F5 launches that same experimental
instance. Command-line builds only produce the VSIX unless deployment is
explicitly enabled. Close the experimental instance before rebuilding.

In the experimental instance, right-click your solution and select **Add > New
Project**, clear any language/platform/project-type filters, and search for
**Alarmlist Project**. Configure the SDK package feed described above before
creating the project. **Add Existing Project** is for an existing `.almproj` file.
The startup program uses the hosting Visual Studio installation. Each version
has its own `AlarmlistExp` instance; deployment and F5 target that host together.

Deploy using the selected **Visual Studio installation's desktop MSBuild**, with Visual Studio extension
development tools installed. Find its path and instance ID with `vswhere`:

```powershell
& "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -version '[17.9,19.0)' -products '*' -format json

& '<VS installation>/MSBuild/Current/Bin/MSBuild.exe' src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj /restore /p:DeployExtension=true /p:DeployTargetInstanceId=<instance-id> /p:VSSDKTargetPlatformRegRootSuffix=AlarmlistExp

powershell.exe -NoProfile -STA -File eng/test-visualstudio.ps1 -VisualStudioPath '<VS installation>'
```

The smoke script starts a separate `AlarmlistExp` instance, discovers the installed
templates, creates a project, adds a folder and ALMX item, renames it, closes and
reopens the solution, deletes the item, checks compilation errors in Error List,
and exercises Clean/Rebuild. It closes only the process it started. The generated
projects, local package feed, and ActivityLog are retained under
`artifacts/log/<Configuration>/VisualStudioSmoke/`. Initialize the experimental
instance manually first if Visual Studio requires first-run or sign-in input.

Run deployment and the smoke test for each installed host when changing extension
compatibility. The manifest's open upper bound follows Microsoft's
[API-version compatibility model](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/extension-compatibility):
VS2026 supports the 17.x APIs used by this extension. The compiler and MSBuild
task frameworks remain unchanged.

For interactive verification, launch `devenv.exe /RootSuffix AlarmlistExp` and
also check the New Project/Add New Item dialogs, Add Existing Item, XML editing,
and the output directory, filename, and automatic inclusion project properties.
Compiler diagnostics currently have IDs and messages but no source spans;
file/line navigation and live diagnostics are later work.
