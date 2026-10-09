# Milestone six: configurable diagnostics and external analyzers

Status: compiler, build and IDE implementation present on 2026-10-09; ordinary
Release regression suites pass. Live IDE and performance acceptance remain open
below. Stop at a validated checkpoint when the account usage allowance reaches
approximately 15% remaining. Commit each completed implementation iteration.

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
| Compiler | 420 passed per net472/net8.0 |
| MSBuild unit | 64 passed per net472/net8.0 |
| MSBuild integration | 4 passed per net472/net8.0 |
| Packaged SDK consumers | 46 passed per net472/net8.0 |
| VS unit | 344 passed, net472 |
| VSIX/template consumers | 11 passed per net472/net8.0 |
| Performance infrastructure | 26 passed; 8 opt-in cases skipped |
| Functional UI | Compiles; all 38 opt-in cases skipped in ordinary run |

Command: `dotnet test AlarmlistProject.slnx --configuration Release --nologo -v:minimal`.
Both SDK test frameworks use dotnet MSBuild by default; these counts do not yet
prove the desktop task path. The existing CA1416 warning in the shared test helper
remains. Two lookup test expectations now normalize C# raw-string CRLF to the XML
reader's LF, preserving the intended content assertion on Windows.

## Remaining validation

- Run the packaged analyzer cases against desktop MSBuild/net472 explicitly.
- Deploy matching test VSIXs, then run the new `Area=Analyzers` xUnit UI cases on
  VS2022 and VS2026. These check live severity/configuration changes, source/output
  preservation and the absence of source DLL locks. No interactive result is claimed yet.
- Run opt-in performance tests directly through their xUnit project, with a focused
  analyzer/configuration workload before drawing overhead or lifecycle conclusions.
- Expand EditorConfig conformance against the upstream suite; current coverage is
  focused, not a claim of full conformance.
- Existing milestone-five manual/vendor/release gates remain separate. No package
  has been published and no VSIX has been distributed for this milestone.
