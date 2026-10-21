# Visual Studio project integration

Milestone one supplies a CPS project type for `.almproj`, project and ALMX item
templates, Solution Explorer file management, project properties, and
Build/Rebuild/Clean through `Alarmlist.MSBuild.SDK`. Compiler errors are reported
through the standard MSBuild logger and Visual Studio Error List.

**Add New Project > Alarmlist Library** creates reusable definitions with an abstract
template and a concrete derived alarm. In its Alarmlist project properties, choose
the output type and library filename, and set the package ID/version before sharing
it through project or NuGet references. Package identity is shared across build
configurations. The [library playground](../../example/Libraries/README.md) includes
two consumers, a transitive dependency, local feed preparation and expected output.

**Add > New Item > Alarmlist > Translations (RESX)** creates a resource file and
opens Visual Studio's resource editor. Keep a culture suffix in its name, such as
`Translations.en.resx` (the default) or `Translations.de.resx`. It is included as
an `AlarmlistResource`; saved string entries are available to `$(loc:Key)` expressions.

Milestone two adds a WPF MVVM editor with **Designer**, **Split**, and **XML**
modes. The designer and embedded Visual Studio source view share one document
buffer, including edits made from additional windows. The
[milestone-two design](MILESTONE-2.md) records its architecture and references.

The [milestone-three plan](MILESTONE-3.md) covers project-wide reference analysis,
inherited fields with WPF chain-icon adorners, inherited test-procedure rows,
live diagnostics, and resolved preview. It recommends section-level local copies
for customizing inherited procedures. The first two iterations add compiler
snapshots, file/line information for build diagnostics, and a read-only analysis
API for field/procedure origins and Clear exclusions. Iteration three adds an
isolated SDK analysis bridge and a CPS adapter for the evaluated compiler path.
Iteration four starts project analysis from CPS subscriptions, using current open
buffers and watched disk inputs. Iteration five connects the designer's reference
picker, scalar inheritance display, chain adorners, and source navigation. Iteration
six adds inherited procedure rows, Clear exclusions, effective sequences, and local
section copies. Iteration seven adds live designer diagnostics and a separate
Error List source with validated navigation. Iteration eight adds read-only
selected-alarm and compiled project XML previews. Iteration nine reduces repeated
designer lookup/refresh work, preserves preview text selection, and fixes themed
expander text while extending accessibility and performance checks. Iteration ten
adds repeatable linked-project acceptance and accessible project/alarm selectors.
Iteration eleven keeps local overrides visible and focused after expanded previews
clear, without taking focus back after subsequent input or view unload.
Iteration twelve verifies scalar commits, unsaved referenced values, linked-project
isolation, and Undo in VS2022 and VS2026, plus the focus/procedure regressions on VS2022.
Iteration thirteen gives scalar and section actions contextual accessibility names
and records live keyboard, theme, and compact-layout checks with explicit manual gates.
Iteration fourteen adds opt-in running-IDE analysis/lifecycle measurements to the
smoke script and records final host regressions and the remaining release gates.
Milestone-two lifecycle validation is recorded below and remains regression coverage.

The [milestone-four plan](MILESTONE-4.md) records the owner's 2026-10-06 decisions:
integrated property expressions, Project/File/Alarm declarations, well-known alarm values,
resolved displays that switch to raw expressions on focus, and local copies that
preserve expressions. Seven implementation iterations cover compiler, SDK, analysis,
designer completion, and validation; there is no opt-in or unreleased-version
backward-compatibility requirement. Inherited expressions use the child's context,
with current-alarm -> current-file -> project value lookup. Bases supply template
content and custom parameter declarations, but their parameter assignments are
not inherited. The `abstract` XML attribute marks template-only alarms, which remain
referenceable but are excluded from compiled output. A parameter table and Abstract
checkbox provide designer editing for these settings. The implementation is present;
its validation record distinguishes passing automated checks from the remaining
interactive-desktop and release acceptance gates.
Alarm-field properties read resolved values; custom parameter assignments remain
literal. Explicit-empty assignments stop fallback, while unassigned parameters
use outer scopes and cause errors only when used unresolved by a concrete alarm.
Milestone three's remaining release checks stay open. Milestone five localization
is implemented; see its [iteration results and remaining gates](MILESTONE-5.md).

[Milestone nine](MILESTONE-9.md) adds reusable alarm libraries through project and
NuGet references. Consumer analysis reads saved referenced projects and their
imports, ALMX, resources, lookups and configuration without running Build or
writing library artifacts. This also works for unloaded projects and projects
outside the solution. Save dependency changes to refresh consumers; unsaved
dependency XML remains private, while the consumer's own open ALMX buffers still
participate in its analysis. Solution configuration mappings select the referenced
project configuration. Library analyzers and EditorConfig validate their owning
library and do not become consumer policy.

Solution Explorer groups direct project references under Dependencies / Projects
(Abhängigkeiten / Projekte in a German IDE). The tree follows evaluated project
items in the active configuration, keeps missing references visible with a warning,
and does not require referenced projects to be loaded. Expanding it does not build
the project. Use the native Add Reference command with the dependency root selected,
or remove a selected direct reference with Delete; removal keeps the referenced
project on disk. Empty Projects groups are hidden.

Dependencies / Packages (Pakete) shows evaluated direct NuGet references. Resolved
nodes show the selected version; a differing requested version/range appears as
`requested → resolved`. Restore-pending and unresolved references remain visible.
Expand a package to inspect its selected transitive dependencies, labelled as
read-only transitive entries. Shared dependencies expand once per direct root;
repeated occurrences stay visible as leaves. Use the native Manage NuGet Packages
action on the dependency root to install, update or uninstall packages. Delete
does not edit package references or transitives. Resolution follows the selected
SDK's restored assets; invalid/failed restore clears resolved children until
recovery. An older SDK without the graph projection leaves package nodes unresolved.
Integrated playground/configuration lifecycle acceptance passed on both supported
hosts; milestone-nine iteration 12 records the scenarios and retained evidence.

Package analysis uses the selected SDK and restored NuGet assets. Restore after
changing package references. Missing or invalid dependencies clear stale results;
saved-file recovery triggers analysis again. Inputs and package selections are
revalidated before results are published or used for editor actions. Reference
choices and preview source labels show library identity/version and can be searched
by library name. Concrete imported alarms appear in the project preview table;
deferred values remain unavailable. Navigation into a referenced project opens
its actual source. Package navigation opens a generated, read-only definition
with portable origin information; it does not expose the original formatting or
write to the package cache. Repeated navigation reuses an identical open definition.
See milestone-nine iteration six for validation evidence.

[Milestone six](MILESTONE-6.md) adds saved EditorConfig settings, diagnostic severity,
project warning controls and explicit `AlarmlistAnalyzer` DLLs. The SDK owns rule
execution and policy; CPS supplies evaluated items/properties to the isolated SDK
bridge. The live Error List reflects the same effective severity as Build. Hidden
and suppressed rules have no live row. Configuration drafts remain private until
saved, and ALMX drafts still participate in analysis. Saved configuration or analyzer
dependency changes invalidate previews/diagnostics immediately and are rechecked
before publication or navigation. Analyzer changes recreate the SDK AppDomain;
compiler and plugin types remain outside the VS host domain. See the
[SDK configuration guide](../SDK/Alarmlist.SDK/README.md#diagnostic-configuration-and-analyzers)
for precedence, runtime selection and the trusted/cooperative analyzer contract.
The functional xUnit scenarios have the `Area=Analyzers` trait and the same host
environment prerequisites as the other UI integration tests.

Milestone nine adds analyzer packages whose ordinary NuGet build targets register
`AlarmlistAnalyzer` items. Dependencies / Analyzers (Analysetools in German) shows
assembly names and their declared `ID: Title` rules for both package and DLL items.
Rules remain visible when they report no violations or ALMX contains errors. Rule
icons show descriptor **default severity**; the Error List reflects the effective
per-file configuration. Configure severity through EditorConfig or project warning
options. Rule nodes do not provide a ruleset editor or delete action. Descriptor
loading stays in the selected SDK domain; unavailable analyzers have no stale rule
children. Manage direct analyzer packages through native NuGet commands.

Milestone five also supports reusable named templates such as
`$(loc:Instruction.Set, path=$(path:Service.Vakuum), value="60")`.
External tools supply `.almlookup` XML catalogues; the SDK includes the XSD.
Saved RESX entries translate each path segment and member label in the same
selected culture. General named lists also supply literal identifiers with optional
translated descriptions and metadata. Explicit Template/Argument mappings provide
contextual Ctrl+Space choices in quoted argument values; selecting replaces the
whole value with a `$(list:List#Entry)` reference. Direct list completion is also
available in scalar fields and procedure rows. See the
[electrical sample](../../example/README.md#try-supplied-lists-and-electrical-instructions).
Completion inserts named argument slots and path/member/metadata
references while preserving local drafts. Saved catalogue regeneration refreshes
preview; unsaved lookup XML remains private. Metadata provides selection guidance
without automatic defaults or range validation. See the [format specification](HMI-LOOKUPS.md)
and the [North/South sample walkthrough](../../example/README.md#try-reusable-instructions-and-hmi-lookups).

Open **View > Other Windows > Alarmlist Preview** for the dockable preview window.
Its **Alarm table**, **Selected alarm** and **Compiled XML** tabs replace the
designer's embedded preview sections. **Scope** defaults to **Follow active editor**,
following its selected alarm and linked-project context. Choose a project to keep
the preview independent of editors, or right-click a loaded Alarmlist project in
Solution Explorer and choose **Preview alarms**. No editor or Build is required.
Explicit project scope survives editor switches/closure and window hide; unloading
that project clears its results and retains the selected identity until reload or
another choice. The window's language selector works without an editor and uses
the same CPS preview setting as the native Alarmlist toolbar.

Both scopes include shared unsaved ALMX text. Follow-active mode clears during
that editor's unapplied field drafts; explicit project mode continues to show the
analyzed buffers and visibly explains that private drafts are excluded. Moving
focus into the window never applies those drafts. Compiled XML requires a
successful whole-project analysis and never writes build output. Source and copy
actions revalidate inputs and the selected scope. Visual Studio manages docking.
The alarm table includes every concrete alarm in the selected project context.
Cells wrap and rows grow to fit their contents. **Test method** combines effective
ordered group titles and entries with bullet steps and Hint/Warning/Note
labels; cleared entries and Clear directives are omitted. Multiline text retains
its line breaks. Click column headings to sort; the filter searches resolved fields, procedures, identities
and source paths. Select a row to inspect its details, or double-click/press Enter
to navigate to its source. Source-path tooltips identify linked files. Ctrl+C or
**Copy selected rows** copies the selection in display order as tab-separated
text with column headings; tabs, newlines and quotes inside values are quoted.
Use **Columns** to show/hide columns, enter widths, move columns left/right with
the keyboard, or reset the layout. Headers also support drag reordering and edge
resizing. Copy includes visible columns in their display order. The chooser stays
available even with all columns hidden. Preferences are shared across the project's
ALMX previews and saved in `<project>.almproj.user`, independently of configuration.
Unrelated user properties are preserved; save failures leave the session layout
usable and show an error. Loose files use session preferences.
In narrow preview windows, the count and row actions wrap below the filter so
the filter remains usable; resizing keeps its text and keyboard focus.
Copying revalidates saved resources/lookups and source snapshots first. Errors
mark unavailable row values and disable compiled XML. See the scope-specific draft
behavior above.

The bridge accepts analysis contract 5 and loads the resolved SDK's net472 compiler
in a dedicated AppDomain. It returns immutable host-owned data and reports missing,
incompatible, or failed SDKs explicitly. Bridge owners must analyze and dispose off
the UI thread, recreating the bridge after SDK/configuration changes. The project
service now owns that lifecycle and input subscriptions, debounces edits by 250 ms,
and clears stale results immediately. Generation, text, and buffer identity checks
guard publication. ALMX XML-only editor buffers take precedence over disk, and drafts
that have not reached the shared buffer remain outside analysis. A context registry
keeps linked projects separate; each view can choose its owning project context.
Unit tests extract the built SDK and VSIX to verify
unsaved analysis, isolation, compatibility, cleanup, and the packaged worker
without a compiler project reference or additional VSIX assemblies.
Contract 5 requires captured-context analysis and compiled preview support. It
also requires library snapshot analysis. CPS captures `OutputType=Library` and
uses that mode for the project's own editor: missing consumer values are unavailable
in previews, without treating those missing values as invalid library definitions.
Library previews never provide final application XML. Application projects retain
strict value validation. The bridge
copies selected culture, saved-resource identities/versions, raw catalogue entries,
translation origins and alarm-specific localization uses, as well as raw expressions,
evaluated values, abstract state, availability, and separate
parameter declaration/assignment origins. Older SDK contracts receive explicit
update guidance. CPS captures project parameters/configuration, and the designer
uses those results for property completion, scoped values and expression displays.

CPS also captures evaluated AlarmlistResource items and AlarmlistCulture. Resource
analysis reads saved RESX contents only, including while native or XML resource
editors contain drafts. Save/external changes refresh preview and diagnostics in
each linked project context; Undo/discard of unsaved resources retain the saved
translation. Current Resource Explorer saves when a cell edit is committed.
Missing/invalid saved resources report errors without substituting buffer contents.
Content is recaptured before publication and navigation to reject stale results.
Run `eng/test-visualstudio.ps1 -Filter 'Area=Localization'` with the documented
host path/profile options to exercise saved-resource behavior in the owned IDE.

The **Preview language** selector lives in the native **Alarmlist** toolbar,
separate from the designer. It follows the active ALMX editor and its selected
project context, including linked files and XML views. Visual Studio manages its
docking and customization; it is also available under **View > Toolbars > Alarmlist**.
It lists keyword, resource cultures and configured build cultures.
Changes persist the shared AlarmlistCulture through CPS; global values are fixed,
and read-only/overridden project settings report an error. Choosing a language
does not commit an ALMX draft or change AlarmlistCultures. Focused expression
fields retain their raw text and selection during analysis refresh.
Leaving the designer for a shell control keeps a draft private. Enter, Save,
layout changes, and moving to another designer field still commit it.
The toolbar uses the SDK's [native combo command pattern](https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/Combo_Box)
and editor command routing, without a global reference to an editor or project.

Type `$(loc:` or use Ctrl+Space in expression fields to search any part of a
localization key, ignoring case: `$(loc:Conta` finds `Electrical.ContactorTerminal`.
Keys beginning with the search text appear first. Insertion preserves the exact
key spelling; compiler key lookup remains case-sensitive. Suggestions show saved
translation text, parent-culture provenance and comments; used translations can
show evaluated values outside drafts. All
expression suggestions use `name (additional information)` labels, with values and
origin details available without changing the inserted token. Enter
or Tab inserts the full token into the draft; a subsequent Enter commits it.
Escape first closes completion, then cancels the draft. Saved inputs are checked
again before displaying and accepting localization suggestions. RESX diagnostics
open the XML source editor for line navigation without saving resource buffers.

For translation handoff, the SDK's explicit `ExportAlarmlistXliff` target exports
saved RESX values while unsaved resource buffers stay private. The project
property page exposes **XLIFF source culture**, defaulting to English (`en`).
See the [SDK export commands](../SDK/Alarmlist.SDK/README.md#xliff-translation-export).
Use a terminal for export so an IDE Build/Save All cannot save documents first.
The [SDK import workflow](../SDK/Alarmlist.SDK/README.md#xliff-translation-import)
provides dry-run review and explicit apply. Save or discard pending changes and
close affected RESX editors before apply, including XML views. The importer also
protects unfinished native cells that have not marked their document dirty.
Successful imports refresh saved-resource analysis; reopen the native editor to
inspect the result. Vendor interoperability remains pending.

The repository-wide [future feature notes](../../FUTURE-FEATURES.md) capture
well-known and custom properties, localization, broader IntelliSense, and syntax
generation, including open questions about UI integration and collection values.

## Designer performance and rendering checks

Run the opt-in benchmark from the repository root on Windows:

```powershell
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1
```

The launcher builds Release and runs x64 net472 BenchmarkDotNet measurements through
xUnit in `Alarmlist.VisualStudio.PerformanceTests`, using the packaged SDK bridge
and actual designer BAML. Its default Short job records five measured iterations
after two warmups for 1,000 alarms in one file, 1,000 across ten files, and 10,000
across 100 files. `-Job Dry` verifies the harness; `-Job Full` uses default
BenchmarkDotNet sampling. `-Filter` accepts xUnit trait filters. Raw CSV, JSON,
Markdown, TRX, revision/working-tree state and runtime details are retained under
`artifacts/log/Release/AnalysisPerformance/`. Normal tests skip measurements.
Timings are observations, not CI pass/fail thresholds; allocation measurements do
not establish retained memory or VS memory guarantees. See the
[performance project README](Alarmlist.VisualStudio.PerformanceTests/README.md)
for scenario selection, measurement limits and opt-in live-IDE xUnit wrappers.

The measurements separate SDK analysis/copying from UI publication/layout,
unchanged refresh, command queries, and a local draft edit. They exclude live
VS/RDT capture, CPS debounce, and real typing/screen-reader interaction. See
[iteration nine](MILESTONE-3.md#iteration-nine-designer-performance-and-preview-accessibility)
for the before/after data and remaining host checks.

The normal unit suite also verifies read-only automation providers, Copy command
availability without changing the clipboard, theme brushes, and 100/150/200%
offscreen rasterization. To retain its preview images, temporarily set
`ALARMLIST_UI_VALIDATION_OUTPUT_DIRECTORY` to an output directory and run the
`PreviewSupportsReadOnlyAutomationCopyAndThemedScaledRendering` test. Restore the
previous variable value afterward. These simulate palettes/DPI outside VS;
actual theme switching and per-monitor DPI still require IDE validation.

## Editing ALMX files

Open an `.almx` file normally to use the combined editor. Use the controls at the
bottom to switch modes, arrange panes side by side, or swap them. Drag the divider
to resize. Layout preferences are stored in Visual Studio user settings, separately
from document contents. **Open With > XML (Text) Editor** remains available.

Filter the alarm list by name or identifier, select an alarm, and edit its local
`FullyQualifiedName`, `Name`, `Code`, `Category`, `Description`, or `ReferenceName`.
The list becomes a selector when the designer pane is narrow.
Both show resolved names in the current preview culture, including inherited
names. When current values are unavailable, they fall back to raw expressions or
identifiers. Filtering matches displayed names, local raw expressions and
identifiers. Analysis refreshes preserve an actively edited alarm and its drafts.
The Add alarm and Delete buttons expose A/D access keys in their tooltips and
accessible help: press A or D while the designer has focus outside a text field.
Inside text fields, letters remain ordinary input and Ctrl+A selects all text.

**File settings** contains the project context, file parameters and live project
diagnostics. **Alarm settings** contains filtering/navigation, alarm parameters,
properties and procedures for the selected alarm. Each area scrolls independently.
In either parameter list, right-click a row or focus a cell and press Shift+F10
for assignment, explicit-empty, fallback and declaration actions. Cell menus also
retain normal text-editing commands. Add and Remove remain above each list.
Use either header to collapse its area; drag the divider or focus it and use
Up/Down to resize. These actions preserve private drafts. Collapse states and the
divider position are restored from Visual Studio user settings. A single project
context is shown as a label; linked files with multiple contexts get a labelled
selector in File settings.

The property form aligns labels on the left and editors on the right, with a
narrow inheritance-icon gutter between them. Edit a field directly to create a
local value; XML-presence checkboxes are no longer needed. Inherited Name, Code,
Category and Description show a chain icon and origin tooltip. Focus shows their
raw expression; typing starts a local draft. **Use inherited value** in the field's
context menu removes its local XML element. Right-click or Shift+F10 opens this
menu, including clipboard and source-navigation actions. Viewing, focusing, or
saving an untouched field does not create an override. Analysis refreshes preserve
active local drafts. The **Abstract template** checkbox still controls exclusion
from compiled output and follows the Visual Studio theme.

Choose a project context for a file linked into multiple projects. **ReferenceName**
combines literal editing with searchable choices: type any part of an identifier
or alarm name, use arrows to choose and Enter to apply. F4/Alt+Down opens the list;
Escape closes it without changing the draft. Enter then commits a literal value,
including unresolved references. Clearing the text and pressing Enter removes the
reference's value without automatically choosing a candidate. Suggestions show
their defining file, omit ambiguous/self/cyclic targets and use virtualized rows.
Selections revalidate the current project inputs before committing. The adjacent
**Go to reference** action opens the immediate target, while **Go to source**
opens the actual defining field through multi-hop inheritance. Copy, picker, and
navigation commands revalidate captured inputs before acting. Pending, incomplete,
unavailable, and loose-file states retain local editing without stale inheritance.

**Tools > Options > Alarmlist > Navigation > Open ALMX sources in** chooses
**Designer** (default) or **XML** for Go to, preview and diagnostic links. This
is a personal Visual Studio preference. Designer navigation selects the exact
source alarm and field, parameter or procedure entry, even with duplicate names;
it reveals collapsed groups, filtered alarms and hidden cleared entries as needed.
Unsupported markup and resource/lookup locations use XML. Find still uses XML.
Navigation refuses to replace or commit an unapplied destination draft. Build
diagnostic links require a matching diagnostic in current analysis; stale results
ask you to wait for analysis or rebuild instead of selecting an outdated position.

Expand **Live project diagnostics** to see compiler IDs, messages, source links,
and related locations for the chosen project context. These describe shared
buffer text; commit or cancel control drafts before navigating. The Visual Studio
Error List publishes the same current errors under **Alarmlist live analysis**
(the IntelliSense/non-Build source). Use **Build + IntelliSense** to see both.
**Build** alone hides live entries, so a duplicate such as ALM0001 can appear to
arrive only after a build. With **IntelliSense** or **Build + IntelliSense**, it
updates when an XML edit or committed designer edit reaches the shared buffer;
no build or save is required. Uncommitted field drafts remain private. The
diagnostics heading's tooltip and accessible help explain this source filter.
Pending analysis clears obsolete live errors, and project unload removes its rows.
Input/SDK failures appear separately as analysis-unavailable warnings without ALM
codes. Neither live analysis nor its navigation saves files or writes compiled output;
existing Build diagnostics remain owned by MSBuild.

Use the preview window's **Selected alarm** tab to inspect the selected alarm's identity,
effective scalar values, and surviving procedure-group entries in execution
order. Chain adorners mark inherited values; **Go to source** follows each value's
SDK origin after revalidating the inputs. Missing values and empty sections are
labelled. A valid alarm remains inspectable when another alarm has errors.

Use the **Compiled XML** tab to select or copy the whole project's
resolved XML. The SDK generates it in memory using the normal build serializer,
including unsaved XML buffers. No source or output file is written. All project
errors block this XML preview; pending/stale analysis clears both previews, as do
private drafts in Follow active editor mode. Each panel identifies the
project/configuration. Older SDKs that
support analysis but lack XML preview show update guidance while keeping the
resolved-alarm preview available.

Enter or leaving a field commits its draft; Shift+Enter inserts a newline, and
Escape cancels a draft. Save and Save All also commit active drafts. Each accepted
designer action is one shared document undo transaction. Ctrl+Z with an uncommitted
draft cancels that draft; subsequent undo uses the document history. A conflicting
edit in another view is reported instead of overwriting it. Escape reloads the
current value after a conflict.

**Add alarm** uses captured saved EditorConfig defaults for the destination file.
Missing settings create Instructions and Reset; an explicit empty group list
creates no groups. Invalid settings block creation, and configuration changes
never rewrite existing alarms. See the [creation settings](../SDK/Alarmlist.SDK/README.md#procedure-groups-and-format-transition).

Expand **Test procedure** below the alarm fields to edit ordered named groups.
Enter a stable group ID and select **Add group**; IDs compare ignoring case.
Edit the title with the same expression editor and completion as alarm fields.
An absent title inherits; an explicit empty title displays the group ID. The
title context menu can remove a local title or navigate to its source.
Move controls reorder newly introduced local groups; inherited order stays fixed.
**Remove local group** removes its local declaration and reveals any inherited
group. Unsupported content requires XML editing. These actions support Undo.
Choose Step, Hint, Warning, Note, or Clear and select
**Add entry**. Each entry has a kind selector, multiline text, move up/down, and
Remove controls. Add focuses the new entry; moving keeps its text selection, and
removing focuses the next entry (or the previous entry/group Add button).
Clear has no text: it discards all entries preceding it in that
section, including inherited entries. Their order therefore matters.

Procedure drafts use the same Enter/focus-loss, Escape, Save, and Undo behavior
as alarm fields. Changes in another view to the same section cause a conflict;
independent group edits can be combined. Duplicate procedure
containers/sections and entries containing nested markup or comments must be
edited in XML. Unknown entries and surrounding source remain intact.

Inherited procedure rows stay read-only and have a chain icon and **Go to source**;
origin/exclusion details are available in tooltips. Cleared rows appear inline with
muted styling and an explicit Cleared label. Local cleared rows remain editable.
Each group's eye button hides/shows cleared rows without committing a draft or
changing XML; Clear boundaries always remain visible. These choices last for the
editor view, including alarm switches. Moving a focused entry across Clear keeps
its editor visible until focus leaves. Analysis never replaces private drafts;
text-only drafts retain structural exclusion information but suppress resolved
values until committed or cancelled.

Each group's **Inheritance** menu offers whole-section actions. Opening it keeps
pending drafts private; tooltips explain each action and disabled copy state.
**Use local entries only** inserts a leading Clear while preserving existing
entries and directives. **Make local copy** replaces that section with a leading
Clear and its current effective sequence, each entry once. It captures a snapshot:
future reference entry changes stop flowing into that group; other groups
continue independently. Commit or cancel drafts before copying. Copies require
current resolved analysis and refuse to discard unsupported source content. Each
section action is one document undo transaction; Undo restores its original source.

Malformed XML disables designer mutations until the source is corrected. Fields
containing nested markup, comments, or duplicate scalar elements must be edited
in XML. Changes preserve untouched source text, including attributes, comments,
unknown elements, procedure sections, and `Clear` directives. Renaming an alarm
does not rewrite references. The complete selected-alarm resolved/XML preview
remains a later milestone-three iteration.

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

Add New Item uses distinct bell/list and language icons for Alarm List and
Translations (RESX). The template artwork is maintained as vector drawings in
`eng/assets/VisualStudio/`; `powershell.exe -NoProfile -Sta -File
eng/render-template-icons.ps1` regenerates the checked-in transparent ICO frames
(16–256 pixels). These icons belong to the templates, not generated projects.
Editor context menus use the VS image service for themed command symbols.

Included project folders named Properties, Resources, Translations or Lookups
use native VS symbols, regardless of name casing. Other names retain their
normal folder icon. Linked, excluded, virtual and missing folders retain CPS's
state indicators; this is a display convention and changes no item membership.

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

The separate [UI integration suite](Alarmlist.VisualStudio.UIIntegrationTests/README.md)
owns all automated functional IDE checks across
VS2022 and VS2026. It covers template/file lifecycle, shared editors, Save As,
live diagnostics, build/Clean/Rebuild and actual designer keyboard commit/undo.
The external net8.0-windows/x64 runner uses DTE/shell APIs and FlaUI. Live cases
are opt-in and require an unlocked desktop; ordinary solution tests skip them.
Package-consumer tests and performance measurements remain separate.

Opted-in functional and live-performance xUnit runs prepare deployment themselves.
They compare the installed extension, templates and dependencies with the built
VSIX test asset and use the selected host's desktop MSBuild/VSSDK to deploy only
when missing or outdated. An already-open target profile is refused without
closing or changing its IDE. Deployment happens before measurements, under the
shared live-test mutex; ordinary solution tests never deploy or launch an IDE.

For the linked-project semantic UI checks, run
`powershell.exe -NoProfile -File eng/prepare-visualstudio-acceptance.ps1` after
building the VSIX. It creates a fresh two-project solution with a shared alarm,
distinct inherited values, procedure Clear cases, isolated SDK restore, and
baseline source/output hashes. Follow [the interactive acceptance walkthrough](ACCEPTANCE.md)
in the matching experimental instance. Record results separately from the
automated smoke; creating the fixture does not validate the UI.

For manual testing, run `scripts/start-vs2022.bat` or `scripts/start-vs2026.bat`.
Each launcher finds the matching installed Visual Studio, builds and deploys the
extension, prepares the matching SDK for [the example playground](../../example/README.md),
then opens `example/Example.sln` in its `AlarmlistExp` instance. The example contains
local alarms, multi-file inheritance, linked North/South contexts, and procedure
Clear cases. Its SDK feed/version/cache are configured automatically; exploratory
source edits are preserved. Close that experimental instance before rerunning the
launcher. Add `--deploy-only` to build, deploy, and prepare without opening the IDE.
For other test solutions, configure the SDK package feed as described above.

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

For manual deployment, use the selected **Visual Studio installation's desktop MSBuild**, with Visual Studio extension
development tools installed. Find its path and instance ID with `vswhere`:

```powershell
& "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -version '[17.9,19.0)' -products '*' -format json

& '<VS installation>/MSBuild/Current/Bin/MSBuild.exe' src/VisualStudio/Alarmlist.VisualStudio/Alarmlist.VisualStudio.csproj /restore /p:DeployExtension=true /p:DeployTargetInstanceId=<instance-id> /p:VSSDKTargetPlatformRegRootSuffix=AlarmlistExp

powershell.exe -NoProfile -STA -File eng/test-visualstudio.ps1 -VisualStudioPath '<VS installation>'
```

Use matching custom deployment/smoke suffixes, for example
`/p:VSSDKTargetPlatformRegRootSuffix=AlarmlistM3` and `-RootSuffix AlarmlistM3`,
when an existing experimental IDE is holding the extension DLL. The smoke waits
for the duplicate-window command to become available during editor startup.

`eng/test-visualstudio.ps1` is now a launcher for the functional xUnit project.
It selects the host from the installation, sets/restores the opt-in environment,
and writes TRX results. Add `-Filter 'Area=SaveAs'` to select a group or use direct
`dotnet test` invocation as described in the
[UI suite README](Alarmlist.VisualStudio.UIIntegrationTests/README.md). The tests
own a fresh IDE and isolated project/feed/cache per case and close their own
processes. Artifacts are retained under
`artifacts/log/<Configuration>/VisualStudioUI/<scenario>/<VSVersion>/<run-id>/`.
Initialize the experimental profile manually if first-run/sign-in input is needed.
Functional `-KeepOpen` is no longer supported; use the interactive acceptance
fixture for inspection. The duplicate-view checks use a stable command GUID/ID
for VS2022's New Window and VS2026's New Tab.

Live latency/memory validation remains in the performance xUnit project, with
ordinary report/ownership validation tests alongside it. Its dedicated driver
sets up a project and measures both modes without rerunning functional scenarios.
See the [live performance workflow](Alarmlist.VisualStudio.PerformanceTests/README.md#live-visual-studio-wrappers).
Direct measurement remains available; the previous
`test-visualstudio.ps1 -MeasureAnalysis` command delegates here for compatibility:

```powershell
powershell.exe -NoProfile -STA -File eng/measure-visualstudio-live.ps1 -VisualStudioPath '<VS installation>' -RootSuffix AlarmlistM3 -MeasurementIterations 100
```

The default is 20 measured samples per mode after two warmups, with 1,000 extra
alarms in one generated input. `-MeasurementIterations` accepts 1-100 and
`-MeasurementAlarmCount` accepts 1-10,000. The two modes replace an unsaved XML-only
buffer repeatedly and reopen/edit/close the ALMX designer for each sample. Timings
start at buffer replacement and end when a unique new diagnostic reaches Error
List; they include capture, debounce, analysis, COM calls, and 50 ms polling.
Editor opening is outside the timer. This does not measure keystroke-to-paint time
or successful compiled-preview generation, since the probe intentionally contains
a missing reference.

`analysis-latency.csv`, `designer-close-memory.csv`, and environment/revision
metadata remain under `artifacts/log/<Configuration>/VisualStudioSmoke/` for
continuity with earlier measurements. Memory samples record the entire IDE's
private bytes, working set, handles, and zero remaining open documents after each
designer close and restored disk analysis. When available, .NET CLR heap-size and
generation-two collection counters are matched to the test IDE's process ID;
blank values mean unavailable. CSV timings use invariant decimal formatting.
The automation driver releases its temporary window wrapper and collects its own
COM wrappers before memory sampling. No IDE collection is forced, and no
machine-dependent thresholds are asserted. Whole-process trends include other
extensions, caches, and automation overhead; they cannot establish leak freedom.
The script verifies that unsaved measurements leave source and compiled output
unchanged, then removes only its generated measurement input from the test project.

Run deployment and the smoke test for each installed host when changing extension
compatibility. The manifest's open upper bound follows Microsoft's
[API-version compatibility model](https://learn.microsoft.com/en-us/visualstudio/extensibility/migration/extension-compatibility):
VS2026 supports the 17.x APIs used by this extension. The compiler and MSBuild
task frameworks remain unchanged.

For interactive verification, launch `devenv.exe /RootSuffix AlarmlistExp` and
also check the New Project/Add New Item dialogs, Add Existing Item, XML editing,
and the output directory, filename, and automatic inclusion project properties.
Compiler reference diagnostics carry source-file and declaration ranges through
the MSBuild logger. The smoke also validates live Error List navigation and
unsaved XML-only analysis. See the milestone-three record for current semantic
UI coverage and remaining interactive validation.

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
