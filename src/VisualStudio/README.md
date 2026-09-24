# Visual Studio project integration

Milestone one supplies a CPS project type for `.almproj`, project and ALMX item
templates, Solution Explorer file management, project properties, and
Build/Rebuild/Clean through `Alarmlist.MSBuild.SDK`. Compiler errors are reported
through the standard MSBuild logger and Visual Studio Error List.

Milestone two adds a WPF MVVM editor with **Designer**, **Split**, and **XML**
modes. The designer and embedded Visual Studio source view share one document
buffer, including edits made from additional windows. The
[milestone-two design](MILESTONE-2.md) records its architecture and references.

The [milestone-three plan](MILESTONE-3.md) covers project-wide reference analysis,
inherited fields with WPF chain-icon adorners, inherited test-procedure rows,
live diagnostics, and resolved preview. It recommends section-level local copies
for customizing inherited procedures and records the remaining milestone-two
lifecycle prerequisites. These features are planned, not yet implemented.

The repository-wide [future feature notes](../../FUTURE-FEATURES.md) capture
well-known and custom properties, localization, broader IntelliSense, and syntax
generation, including open questions about UI integration and collection values.

## Editing ALMX files

Open an `.almx` file normally to use the combined editor. Use the controls at the
bottom to switch modes, arrange panes side by side, or swap them. Drag the divider
to resize. Layout preferences are stored in Visual Studio user settings, separately
from document contents. **Open With > XML (Text) Editor** remains available.

Filter the alarm list by name or identifier, select an alarm, and edit its local
`FullyQualifiedName`, `Name`, `Code`, `Category`, `Description`, or `ReferenceName`.
A checked field is present in the source; clearing its checkbox removes it. An
empty checked field remains an explicit empty local value. The list becomes a
selector when the designer pane is narrow.

Enter or leaving a field commits its draft; Shift+Enter inserts a newline, and
Escape cancels a draft. Save and Save All also commit active drafts. Each accepted
designer action is one shared document undo transaction. Ctrl+Z with an uncommitted
draft cancels that draft; subsequent undo uses the document history. A conflicting
edit in another view is reported instead of overwriting it. Escape reloads the
current value after a conflict.

Expand **Test procedure** below the alarm fields to edit the local **Instructions**
and **Reset** sections. Choose Step, Hint, Warning, Note, or Clear and select
**Add entry**. Each entry has a kind selector, multiline text, move up/down, and
Remove controls. Clear has no text: it discards all entries preceding it in that
section, including inherited entries. Their order therefore matters.

Procedure drafts use the same Enter/focus-loss, Escape, Save, and Undo behavior
as alarm fields. Changes in another view to the same section cause a conflict;
independent Instructions and Reset edits can be combined. Duplicate procedure
containers/sections and entries containing nested markup or comments must be
edited in XML. Unknown entries and surrounding source remain intact.

Malformed XML disables designer mutations until the source is corrected. Fields
containing nested markup, comments, or duplicate scalar elements must be edited
in XML. Changes preserve untouched source text, including attributes, comments,
unknown elements, procedure sections, and `Clear` directives. Renaming an alarm
does not rewrite references. Reference completion, inherited-value previews,
and live semantic diagnostics remain milestone three.

Save As keeps the tab, project item, and Running Document Table at the new path
while all open views retain the same buffer. The original file remains on disk
and is excluded from compilation so default globs cannot compile both copies.
Saving outside the project creates a linked item. This also applies when using
the standard XML editor for an ALMX project item.

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
package registers the project type and combined ALMX editor. The compiler and
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

The package composes the editor through Visual Studio MEF. Typed factories inject
the buffer adapter and XML editing service into `AlmxDocumentModel`, and the shared
model and alarm-view-model factory into each designer view model. The model and
view models do not locate services themselves. `Editor/` contains host adapters,
`Documents/` contains the buffer abstraction and shared model, `Editing/` contains
source projections and span edits, and `UI/` contains views and view models.

The file-handling reference is Microsoft's
[Supporting Multiple Document Views](https://learn.microsoft.com/en-us/visualstudio/extensibility/supporting-multiple-document-views):
views are separate from document data. `AlmxEditorFactory` reuses compatible
`punkDocDataExisting` from Visual Studio and attaches one document session to the
actual managed buffer's property collection. Each view has its own selection,
filter, and drafts. Visual Studio's Running Document Table and standard text
buffer own persistence, rename, external reload, and save prompts. There are no
independent file writers or per-view disk caches. Targeted text edits use the
buffer's existing undo history. Closing a view releases its subscriptions; the
session is disposed when the text document is disposed.

This applies Roslyn's separation of source text and derived models at document
scope, without adding a compiler workspace or a Roslyn dependency. Projections
are currently parsed synchronously from current snapshots; there are no background
parse results that can arrive out of order. Large-file parsing performance has not
been benchmarked.

The embedded source pane is the native VS code window's `IVsUIElementPane` element.
This initializes XML language services and syntax coloring while retaining the
document buffer shared with the designer.

## Automated validation

```powershell
dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
```

The unit tests run on `net472`, exercising source preservation, draft conflicts,
multi-view state, invalid XML recovery, layout preferences, the CPS build check,
and MSBuild XAML rules. Integration tests run on both repository
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
and exercises Clean/Rebuild. It also opens the custom editor, checks its embedded
source view, compares document-data identity across New Window and XML-first
opening, and exercises XML edits, undo/redo, Save All, and XML-view survival after
designer closure. For both editor types, it checks Save As to local and linked
files and back to the original path, shared buffer/RDT identity across two views,
subsequent saves, and preservation of the original disk file. The duplicate-view
command is resolved by GUID/ID to support VS2022's New Window and VS2026's New Tab.
It closes only the process it started. Add `-KeepOpen` to retain a successful or
failed run with interactive prompts enabled for inspection. The generated
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

## Milestone-two validation record (2026-09-24)

- Extension unit tests: **57 passed**, `net472`, including procedure editing,
  concurrent drafts, XML preservation, and row stability during commits.
- VSIX/template integration tests: **10 passed per framework**, `net472` and `net8.0`.
- SDK regression tests: **17 passed per framework** in the preceding implementation
  run; this editor change does not modify SDK contents.
- VS2022 **17.14.26**: final native code-window editor and project smoke passed,
  including local/linked Save As with both editors and two shared views.
  Log directory: `VisualStudioSmoke/9a7917ebd60149dc9fe1ffaf822204c8`.
- Test procedure interactive checks on VS2022: added Instructions and Reset
  entries, edited and saved text containing XML-sensitive characters, removed a
  row immediately after typing, and restored it with shared Undo. The saved file
  in `VisualStudioSmoke/bfed25f6f77c458e86a385fc4808a46e` contains both sections.
- VS2022 interactive checks: XML syntax coloring, split layout, focused-field
  commits, and loose-file Save As followed by another save. Retained files:
  `VisualStudioSmoke/7470c4f323dc410d9b01494594424330`.
- VS2026 **18.10.12217.157**: final native code-window editor and project smoke
  passed with the same expanded Save As checks.
  Log directory: `VisualStudioSmoke/ae46e99398114cdab47acc9dc145db27`.
- Both hosts: interactive dirty-close cancel, clean external reload into the
  designer, declined reload with local edits, cancelled external overwrite, and
  cancelled Git read-only Query Edit. Drafts survived rejection and saved after
  write access was restored. Disk contents were checked after cancellation.
- VS2022: cancelled the native read-only Save As fallback without losing the draft.
- VS2026: project Save As through the dialog accepted `GUI Saved & 100%;.almx`;
  a following designer edit saved to that file and left the original unchanged.

These log paths are relative to `artifacts/log/Debug/`. Checkout against a
checkout-based source-control provider remains unverified; the available Git
provider's read-only workflow was exercised. Large-file parsing has not been
benchmarked. See [milestone two](MILESTONE-2.md) for implementation decisions and
the shared-document references.
