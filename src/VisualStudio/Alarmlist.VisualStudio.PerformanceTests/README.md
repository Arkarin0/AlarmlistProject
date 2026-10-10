# Visual Studio performance checks

This net472 project uses xUnit for discovery, traits, assertions and TRX results.
Its isolated scenarios run BenchmarkDotNet 0.15.8 in separate Release/x64 processes.
The compiler is still loaded from the built SDK package through the extension's
AppDomain bridge. Benchmark dependencies are private test dependencies, not VSIX
contents. Ordinary solution tests skip all measurements and live IDE launches.

## Isolated benchmarks

From the repository root on Windows:

```powershell
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1 -Job Dry
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1 -Job Full -Filter 'Category=Performance&Host=Isolated&Area=Bridge'
```

The default Short job uses two warmups and five measured iterations. Dry checks
execution/reporting only; it is not performance evidence. Full uses BenchmarkDotNet's
default sampling policy. All jobs use one invocation per iteration to control WPF
state changes. CommandQueries100 measures a batch of 100 availability queries.

The Bridge and Designer xUnit theories each select one of three scenarios:
1,000 alarms in one file, 1,000 across ten files, or 10,000 across 100 files.
Designer covers fresh result publication/layout, unchanged refresh/layout,
context-change publication/layout, command queries, completion filtering and draft
editing/layout. WPF runs on STA with actual designer
BAML. Analysis, input preparation and draft reset are outside the UI measurements.
Each draft starts from a resolved preview; the measured edit must clear it without
committing source. Correctness checks run outside the measured interval.

`Area=Properties` selects an additional 1,000-alarm/ten-file workload with 32
project parameters, file shadowing, abstract templates, literal escapes and
alarm-field dependencies. Bridge measurements include a changed captured context.
CompletionQueries100 invokes the production prefix filter 100 times; it measures
filtering rather than popup rendering or speech. ContextChangeAndLayout publishes
precomputed results from alternating contexts; compiler work remains outside that
UI interval. Run this workload with the same Short job for reproducible evidence:

```powershell
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1 -Job Short -Filter 'Category=Performance&Host=Isolated&Area=Properties'
```

The launcher sets and restores `ALARMLIST_RUN_PERFORMANCE_TESTS`,
`ALARMLIST_PERFORMANCE_JOB`, and `ALARMLIST_PERFORMANCE_OUTPUT_DIRECTORY`.
For direct `dotnet test` use, set those variables explicitly and select Release
and `RunConfiguration.TargetPlatform=x64`. Traits support `Area=Bridge`,
`Area=Designer`, and `Host=Isolated`. Traits alone do not enable a skipped test.

`Area=Localization` measures 1,000 alarms across ten ALMX files with 10,002
saved translations across twelve RESX snapshots. It includes named instruction
arguments, inherited procedures, alarm-field expressions inside translations,
parent-culture lookup, and switching between de-DE and en-US. The unused part
of the catalogue also participates in completion and captured analysis.
Correctness checks verify resolved values and compiler-reported parent origins
outside the timing interval. The designer uses the captured resources from the
same analysis and filters localization suggestions in its completion benchmark.

```powershell
powershell.exe -NoProfile -File eng/measure-visualstudio-analysis.ps1 -Job Short -Filter 'Category=Performance&Host=Isolated&Area=Localization'
```

Results live under `artifacts/log/Release/AnalysisPerformance/<run-id>/`:
TRX, revision/working-tree and runtime metadata, per-scenario BenchmarkDotNet logs,
raw measurement CSVs, full JSON and Markdown summaries. xUnit checks the expected
number of reports, successful execution, finite measurements and exported JSON.
There are no timing-budget assertions. Reports must be compared on a controlled
machine; do not compare Debug or Dry observations with full Release measurements.

These are warm SDK operations, not cold SDK startup, live CPS/RDT capture, physical
typing or screen paint. Allocated bytes describe benchmark operations, not retained
objects or the memory of a running Visual Studio. Existing collectibility regression
tests remain in UnitTests. Old Stopwatch results remain historical evidence in
MILESTONE-3.md; the new measurement method establishes a new baseline.

## Live Visual Studio wrappers

Functional IDE tests belong to the separate
[UI integration suite](../Alarmlist.VisualStudio.UIIntegrationTests/README.md).
Latency and designer-close memory belong here. One performance case per host
invokes `eng/measure-visualstudio-live.ps1` for fixture setup, both measurement
modes and cleanup, then validates its completion report. It does not rerun the
functional suite. Shared C# automation primitives are also compiled by that driver.
Additional live fixture alarms now use a file parameter and a resolved alarm-field
dependency; workload metadata identifies this change from the literal-only baseline.
These wrappers target net472; packaged integration tests keep both net472/net8.0,
and the external functional runner uses net8.0-windows. Ordinary tests in this
project validate measurement reports and process ownership without launching an IDE.
No wall-clock performance limit or memory-growth threshold is asserted. Validation
requires complete host/scenario evidence, two warmups per mode, finite samples,
source/output preservation and owned-IDE cleanup.

The xUnit setup compares the installed extension, templates and dependencies
with its built VSIX asset. It automatically deploys missing or outdated test
extensions through the selected host's desktop MSBuild/VSSDK, before measurements.
Matching payloads are reused; an open target profile is refused without changing
or stopping its IDE. This shares setup and serialization with functional UI tests.
Build assets through `dotnet test`; complete Visual Studio first-run screens
manually if needed and close that profile. Set the path for each host you
want to run; unconfigured hosts are skipped:

```powershell
$env:ALARMLIST_RUN_LIVE_VS_TESTS = '1'
$env:ALARMLIST_VS2022_PATH = '<VS2022 installation>'
$env:ALARMLIST_VS2026_PATH = '<VS2026 installation>'
$env:ALARMLIST_VS_ROOT_SUFFIX = 'AlarmlistExp'
$env:ALARMLIST_VS_MEASUREMENT_ITERATIONS = '20'
$env:ALARMLIST_VS_MEASUREMENT_ALARMS = '1000'

dotnet test src/VisualStudio/Alarmlist.VisualStudio.PerformanceTests/Alarmlist.VisualStudio.PerformanceTests.csproj --filter 'Host=VisualStudio&VSVersion=2022'
```

Restore/unset the environment variables after the opt-in run. Root suffix defaults
to AlarmlistExp, measured iterations to 20 (range 1-100), and additional alarms to
1,000 (range 1-10,000). Use `-c Release` for measurements; setup prepares that configuration.
Run the projects sequentially; a cross-process mutex rejects simultaneous live
runs. Performance tests also disable within-assembly parallelism. Do not run other
benchmarks or builds alongside measurements. A watchdog defaults to 30 minutes;
`ALARMLIST_VS_TIMEOUT_SECONDS` accepts 30-7,200 seconds. This detects stuck runs,
not performance regressions. On timeout the wrapper requests cancellation, then
stops only the IDE identified by the driver's ownership receipt and matching PID
and start time, and stops its driver. Cleanup failures are reported in test output.
The driver always uses x64 Windows PowerShell with its own module search paths,
even when the test runner is x86 or launched from PowerShell 7.

Wrapper logs, the IDE ownership receipt and `result.xml` are under
`artifacts/log/<Configuration>/LiveVisualStudioTests/<run-id>/`. The report links
the retained measurement fixture, ActivityLog, latency/memory CSVs and measurement
metadata under `VisualStudioSmoke/`. Failures retain logs and cannot pass with an
empty, partial, wrong-host or unsuccessful report. No collection is forced in the
measured IDE. Diagnostic polling includes COM overhead and does not measure typing
to paint; memory totals do not establish leak freedom.

`Host=VisualStudio&Area=Analyzers` selects two additional xUnit cases, one per IDE
host, with the same opt-in variables and ownership rules. The fixture explicitly
references the saved sample analyzer DLL and captures EditorConfig. Its 1,000
additional alarms satisfy their configured prefix; the live input produces a
unique SAMPLE001 diagnostic per edit. It measures unsaved XML buffers, repeated
designer open/edit/close, and saved EditorConfig prefix changes. Each mode retains
two warmups plus the requested sample count. Original configuration is restored,
and source, output, configuration and analyzer image hashes are checked. Reports
must identify this workload and include all three modes to pass validation.

```powershell
dotnet test src/VisualStudio/Alarmlist.VisualStudio.PerformanceTests/Alarmlist.VisualStudio.PerformanceTests.csproj --configuration Release --filter 'Host=VisualStudio&Area=Analyzers'
```

Set `ALARMLIST_VS_MEASUREMENT_ITERATIONS=100` for the extended lifecycle run. These
measurements observe the whole IDE without forcing garbage collection. They do
not establish a timing budget or prove absence of leaks.

## Heap and lifetime investigation

For retained-object or stalled-driver investigation, use the separate
[heap diagnostic tool](../../../eng/tools/VisualStudioHeapProbe/README.md).
`eng/capture-visualstudio-heap.ps1 -ProcessId <test-process-pid>` retains managed
heap totals, selected root paths and thread stacks with revision/runtime metadata.
It requires Windows/x64 and .NET 8 and does not force collection in the inspected
process. Run it separately from benchmarks; normal tests do not invoke it.
