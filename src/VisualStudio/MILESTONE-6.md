# Milestone six: configurable diagnostics and external analyzers

Status: compiler, build and IDE implementation present on 2026-10-09; ordinary
Release regression suites pass. Focused live IDE and analyzer performance checks
also pass; remaining acceptance is recorded below. The owner removed the 15%
remaining-allowance stop condition on 2026-10-10. Commit each completed implementation
iteration and report a short summary before starting the next iteration.

## Scope and iterations

1. Establish the diagnostic/configuration contract and update the feature status.
2. Resolve captured EditorConfig text with file hierarchy, globs, root and unset.
3. Apply severity, suppression and warning promotion before output installation;
   capture saved configuration in builds and expose the project properties.
4. Add a versioned external analyzer API and explicit assembly references, with
   immutable inputs, cancellation, failure reporting and a sample analyzer.
5. Integrate configuration/analyzers with CPS, the isolated SDK bridge, live
   diagnostics, input validation and disposal.
6. Validate both compiler/task frameworks, packaged consumers and both supported
   IDE hosts. Keep actual validation separate from planned acceptance.

NuGet analyzer discovery, alarm libraries/project references, custom procedure
groups, Shared Projects, XML IntelliSense and syntax generation remain separate
work. Existing milestone-five support/release gates remain recorded there.

## Configuration and diagnostics contract

- Hosts discover and capture saved `.editorconfig` text. The compiler resolves
  only captured inputs and never reads configuration paths or the working directory.
  Capture physical source paths, including linked inputs; source paths select the
  configuration hierarchy. No `.globalconfig` support in this increment.
- Keys are case-insensitive. Globs and literal custom values preserve case.
  Later sections and nearer files override earlier settings. A preamble
  `root = true` stops ancestor configuration. `unset` removes an option.
  Inline `#` and `;` are part of values. Invalid lines are ignored.
- `Alarmlist_diagnostic.<ID>.severity` accepts error, warning, suggestion (Info),
  silent (Hidden), none (suppressed), and default (descriptor default). Unknown
  IDs have no effect; invalid severity values retain the descriptor default.
- A diagnostic's primary source location governs its file settings; related
  locations do not. Location-free diagnostics use project policy only. An analyzer
  checking inherited content in a consuming alarm should report that consuming
  alarm as the primary location and the declaration as a related location.
- All ALM0001-ALM0025 compiler errors are mandatory. They cannot be
  suppressed or demoted. New analyzer rules explicitly declare configurability;
  the existing four-argument descriptor constructor defaults errors to mandatory
  and non-errors to configurable. Preserve existing diagnostic numeric enum values.
- Apply descriptor default, file severity, NoWarn, then warning promotion.
  NoWarn wins over configurable file severity. WarningsAsErrors explicitly promotes
  a warning even if also listed in WarningsNotAsErrors. WarningsNotAsErrors exempts
  only blanket TreatWarningsAsErrors promotion, never an explicit error severity.
  Only warnings are promoted; suggestion/silent remain nonfatal.
- Project ID lists accept semicolons or commas, trim entries, and compare IDs
  case-insensitively. Configuration does not mutate diagnostic descriptors.
- Determine compilation success after policy. Any error in any requested culture
  preserves all previous outputs. Hidden diagnostics remain available to analysis
  but are not logged as build messages or shown as live Error List rows.
- Preserve existing public constructors and the contract-3 bridge while adding
  capability checks for the new captured configuration/analyzer inputs.

## Reference decisions

The structure follows Roslyn's separation of captured configuration and per-source
options ([AnalyzerConfigSet](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/CommandLine/AnalyzerConfigSet.cs)).
The [EditorConfig specification](https://spec.editorconfig.org/) defines matching
and precedence; [C# warning controls](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/errors-warnings)
inform the project policy. Alarmlist interprets its own diagnostic prefix.

Inspected Roslyn's parser and editorconfig-core-net's parser. Both inspected
implementations strip inline #/; values, contrary to the current specification.
This increment uses a small captured-text parser/matcher without a new runtime
dependency. Focused tests cover the required behavior; passing the complete
upstream conformance suite has not yet been established. Root build configuration,
Arcade integration and compiler/host framework support remain unchanged.

## Implementation record

Iterations 1–5 are implemented: captured configuration, shared diagnostic policy,
saved host discovery, warning properties, the contract-1 analyzer API, explicit
assembly references, a sample code-prefix rule, and CPS/SDK bridge integration.
Compilation and detached analysis use the same pipeline. Mandatory errors continue
to block XML previews; effective errors preserve every prior culture output and
manifest. IDE input validation includes saved configuration and dependency bytes.
Cancellation reaches running analyzers across the isolated bridge. Tests cover
configuration races, failed plugins, reserved/duplicate IDs, partial-result discard,
domain replacement, hidden diagnostics and host isolation.

The loader uses captured bytes and does not lock originals. .NET 8 loads each
reference in a collectible context. .NET Framework retains assemblies for its
AppDomain lifetime. Same-version dependency replacement revealed CLR binding reuse
in the full suite; the loader now rejects changed/conflicting images in that domain
with ALM0025. VS automatically replaces the domain when payloads change. Desktop
MSBuild users must start a fresh process after replacing analyzer DLLs; use
`/nodeReuse:false`. This is an explicit v1 limitation, not transparent desktop reload.
Analyzers are trusted and cancellation is cooperative; there is no hard timeout.

Validation on 2026-10-09:

| Suite | Release result |
| --- | --- |
| Compiler | 430 passed per net472/net8.0 |
| MSBuild unit | 64 passed per net472/net8.0 |
| MSBuild integration | 4 passed per net472/net8.0 |
| Packaged SDK consumers | 46 passed per net472/net8.0 |
| VS unit | 344 passed, net472 |
| VSIX/template consumers | 11 passed per net472/net8.0 |
| Performance infrastructure | 26 passed; 9 opt-in cases skipped |
| Functional UI | Compiles; all 38 opt-in cases skipped in ordinary run |

Command: `dotnet test AlarmlistProject.slnx --configuration Release --nologo -v:minimal`.
Both SDK test frameworks use dotnet MSBuild by default; these counts do not yet
prove the desktop task path. The existing CA1416 warning in the shared test helper
remains. Two lookup test expectations now normalize C# raw-string CRLF to the XML
reader's LF, preserving the intended content assertion on Windows.

## Remaining validation

- Expand EditorConfig conformance against the upstream suite; current coverage is
  focused, not a claim of full conformance.
- Bounded lifecycle checks now cover 32 analyzers, repeated execution, cleanup
  failures and .NET 8 context collection (iteration 5). Long-running plugin
  lifecycle and whole-IDE memory/latency measurements remain unmeasured. No
  performance threshold or leak-freedom claim.
- Existing milestone-five manual/vendor/release gates remain separate. No package
  has been published and no VSIX has been distributed for this milestone.

## Validation iteration 2

Implementation checkpoint: `644dc0d`. Added a dedicated `Area=Analyzers` bridge
workload in PerformanceTests. It captures the sample DLL plus configuration,
checks 1,000 warning results and then zero results after the prefix changes, and
verifies SDK/analyzer isolation outside measured intervals. Run through xUnit,
with the existing opt-in environment variables (no standalone benchmark script).

- Desktop MSBuild 17.14/net472 packaged analyzer cases: 3 passed.
- Functional xUnit `Area=Analyzers`: 2 passed, no skips, on VS2022 17.14 and
  VS2026 18.10. Matching Release VSIXs were deployed to `AlarmlistExp` using desktop
  MSBuild. Each case owns and closes its IDE; source and output preservation,
  saved configuration refresh, live severity and source DLL lock checks passed.
- Analyzer bridge benchmark: 1 xUnit case passed with two BenchmarkDotNet reports.
  Short job, two warmups and five samples per method, x64 .NET Framework 4.8.1
  running net472 on Windows 11 / i7-11700K. The 1,000-warning context averaged
  287.4 ms; the configured-prefix context with no warnings averaged 266.4 ms.
  These are complete isolated bridge calls, not live typing latency or an estimate
  of incremental analyzer overhead. Allocation data is benchmark-host scoped.

Artifacts: `artifacts/log/Release/Milestone6Performance/BridgeBenchmarks-1000-100-analyzers-ecffdf99041d455a9fc8f5d7250225b3/`
contains raw CSV/JSON and reports. UI records are under
`artifacts/log/Release/VisualStudioUI/ConfiguredAnalyzer/2022/41f66b0a104a4c6397069568b6a9d9c2/`
and `2026/9bdd80f93d66433ab209b699239a1197/`. Generated local runsettings select
the test hosts and flags and are not checked in.

## Glob correction iteration 3

Reviewing the [upstream glob cases](https://github.com/editorconfig/editorconfig-core-test/tree/master/glob)
against the [current specification](https://spec.editorconfig.org/#glob-expressions)
identified two path-matching defects: a slash-free `Plant**Alarm.almx` must still
allow `**` to span directory separators, while `Plant**/Alarm.almx` must retain its
explicit slash. Added ten regressions and corrected the shared matcher. This review
does not establish full upstream conformance; older core-test expectations for
character ranges differ from the current specification's literal bracket sequences.
The full Release solution run passed again after this correction: 1,480 ordinary
tests passed and 47 opt-in cases skipped. The targeted desktop/IDE/benchmark runs
above preceded this final matcher correction; their simple `*.almx` configurations
were unchanged. No additional live or timing claim is made for the ten new patterns.

## Section parsing iteration 4 (2026-10-10)

Configuration parsing and saved-input discovery now share the same complete-header
check: a section starts with `[` and ends with `]`. Previously an unfinished header
or a header followed by inline comment text could discard subsequent diagnostic
settings or stop discovery from recognizing `root=true`. Invalid header-only lines
now leave the active section/preamble intact. Valid empty sections still end them;
headers containing `=` take precedence over pairs, and bracket-prefixed pair keys
remain valid outside a complete header.

Added seven compiler cases, four host-discovery cases and a packaged analyzer
regression covering suppression, subsequent scope changes and preservation of
previous output on error. The compiler checks also cover the specification's
minimum supported section/key/value lengths (1024/1024/4096 characters).
The focused compiler run passed 437 tests per net472/net8.0. Full upstream
conformance remains unclaimed. The pre-existing edit to `example/North/Alarms1.almx`
is outside this iteration and is preserved.

Validation: the full Release solution run passed 1,504 ordinary tests, with 47
opt-in cases skipped. Counts are 437 compiler, 68 task unit, 4 task integration,
47 SDK and 11 VSIX consumer tests per framework, plus 344 VS unit and 26 performance
infrastructure tests. The focused desktop MSBuild/net472 analyzer run passed all
4 cases. Live IDE and benchmarks were not rerun for this parsing iteration.
Stopped after this validated iteration at the owner's then-active 15% remaining
allowance limit, subsequently removed on 2026-10-10.

## Analyzer lifecycle iteration 5 (2026-10-10)

Added executable cleanup coverage using the existing xUnit projects. Compiler
tests prove captured-image isolation, missing/invalid image diagnostics and
idempotent disposal on both runtimes. The .NET 8 case loads and releases twenty
collectible contexts, retains their disposed loaders and compilation results,
and confirms collection without losing diagnostic text. The sample DLL is a
copied test asset, not a reference loaded into the test host's default context.

SDK bridge cases run 32 analyzers ten times, validate all 320 disposals, and retain
detached results after AppDomain unload. Constructor and execution failure cases
check ALM0025; cancellation checks disposal before returning. A disposal failure
still releases the other instances, makes live analysis unavailable, and allows
the next request to succeed. No plugin or compiler assemblies enter the VS host.

A packaged consumer builds its own disposable analyzer against the restored
compiler, checks a single disposal after both cultures, then changes source and
throws during disposal. Both previous outputs and the ownership manifest remain
unchanged; a subsequent successful build installs the updated source. Disposal
failures surface as host failures, rather than successful partial results. No
runtime behavior changes were required for these lifecycle cases.

Validation: the full Release solution run passed 1,518 ordinary tests, with 47
opt-in cases skipped. Compiler counts are 440 net472 / 441 net8.0, SDK consumers
48 per framework, and VS unit tests 349. The focused desktop MSBuild/net472
analyzer run passed all 5 cases, including the new disposal-failure consumer.
No live IDE or timing workload was rerun for this test-only iteration.

## Escaped paths and numeric globs iteration 6 (2026-10-10)

An escaped opening bracket no longer hides a later path separator during section
scoping. For example, `Plant\[1/Alarm].almx` matches only that relative path, not
`Other/Plant[1/Alarm].almx`. A regression reproduced the incorrect suppression on
both frameworks before the fix. Escaped separators still anchor the pattern.

Numeric brace ranges now encode complete digit suffix intervals once instead of
recursively duplicating them. This avoids exponential regex construction for
large valid ranges while retaining inclusive bounds, signed values and existing
leading-zero behavior. New cases exercise Int64 endpoints, out-of-range values,
escaped/nested alternatives and integer comparisons across sign/digit boundaries.
This extends the focused [specification](https://spec.editorconfig.org/#glob-expressions)
coverage; it does not claim complete upstream conformance.

Packaged consumer cases verify suppression only for the intended physical source,
then move it outside the section and check warning promotion and preserved output.
The opt-in xUnit/BenchmarkDotNet analyzer workload now includes an additional
`Input{0..9223372036854775807}.almx` scenario using the same 1,000-alarm fixture,
warning counts, changed-prefix context and host-isolation checks as the baseline.

Validation: the full Release solution run passed 1,588 ordinary tests (473 compiler
net472 / 474 net8.0, 50 SDK consumers per framework, other counts as in iteration 5),
with 47 opt-in cases skipped. All 7 focused desktop MSBuild/net472 analyzer cases
also passed. Live IDE scenarios were not rerun for this compiler matcher change.

Both `Area=Analyzers` performance cases passed through xUnit, producing four
BenchmarkDotNet reports (Short job, two warmups/five samples, x64 .NET Framework
4.8.1 on the same i7-11700K host). Ordinary-pattern means were 287.2 ms with 1,000
warnings and 262.6 ms with the configured prefix; large-range means were 290.8 ms
and 261.9 ms respectively. These measure complete bridge calls, including captured
configuration construction; they are not typing latency or a regression threshold.
Host allocation figures do not measure the complete isolated SDK domain.

Raw CSV/JSON and summaries are retained under
`artifacts/log/Release/Milestone6Performance/BridgeBenchmarks-1000-100-analyzers-994688c1cda5400f8b9a486b4688f359/`
and `BridgeBenchmarks-1000-100-analyzers-ranges-f983fde97d5a4e3f81a80089cdb3318e/`.
