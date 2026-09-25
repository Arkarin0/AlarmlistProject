# Visual Studio heap diagnostics

This optional Windows/x64 .NET 8 tool captures managed heap and thread information
from an explicitly selected experimental Visual Studio, test host or Windows
PowerShell smoke-driver process. It uses Microsoft.Diagnostics.Runtime (ClrMD)
and is separate from the performance test project and VSIX.

From the repository root, use the PID of the test process you want to inspect:

```powershell
powershell.exe -NoProfile -File eng/capture-visualstudio-heap.ps1 -ProcessId 12345
```

The launcher records the process start time and the tool verifies that identity
before capture. It builds the diagnostic tool in Release and creates a new report
under `artifacts/log/Release/HeapProfiles/<run-id>/`. `-OutputDirectory` overrides
the destination. Revision, working-tree status and .NET runtime information are
retained beside the JSON. Existing reports are never overwritten.

The report contains each CLR's object counts and sizes, root-reachable objects,
per-type totals, managed thread stacks and selected Alarmlist editor/analysis
objects. For those selected types it includes disposal state when available and
up to three sample root paths per type. It does not save a full dump or heap string
contents. Capture can briefly suspend the selected process and use substantial
memory in the diagnostic process; run it separately from timing measurements.
No collection is forced in the inspected process.

A reachable object is not by itself evidence of a leak. Compare equivalent
lifecycle points, inspect the root paths, and distinguish active documents from
disposed views. This diagnostic report complements the automated collectibility
regressions and repeated close/memory measurements; it has no pass/fail threshold.
It also serves as a reusable replacement for the temporary heap probe used during
milestone 3 investigation.
