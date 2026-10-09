# Visual Studio UI integration tests

This project owns the automated functional IDE suite. xUnit discovers and reports
individual cases; C# drives Visual Studio's supported DTE/shell APIs for project
and editor operations, and FlaUI 5/UIA3 supplies actual mouse and keyboard input
for designer interaction. The runner is an external `net8.0-windows`/x64 process.
No test dependencies or test service are installed in the VSIX, whose host remains
net472. Package-consumer tests remain in `Alarmlist.VisualStudio.IntegrationTests`;
measurements remain in `Alarmlist.VisualStudio.PerformanceTests`.

## Coverage

Each scenario has separate VS2022 and VS2026 xUnit cases (28 live cases total):

| Scenario | Observable checks | Area trait |
| --- | --- | --- |
| Templates and file lifecycle | Installed project/item templates, build, folder/item creation, rename, solution reload, delete and updated output | `ProjectItems` |
| Shared editor | Custom factory/native XML view, New Window/New Tab, shared document data, edit/undo/redo/Save All, XML-first opening and XML survival after designer closure | `SharedEditor` |
| Designer Save As | Local and linked destinations and back, two views sharing RDT/buffer identity, subsequent saves and preservation of previous files | `SaveAs` |
| XML Save As | The same Save As contract starting with the XML editor | `SaveAs` |
| Live diagnostics | Publication before Build, exact navigation position, unload/reload, XML-only unsaved correction, disk/output preservation, discard and deletion | `LiveDiagnostics` |
| Build failure/Clean/Rebuild | Error List, failure preserves output, updated live errors preserve Build errors, Clean removes output, Rebuild restores it | `Build` |
| Designer keyboard commit/undo | Real typing creates a draft, Enter commits exact shared XML without saving, focus survives, Ctrl+Z restores XML and field | `Keyboard` |
| Properties and completion | Empty/unassigned/local values, parameter Undo/Redo, focused raw and unfocused evaluated text, completion Enter/Escape, Abstract, raw override and saved build parity | `Properties` |
| Linked property contexts | North/South project values, configuration reevaluation, shared unsaved file assignment, property diagnostics before Build, discard and unload/reload | `Properties` |
| Native resource editor | Open RESX in an ALMX project, retain saved disk contents during cell drafts, Undo/Escape drafts, commit a cell, Save, and reopen | `Resources` |
| Saved localization and linked contexts | Native drafts stay private, save/external changes refresh two project scopes, independent language choices persist after reopening | `Localization` |
| Language and saved completion | Keyword default, culture choice during ALMX drafts, saved-only completion while RESX XML is dirty, token insertion/Undo, save refresh and resource diagnostic navigation | `Localization` |
| Saved XLIFF export | Explicit external MSBuild export uses saved RESX while its XML buffer stays dirty; source and compiled output bytes stay unchanged | `Xliff` |
| Lookup and template completion | Saved metadata choices while lookup XML is dirty, named argument slots, expression insertion/Undo, keyword and translated preview, saved catalogue regeneration, unchanged source/output bytes | `Lookups` |

Project and document lifecycle checks retain the former smoke script's shell/API
operations. They do not simulate clicks through New Project or Save As dialogs.
The keyboard case does not replace physical input with buffer/property assignment.
Dialogs, visual appearance and remaining host interactions still have
[interactive acceptance checks](../ACCEPTANCE.md).

The resource case verifies Visual Studio's native authoring/save boundary. The
localization cases verify preview and completion refresh. They use the Resource
Explorer table on the installed VS2022/VS2026 hosts; the older VS2022 17.9 resource
editor surface remains a separate support check.
Resource Explorer saves a committed cell automatically on the verified VS2022
17.14 and VS2026 hosts; the case checks that unfinished cell drafts remain off disk.
Alarmlist
still consumes only saved files and does not trigger saves itself.

## Run

Use Windows, an unlocked interactive desktop, the .NET 8 desktop runtime and a
supported Visual Studio installation. First build and deploy the matching
configuration to the selected experimental profile using the
[deployment workflow](../README.md#experimental-ide-smoke-test).
Complete first-run screens manually and close that profile before testing.
The tests build package assets but do not deploy or compare the deployed DLL to
the checkout; matching the configuration and deployment is the caller's responsibility.

The existing entry point now launches xUnit and writes TRX results:

```powershell
powershell.exe -NoProfile -File eng/test-visualstudio.ps1 -VisualStudioPath '<VS installation>'
powershell.exe -NoProfile -File eng/test-visualstudio.ps1 -VisualStudioPath '<VS installation>' -RootSuffix AlarmlistExp -Filter 'Area=SaveAs'
```

It fails if the selection executes no cases or skips cases. It chooses VS2022/2026
from the installation, sets/restores opt-in variables and
runs only that host. Use `-Configuration Release` for a Release deployment.
Functional runs always close their IDEs; `-KeepOpen` is no longer a functional-test
option. Use the acceptance fixture or `scripts/start-vs2022.bat`/`start-vs2026.bat`
for interactive inspection. The old `-MeasureAnalysis` command delegates to the
separate live measurement driver, outside this functional suite.

Ordinary solution runs discover and skip the live cases:

```powershell
dotnet test src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj
```

For direct xUnit invocation, configure the hosts explicitly and restore the values
afterward (the launcher handles this automatically):

```powershell
$names = @('ALARMLIST_RUN_UI_VS_TESTS', 'ALARMLIST_VS2022_PATH', 'ALARMLIST_VS2026_PATH', 'ALARMLIST_VS_ROOT_SUFFIX')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
  $env:ALARMLIST_RUN_UI_VS_TESTS = '1'
  $env:ALARMLIST_VS2022_PATH = '<VS2022 installation>'
  $env:ALARMLIST_VS2026_PATH = '<VS2026 installation>'
  $env:ALARMLIST_VS_ROOT_SUFFIX = 'AlarmlistExp'
  dotnet test src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj --logger trx --results-directory artifacts/log/Debug/VisualStudioUI/TestResults
} finally {
  foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
```

The `Area=Preview` cases verify the dockable Alarmlist Preview window, active
document/culture changes, draft preservation, source navigation and close/reopen.

Filter by `VSVersion=2022`, `VSVersion=2026`, or `Area=LiveDiagnostics` (combine
with `&`). Unconfigured hosts skip; an explicitly configured missing or wrong
installation fails. Traits alone do not enable skipped tests. Keep the desktop
available during keyboard cases: every input checks the owned foreground process
and field focus, and loss of focus fails the test.

## Isolation and results

Each case starts a fresh experimental IDE and solution. Project cases use a local
feed containing only the exact SDK pinned by the built VSIX and a fresh package
cache. Neutral Directory.Build files prevent inheriting repository build settings.
Tests run serially on STA and share `Local\Alarmlist.LiveVisualStudioTests` with
performance runs. Concurrent live suites are rejected. The fixture binds to its
own PID, closes without saving and terminates only its own process if exit stalls.
`ALARMLIST_VS_TIMEOUT_SECONDS` defaults to 1,800 (range 30-7,200); the watchdog stops
the owned IDE on timeout to break blocked automation and records the failure.
An unresponsive native UIA call can still require runner intervention; use an
interactive test session rather than a Windows service.

Artifacts are retained under
`artifacts/log/<Configuration>/VisualStudioUI/<scenario>/<VSVersion>/<run-id>/`:
fixture/feed/cache, host version/PID/start time, ActivityLog, steps and cleanup
records, and success/failure text. Failures also attempt a screenshot, UIA tree
and build-output capture. `passed.txt` is written only after IDE cleanup; xUnit/TRX
is authoritative. The launcher puts TRX under `VisualStudioUI/TestResults/<run-id>/`.

## Architecture decision

The keyboard proof passed on both VS2022 and VS2026 with no production changes.
The remaining automated functional smoke scenarios now run as individual xUnit
cases rather than one PowerShell smoke wrapper. Shared automation primitives
remain in `src/shared/VisualStudioTesting/VisualStudioAutomation.cs`; the
performance driver compiles that same source. Performance report/ownership
validation tests live alongside the performance wrappers.

This adapts the dedicated IDE-test project/harness boundary used by
[Roslyn](https://github.com/dotnet/roslyn/blob/main/src/VisualStudio/IntegrationTest/New.IntegrationTests/Microsoft.VisualStudio.LanguageServices.New.IntegrationTests.csproj)
to this repository's size. An in-process test extension or Appium server is not
needed for these checks. Build policy and xUnit versions still come from the
pinned Arcade SDK. [FlaUI 5](https://github.com/FlaUI/FlaUI/releases/tag/v5.0.0)
provides a supported .NET 8 Windows target independent of the extension's runtime.

## Migration validation (2026-09-26)

- 14 functional cases passed across VS2022 17.14 and VS2026 18.10; all owned IDEs
  exited normally. TRX is retained under `VisualStudioUI/MigrationValidation/`.
- Default UI discovery skipped all 14 live cases without opt-in.
- Package-consumer integration tests passed: 10 on net472 and 10 on net8.0.
- Performance report/ownership tests passed: 26; all four opt-in measurements skipped.
- Both live performance wrappers passed with two warmups and one measured sample
  per mode, using 100 additional alarms. This checks driver extraction and report
  integrity, not performance characteristics. TRX is under
  `LiveVisualStudioTests/MigrationValidation/`.
