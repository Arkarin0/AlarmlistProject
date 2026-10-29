# Milestone 14 — Explain this value

Status: complete; all seven authorized iterations and required acceptance gates
passed on 2026-10-29. Timing/allocation limits and evidence are recorded below.

## Outcome and confirmed scope

Provide Explain this value actions in the designer and Alarm Summary, displaying
a read-only expandable Explanation section in Alarm Summary. Cover scalar fields,
custom/well-known parameters, procedure titles/entries and simple/record collection
values. Include inheritance, defaults, expression dependencies, localization
fallback, lookup/template expansion, typed conversion and unavailable/invalid
results. Show chosen contributors only. Exclude shadowed/overridden alternatives,
Rename, Git integration, XML-caret commands and editing through the explanation.

Capture inside existing compiler binding/evaluation paths; the IDE must not
reconstruct expression semantics or resolve resources. Use opt-in target-specific
ExplainSnapshots over captured inputs/context with cancellation. Return immutable
detached nodes with raw/resolved/canonical values, state, origins, contributing
edges and relevant diagnostics. Share dependencies and terminate cycles explicitly.
Ordinary analysis/builds retain no explanation graphs. Match existing compiler
Analysis separation and detached SDK worker architecture rather than introducing
another evaluator or compiler dependency in the host. Roslyn/compiler and Arcade/
build alignment remains as documented in AGENTS.md; no new build pattern is needed.

Use existing selected project/configuration/culture, shared unsaved ALMX buffers
and saved resource/dependency rules. Private drafts require commit/cancel, just as
Alarm Summary does; Explain never commits them. Cancel stale work and invalidate
old display on changed target/context/input. Refresh only when the same target can
be identified; never retarget moved entries by index alone. Revalidate before
publication and navigation. Use physical or read-only package navigation. Show
partial contributing steps/diagnostics for invalid/deferred values, or an explicit
unavailable state when parsing/binding cannot identify a reliable explanation.

Expose the full compiler API and detached transport with analysis contract 8
(activated in iteration 5). Preserve analyzer 5, library format 3, export contract 1 and semantic
definition fingerprints. Keep intermediate tracing internal until all target
categories are supported; keep compiler/SDK/extension usable at each boundary.

## Numbered iterations and planned acceptance

Each iteration depends on its predecessor. Include tests and relevant documentation
with behavior, record actual evidence separately and commit after a successful
iteration before starting the next. Run desktop tests sequentially and account for
shared/RDP input. No user IDE may be closed for deployment.

1. **Branches, plan and contracts.** Refresh main; create feature/milestone-14 then
   dev/milestone-14, and work on dev. Record scope/contracts/acceptance here; update
   backlog and instruction links. Review paths/diff; documentation-only, no tests.
2. **Compiler provenance foundation.** Internal opt-in scalar selection, inheritance,
   literal/default values and parameter declaration/assignment origins. Test
   defaults/explicit empty, child context and project/library origins on both
   frameworks. Verify ordinary compilation keeps no graph. No public/IDE activation.
3. **Expressions, localization and lookups.** Capture field/parameter dependencies,
   well-known values, selected resource culture, translated expressions/formatted
   arguments, lookup labels/metadata/lists. Test literal escapes, keyword/parent
   fallback, missing resources, conversions and cycles on both frameworks.
4. **Procedures and collections.** Capture titles, entries, simple items, record
   fields/siblings/defaults and effective membership. An excluded entry points to
   its controlling Clear, without discarded-value histories. Validate generated
   scalars/collections and enabled/disabled procedures on both frameworks.
5. **Public API and SDK bridge.** Expose ExplainSnapshots with an unambiguous
   snapshot-bound target and detached result. Activate analysis contract 8 together
   with matching SDK worker/host adapter. Packaged consumers on both runtimes,
   incompatible tools, cancellation and assembly isolation must pass.
6. **Designer and Alarm Summary.** Actions beside supported controls and resolved
   summary values; display value/context, contributing steps, diagnostics and
   navigation. Include keyboard/accessibility, draft and stale-request tests and
   feature documentation. Validate host unit/native behavior on both IDEs.
7. **Integrated acceptance and tutorial.** Both-framework compiler/downstream
   regressions, both desktop MSBuild hosts, both native IDEs, shared contexts,
   project/NuGet libraries, custom forks, freshness/cancellation and source
   preservation. Add guided tutorial. Measure ordinary analysis versus on-demand
   explanation with existing large fixtures; report timings/allocations without
   arbitrary latency thresholds. Record limitations and commit final acceptance.

## Actual evidence

Iteration 1: clean main refreshed from origin/main; created feature/milestone-14
and dev/milestone-14, checked out dev. Read current compiler evaluator, analysis
origins/resource resolution and SDK reflection adapter. Existing origins identify
selected definitions but do not yet form dependency graphs. Plan/backlog/instruction
links and diff reviewed; tests not run (documentation-only planning iteration).

### Iteration 2 — complete

- Added internal immutable ValueExplanationNode/Graph and an explicit request-local
  recorder selecting consuming alarms. Existing field/property resolution supplies
  chosen sources, inheritance chain, declaration/assignment ownership, raw/evaluated/
  canonical values, type and state. No host/public API or contract activation.
- Ordinary evaluator construction creates no recorder/graph. Frozen results retain
  no syntax, delegate or mutable recorder objects; input/root/node collections are
  defensive read-only copies. Repeated field/parameter dependencies share nodes;
  repeated failed self-references terminate in Cycle leaves rather than graph loops.
- Tests cover child overrides, alarm/file/project scope, explicit empty vs absent,
  literal escapes, parameter text that looks like expressions, library definitions
  vs consumer assignments, selected-alarm isolation, failures, cancellation,
  immutable snapshots and unchanged ordinary XML. Generated scalar fixture checks
  typed definition defaults and literal defaults without forged source locations.
- Internal authoring README and shared-build sequencing guidance added. Initial
  parallel downstream builds collided in shared intermediate outputs; sequential
  unit/integration runs passed. An initial generated fixture variable-name collision
  was corrected. No production behavior was changed to bypass a validation gate.

| Gate | Result |
| --- | --- |
| Full compiler | 702 net472 / 704 net8.0 passed, zero failures/skips; Milestone14/Iteration2/CompilerFinal. |
| Task unit | 124 per framework passed, zero failures/skips; Iteration2/TaskUnit. |
| Task integration | 4 per framework passed, zero failures/skips; Iteration2/TaskIntegration. |
| Generated scalar/default and generation lifecycle | Both frameworks pass; AlarmDefinitionGeneration/5585d2282cd746cb826ad31e3dc8a314. |

Commands: dotnet test on compiler and both task test projects with --no-restore
-v quiet -m:1 -nr:false --logger trx and results under
artifacts/log/Debug/Milestone14/Iteration2; eng/test-alarm-definition-generation.ps1.
Existing shared TestHelper CA1416 warning remains. Diff/links reviewed and
git diff --check passed. No scoped blocker. Expression detail and later target
categories, public ExplainSnapshots, SDK/UI and native acceptance remain incomplete.

### Iteration 3 — complete

- Existing localization/lookup evaluation now records expression contributors:
  selected Resource nodes carry requested/supplying culture and actual value
  locations; localization uses, formatted arguments, lookup definitions/labels and
  well-known context values carry their compiler-owned results. Ordinary execution
  still creates no recorder. Resource expansion uses the same parsed expression,
  argument checks, fallback selection and evaluator; there is no second resolver.
- Only supplying resources are included, including parent-culture fallback after
  an empty entry. Keyword localization stays deferred without selecting resources.
  Quoted arguments, lookup labels, metadata/list values, parameter assignments and
  defaults retain their literal semantics. Shared field/parameter dependencies
  remain graph references; cycles through resources terminate explicitly.
- Added canonical conversion steps from AlarmFieldValue results, including invalid
  and deferred states, rather than parsing values again. Generated scalar fixtures
  verify default conversion and +00042/invalid Count results on both runtimes.
- Tests cover supplying culture/location/version, child parameter use inside
  resources, template argument values, lookup/list origins, literal tokens,
  repeated field dependencies, well-known values, keyword deferral, missing lookup/
  translation, malformed resources and resource-mediated field cycles. The first
  well-known-path fixture used a relative path; corrected to the captured absolute
  path required by the existing well-known-property contract.

| Gate | Result |
| --- | --- |
| Full compiler | 712 net472 / 714 net8.0 passed, zero failures/skips; Milestone14/Iteration3/CompilerFinal. |
| Task unit/integration | 124 unit and 4 integration per framework passed, zero failures/skips; Iteration3/TaskUnit and TaskIntegration. |
| Generated scalar/runtime and generation lifecycle | Both frameworks pass; AlarmDefinitionGeneration/19945905c9e74713b755f15c519c7ed5. |

Commands use dotnet test on the compiler/task projects with --no-restore -v quiet
-m:1 -nr:false --logger trx, results under artifacts/log/Debug/Milestone14/Iteration3,
followed sequentially by eng/test-alarm-definition-generation.ps1. Existing
TestHelper CA1416 warning remains. Internal capture README, diff and links reviewed;
git diff --check passed. No scoped blocker. Procedures/collections, public API/SDK,
UI and final integrated acceptance remain incomplete.

### Iteration 4 — complete

- Captured chosen procedure titles and effective entries, simple collection items,
  record fields, sibling dependencies, definition defaults and typed conversion
  inside their existing evaluation paths. Membership records consuming versus
  defining alarms and library origins. Keyword results retain deferred states.
- Existing analysis Clear resolution supplies excluded-entry explanations: the
  selected raw value and controlling Clear only; discarded expressions are never
  evaluated. Effective-entry indices are snapshot-local and must not be used as
  persistent identities by the future public API or host.
- Added procedure tests for inheritance, child parameter context, empty-title
  fallback, keyword deferral, invalid entries and library origins. Generated
  collection checks cover defaults, sibling sharing/cycles, canonical values,
  Clear, library records and enabled/disabled procedures on both frameworks.

| Gate | Result |
| --- | --- |
| Full compiler | 718 net472 / 720 net8.0 passed; Milestone14/Iteration4/Compiler. |
| Task unit/integration | 124 unit and 4 integration per framework passed; Iteration4/TaskUnit and TaskIntegration. |
| Generated collections, procedures enabled/disabled | Both frameworks passed; AlarmCollections/8e8fa78d5bd549d2aa361c07c517088b. |
| Generated scalars and generation lifecycle | Both frameworks passed; AlarmDefinitionGeneration/6f33596806664142b451140b5c9f1bfe. |

Commands: sequential dotnet test compiler/task projects with --no-restore -v quiet
-m:1 -nr:false --logger trx, then eng/test-alarm-collections.ps1 and
eng/test-alarm-definition-generation.ps1. Internal README, diff and links reviewed;
git diff --check passed. No scoped blocker. Public API/SDK, UI and integrated
acceptance remain incomplete; analysis contract remains 7 until iteration 5.

### Iteration 5 — complete

- Exposed ExplainSnapshots, immutable ValueExplanationTarget/Result/Node, and the
  matching isolated SDK worker/host transport. Activated analysis contract 8
  together; analyzer 5, library format 3, export 1 and definition fingerprints
  remain unchanged. Older/malformed/mismatched toolsets retain existing fallback.
- Targets identify the consuming alarm by source location/version and entry values
  by their defining entry location/version. Effective ordinals remain internal.
  Unknown, ambiguous, moved/stale, unbound and unparseable targets return explicit
  unavailable results. Hosts still must validate captured dependency/context bytes.
- The public result slices only reachable chosen contributors, shares dependency
  IDs and includes context and relevant policy-applied diagnostics. No compiler
  graph is added to ordinary analysis/build results. Unused parameters, source
  scopes, well-known aliases and selected abstract/library deferral are supported.
  Missing required fields retain diagnostics even without a value source location.
- Added public API, generated collection/default/Clear and packaged library tests,
  plus detached bridge survival/cancellation/stale-target/assembly-isolation checks.
  Updated SDK/compiler authoring guidance and current repository contract guidance.
  The packaged fixture now restores its intentionally changed snapshots before
  requesting the original target, separately asserting stale-target rejection.
  Generated bridge fixtures explicitly include the new detached types.

| Gate | Result |
| --- | --- |
| Full compiler | 730 net472 / 732 net8.0 passed; Iteration5/CompilerFinal. |
| Full extension unit | 834 passed; Iteration5/Host. |
| Focused bridge after final transport updates | 56 passed; Iteration5/BridgeFinal. |
| Packaged contract/contents | 5 per framework passed; Iteration5/PackageFinal. |
| Desktop packaged contract/contents | 5 passed on each VS 2022/2026 MSBuild host; Iteration5/Desktop2022 and Desktop2026. |
| Task unit/integration | 124 unit and 4 integration per framework passed; Iteration5/TaskUnit and TaskIntegration. |
| Generated collections and procedure variants | Both frameworks passed; AlarmCollections/bc15de13b47f4b388bb292997c3ed62f. |
| Generated scalars/required values and lifecycle | Both frameworks passed; AlarmDefinitionGeneration/7a018758be2d41f3827e7c923e6023c0. |

Commands: sequential dotnet test compiler/task/extension unit projects with
--no-restore -v quiet -m:1 -nr:false --logger trx; SDK PackageTests on both
frameworks and with ALARMLIST_TEST_MSBUILD_PATH set to each installed desktop host
(previous value restored); eng/test-alarm-collections.ps1;
eng/test-alarm-definition-generation.ps1. Evidence is under
artifacts/log/Debug/Milestone14/Iteration5 and the fixture directories above.
Diff, paths and contract consistency reviewed; git diff --check passed. Existing
TestHelper CA1416 warning remains. No scoped blocker. Designer/Summary UI, native
acceptance, tutorial and timing/allocation reporting remain incomplete.

### Iteration 6 — complete

- Added designer and selected-alarm summary actions for fields, custom/context
  parameters, procedure titles/effective/excluded entries and collection fields.
  Current presentation objects map to detached source/version targets. Summary
  details keep failed/abstract targets visible with explicit unavailable values.
- Added an expandable read-only Explanation section: context, raw/resolved/
  canonical values, states, diagnostics, shared step IDs, expandable details and
  source links. Presentation reveals 50 steps per page without recursive graph
  duplication. No source editing or draft application is performed.
- The host performs a cancellable SDK request off the UI thread and revalidates
  saved/unsaved inputs before publication and navigation. Target/context/input
  changes clear results; pending drafts and culture writes block actions. Existing
  physical and read-only package navigation handles located contributors.
- Added field-specific button automation peers and keyboard-capable routed actions.
  Native fixture explicitly identifies the owned IDE window, restores the designer
  after an XML-buffer edit, and accepts a collapsed/recreated empty explanation.
  Feature documentation and repository/authoring guidance accompany the behavior.
- Updated regression expectations for inspectable failed/abstract targets, selected
  the literal chooser by its actual action identity, and measured sticky-header
  pinning after reaching a group in its resized layout. No sticky-header production
  behavior was changed. Native screenshots reviewed for read-only result/layout.

| Gate | Result |
| --- | --- |
| Full extension unit | 844 passed, zero failures/skips; Iteration6/HostFinal. |
| Focused target/draft/cancellation/navigation/paging/layout | 29 passed before the final two culture-write cases; Iteration6/Focused. Both added cases pass in the full suite. |
| Native VS 2022 | ValueExplanation passed, keyboard activation, fallback, draft preservation/cancellation, language/unsaved-input invalidation, custom parameter, procedure title/entry and ALMX source navigation; TestResults/08f517e8802d4b9e9be2e3fb4669b430, workspace ValueExplanation/2022/328934be2dd049bda8669151a286a2d5. |
| Native VS 2026 | Same scenario passed; TestResults/ea2f356c86354dc28dbd2c5660fc1d2e, workspace ValueExplanation/2026/79a8f7fbcadf41dd8c3a358399cd3f30. |
| Final themed presentation | Native VS 2026 repeat passed after explicit themed step-header foregrounds; TestResults/e6f76a5f46bb4381940e1babb5d571b0, workspace ValueExplanation/2026/34933c90553d4c289021a95661080dd6. Focused Explanation unit selection: 9 passed; Iteration6/PresentationFinal. |

Commands: full dotnet test extension unit project with --no-restore -v quiet
-m:1 -nr:false --logger trx, then sequential eng/test-visualstudio.ps1
-VisualStudioPath <host> -RootSuffix Alarmlist13Exp -Filter Area=ValueExplanation
for each IDE. Native fixtures preserved ALMX bytes and prior compiled output.
No existing user IDE was closed. Diff, documentation links and paths reviewed;
git diff --check passed. No scoped blocker. Shared/library/custom-fork integrated
acceptance, complete downstream regressions, tutorial and timing/allocation evidence
remain iteration 7 work.

### Iteration 7 — complete

- Added a guided inheritance/parameter/field-dependency tutorial, with its exact
  example verified through the public API. Source-scope requests now omit
  alarm-only well-known values, matching ordinary analysis instead of inventing
  an empty alarm context. Added cancellation coverage during evaluation/freezing.
- Reused the project-owned SDK worker under its analysis gate. Requests cancel
  when project state changes, validate inputs before/after evaluation and never
  replace ordinary analysis. Focused explanation/service checks: 61 passed in
  Iteration7/Service, including stop/cancellation and input changes without events.
- Extended native shared/library/custom-definition acceptance with explanation
  assertions and source navigation. Standard integrated acceptance passed on both
  IDEs. Custom-definition acceptance passed with procedures enabled and disabled:
  isolated forks col13/e41d2a6a and col13/443e4cc0. Each passed project/package,
  analyzer and packaged API consumers on both task runtimes (desktop VS 2022),
  then Area=MetadataCollections on both IDEs, including defaults, sibling
  references, inherited values and Clear explanations. Final regressions passed.
- Corrected the tutorial's expected parameter declaration: an assigned child
  Property also redeclares it, so the chosen child declaration/assignment appear.
  The shadowed base declaration is excluded. The initial new assertion failed
  on both frameworks; compiler behavior was correct. The corrected full compiler
  run passed 734 net472 / 736 net8.0 tests in Iteration7/CompilerFinal.
- Default example preparation now disables build-node reuse and limits build
  parallelism: two orphan child workers inherited the captured output pipes,
  preventing the native fixture from completing after preparation exited. Only
  those verified test workers were stopped; no user IDE was closed. The corrected
  preparation ran twice without intervention in the final VS 2026 check.
- Native fixtures now wait for the English resolved value before invoking Explain,
  reactivate the designer after hiding Explanation, and focus the shared consumer
  selector. Initial failures captured a different selected language or pending
  project evaluation, rather than the expected English result. Required cases
  passed after these focus/readiness corrections:
  VS 2022 shared/project-library/NuGet-library in TestResults/50ea140a449d4996aa461d4caeea602e
  (three passed, initial language case failed), language/keyboard/draft/freshness
  repeat in TestResults/464a874efef848e4849495b2c03cf3b8 (one passed).
  VS 2026 language/project-library/NuGet-library in
  TestResults/d1c871091d6747fca508fe928379e340 (three passed, initial shared case
  failed), shared repeat in TestResults/93d32e1ac72b4efcae2b5dcb0bbde990 (one passed).

Performance evidence: Release/x64 .NET Framework 4.8.1, existing large-project
fixtures, two warmups and five measured samples. BenchmarkDotNet ShortRun reports
are under AnalysisPerformance/779e863869e64fb7a141b1840cde8cb6 (four tests,
twelve benchmarks passed). Times below are means in milliseconds, without a
latency threshold:

| Fixture | Ordinary analysis | Explanation, reused worker | Explanation, new bridge |
| --- | ---: | ---: | ---: |
| 1,000 alarms | 345.58 | 79.58 | 275.70 |
| 10,000 alarms | 3845.1 | 899.7 | 1093.0 |
| 1,000, parameters | 569.4 | 142.8 | 351.4 |
| 1,000, localization | 578.3 | 153.4 | 374.6 |

The BDN host allocation counter excludes SDK-domain allocations; empty allocation
cells do not mean zero allocations. A separate test-only domain-local probe
measured cumulative allocated bytes around compiler/SDK reader work, excluding
outer remoting/UI. Its four cases passed; raw paired samples are under
AnalysisPerformance/ccdde155caef464fbfe21727c283276f/explanation-worker-*.csv.
The probe enables monitoring only in its test process. Means:

| Fixture | Ordinary ms / MiB | Explanation ms / MiB | Returned nodes |
| --- | ---: | ---: | ---: |
| 1,000 alarms | 242.3 / 79.47 | 95.2 / 54.49 | 2 |
| 10,000 alarms | 2392.3 / 793.34 | 949.6 / 544.15 | 2 |
| 1,000, parameters | 405.1 / 123.21 | 166.9 / 88.75 | 5 |
| 1,000, localization | 388.8 / 149.13 | 156.6 / 94.22 | 3 |

Ordinary analysis includes full analysis/XML output; explanation evaluates the
selected target over freshly bound captured inputs. Explanation still allocates
compiler data proportional to input size. These are allocation totals, not
retained-memory or collectibility evidence. Native painting, CPS/input capture,
watchers and debounce are outside these intervals. Separate runs have different
instrumentation and must not be compared as identical timing samples. Dependency
sharing, cycle termination and 50-step paging are correctness gates, not speed
thresholds.

Final regression evidence (zero failures/skips in the final selected runs):

| Gate | Result |
| --- | --- |
| Compiler | 734 net472 / 736 net8.0 passed; Iteration7/CompilerFinal, final reports at 18:13. |
| Extension unit | 847 passed; Iteration7/HostFinal. |
| Task unit/integration | 124 unit and 4 integration per framework passed; Iteration7/TaskUnitFinal and TaskIntegrationFinal. |
| Full SDK | 97 net472 / 94 net8.0 passed; Iteration7/SDK. |
| Packaged extension integration | 14 per framework passed; Iteration7/VSIntegration. |
| Desktop packaged compiler/contract/contents/evaluation | 3 passed on each VS 2022/2026 MSBuild host; Iteration7/Desktop2022 and Desktop2026. |
| Native standard/shared/project-library/NuGet-library | All four required cases passed on each host, including repeats detailed above. |
| Generated collection/procedure variants | Both task runtimes and native IDE hosts passed in each isolated fork detailed above. Scalar/collection generation gates also passed in iteration 5. |
| Timing/allocation | Four benchmark cases (12 methods) and four domain-local allocation cases passed; tables above. |

Commands: sequential dotnet test compiler, extension unit, task unit/integration,
full SDK and extension integration projects with --no-restore -v quiet -m:1
-nr:false --logger trx; final desktop runs selected the PackageTests class with
ALARMLIST_TEST_MSBUILD_PATH set to each desktop host, then restored its previous
value. Sequential eng/test-visualstudio.ps1 used Alarmlist13Exp and the four
Area filters above. eng/test-alarm-collection-fork.ps1 -Designer used both IDE
paths, VS 2022 DesktopMSBuildPath and the idle AlarmlistDefinitionExp profile.
Performance commands: eng/measure-visualstudio-analysis.ps1 -Job Short with
Area=Explanations and Area=ExplanationAllocations separately. Standard evidence
paths are under artifacts/log/Debug/Milestone14/Iteration7, VisualStudioUI and
artifacts/log/Release/AnalysisPerformance; isolated fork logs remain in artifacts/col13.

Reviewed diff, source/contract compatibility, tutorial examples and local links;
git diff --check passed. Existing TestHelper CA1416 warning remains. Desktop tests
ran sequentially with explicit focus/readiness checks for concurrent RDP input.
No essential check is deferred and no scoped implementation blocker remains.
Package publication and release deployment were not requested. Explanations
still bind captured input on demand; the allocation/timing figures above describe
this implementation, with no arbitrary latency or retained-memory guarantee.
