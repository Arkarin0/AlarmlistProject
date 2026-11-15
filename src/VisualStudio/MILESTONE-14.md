# Milestone 14 — Explain this value

Status: original iterations 1–7 complete with required acceptance gates passed on
2026-10-29. Follow-up iterations 8–14 (Find All References and Rename) authorized;
iterations 8–14 complete with required acceptance gates passed on 2026-10-30.
Original evidence and performance limits remain below.

Follow-up iterations 15–22 are authorized on dev/milestone-14. Iteration 15 is
complete with its required gates passed on 2026-10-31. Iteration 16 is complete
with its required gates passed on 2026-11-14. Iteration 17 is complete with its
required gates passed on 2026-11-14. Iteration 18 is complete with required gates
passed on 2026-11-14. Iteration 19 is complete with required gates passed on
2026-11-14. Iterations 20–22 are complete with their required scoped gates passed
on 2026-11-15. Standard RESX-table commands remain explicitly deferred by the
user; direct RESX XML commands are accepted. No native acceptance gate for the
accepted XML/designer scope remains open.
Implementation and evidence are recorded in the appended follow-up.

Iteration 23 is complete (2026-11-15): designer Backspace restored, with required
native keyboard/focus regression checks passed on both supported IDEs.

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

## Authorized follow-up — Find All References and Rename

Iterations 8–14 extend this milestone on dev/milestone-14. No branch creation,
merge or push is authorized by this follow-up. Preserve iterations 1–7 above.

### Confirmed behavior and compatibility

- Provide compiler-owned Find All References and previewed Rename for alarms,
  custom parameters, localization keys, lookup identities and procedure groups,
  from designer, Alarm Summary and semantic XML tokens. References results are
  grouped by definition, physical source and consumer context, with navigation.
- Use opt-in FindReferencesSnapshots and PreviewRenameSnapshots APIs accepting
  captured inputs/context, snapshot-bound targets and cancellation. Return detached,
  immutable identities, declarations, exact source spans, occurrences, diagnostics
  and coverage; rename also returns versioned replacements and overlay diagnostics.
  Extend parser/binder capture; never implement semantic matching in the host.
- Preserve existing binding/case rules. Families follow actual inheritance,
  overrides, resource providers and consumer bindings; equal text alone is not
  identity. Coordinate related declarations, translations and overrides. Include
  group membership/Clear, resource labels, translated/formatted expressions and
  lookup path/member/metadata/list/entry/template argument bindings. Literal text,
  comments and escaped expression-like text are not references. Built-ins,
  generated definitions and packages are searchable but immutable. Report dynamic
  or ambiguous identities without guessed edits. Preserve XML/entity/CDATA text.
- Discover known solution projects and reachable dependencies, including unloaded
  projects where saved capture is possible without targets. Validate active
  configuration/platform and every configured culture. Explicitly report unavailable
  projects and excluded configurations; never claim unknown external consumers.
- Use committed unsaved ALMX and current saved resource/dependency rules. Private
  drafts block affected actions and are never committed implicitly. Temporary rename
  validation overlays may include resource/project edits without changing ordinary
  analysis. Deduplicate physical edits while retaining consumer contexts. Cancel and
  invalidate stale targets, context, graphs, toolsets or inputs, including navigation.
- Preview before/after text, skipped findings and diagnostic differences. Allow
  excluded edit groups and partial rename only after revalidation and explicit
  acknowledgement of incomplete coverage, remaining references and expected new
  diagnostics. Invalid names, collisions, overlapping edits and incompatible shared
  bindings remain non-applicable; partial rename can apply proven edits elsewhere.
- Open editable documents, perform Query Edit, then recheck every version/input.
  Apply the approved set atomically as one linked Undo/Redo operation in unsaved
  buffers. Failure/cancellation rolls back every touched buffer. An editability
  failure never silently shrinks the set. Explain resource/dependency save needs.
- Activate analysis contract 9 only with complete public API/transport in iteration
  12. Preserve analyzer 5, library 3, export 1 and semantic definition fingerprints.
  Ordinary compilation retains no reference index. No Git integration, file/project
  renaming or schema-definition renaming is included.

Architectural references reviewed: Roslyn's [symbol search](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/Core/Portable/FindSymbols/SymbolFinder.cs)
and [rename separation](https://github.com/dotnet/roslyn/blob/main/src/Workspaces/Core/Portable/Rename/Renamer.cs)
guide compiler-owned binding, solution coordination and proposed immutable edits.
Keep the existing SDK-domain isolation rather than adding Roslyn dependencies.
Use Visual Studio's [linked undo manager](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.textmanager.interop.ivslinkedundotransactionmanager?view=visualstudiosdk-2022)
for native transaction grouping. Existing Arcade build/package layout is retained.

### Numbered iterations and required gates

Each iteration depends on its predecessor, carries tests/documentation/evidence,
and receives a focused commit before the next starts. Never call a blocked gate
successful. Desktop/WPF/resource-apply suites run sequentially with RDP focus checks.

| Iteration | Deliverable and completion criteria |
| --- | --- |
| 8 | Record approved scope/contracts here, update backlog and repository guidance; review diff/links and commit the planning iteration. No build needed. |
| 9 | Compiler-owned token/source mapping and coordinated host buffer transaction. Test exact spans, entity/CDATA/escaping/format preservation; prove multi-file Apply, failure rollback and one-step Undo/Redo for ALMX/RESX/lookup/project files in both IDEs before further application work. Keep tracing internal. |
| 10 | Alarm/parameter identities, families, discovery and rename preview. Both-framework tests cover inheritance/shadowing, project/library origins, shared consumers and unrelated identical names. |
| 11 | Complete resources/lookups/groups, cultures, translated/formatted arguments, group/Clear families. Both-framework tests and generated scalar/collection/procedure-enabled/disabled validation. |
| 12 | Complete opt-in public APIs, contract-9 detached bridge and solution/dependency coordination, overlays, coverage, cancellation/shared conflicts. Packaged compatibility/isolation checks on both runtimes and both desktop hosts. |
| 13 | References tool window, rename preview, explicit partial acknowledgement, designer/Summary/XML command routing and fallback, navigation/accessibility. Feature documentation and both-host native functional checks. |
| 14 | Guided tutorial and integrated solution/shared/project-library/NuGet/custom-definition acceptance. Full affected compiler/downstream suites on both frameworks, both desktop bridge hosts and both IDEs. Large-project ordinary/search/rename timing and allocation reports, cancellation/bounded presentation; no arbitrary latency threshold. Commit only after all required gates pass. |

### Follow-up actual evidence

Iteration 8 — complete: working tree initially clean on dev/milestone-14 at
9520de3. Read compiler analysis/property expression/binding/source locations,
source-preserving editing, native buffer/Undo and current project bridge structure.
Reviewed the architectural references above. Appended this approved plan without
changing completed evidence; updated backlog and repository guidance. Documentation
links, paths and diff reviewed. Tests not run (planning/documentation only).

Iteration 9 — complete:

- Added request-local XML decoded-value to original UTF-16 span mapping after
  authoritative XML parsing. Handles attributes/text, XML entities, CRLF
  normalization, CDATA and namespace-qualified attributes. Rejects ranges crossing
  comments/markup or splitting surrogate/entity units; never discards authored
  markup. Expression parser tokens and formatted argument names now carry exact
  offsets, including nested tokens, with escaped/literal expressions excluded.
- Added the internal coordinated edit engine, preflight text/version and read-only
  checks, Query Edit revalidation, cancellation, accepted-text verification and
  rollback. SourceEdit moved to its own file to share the same production engine
  and automation adapter with the external native fixture. The adapter verifies
  thread access and uses the native automation UndoContext front door to linked
  Undo; no test command or test dependencies are installed in the VSIX. The
  caller must supply real Query Edit and version capture when wired in iteration 13.
- Both native hosts passed the strengthened Area=RenameTransaction fixture:
  ALMX/RESX/lookup/standalone project XML buffers remain unsaved; one Undo/Redo
  covers all four; failure after four edits and cancellation after two roll back;
  the preceding successful Undo remains usable. VS 2022 final report:
  VisualStudioUI/TestResults/188140601ba347388b638976c4b6d682; VS 2026:
  VisualStudioUI/TestResults/3a97883d33ee42ebae34f86c76fdcc0d. An initial native
  failure exposed that SetAborted already closes UndoContext; removed the redundant
  Close and verified the original exception and rollback remain observable.
- Full compiler: 748 net472 / 750 net8.0 passed; Iteration9/Compiler. Focused
  transaction units: nine passed; Iteration9/TransactionUnit. Final host: 856 passed
  in Iteration9/HostVerified; task unit/integration: 124 and four per framework
  passed in Iteration9/TaskUnit and TaskIntegration. Packaged compiler checks:
  three per framework; packaged extension integration: 14 per framework, in
  Iteration9/Package and VSIntegration. Contract 8 remains active and no public reference API
  or ordinary retained index has been introduced.
- Existing popup/accessibility host tests initially timed out (six in the full
  run and six in the isolated repeat). Captured owned test-host stacks show WPF
  blocked in PenThreadWorker.WorkerGetTabletsInfo during mouse capture, with an
  active RDP session. Evidence: Iteration9/Diagnostics/bd05d66fcc6a457ea376968da2de70f5/host.json.
  A temporary diagnostic confirmed runtime config overrides were not read in
  the secondary test AppDomain. The unit STA harness now initializes WPF's
  [documented source switch](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/CoreAppContextSwitches.cs)
  directly, disabling only isolated-test pen/touch discovery. Keyboard/mouse and
  accessibility assertions remain intact; native IDE input settings are unchanged.
  Eight diagnostic/reproduced cases passed in Iteration9/DesktopInitialized; the
  temporary diagnostic test was removed before final regression. No unrelated
  production input settings were changed.
- Subsequent full host checks exposed three deterministic parameter-menu failures:
  Select all committed a private draft, and Assign left its menu open. A temporary
  stack capture traced the commit to OnFieldLostFocus during MenuItem.FocusOrSelect.
  Marked the parameter menu PreserveDraft and explicitly closed Assign's menu
  before deferred editor focus. All five existing parameter menu/selection/focus
  regressions passed in Iteration9/ParameterMenusFinal with original assertions;
  temporary diagnostic code was removed. The full host repeat passed (856).
- Final review moved captured-input/private-draft validation before opening any
  document as well as retaining post-open/Query Edit checks: opening a document
  changes focus and must not precede the caller's draft guards. The transaction
  regression asserts a stale context opens no documents. Final focused checks are
  recorded separately in Iteration9/FinalFocused.
- Final focused host checks passed (14, Iteration9/FinalFocused), including the
  pre-open stale-context guard and the existing parameter-menu regressions.
- After the user reconnected/unlocked the desktop, native Properties acceptance
  exposed a fixture readiness gap: explicit completion sent Enter before its
  candidates were ready during CPS refresh. Wait for current analysis and the
  actual completion items before Enter; preserve the original insertion and draft
  assertions. Removed all temporary tracing. VS 2022 passed all three native
  Properties/RenameTransaction cases in TestResults/7c5769d74bf64aa8abe0a10006767c3a.
- One VS 2026 diagnostic attempt finished transaction assertions but stalled in
  DTE Solution.Close during cleanup. Captured runner stacks in
  Iteration9/Diagnostics/541d80ace23e4e3a827ff85cb830b7ef/runner.json, then stopped
  only the verified owned test IDE. That interrupted attempt is not final
  acceptance evidence. The exact cause of the earlier close stall was not
  established. A diagnostic variant adding a synchronous initial Build passed
  on VS 2026, but stalled before edits on VS 2022 (diagnostic stacks:
  Iteration9/Diagnostics/45501c9d78fc48b5b40e652c2f359d34/ide.json). Interrupted
  only that verified owned IDE; reverted the optional initial-build variant.
  Keep this focused transaction gate independent of native project-build
  acceptance. Its final assertions compare saved bytes for all four file types.
- The final VS 2026 repeat passed all three cases in
  TestResults/36077400e1ed43aebdaa1f9096ef3e4b (54 seconds), including normal owned
  IDE exit without forced termination. Final focused transaction repeats passed
  with the final fixture on both hosts: VS 2022 (19 seconds),
  TestResults/93338f3511da4e79be422e69bd4bbb8e; VS 2026 (15 seconds),
  TestResults/8fcaa5e49b0447b7a27bbf823ebc1781. Both report normal owned-IDE exit
  without forced termination. This completes the iteration 9 early transaction
  gate; full rename commands, solution coordination and integrated build/output
  acceptance remain in their planned later iterations.

Iteration 10 — complete:

- Added internal request-only alarm/parameter identities and reference capture.
  Observe Binder/LibraryCompilation inheritance, PropertyResolver declaration and
  assignment choices, existing scalar origins and procedure/collection resolution.
  Use the expression parser's tokens and XML decoded/source mapping. Literal
  defaults/assignments, comments, escaped expressions and quoted arguments remain
  untouched. Ordinary compilation/analysis constructs no index.
- Actual inheritance and declaration/assignment edges connect families; unrelated
  equal names remain separate. Preserve case-sensitive alarm and case-insensitive
  parameter rules. Duplicate/invalid declarations and unbound references have
  explicit findings without guessed edits. Keep consumer bindings on each token,
  including inherited expressions; conflicting physical bindings skip that edit
  while proven edits elsewhere remain eligible for acknowledged partial rename.
- Detached immutable results include declarations, occurrences, exact UTF-16
  spans, consumer names, coverage and diagnostics. Snapshot-bound target IDs cover
  source/resource/lookup/library bytes and project/configuration/platform/culture.
  Changing another input or consumer invalidates the target, even with unchanged
  source version. Imported libraries and uncaptured project declarations are
  searchable but read-only. Source lookup uses precomputed line/column maps;
  family roots compress paths; shared conflict checks use an indexed span table.
- Internal preview validates names/collisions/overlaps, deduplicates physical edits,
  creates an in-memory ALMX overlay and returns versioned replacements plus proposed
  diagnostics. Skipped/read-only findings and additional diagnostic occurrences
  require acknowledgement; they do not silently make all proven edits ineligible.
  No host Apply or public reference API is exposed in this iteration.
- Added 22 reference regression cases covering inheritance, shadowing, file/project
  scopes, package origins, two consumers of shared ALMX, unrelated identical names,
  partial edits, collisions, exact entities/CDATA, comments, escaped/quoted literals,
  procedure titles/entries/Clear, invalid inputs, cancellation, detached read-only
  collections and ordinary-analysis preservation. Added two XML mapping cases for
  carriage returns in replacement names. An initial test used XDocument's default
  whitespace dropping; switched its verification to PreserveWhitespace and added
  actual ALMX-parser checks for both text and CDATA. These verify the exact value,
  including character-reference/CDATA boundaries.
- Full compiler acceptance passes: 772 net472 / 774 net8.0, including all new cases;
  logs: Iteration10/CompilerAcceptance. Task units pass: 124 per framework,
  Iteration10/TaskUnit. Final rebuilt package acceptance passes five per framework,
  Iteration10/PackageAcceptance. Changed contributor links, paths and diff reviewed.
- Contributor API documentation updated in Compiler/Analysis/README.md. User-facing
  References/Rename documentation remains iteration 13. Resource/lookup/group
  identities, all-culture solution coordination, resource/project overlays,
  toolset/graph freshness, command/UI and integrated performance/native gates stay
  in iterations 11–14. Analysis contract 8, analyzer 5, library 3, export 1 and
  semantic definition fingerprints are unchanged.

Iteration 11 — complete:

- Added opt-in catalogue observation at existing resource and lookup provider/
  override selections. Callbacks are supplied only by reference requests; ordinary
  catalogue construction retains no reference index or observer. Added parser-owned
  formatted-placeholder spans without changing expression evaluation.
- Resource capture now records culture families within actual providers and
  connects overrides through the catalogue's existing dependency selections.
  Resource/lookup source maps include attribute locations. Translated parameter
  references follow the actual selected culture/parent fallback; escaped tokens
  and comments remain literal. Internal preview now builds temporary resource/
  lookup snapshots alongside ALMX overlays; ordinary saved-input rules are intact.
- Added path/member/metadata/list/entry/template/argument identities and exact
  references. Factor LookupTokenBinding out of the evaluator and use it for both
  evaluation and capture, preserving the original selection/diagnostic rules.
  List identities follow actual additive merging; path/entry/binding overrides
  follow existing provider selection. Namespace collisions use actual parent
  families, so unrelated members in other paths remain independent. Template
  resource declarations, translations and formatted argument/placeholder names
  coordinate together. Metadata/list/caption payloads remain literal.
- Procedure groups connect through actual inheritance, expose Clear controls and
  preserve title/entry text. Generated scalar/collection fields, sibling names and
  well-known names are searchable and immutable. Resource-key renames report
  immutable generated caption references. Ambiguous ownership propagates to
  members; invalid alarm chains report unavailable consumer binding using the
  evaluator's existing HasValidReferenceChain guard, rather than exposing a
  binder's partial choice as a proven expression use. Known physical spans remain
  navigable without guessed replacements.
- Added 35 compiler cases for all lookup kinds, parent/culture families, translated
  parameters, formatted placeholders, scoped collisions, package overrides,
  unrelated/ambiguous providers, group/Clear behavior, generated/built-in names,
  deferred template hints and ambiguous inheritance/ownership. Full compiler
  acceptance passes: 807 net472 / 809 net8.0, Iteration11/CompilerAcceptance.
- Generated-definition acceptance passes via eng/test-alarm-collections.ps1:
  AlarmCollections/74b6f20f1a424569ac73f0174202b114. Both frameworks pass 119 checks
  with procedures disabled, 117 enabled and 13 minimum-count checks. New checks
  cover simple/record/inherited/translated parameter edits, immutable sibling
  definitions, literal defaults, caption-key partial findings and procedure guards.
- Task unit/integration checks pass: 124 / four per framework, in
  Iteration11/TaskUnit and TaskIntegration. Final rebuilt package acceptance
  passes five per framework (Iteration11/PackageAcceptance); affected isolated
  SDK bridge checks pass 56 (Iteration11/Bridge), retaining contract-8 consumers.
  Initial Foundation passed 42 focused
  localization/library cases per framework; a compiler-server build stall was
  interrupted and retried with UseSharedCompilation=false. Final compiler tests
  above use the ordinary build settings. No IDE or unrelated process was stopped
  for that compiler retry.
- Contributor API documentation and durable repository guidance updated; user
  documentation/commands remain iteration 13. Solution/configuration/culture
  coordination, project/dependency overlays, selection revalidation, completed
  public APIs and contract-9 transport remain iteration 12. Changed contributor
  links, paths, diff and required checks reviewed. Analysis 8,
  analyzer 5, library 3, export 1 and definition fingerprints are unchanged.

Iteration 12 — complete:

- Internal request/target foundation captures source/context/project-document
  bytes, library ownership/portable names, definition tooling fingerprint and
  compiler module identity. Targets use declaration origins, compiler-mapped token
  positions or compiler-issued declaration identities; stale captured targets are
  rejected. Identity lookup in another frozen consumer creates a target bound to
  that consumer's inputs instead of reusing another request's target.
- Detached declaration identities include actual physical owners. Owning library
  inputs can supply their authoritative portable source names so direct project
  declarations and imported projections share identities; moving a declaration
  to another owner does not reuse its identity at the same source position.
  Compiler diagnostic source identities remain ordinal; host physical edit
  deduplication remains a separate responsibility.
- Captured project XML can map evaluated parameter declaration origins. Static
  names require exact agreement with decoded declaration text; dynamic/composite
  includes report explicit skipped findings. Project-file edits are not applicable
  until SDK-owned overlay evaluation validates the proposed project context.
  The initial prototype exposed those edits only for that pending validation.
- Focused target/identity/compiler reference checks pass on both frameworks in
  Iteration12/Targets and Identity (29 and 31 cases respectively); the latest
  full compiler foundation verification passes 816 net472 / 818 net8.0 in
  Iteration12/Foundation, including the project-evaluation applicability guard.
  These foundation checks preceded public API/transport activation and used
  analysis 8 with internal request/target types. The later contract-9 transition
  and its evidence are recorded below; iteration-wide integration remains pending.
- Internal preview selections now use stable snapshot/name-bound file groups;
  excluded groups recompile the actual selection and report remaining changes
  and expected diagnostics. Exact projected-input validation rejects omitted
  changed files, extra edits, stale versions and another consumer context. A
  matching SDK-evaluated project overlay clears the project-evaluation guard;
  the request pipeline still needs to supply that complete overlay. Focused
  selection/target checks pass 16 per framework in Iteration12/Selection.
- Saved project evaluation now captures evaluated parameter Include origins and
  accepts request-local project/import XML overlays through private MSBuild XML
  roots. This reevaluates item membership and metadata without running targets.
  Library graph/input capture accepts the same private overlay for project,
  ALMX, RESX and lookup inputs. Normal callers retain saved-input behavior.
  Dependency-overlay acceptance passes 21 net472 tests in
  Iteration12/DependencyOverlay, including graph changes, conflicting shared
  text, virtual inputs, preserved disk bytes and ordinary recapture.
- Internal cross-context search follows actual declaration/family links to a
  fixed point, retaining separate consumer/culture slices and external coverage
  gaps. It does not connect unrelated equal names. Incompatible shared token
  bindings are explicit unavailable occurrences without replacements. Focused
  solution/target checks pass 12 per framework in Iteration12/SolutionFamilies;
  the subsequent shared-conflict cases pass four per framework in
  Iteration12/SharedSolution. The first build lacked a test namespace import;
  it was corrected before these successful runs. A stalled shared-compiler
  invocation was cancelled; these runs use UseSharedCompilation=false.
- Saved all-culture capture reevaluates active configuration/platform membership
  and metadata separately for each configured culture, also retaining the selected
  consumer culture. Failed culture evaluations become explicit coverage findings;
  cancellation still propagates. Import text is captured and replayed in a private
  MSBuild collection so parameter locations and membership describe exactly the
  attached XML bytes. A newly discovered import or changed captured text rejects
  the request instead of pairing stale origins with newer text. Ordinary analysis
  does not perform or retain this request-only capture.
- All 27 focused host capture checks pass in
  Iteration12/CapturedCultureDocuments (net472), including project/import overlays,
  condition-dependent culture membership, partial culture coverage, exact import
  text/locations, changed import discovery, saved-input preservation and cancellation.
  Earlier culture-only foundation passed 25 in Iteration12/CultureCapture.
  All 56 isolated packaged SDK bridge checks pass in Iteration12/ProjectOriginBridge;
  project parameter source path/span/version survive domain unload while the host
  remains free of compiler assemblies. These are contract-8 regression checks;
  contract-9 reference transport and both desktop MSBuild host gates remain pending.
- Latest full compiler regression checks pass 827 net472 / 829 net8.0 in
  Iteration12/CompilerFoundation, using the current built assemblies after the
  focused checks. The changed documentation paths and diff were reviewed.
- The public FindReferencesSnapshots and PreviewRenameSnapshots APIs and detached
  host transport now use analysis contract 9 together. Captured request creation
  supports cancellation, library portable ownership and exact project/import text.
  Search results and rename previews expose the contract/tooling fingerprint;
  previews retain the snapshot-bound target/context and proposed name. Batch search
  binds multiple independent targets in one pass, supporting solution identity
  propagation and shared-token probes without retaining ordinary-analysis indexes.
  Analyzer 5, library 3, export 1 and semantic definition fingerprints are unchanged.
- The isolated SDK worker constructs only captured compiler inputs and copies
  detached immutable declarations, occurrences, spans, diagnostics, groups and
  replacements. It rejects stale request stamps before preview. Selected resource
  and SDK-reevaluated project overlays are covered through the real package bridge;
  no compiler objects or implementation assemblies enter the VS host. Contributor,
  SDK and extension documentation, contract fixtures and repository guidance now
  describe contract 9. Historical iteration records above retain their original
  contract versions and acceptance evidence.
- Full contract-9 compiler suites pass 828 net472 / 830 net8.0 in
  Iteration12/Contract9Compiler. Isolated bridge checks pass 63 net472 in
  Iteration12/SolutionReferenceBridge, including batch transport, transitive
  cross-consumer/culture families, unrelated equal names, shared consumers with
  incompatible bindings or captured text, source versions, partial groups,
  reevaluated project metadata, cancellation and old-contract isolation.
  Earlier transition checks passed 57 in Contract9BridgeFoundation and 60 in
  ReferenceBridge before the final solution/batch additions.
- The host solution search coordinator follows only compiler-issued declaration
  identities and family edges. It retains consumer slices and coverage gaps;
  shared token checks send exact compiler-issued spans back to each consumer's
  SDK rather than interpreting text. Snapshot changes or failed revalidation
  invalidate the search. Its request owner must still supply complete solution
  capture and revalidate membership/toolsets/all inputs before publication.
- Packaged SDK acceptance passes five cases per test framework using .NET MSBuild
  in Iteration12/Contract9Package. The public reference/rename consumer fixture
  verifies inheritance uses, compiler-issued declaration identities, immutable
  versioned edits, excluded groups, cancellation and no captured-path file access.
  Desktop MSBuild acceptance passes five cases each with VS 2022 and VS 2026 in
  Iteration12/Contract9Desktop2022 and Contract9Desktop2026, sequentially. The
  previous MSBuild host environment setting is restored after each run.
- Generated-definition regression checks pass via eng/test-alarm-collections.ps1:
  AlarmCollections/7dccf02ca9dc4af29727446b593943ac, 119 disabled / 117 enabled /
  13 minimum-count checks per framework. The fixture now links the detached
  reference result types and reader alongside the existing analysis transport.
  These checks preserve procedure capability guards and collection metadata under
  analysis contract 9.
- The combined final bridge/capture run passes 90 net472 checks in
  Iteration12/Contract9BridgeAndCapture after contract-shape constructor/target
  checks were strengthened. Package checks exercise actual restored payloads;
  host checks additionally verify detached transport/domain unload and shared
  compiler-issued token probes. No native IDE interaction was needed for these
  checks; interactive acceptance remains iterations 13–14.
- The packaged owning-library/read-only-projection reference check passes in
  Iteration12/ReferenceLibraryBridge (one net472 case). It verifies that the
  library's portable source ownership survives detached transport, imported
  declaration identities match the owning project, related consumer assignments
  join the family, package declarations stay immutable and proven consumer edits
  remain eligible only with partial-result acknowledgement.
- Joint multi-target rename now validates one selected overlay for all participating
  compiler-issued families in a consumer, retaining every target in ReferenceSearches.
  It rejects incompatible names/kinds, coordinates a shared token only when all its
  bound families participate, and leaves colliding families unchanged while proven
  edits elsewhere require acknowledgement. Compiler groups now identify individual
  physical replacements, so excluding one incompatible token does not discard other
  proven changes in the same file; the host can aggregate groups for presentation.
- Batch target lookup now indexes compiler-issued declaration/location/token spans.
  A prefix-end interval index handles overlapping token uses without scanning the
  complete index per probe. Family payloads share immutable declaration/occurrence
  arrays in compiler batches and detached transport, including after domain unload.
  Focused batch checks pass 11 cases per framework in Iteration12/BatchPayloadSharing,
  including 1,000 probes with preserved targets and shared payload identity. Focused
  joint/selection/reference checks pass 45 per framework in Iteration12/JointRename.
- Reference occurrences retain their decoded token Spelling. Case-only renames use
  each actual spelling, preserve already-correct XML entities and avoid no-op edits;
  an existing independent/shadowing name in an insensitive namespace does not become
  a new collision solely through case normalization. Full compiler acceptance passes
  835 net472 / 837 net8.0 in Iteration12/JointCompilerFinal. The initial full run
  used an incorrect unassigned-property assertion for a deliberately excluded
  declaration edit; corrected it to the actual unknown-property diagnostic ALM0007
  before the successful full run.
- Added frozen reference-project and solution input capture. Known selections carry
  active configuration/platform; missing selections become coverage gaps instead of
  guessed defaults. Every configured culture and selected culture is evaluated.
  Reachable saved project dependencies are discovered before artifact creation, so
  an invalid library cannot hide other reachable projects. Consumer ALMX buffers
  participate, dependencies remain saved, and resources/lookups/configuration/analyzer
  inputs retain saved rules outside the private overlay. Captures include exact
  project/import XML, portable library ownership and compiler hashes for every
  involved project toolset. Comparison checks buffers, bytes, configuration,
  membership, references, package assets/artifacts and toolsets. Active CPS membership
  discrepancies are explicit coverage gaps; proposed overlays intentionally differ
  from CPS and require original-input revalidation by their request owner.
- Host capture/transport verification passes 69 checks in Iteration12/JointBridgeAndCapture.
  Solution input capture passes eight cases in Iteration12/SolutionInputCaptureFinal,
  including all cultures, missing contexts, active membership, virtual overlay
  inputs and an invalid reachable library. The first run caught InvalidDataException
  escaping the coverage boundary; added it to the explicit capture-failure filter
  and verified continued dependency discovery. Final combined bridge/input checks
  pass 100 net472 in Iteration12/JointSolutionBridgeFinal, including decoded spelling,
  exact-token groups, joint transport and shared 1,000-target payloads. New async test
  methods follow the host threading analyzer naming convention.
- Investigation for the remaining project-declaration gate: the pinned MSBuild
  evaluator's GetItemProvenance(string, itemType), with RecordEvaluatedItemElements,
  reports applied Update operations, including overridden metadata updates, and
  excludes false-conditioned/unmatched updates. This was verified with the local
  MSBuildProvenanceProbe under Iteration12; its API XML comments still describe
  Update as unimplemented. Capture these evaluator-issued name origins on demand
  so a rename includes statically bound project parameter Update names. Do not
  identify project usages through XML name matching; preserve explicit unavailable
  findings for dynamic/ambiguous operations and retain ordinary saved-input rules.
- Project item-name provenance now captures evaluator-issued Include and applied
  Update origins on demand, including statically bound case aliases. Dynamic or
  ambiguous origins remain searchable with explicit unavailable edits. Ordinary
  evaluation does not record provenance. Focused host checks pass 81 in
  Iteration12/ProjectBindingOrigins; full compiler checks pass 836 net472 / 838
  net8.0 in ProjectOriginCompiler and RenameApplicability. Snapshot identity now
  includes diagnostic policy, captured EditorConfig, analyzer identity and project
  binding origins. Reference occurrences carry compiler-issued source-token and
  declaration identities across owning libraries and imported projections.
- Added the native solution catalogue for active configuration/platform mappings,
  including unloaded projects and explicit unavailable/excluded selections. It
  listens for solution/configuration/project registry changes. The first compile
  exposed the native FindActiveProjectCfg array signature; corrected it. Combined
  host build/capture checks pass 82 in Iteration12/ProjectBindingsAndCatalogue;
  this is not evidence of native catalogue behavior on either IDE host.
- Added solution physical edit selection, deduplication across cultures, exact
  replacement exclusions, shared binding conflicts and compiler-issued ambiguous
  or colliding family impact. Physical IDs include captured text and snapshot
  identities; changed names and stale selections are rejected. Proposed overlays
  remain private. Four focused bridge/selection checks pass in
  Iteration12/PhysicalRenameSelection; five checks pass in PhysicalRenameValidation,
  including exact selected overlay validation and actual partial-result diagnostic
  differences. These runs preceded the final request-session integration.
- Added request-owned session coordination, complete proposed-solution recapture,
  SDK evaluation of proposed diagnostics and compiler token probes after edits.
  Original inputs are recaptured before publication; navigation and Apply must
  call the same revalidation. Watchers/catalogue events cancel obsolete operations.
  The first request-session test failed through unexpected watcher cancellation
  while opening its SDK worker (ReferenceSession: five passed, one failed).
  Added observed-path diagnostics; investigation and the corrected acceptance run
  remain pending. Do not treat this lifecycle gate as successful yet.
- The watcher investigation identified a directory Changed event for the extracted
  SDK's tasks directory during worker opening. Directory timestamp notifications
  previously invalidated every selected descendant. Changed-directory timestamps
  are now ignored while file changes and directory create/delete/rename events
  retain their invalidation behavior. ReferenceSessionDiagnostics passed four
  checks and reproduced the single session failure with its observed path before
  this correction. ReferenceSessionWatcherFix passes all five focused checks after
  the correction, including full session capture/search/preview/revalidation,
  catalogue cancellation, a missed buffer event, exact partial-result validation,
  shared physical selection and owning-library collision impact. A colliding
  consumer prevents edits to the contributing library tokens while independent
  proven consumer edits remain available with acknowledgement. The session test
  confirms previews never modify the saved source. These are host tests, not
  native command or buffer-Apply acceptance.
- Structured ambiguity is now part of both target results and declaration data;
  post-edit probes distinguish ambiguous bindings without reading message text.
  Full compiler acceptance passes 837 net472 / 839 net8.0 in
  Iteration12/StructuredReferenceAmbiguityFixed, including a translated resource
  token bound to independent parameter families in two alarms. The first attempt
  used the wrong namespace for the test's ResxTextSnapshot; fixed that test-only
  compile error before the successful run.
- The integrated host run passes 103 net472 checks in
  Iteration12/IntegratedReferenceSession, covering SDK bridge isolation, frozen
  captures, evaluator provenance, resources and evaluation-watch recovery after
  session integration. Diagnostic comparison now retains physical origins and
  adjusts known locations through approved edits, so fixing one file cannot hide
  an identically worded new diagnostic in another. Owning-buffer and saved-library
  contexts retain distinct captured identities without displaying evaluator globals.
- Full request-session library/project overlays pass seven focused checks in
  Iteration12/LibraryOverlaySessionFixed. They recapture owning buffers and saved
  dependency contexts, recreate the library privately, validate every consumer and
  prove that project Include plus case-aliased Update parameter bindings are
  reevaluated together. Imported immutable projections of tokens edited through
  their owning project no longer create false skipped findings. Actual immutable,
  excluded or conflicting tokens remain explicit. The initial library case found
  a null assets path for a project with no restored packages; absent optional
  watcher paths are now omitted before watcher creation. No source or project file
  is changed by capture, preview or proposed-result validation.
- Final package checks pass five cases per framework in
  Iteration12/FinalReferencePackage. Both desktop hosts pass five cases each in
  FinalReferenceDesktop2022 and FinalReferenceDesktop2026, run sequentially with
  the previous host environment restored. Generated-definition acceptance passes
  via eng/test-alarm-collections.ps1 at
  AlarmCollections/c29bc9f5d82e44b4b911545deb69b543 (procedure-enabled, disabled and
  minimum-count variants on both frameworks), using the final detached contract-9
  reference shapes and reader (119 disabled / 117 enabled / 13 minimum-count
  checks per framework).
- Added ownership handling for format-3 library project parameters, whose imported
  declarations intentionally have no physical location. Compiler declaration IDs
  connect that projection to the editable owning declaration; the host neither
  guesses a project span nor changes the library format. The first final host run
  passed 105 and failed one new assertion because its child application context
  lacked a project parameter assignment: library project defaults do not flow
  into that child's context, and the compiler correctly reported unbound P. The
  ownership test now inspects the defining library projection; the actual unbound
  coverage behavior is preserved. FinalSolutionReferenceHostFixed passes all 106
  net472 host checks, including the corrected ownership scenario.
- Completion: public opt-in APIs and immutable contract-9 transport, exact source
  targeting, active/all-culture solution/dependency capture, coverage, physical
  selection, private project/resource/library overlays, proposed diagnostics,
  cancellation and original-input publication revalidation are implemented. The
  compiler, focused host, packaged desktop and generated-definition gates above
  pass. Diff/contract/documentation consistency reviewed; no generated evidence
  files are included in the iteration commit.
- Planned remaining gates: native solution catalogue mappings, References/rename
  commands, navigation, private-draft blocking and buffer Apply integration on
  both IDEs belong to iteration 13. Full integrated/native/tutorial/performance
  acceptance belongs to iteration 14. No end-to-end References/Rename UI acceptance
  is claimed by this iteration. Both later iterations remain authorized.

Iteration 13 — completed:

- Implement a References window over detached compiler families, grouped by
  declaration, physical source and consumer context. Keep immutable, unavailable
  and ambiguous findings visible; page large result sets and retain source links.
- Route designer and Alarm Summary selections through compiler-owned declaration
  locations or explicit compiler-resolved value targets. XML commands use the
  captured caret/token and selected consumer. Unsupported tokens retain native
  fallback; an ambiguous shared XML owner requires an explicit selection.
- Present rename changes with before/after text, physical selection, coverage and
  actual diagnostic differences. Changing the name/selection invalidates approval;
  acknowledgement is never copied implicitly to another preview. Apply requires a
  current validated preview and explicit acknowledgement where needed.
- Open affected editable buffers before the final reviewable preview and capture
  their actual transaction versions. Saved resource/dependency analysis rules stay
  unchanged. Reject incompatible buffer text; do not make a source save implicit.
  Query Edit and final input/version checks precede linked mutation. Cancellation
  of obsolete search results by the rename's own buffer changes must not cancel
  its transaction; explicit application cancellation still rolls back all edits.
- Reuse iteration 9's production linked-edit coordinator and native adapter, with
  complete-set preflight, rollback and one-step Undo/Redo. Native checks on both
  hosts must cover routing, drafts, shared ownership, coverage/partial selection,
  freshness and keyboard/accessibility before this iteration is successful.
- Ship the feature article and documentation coverage with the UI. Full tutorial,
  broad native/library/fork acceptance and performance measurements remain 14.

Actual work and checks:

- Added the Alarmlist References tool window registration/menu, grouped result
  presentation, compiler-token-based owning-source grouping, explicit coverage,
  100-finding presentation pages, source links, Enter navigation and live status.
  Added a read-only rename review with before/after excerpts, editable physical
  change selection, conflict display, diagnostic/coverage sections and explicit
  acknowledgement. A changed name/selection clears approval; late cancelled
  responses cannot publish an old review. These controls are not yet connected to
  all designer/Summary/XML invocation routes.
- ReferenceResults passes three model checks in Iteration13/ReferenceResults.
  ReferencesAndReview passes seven model checks for grouping, paging, stale links,
  partial-selection revalidation, acknowledgement reset and conflicts. ReviewLifetime
  passes 18 model/session/capture checks, including independent request cancellation
  and buffer-opening comparison: identical bytes allow a new review against actual
  buffer versions, but never reuse an already approved snapshot.
- ReferenceRenameApplication connects an exact validated proposal to iteration 9's
  linked-edit coordinator. It captures actual buffer versions only after exact
  text comparison and binds those snapshots to that proposal object. Acknowledgement
  is checked again at Apply. ApprovedBufferApplication passes ten checks, including
  real packaged compiler proposals, complete-set Query Edit reload rejection,
  cancellation rollback and the distinction between the rename's own obsolete
  search capture and explicit transaction cancellation. This is not native Apply
  integration evidence yet.
- ReferencesRenderingAndLifetime passes 23 checks, including actual WPF rendering,
  grouped findings, bounded pages, Enter navigation, live-status controls and the
  acknowledgement checkbox disabling Apply after a selection change. These tests
  use the existing isolated STA harness, not either IDE's native command route.
- Added shell search/navigation coordination over selected consumer contexts,
  draft blocking across captured open ALMX buffers and revalidated SDK-owned source
  navigation. Navigation reuses physical/project-library/read-only package helpers.
  Disposed sessions release captured inputs and reference results after pending
  work exits. The shell-foundation build reported threading/lifetime analyzer
  warnings; cleanup now uses package-tracked async work in accordance with
  [VSSDK007](https://microsoft.github.io/VSSDK-Analyzers/analyzers/VSSDK007.html),
  and window entry points assert the UI thread. ReferenceSearchShellFoundation
  passed 23 checks before the warning fixes; ReferenceSearchShellLifetime passes
  all 23 after them with a clean build. It additionally verifies SDK-owned physical
  source/project navigation and released session captures. Native MEF invocation
  and native command behavior remain unverified until their acceptance below.
- Remaining at the shell-foundation check: complete compiler-resolved value/symbol choice, designer/Summary/XML
  commands with native fallback and explicit shared consumer selection; connect
  buffer opening, Query Edit and final Apply; enforce package source immutability
  for captured import/raw-source cases as well as existing library projections;
  finish feature documentation and both-IDE native acceptance. Iteration 13 is not
  successful or committed. Iteration 14 remains authorized and pending.

Further iteration 13 work (still not completion):

- Captured inputs now carry explicit read-only ownership for raw ALMX, RESX,
  lookup and imported project XML under evaluated package/SDK roots. Ownership
  participates in snapshot identity, selection/overlay validation and shared-edit
  checks. Navigation presents those sources read-only. Provider roots come from
  evaluated MSBuild package properties and resolved SDK imports, not cache scans.
- Compiler-owned value, source/alarm parameter-context and built-in selectors
  support designer/Summary invocation without parsing rendered text. Multiple
  bound families return detached candidates for explicit selection; no candidate
  is selected automatically. CompilerValueTargets passes 30 focused checks on
  each framework. CompilerSelectedValuesBridge passes 33 host checks covering
  transported candidates, parameter shadowing, ownership and package-import
  navigation. ReadOnlyReferenceFoundation previously passed 28 per framework;
  ReadOnlySourceOwnership passed 30 host checks before the additional bridge cases.
- After the user reconnected/unlocked the desktop, sequential Area=RenameTransaction
  checks passed on VS2022 (VisualStudioUI/TestResults/6f6e2f7dd90247b19fb5c3eb87a8a04c)
  and VS2026 (VisualStudioUI/TestResults/bccae52b0b7442f1bb0864160f54f8ec), one case
  each. These rerun the production linked-edit adapter's native transaction gate;
  they do not establish acceptance of the new References/Rename command routes.
- Added buffer identity to the linked-edit snapshots so identical text/version
  cannot hide a checkout reload. The native adapter accepts a current-buffer
  identity callback and checks capture consistency; final acceptance checks the
  accepted buffer identity as well as text. Added the reload-identity regression.
- Connected designer/Summary value actions to the shell search service, using
  compiler value locations and explicit binding context selectors. Source drafts,
  context changes and selected-value changes invalidate the request lifetime.
  Native command routing, full rename application integration, documentation and
  both-host acceptance of the complete UX remain pending. No iteration commit yet.
- ValueReferenceActions initially passed 61 and failed 24 checks with the same
  disposed cancellation-source error during editor cleanup. Invalidation is now
  safe after disposal; ValueReferenceActionsDisposedFixed passes all 95 affected
  host/WPF checks, including drafts, selected-consumer binding, stale entry
  identity and the buffer identity reload regression.
- ValueAndIdentityActions stopped at compilation because the native Query Edit
  service takes an Int32 file count. Corrected that adapter call;
  ValueAndIdentityActionsFixed passes all 97 affected host/WPF checks with a clean
  build, including routed keyboard commands and distinct alarm/group/value
  targets. The shell workspace reads live RDT buffer identities and queries the
  complete approved file set; full native Apply integration remains pending.
- CompleteCompiler passes all 841 net472 and 843 net8.0 compiler checks for the
  current selectors, context bindings and protected-source capture. PackagedBridge
  passes all five package-consumer checks on net8.0 and all five on net472,
  including the current protected-input API shape. Desktop SDK hosts and generated
  definition variants still need their current iteration reruns.
- Connected the Rename review to the production native workspace: compiler preview,
  exact-text editable-buffer preparation, safe whole-capture rebase and a new final
  preview. Snapshot-bound selected groups are never silently carried across a
  rebase. Apply performs complete-set Query Edit before asynchronous recapture,
  then consumes a one-use receipt for precisely that approved file set in the
  linked transaction. Buffer identity/version checks still bracket mutation.
- The rename's own input changes defer search invalidation until its transaction
  finishes; explicit cancellation retains rollback. Ending search results does not
  cancel the independent Apply request. Source navigation is disabled during
  preview/application so it cannot compete for the same SDK operation gate.
  A rebase refreshes result locations while preserving the active review.
- NativeRenameBackend passed 99 host/WPF checks but reported one UI-thread analyzer
  warning in the buffer identity helper. Added its explicit thread assertion;
  NativeRenameBackendClean passes 100 checks with a clean build, including receipt
  set protection, reload refusal, independent capture cancellation and active-review
  preservation. Rename and reference live regions expose their actual status text.
- Added Area=ReferencesRename, using the owned shell HWND and focus checks. It
  starts with a closed consumer, then checks compiler search, final preview,
  acknowledged Apply, unchanged saved bytes, literal preservation and one-step
  Undo/Redo. The first VS2022 run stopped before results
  (ReferencesRename/2022/47043113cf274aaaa5967523896d9af0); the diagnostic repeat
  (bcb473aeb9ac4638878b89cd2ca14c9e) proved a configuration mapping defect:
  IVsCfg's "Any CPU" display name was rejected against CPS/MSBuild's "AnyCPU".
  Reference capture now normalizes that standard alias and preserves custom
  platforms. Added conditional-input regressions. One follow-up stopped at a
  duplicate local variable in that fix; corrected it. Those attempts are not
  native acceptance evidence.
- Native search then passed, and closed-consumer preparation exposed ordinary
  analysis refresh cancelling the original view snapshot (4e22aff936624c089bfd1452181a3749).
  Added separate initial-snapshot and selected-intent lifetimes; only actual
  selection, draft, context and source changes cancel intent. The resolved compiler
  identity still requires full capture comparison before any rebase. Further
  diagnostic runs (d2aa8581f9864907a5ca9adc11c72b81 and
  b70a5db55db44d76a1237fd4dc16eafc) exposed a pending CPS context during recapture.
  Added typed readiness over catalogue events, with explicit cancellation and
  subscription cleanup. The first readiness attempt (dea09f8aba5549d88c8cdaf5c3f8975c)
  keyed the transient full globals too strictly: pending snapshots carry an older
  solution mapping. Readiness now identifies only the same selected active context;
  full globals, coverage, bytes, graph, ownership and toolsets are still compared
  after readiness before a new compiler search/preview. Obsolete links clear while
  buffers are being prepared.
- Area=ReferencesRename passed on VS2022 in
  VisualStudioUI/TestResults/22962e638b834c3fabe195c505a5ee13 (26 seconds; workspace
  ReferencesRename/2022/d1c7167cc7ef44e3aa6c9522ce0bd01b), and sequentially on VS2026
  in TestResults/dfafeefd03e94f87a965e45240662877 (27 seconds; workspace
  ReferencesRename/2026/94b3983399a1467a9ab4a5386813fcda). Both verify search from
  the designer with keyboard focus, closed-buffer opening, a reviewable compiler
  preview, required acknowledgement, unsaved two-file Apply, unchanged literal text
  and saved bytes, and one-step Undo/Redo. Both owned IDEs exited normally.
- NativeRenameRebase passes 104 affected host/WPF checks after the readiness and
  intent changes. Its single test-thread warning is now scoped/documented at the
  cancellation assertion, and the initial/intent refresh test is named so the
  focused reference filter includes it. NativeRenameRebaseClean passes all 105
  affected checks with a clean build, including explicit initial-snapshot versus
  resolved-intent cancellation and readiness subscription cleanup. This uses the
  extension binaries built and verified in the native runs, with
  BuildProjectReferences=false for the final test-only changes.
- Remaining iteration 13 gates include XML command routing/fallback and explicit
  shared consumers, direct Rename invocation, broader native symbol/partial-edit
  scenarios, feature documentation and final affected package/generated checks.
  This iteration remains in progress and uncommitted. Iteration 14 remains pending.

- DirectRenameActions passes 105 host checks. The enhanced native designer scenario
  also passes on VS2022 (TestResults/ea55b36cc1b648ad926546a5d2ee7b68), including
  direct Rename opening and focusing the new-name field without source mutation.
  Its dispatcher thread assertions and unload cleanup were subsequently tightened.
- Added compiler-owned XML token probes, distinguishing unsupported literal/markup
  positions from recognized unbound or invalid semantic tokens. Caret-at-end and
  fragmented XML token search preserve comments/markup and refuse unsafe edits.
  XmlTokenCompilerFull passes 850 net472 and 852 net8.0 checks; the latter reports
  the existing TestHelper.cs Windows apartment-state platform warning.
- XmlReferenceCommandsFoundation passes 108 affected host/WPF checks, including
  detached token availability, saved auxiliary-version mapping after whole-text
  equality, physical membership and native command IDs. Native XML routing is
  under validation. Its command filter preserves standard file/project Rename,
  uses compiler token decisions for semantic fallback, and requires explicit
  shared ownership. Published identity results survive caret navigation; source
  and consumer changes still invalidate them. New commands do not retain COM
  argument pointers or implicitly commit drafts.
- Extended Area=ReferencesRename with native XML Find All References and Rename
  at an inheritance token's end. The first attempt (workspace db0c8f07c8a5470da494952230cf02f0)
  stopped because the fixture requested a selection from a designer view. The
  next (ce6763b2197a4cd794d5d2067cf1cb55) timed out without XML results;
  its screenshot showed multiple views and the visible caret at offset zero.
  The fixture now selects and asserts the active view's caret after focus.
  A subsequent run (4f4ca8a89f3d472992bdc52c21dff724) stalled in native
  Solution.SaveAs during initial project creation, before the scenario. Captured
  owned IDE/test-host stacks under XmlCommandHang/XmlCommandTestHost and stopped
  only that verified test IDE; its RPC failure is not feature acceptance evidence.
  Status polling now uses project membership only, without resolving RDT ownership
  or constructing an analysis adapter. Explicit command invocation owns that work.
  Native no-argument routing also accepts synchronous empty BSTR/EMPTY/NULL variants
  from automation, while forwarding nonempty/by-reference arguments unchanged.
  The current native rerun and new argument regression remain pending.
- The next startup (9e2657a07d07493096564562bb53ebff) encountered the stopped
  test solution's recovery prompt. Restored only that isolated test backup with
  the computer-use skill; the run then timed out at its initial action because
  the experimental profile retained XML mode. The fixture now explicitly selects
  the actual Designer radio control instead of relying on an obsolete TabItem
  selector or a previous layout. These failed startup attempts are not acceptance.
- Area=ReferencesRename now passes on VS2022 in
  TestResults/8d6e65221cbf46c8adb60412fa1c0753 (35 seconds), including the entire
  designer transaction, direct Rename focus, native XML Find All References at a
  reference token's end, and native XML Rename focus. Saved bytes and literal text
  remain unchanged. The owned IDE exited normally. VS2026's same enhanced scenario
  is running sequentially; the new argument regression still needs its host run.
- The same complete Area=ReferencesRename scenario passes on VS2026 in
  TestResults/ee430154c6214d46813889f1427c0ebc (32 seconds). Both hosts now verify
  direct designer Rename and standard XML commands against the current source.
- XmlTokenPackagedBridge passes all five package-consumer checks on net8.0 and
  all five on net472, including unsupported/unbound token distinction and caret
  boundaries. Current desktop-host repeats are in progress.
- Added Area=SharedReferencesRename using the native Shared Project fixture.
  VS2022 passes in TestResults/497eb3312e224db6bf3afea54dfc1ab8 (44 seconds): no
  consumer is guessed for shared XML; explicit selection enables compiler-resolved
  localization-key Rename; ALMX and both language resources change together in
  unsaved buffers; one-step Undo/Redo and saved-byte preservation pass. The owned
  IDE exits normally. The VS2026 repeat is running sequentially.
- Both desktop MSBuild host repeats of XmlTokenPackagedBridge pass all five
  checks (VS2022 and VS2026). Generated collection runtime checks pass in
  AlarmCollections/e918a84f68d44c4abf901a38d167fffd: 119 procedure-disabled,
  117 procedure-enabled and 13 minimum-count checks on each framework, with
  matching export bytes.
- VS2026's first SharedReferencesRename run spent several minutes waiting on
  three idle native build workers during initial Build. Diagnostic stacks are
  under SharedXmlStartup2026/SharedXmlBuild2026. Stopped only workers whose parent,
  executable and node-mode arguments matched that owned IDE. The build recovered,
  all shared rename assertions passed and the IDE exited normally
  (TestResults/ae324d8bdea54d10b81c809d0f5ae1c5; 5 minutes 22 seconds). This records
  the setup intervention explicitly; a repeat will run with desktop build-host
  checks fully sequential. The fixture now also includes acknowledged partial
  project-parameter Rename when a shared use binds incompatibly in another
  consumer. That expanded scenario still needs acceptance on both hosts.
- The first expanded shared scenario fails at project-file preparation on VS2022
  (workspace 60df270fd8be4f8a8a38f1681551537d;
  TestResults/151b37a534eb46558c3d8fa6597e7696). Its native status reports
  E_NOINTERFACE before an applicable preview; the prior shared ALMX/resource
  transaction and Undo/Redo pass. Production preparation now explicitly opens
  project/import XML in the native XML editor, using the existing navigation
  pattern, before obtaining its editable buffer. COM preparation errors include
  the affected path. This fix still requires the current native rerun.
  Added a separate auxiliary fixture for lookup identity/reference and procedure
  group XML Rename, linked Undo and unchanged literal/saved bytes; both hosts
  still need its native execution. Iteration 13 remains in progress.
- Explicit XML opening did not resolve the preview failure (workspace
  17ee891966154290807b1df571c6d31a). Added recoverable preview exception logging
  to the native Activity Log. One diagnostic attempt
  (76c5223963f6409d858efd9f4dd11632) cancelled before the resource target resolved;
  the fixture now confirms the selected consumer's published resolved alarm
  before its XML request. The next run (4c6e833680f645b5804382cbcb970f54;
  TestResults/9003663f368543848ea02cde15a229de) identifies the actual failure:
  InvalidCastException at EnvDTE.Document.ReadOnly, through
  DteLinkedEditWorkspace.Buffer.IsReadOnly. The CPS project exposes text but not
  that automation property. Removed the unrelated explicit-editor workaround;
  production now supplies live ITextBuffer edit restrictions through the shared
  adapter's optional read-only callback, alongside current version and identity.
  The native transaction fixture retains its existing automation fallback. The
  current rerun must verify this fix before acceptance.
- The expanded Area=SharedReferencesRename now passes on VS2022 in
  TestResults/7d8f872188024fd88764800abc2358f4 (1 minute 6 seconds; workspace
  e01cbb9bd47e4c8a903fbbc79061968a), with normal owned-IDE cleanup. It verifies
  the shared ALMX/two-resource transaction and linked Undo/Redo, followed by an
  explicitly acknowledged partial North project-parameter rename. The shared
  use bound differently in South is skipped, South stays unchanged, expected
  diagnostics are reviewed, project edits stay unsaved and Undo restores them.
  VS2026's expanded repeat is now running sequentially. Auxiliary symbol fixtures
  and the final full affected host regression are still pending.
- The expanded shared/partial scenario passes sequentially on VS2026 in
  TestResults/a91af55074c64411b6187a0c6d9dbe7a (1 minute 1 second), with normal
  startup, all assertions and owned-IDE cleanup and no setup intervention. The
  current build includes explicit stale-solution guidance for an internally
  cancelled XML discovery instead of displaying a raw cancellation message.
- Area=AuxiliaryReferencesRename's first VS2022 attempt stopped at a fixture
  assumption that ItemOperations.OpenFile always returns a Window (workspace
  401b2f007c1e49639836e5fb3248eead). It now activates the opened Document, matching
  existing XML fixtures. The scenario passes on VS2022 in
  TestResults/ecaf7f8473a7494cb82c1e9bf1b65e81 (32 seconds) and VS2026 in
  TestResults/e3d6ba1506694f1ebca8514175020af8 (34 seconds): native lookup XML
  resolves its identity, Apply changes the lookup and ALMX use together, linked
  Undo restores both, procedure-group XML Rename/Undo works, and literal and saved
  bytes are preserved. Both owned IDEs exit normally.
- Value actions now include their field/parameter/collection context in accessible
  reference/Rename names. The existing routed-button test asserts these labels.
  Extended Area=ReferencesRename to invoke actual resolved Alarm Summary identity
  actions and verify its selected Child identity and direct Rename focus. Its
  current runs and the final full host regression remain pending.
- The complete Area=ReferencesRename scenario, including actual Alarm Summary
  selected-identity Find and direct Rename, passes on VS2022 in
  TestResults/6919d40724e34d42a42ec6bc501a77b8 and VS2026 in
  TestResults/ab29d8ce64e94a3b8d6f2768f8938332 (37 seconds each). Both retain the
  earlier designer transaction, keyboard action, unsaved/literal preservation,
  linked Undo/Redo and native XML checks, and both owned IDEs exit normally.
  The shared edit adapter's native transaction gate is being repeated before
  the full host unit regression and final iteration review.
- Area=RenameTransaction passes again on VS2022 in
  TestResults/7f7a7934104b4dabae43f011f4d54d45 (18 seconds) and VS2026 in
  TestResults/cc32f4beb4d1440e9bc264656c7fb897 (15 seconds), retaining four-file
  ALMX/RESX/lookup/project Apply, rollback and linked Undo/Redo coverage after the
  shared adapter changes. Both owned IDEs exit normally. Full host/WPF regression
  is now running. Reviewed the feature reference against the native examples,
  confirmed no unfinished placeholders, and verified 84 local documentation links.
- CompleteHost passes all 929 host/WPF checks, including current semantic XML
  argument handling, source ownership, SDK isolation, context/freshness, drafts,
  reference/Rename routing, accessible names, preview selection/acknowledgement
  and transactional regressions. The full build reports no warnings. Added the
  References tool-window registration assertion to the existing VSIX package
  test; full VSIX/package/project-evaluation checks on both frameworks are the
  final iteration-13 verification before its focused commit.
- CompleteVsix passes all 14 package/project-evaluation checks on net8.0 and all
  14 on net472, including References registration. Final diff, local paths, links,
  compatibility values and source-preservation behavior were reviewed. No generated
  outputs are included. Iteration 13 is complete: all designer/Summary/XML routes,
  compiler-owned token selection/fallback classification, explicit shared contexts,
  reference results, preview exclusions and acknowledgement, native unsaved Apply,
  rollback, linked Undo/Redo, navigation/freshness guards, keyboard/accessibility
  and feature documentation have their scoped checks. Current compiler suites
  pass 850/852 tests; the net8.0 build retains its existing test-helper platform
  warning. Package bridge checks pass on both task runtimes and both desktop
  hosts; generated procedure/collection variants pass on both frameworks; host
  regression passes 929 checks and VSIX integration passes 14 per framework.
  Native ReferencesRename, SharedReferencesRename, AuxiliaryReferencesRename and
  RenameTransaction pass on both IDE hosts, sequentially with the setup failures
  and corrected production defects documented above. No iteration-13 blocker
  remains. Iteration 14 still owns the guided tutorial, broader integrated
  library/NuGet/fork/unloaded scenarios, performance/allocation/cancellation
  measurements and final milestone regressions; no final acceptance is claimed.

Iteration 13 committed as be0e2e9. Working tree clean before iteration 14.

Iteration 14 — completed 2026-10-30:

- Add ordinary analysis/reference-search/rename-preview measurements over the
  existing 1,000/10,000-alarm, parameter and localization fixtures. Use the existing
  Release/x64 BenchmarkDotNet launcher and separate SDK-domain allocation counters;
  report actual timing/allocation samples without latency limits. Verify correct
  results, cancellation/recovery and bounded presentation with those workloads.
- Complete a guided native reference/rename tutorial and verify its concrete
  source and expected result. Add integrated saved/project/package/shared/unloaded
  and generated-definition coverage using existing fixtures and private feeds.
- Run native checks sequentially on both IDEs and affected full compiler/downstream
  suites on both frameworks. Preserve scoped iteration-13 evidence, document actual
  results/limits, and commit final acceptance only after these gates pass.

Actual work and checks:

- Added ReferenceBenchmarks over the existing four analysis fixtures, SDK-domain
  allocation sampling, and large-result/cancellation/recovery correctness checks.
  The Dry 1,000-alarm pipeline passes all three reports in
  AnalysisPerformance/7f2dfe1a04ca41a68f950a8081f822b7. This is execution/reporting
  validation only, not timing acceptance. Completed timing, allocation and
  correctness results follow.
- Short timing runs pass all four scenarios and twelve reports in
  AnalysisPerformance/e867e9d63ba44039a6736261ae8dbd18. SDK-domain allocation runs
  pass all four scenarios in 8b1f13a6d7f54e74822d0316302900a4. Release/x64,
  BenchmarkDotNet 0.15.8, .NET Framework 4.8.1 running net472-targeted tools on
  the i7-11700K host; two warmups/five iterations with standard outlier handling.
  These are warm captured-input operations, excluding native UI, live solution
  capture, debounce and cold SDK startup. Small-sample percentiles are descriptive.

| Existing fixture | Analysis mean / p95 ms | References mean / p95 ms | Rename preview mean / p95 ms |
| --- | ---: | ---: | ---: |
| 1,000 alarms / 10 files | 334.6 / 336.0 | 129.3 / 131.1 | 364.0 / 375.7 |
| 10,000 alarms / 100 files | 3875.1 / 3886.3 | 1372.5 / 1378.6 | 3945.5 / 3953.1 |
| 1,000 alarms with parameters | 573.8 / 577.2 | 345.3 / 348.2 | 924.1 / 926.7 |
| 1,000 alarms / 10,002 translations | 589.3 / 605.4 | 1227.2 / 1247.3 | 2718.8 / 2750.3 |

| Existing fixture | Analysis allocated MiB | References allocated MiB | Rename preview allocated MiB |
| --- | ---: | ---: | ---: |
| 1,000 alarms / 10 files | 79.40 | 102.08 | 261.14 |
| 10,000 alarms / 100 files | 793.12 | 1030.07 | 2626.18 |
| 1,000 alarms with parameters | 123.19 | 223.92 | 542.45 |
| 1,000 alarms / 10,002 translations | 149.03 | 409.56 | 939.14 |

  Allocation values average five paired SDK-domain samples after two warmups;
  they include compiler and detached-reader allocation totals, excluding outer
  remoting/UI work. They are not retained-memory measurements. Outer benchmark
  empty allocation cells cannot describe compiler allocation. Reference capture
  scans available related resource declarations, so the localization catalogue
  adds on-demand work even for the selected alarm identity; ordinary compilation
  still retains no reference index. No latency or allocation threshold is imposed.
- ReferenceCorrectness passes both large workloads in
  AnalysisPerformance/bc6a101789614a74b35f38929b944106. Both retain large immutable
  built-in findings while presenting 100, then 200 rows; invalidation clears them.
  All four scheduled requests actually cancel: localization reference/rename
  elapsed 51.9/64.8 ms; 10,000-alarm reference/rename 52.7/55.7 ms. The 25 ms trigger
  is a cancellation request, not an acceptance deadline. Pre-cancelled calls and
  subsequent successful search/analysis on the same SDK worker pass.
- Added the guided two-file tutorial and a native fixture reading its actual XML
  code blocks. It covers source navigation, fresh search, preview, unsaved Apply,
  literal preservation, linked Undo/Redo, Save and Build. Preparing that fixture
  exposed tree-item accessibility names using an internal type name. Reference
  tree items now bind their visible labels; a WPF peer assertion and the tutorial
  native checks must validate this improvement before final acceptance.
- ReferenceTreeAccessibility passes both actual WPF rendering checks, including
  the leaf automation peer's visible label. The first native tutorial attempt
  (workspace ReferenceTutorial/2022/7a776ff07dfe4b23b55f15b848ae01c3;
  TestResults/01abd47a392d429d884583e4bc49e8c1) times out acquiring automation in
  the fresh Alarmlist14Exp profile before any tutorial action. The fixture cleans
  up its owned IDE. Resumed in the established Alarmlist13Exp profile; this startup
  failure is not tutorial acceptance evidence.
- The established-profile tutorial run reaches its final saved-text assertion
  (ReferenceTutorial/2022/260ecad662d74fba8855170776fe0c15), where the editor adds
  the configured final newline. Starting files now include the code block's final
  newline, preserving exact assertions instead of ignoring text differences.
  The complete tutorial passes on VS2022 in
  TestResults/51cab894666b4552a7d4715f2bd259e8 (28 seconds), including real source
  navigation, fresh discovery, preview, unsaved Apply, literal preservation,
  linked Undo/Redo, Save and Build. The owned IDE exits normally.
- Extended existing project-library, package-definition, shared-example and
  generated-collection native fixtures with compiler-owned reference discovery.
  Project-library coverage adds unsaved coordinated producer/consumer Rename and
  linked Undo; packages/generated siblings must remain explicitly read-only.
  These integrated runs still need acceptance. Affected collection/library
  fixtures use the authoritative owned main-window handle for their UI checks.
- The first VS2026 tutorial attempt cancels during initial solution discovery
  (TestResults/f8655f2fe89f43b7942ca64c5c12b5f6). The designer exposed the raw
  cancellation message even though the originating selection remained current.
  It now reports changed captured inputs with explicit fresh-search guidance;
  actual caller cancellation remains silent and no request is automatically
  retried or retargeted. The focused DiscoveryCancellation regression passes.
  The complete VS2026 tutorial rerun passes in
  TestResults/521d72db571144eaa473365f41bacdf1 (30 seconds).
- VS2022's first integrated library run passes package-definition discovery but
  fails project-library fixture restoration because it selects a hidden Summary
  tab (TestResults/52373a39e74e4aef8e394da57c139059). The fixture explicitly
  reopens Summary. Its rerun passes all library assertions but stalls during IDE
  shutdown; diagnostic captures are in Iteration14/LibraryStall and
  LibraryRunnerStall. The runner is blocked in native COM, while the IDE's
  managed capture supplies no actionable frames. Stopping its verified owned
  build worker does not release it; stopping only the verified isolated IDE
  releases shutdown and the runner reports success in
  TestResults/5313715613da4d7ea7b31e6f8e3fe7f9. This intervened run is recorded
  separately from clean acceptance. The fixture closes the temporary library
  source after Undo and logs restoration/load transitions for the clean repeat.
- The clean VS2022 project-library repeat passes with normal owned-IDE cleanup
  in TestResults/3b039bb13edd434a88a519377c3a7558 (53 seconds): out-of-solution
  search, coordinated unsaved producer/consumer Apply and Undo, saved dependency
  refresh, unloaded source discovery and reloaded project refresh all pass.
  PackageDefinition2022 passed in the earlier two-case run; package artifact bytes
  remain unchanged and the compiler-owned alarm identity is explicitly read-only.
- VS2026 project-library and package-definition cases both pass with normal cleanup
  in TestResults/b1363c91765d4ea19d59e5441108b25c (two cases, 1 minute 32 seconds).
  The library case includes out-of-solution/unloaded discovery and linked unsaved
  Apply/Undo; package discovery reports the immutable definition without changing
  the packaged artifact. The real default shared example and final native rename
  regression groups are next, followed by generated-definition acceptance.
- The final VS2022 regression group's AuxiliaryReferencesRename case stalls in
  initial fixture setup with an idle desktop MSBuild node, before rename actions
  (workspace 0ecbb3c56fc14014b7fff497658c4c97). Stopping only the worker whose
  parent/path/node-mode identity matches that owned IDE releases the run; its
  assertions and normal IDE cleanup then pass. This intervened result is not a
  clean repeat. Owned native IDE fixtures now set MSBUILDDISABLENODEREUSE=1 in
  their private process environment, consistently with isolated preparation;
  no machine or extension policy changes. Fresh native acceptance must validate it.
- The VS2022 final native group passes all five cases in
  TestResults/320b7e751e2f428495d27befd736dccc (8 minutes 2 seconds):
  ReferencesRename, SharedReferencesRename, AuxiliaryReferencesRename,
  DefaultSharedExample and RenameTransaction. Only AuxiliaryReferencesRename
  needed the intervention above; its fresh repeat remains required. The current
  DefaultSharedExample workspace 10df030468b242a7b4a7f6db2bdb8c38 validates
  preparation twice, both shared consumer contexts, unsaved shared analysis,
  reopening and North XML/South JSON/Zenon keyword XML with unchanged alarm counts.
- AuxiliaryReferencesRename2022's fresh repeat with private worker reuse disabled
  passes in TestResults/5ac9e458660e4141a8327d0718798bbd (34 seconds), without
  intervention and with normal IDE cleanup. This supplies its clean final native
  evidence alongside the other four successful regression cases above.
- VS2026's final group passes ReferencesRename, AuxiliaryReferencesRename,
  SharedReferencesRename and RenameTransaction without intervention in
  TestResults/4918aad010ec4e76ae88d0948b8e920e. DefaultSharedExample completes
  preparation, shared/reference actions, outputs and first-session cleanup, but
  its restarted reference manager times out on a coordinate checkbox click.
  Restart/failure.png in workspace 8e1e096ff7c04f7a9674b8a599a5ec0b shows child
  console windows obscuring the reference dialog. The fixture now uses the native
  checkbox Toggle pattern, preserving initial-state, resulting-state, saved
  membership and source-byte assertions. Its repeat must pass before acceptance.
- The existing packaged custom-definition verifier now checks the completed
  public reference/rename APIs against its packaged fork compiler: alarm
  inheritance edits preserve a literal; retyped scalar and record-sibling
  definition names remain searchable/immutable; pre-cancellation and recovery
  work. These checks run on both frameworks as part of each consumer verification.
- The next VS2026 example repeat passes Toggle and closes the reference manager,
  but times out on the saved membership assertion in
  TestResults/8669eb2deaa643bd9522c390eb4496a8 (workspace
  e92b9f2a06554e048e0cca417af87b89). Its original shared/reference/output checks
  and first-session cleanup pass. The fixture now waits for CPS to acknowledge
  the project change (dirty state or already-persisted membership) before Save,
  instead of assuming dialog closure means the asynchronous edit is published.
  Exact saved membership and source-byte assertions remain required.
- The acknowledgement check exposes the native Toggle-provider limitation:
  visible checkbox state changes, but no project edit appears
  (TestResults/87e0c78e527847188ed336f5fcb78ac2, workspace
  5537f071a8fa4c1c8c1ea45fbb19397f). The fixture now foregrounds the owned native
  reference dialog, focuses its actual reference checkbox, checks focus and
  process ownership, and sends Space. The earlier Toggle-only approach is
  superseded; project acknowledgement and exact saved membership still gate success.
- The focused-input repeat passes DefaultSharedExample and ReferenceTutorial on
  VS2026 in TestResults/b33d812bc7854204a2ca2f97c5e3b5c0 (two cases,
  3 minutes 38 seconds), with normal cleanup and no intervention. Shared reference
  removal/restoration persists, Shared unload/reload and all three projects'
  selected-format outputs pass. The tutorial now asserts the concrete child's
  exact inherited Name/Code and preserved Description after Save/Build.
  VS2022 is repeating the same updated fixtures before generated-fork acceptance.
- The identical updated DefaultSharedExample and ReferenceTutorial fixtures pass
  on VS2022 in TestResults/27a88b8d0b434dd082fb1f5b269b62b3 (two cases,
  3 minutes 53 seconds), with normal cleanup and no intervention. Both native
  hosts now pass the exact tutorial values and the full real-example lifecycle.
  Generated collection forks are running with -Designer -Exports, both IDE paths,
  the established AlarmlistDefinitionExp profile and the VS2022 desktop build host;
  consumer acceptance will also repeat on the VS2026 desktop build host.
- The first procedure-enabled generated fork, col13/ec71a219, passes project/
  package consumers, analyzers, exports and packaged reference/rename APIs on both
  frameworks (consumer-checks/e7c9756a). Its native VS2022 run passes the new
  generated-sibling reference action but exposes disabled Explain actions after
  Clear (results-2022). Isolated diagnostic repeats reproduce this with available
  analysis, no drafts and populated targets. The loaded-action trace in workspace
  e713c09a7dac429fb8ca8a0a65921bdb records enabled=False, can=True: the routed
  command cached its disabled state before attachment. A requery on load releases
  the action and passes the excluded-value/Clear explanation. That diagnostic run
  later refuses culture input when WindowsTerminal owns the foreground; it is not
  final acceptance. Culture selection now foregrounds the owned IDE before its
  existing process/focus guard and native input.
- Added a WPF lifecycle regression that inserts the action group into an already
  loaded host. Before the fix all three actions remain disabled despite current
  targets (LateAttachedActionsLoadedHostBeforeFix); after ValueActionsControl
  requests command requery on Loaded, all three become enabled and private drafts
  disable them without source edits. ValueActionLifecycle passes all three checks,
  including both cancellation cases. Diagnostic logging exists only in the ignored
  isolated fork; fresh acceptance uses uninstrumented production sources.
- The fresh procedure-enabled fork col13/750267e3 passes packaged project/package
  consumers, analyzers, exports and the new public reference/rename checks on both
  frameworks (consumer-checks/c6174e83). Its uninstrumented VS2022 native collection
  case passes in results-2022 (1 minute 57 seconds), including generated sibling
  references, excluded-value explanation after Clear, all collection editing and
  caption/culture behavior. VS2026 and the procedure-disabled variant remain pending.
- That fork's first VS2026 run times out on its initial Count explanation before
  reference discovery (results-2026; workspace 24dedb76ff5a4ec1afe263d044b8ba63).
  Its capture exposes only the fixed accessibility label "Explanation status",
  hiding the actual message. Explanation status now has stable AutomationId
  ExplanationStatus and the TextBlock's current message as its accessible name.
  A WPF peer check verifies initial, unavailable and invalidated messages; the
  corrected expanded-control fixture passes in ExplanationStatusAccessibilityExpanded.
  Native explanation helpers report unavailable/obsolete status immediately rather
  than losing that reason in an eventual value timeout. Fresh generated acceptance
  is repeating with those diagnostics. ProcedureMode selects Enabled/Disabled for
  focused repeats; its existing default still checks Both.
- Procedure-enabled fork col13/6b4f5e18 passes packaged consumers/APIs on both
  frameworks and native VS2022. Its improved VS2026 capture identifies the initial
  Count request's actual outcome: "Inputs or the selected value changed. Explain
  this value again." (results-2026, 34 seconds). Obsolete work is correctly
  discarded. The startup fixture now explicitly verifies unchanged ALMX text and
  reactivates the same designer before issuing one fresh Count request. Only this
  initial, unchanged-source case opts in; other obsolete actions still fail the
  helper, and the fresh request must return the exact value and steps. No host
  automatic retry, guessed retargeting or weakened value/source assertion is added.
- Procedure-enabled fork col13/46937f6b completes all requested acceptance:
  packaged project/package consumers, analyzers, canonical XML/JSON/Zenon exports
  and public reference/rename APIs on both frameworks (consumer-checks/838d546f),
  plus native collection workflows on VS2022 (results-2022, 1 minute 56 seconds)
  and VS2026 (results-2026, 1 minute 47 seconds), with normal cleanup.
  Procedure-disabled acceptance is running separately through ProcedureMode Disabled.
- Procedure-disabled fork col13/2135ab08 passes packaged consumers, analyzers,
  all export formats and reference/rename APIs on both frameworks
  (consumer-checks/0aee6369), and native VS2022 (results-2022, 1 minute 56 seconds).
  Its first VS2026 run refuses culture input when WindowsTerminal owns the
  foreground (results-2026, 1 minute 13 seconds); no input is sent. Culture
  selection now waits up to ten seconds for owned-IDE foreground, then retains
  its final ownership guard. The focused VS2026 repeat passes the full workflow
  in results-2026-focus (1 minute 45 seconds), with normal cleanup and source
  preview templates temporarily unavailable as in the main fork driver. Only
  native runner helpers are refreshed; the accepted SDK/VSIX definition remains
  unchanged. No additional shells are launched during that interactive repeat.
  Both procedure variants now pass native acceptance on both hosts.
- Both accepted forks also pass the packaged project/package/analyzer/export/API
  consumer workflow using the VS2026 desktop MSBuild host: enabled
  46937f6b/consumer-checks/c2399660 and disabled
  2135ab08/consumer-checks/add8fb6d. Together with their VS2022-host runs, both
  task runtimes/frameworks and desktop build hosts are covered for each capability.
- Final standard-definition native regressions pass ReferencesRename and
  ValueExplanation on VS2022 in TestResults/b25398a2b8b34de5a72398ccf2973be3
  (two cases, 1 minute 39 seconds), after the command-lifecycle and accessibility
  fixes. VS2026 is running the same pair sequentially.
- VS2026 passes ReferencesRename in TestResults/e432d20b14dd46049bd00cd27e25a8d9,
  but ValueExplanation times out opening its initial en-GB language selector.
  failure.png in workspace ValueExplanation/2026/f3af98dc2e84495d8a65b0e3ef458d80
  shows the previous test's restored floating References window covering the
  toolbar. The explanation fixture now explicitly hides unrelated References/
  Summary panes before its mouse-based toolbar step. The focused repeat retains
  every original explanation, freshness, source/output and culture assertion.
- The focused ValueExplanation2026 repeat passes in
  TestResults/8b2b40a776234c4ca9053d80b158e3e1 (57 seconds), with normal cleanup.
  Final standard reference/rename and explanation regressions now pass on both
  IDE hosts. Full compiler, task, exporter, SDK, VSIX and host-unit regressions
  are running sequentially, followed by packaged bridge checks on both desktop
  build hosts. No native check remains pending.
- FinalRegressions/a13782fe98984871a1f7a267717d17e2 currently passes full compiler
  suites (850 net472 / 852 net8.0), task units (124 per framework), task integration
  (4 per framework) and zenon exporter units (9 net472 / 10 net8.0), with no skips.
  SDK, VSIX, host units and desktop packaged bridge gates remain in progress.
- The full net472 SDK suite also passes all 97 tests with no skips in that
  FinalRegressions run; the net8.0 SDK suite and remaining host/bridge gates follow.

Final iteration-14 acceptance:

All final regressions pass in
`artifacts/log/Debug/Iteration14/FinalRegressions/a13782fe98984871a1f7a267717d17e2`.
The sequential runner invokes `dotnet test <project> --framework <framework>
--logger trx --results-directory <suite-directory>` for the corresponding projects
in the repository build guide; host units use net472 only. Desktop repeats set
ALARMLIST_TEST_MSBUILD_PATH to each installed host and invoke the SDK test project
with `--framework net472 --no-build --no-restore --filter
FullyQualifiedName~PackageTests`. Per-suite commands, output and TRX are retained.

| Suite | net472 passed | net8.0 passed |
| --- | ---: | ---: |
| Compiler | 850 | 852 |
| MSBuild task units | 124 | 124 |
| MSBuild task integration | 4 | 4 |
| Zenon exporter | 9 | 10 |
| Full packaged SDK | 97 | 94 |
| VSIX integration | 14 | 14 |
| Extension host units | 933 | — |
| Packaged bridge, VS2022 desktop MSBuild | 5 | — |
| Packaged bridge, VS2026 desktop MSBuild | 5 | — |

Total: **3,139 tests passed, zero failures and zero skips**. Native acceptance
above separately covers both IDEs, the guided tutorial, shared/solution/project/
package/unloaded inputs, exact unsaved edits and linked Undo/Redo/rollback, active
consumer/culture selection and both generated procedure capabilities. Runs are
sequential; setup/focus/obsolete-request failures and their successful repeats
remain recorded above. No essential gate is deferred or blocked.

The [guided reference/rename tutorial](../../documentation/tutorials/find-and-rename-an-alarm.md)
uses the XML consumed by the native tests. Documentation review checks its UI
labels, exact final values, English templates and local links. Warm performance,
SDK-domain allocation and cancellation/bounded-presentation evidence are reported
above with their exclusions; they establish no latency limit or retained-memory
guarantee. Unknown external consumers, Git integration, file/project renaming and
schema-definition renaming remain outside the confirmed scope. Analysis contract
9, analyzer 5, library format 3, export contract 1 and semantic fingerprints remain
as accepted in iteration 12.

Final diff/whitespace review passes; 167 local links across the affected guidance,
feature/tutorial, backlog, contributor and milestone documents resolve. No
diagnostic probes, generated binaries, machine paths or unrelated edits are staged.

## Follow-up iterations 15–22 — semantic editing and RESX commands

Status: iterations 15 (aae945e), 16 (fa5e72f), 17 (5ac54d6), 18 (b37f56c),
19 (43a172f), 20 (53f0464), 21 (6f72650) and 22 complete with scoped acceptance.
Completed iterations 1–14 and their
acceptance evidence above remain unchanged. No branch creation, merge or push is
part of this follow-up. Work continues on dev/milestone-14.

### Confirmed behavior and design

Move value actions into context menus throughout designer and Alarm Summary,
retaining existing edit, assignment, inheritance and navigation commands. Menus
capture their owner and target and close on obsolete source/context/selection.
Mouse, Shift+F10 and Menu-key invocation never commits a private draft, including
when another parameter row is targeted. Identity menus remain distinct from title
expression menus. Explanation stays read-only within Alarm Summary.

Rename becomes one reusable dockable Alarmlist Rename window with its own session;
new reference searches cannot replace a review. Hide retains it, Cancel clears
approval, and a new request replaces idle/previewing work but never an Apply.
Preserve all existing binding, coverage, acknowledgement, Query Edit, freshness,
unsaved transaction, rollback and linked Undo/Redo guards. Pixel scrolling and
stable change-owned expansion state replace nested scrolling and visual-only
expansion state. Scrolling/recycling/resizing/selection cannot toggle expansion.

Use Visual Studio theme resources in every nested References/Rename control and
menu, including selection/focus/hover/disabled states and live theme changes.

Introduce immutable ExpressionSyntaxKind, ExpressionSyntaxToken and structured
reference/argument nodes following Roslyn's token/node separation. Preserve exact
text, spans, whitespace, escaping and XML source mapping. Incomplete/malformed
syntax has missing/invalid tokens; it is never executable by recovery alone.
Evaluation, binding, explanations, references and classifications share this one
compiler parser. Keep current grammar, case rules, diagnostics and fingerprints.
The host receives detached classifications, never compiler syntax or bindings.

Reuse the Visual Studio editor for designer expressions, with a dedicated content
type and the same classifications used inside supported XML expressions. Preserve
raw/resolved display, drafts, completion, multiline input, clipboard, accessibility
and Undo/commit behavior. Target tokens under pointer/caret, including nested
arguments; Go to Definition, Find All References and Rename use compiler-owned
identities. Explain this value targets the containing value. Resolved output and
escaped expression-like literals are not source expressions. Private drafts may
be colored lexically but block semantic operations until commit/cancel.

RESX XML key declarations already have a command route and need direct acceptance.
The originally confirmed standard resource-table commands require supported,
unambiguous resource/key/context and pending-edit detection; never infer identities
from displayed row text, implicitly save or use production UI automation/private
reflection. Both native feasibility probes fail the selection/pending-edit
prerequisites through the tested public services. On 2026-11-14 the user explicitly
chooses: "Defer standard-table commands and finish XML/designer support."
Standard-table Find All References/Rename is therefore deferred, while direct RESX
XML commands remain in scope. Keep actual failed-gate evidence and the vendor
limitation visible. A future table implementation must still prove selection,
pending edits and preview/unsaved Apply/linked Undo/Redo synchronization on both
IDEs; completion of this revised follow-up makes no claim about that surface.

Iterations 15–18 retain analysis contract 9. Iteration 19 activates contract 10 in
compiler, SDK worker and extension together. Preserve analyzer 5, library format 3,
export contract 1 and semantic definition fingerprints. Ordinary builds/analysis
retain no reference indexes; classification caches belong only to current views.
Saved resource/dependency rules, shared explicit consumers, version/identity checks
and cancellation remain in force.

### Numbered implementation and acceptance gates

Each iteration depends on its predecessor and receives behavior tests,
documentation, actual evidence and a focused commit before the next begins.

| Iteration | Deliverable | Required acceptance |
| --- | --- | --- |
| 15 — Context menus | Replace inline value/identity actions with shared accessible menu attachment, exact owner/target routing and draft/freshness guards. | Host WPF routing, keyboard, readonly values, stale target, cross-row draft and cleanup tests; affected native explanation/reference scenarios on both IDEs. |
| 16 — Dockable Rename and scrolling | Independent persistent tool window/session; pixel-scrolling virtualized list with change-owned expansion. | Independent search/review lifetimes, replacement/cancel/hide/reopen, stale inputs, partial Apply/Undo; oversized expanded rows, wheel/scrollbar/keyboard, recycling and resize on both IDEs. |
| 17 — Themes | Dynamic VS styles for References/Rename/menus and all nested states. | Light/Dark/High Contrast injected palettes, selected/inactive/disabled/recycled rows, live theme swaps and both native IDEs. |
| 18 — Expression syntax | Immutable typed tokens/nodes, exact mapping and missing/invalid syntax; migrate all compiler expression consumers. | Both-framework grammar/evaluation/diagnostic/reference/explanation regressions, malformed/incomplete/escaped/quoted/nested/placeholder/Unicode/entity/CDATA fidelity and generated definitions. |
| 19 — Classification API/bridge | Opt-in ClassifyExpressionsSnapshots for versioned document/value snapshots and context, detached kinds/spans/diagnostics, cancellation; activate analysis 10. | Both runtimes and desktop packaged hosts, incompatible tools, isolation/cancellation, draft lexical classification without saved-input changes or request-owned index retention. |
| 20 — XML and RESX | Shared classifications/Fonts and Colors; token commands and definition choices; direct RESX XML commands. Standard-table commands deferred by the user on 2026-11-14 after failed native feasibility. | Exact/nested token targets, XML fallback/mapping, direct saved RESX keys, shared/culture/resource ambiguity and native XML checks on both IDEs. Retain failed standard-table gate/vendor evidence separately. |
| 21 — Designer expression editor | Embedded VS editor, dedicated content type and shared classifications/commands with existing binding/draft/completion contract. | Typing, caret/selection, raw/resolved transition, completion, multiline/Unicode/clipboard/accessibility/Undo, token versus value menus, draft/stale guards on both IDEs. |
| 22 — Integrated acceptance | Solution/shared/culture/fallback/library/package/generated-definition coverage, tutorial and performance evidence. | Compiler/affected downstream both frameworks; packaged bridge both desktop hosts; native both IDEs; typing/large-document timings/allocations, cancellation and bounded caches, no solution searches per keystroke. |

Run WPF, native IDE and desktop MSBuild suites sequentially. Guard owned window
focus/readiness against RDP input, never close a user IDE and avoid extra shell
windows during native input. Report actual timing/allocation limits without
arbitrary latency thresholds. Native tests cover the user's oversized Rename
preview recording, scrolling items out/back and preserving expansion/inclusion.

Excluded: Roslyn fork, C# grammar integration, whole-ALMX syntax-tree redesign,
third-party resource editors, CodeLens, hover evaluation, additional refactorings,
unknown external consumers, Git integration and file/project/schema renaming.

### Actual follow-up evidence

Iteration 15 started from clean dev/milestone-14 at affbc2b. Both feature and dev
point to that accepted iteration-14 commit. No follow-up acceptance is claimed yet.

Iteration 15 implementation/validation in progress (2026-10-31):

- Replaced inline action stacks and alarm/group identity buttons with attached
  context menus on labels, editors, parameter rows and resolved values. Existing
  field/literal/assignment/text-editing menus retain their bindings and actions.
  Popup commands capture the owning view/value and reference-context generation;
  source/context changes and unload close the menu, and commands recheck the
  captured generation even after WPF popup closure. Accessible value descriptions
  advertise Shift+F10/Menu invocation without replacing existing help text.
- Right-button focus changes preserve drafts. Parameter menus refuse another row
  before its ordinary commit-on-selection setter, including valid private drafts.
  The native helpers now open actual menus; initial explanation uses Shift+F10
  and initial reference search uses Menu, followed by Enter with focus checks.
- Initial restricted restore failed with NU1301 (package host resolution). Restore
  with network access and NuGetAudit=false passed. A restricted test-host run
  stalled and was cancelled; the desktop-capable repeat ran normally. Initial
  focused tests caught early local menu initialization overriding parameter styles
  and duplicate popup sessions; deferred style initialization and session cleanup
  corrected both. Focused3 passes 7 tests, zero failures/skips.
- Full host suite passes 936 tests, zero failures/skips, in
  artifacts/log/Debug/Milestone14/Iteration15/HostFull (2 minutes 19 seconds).
  It includes new stale-after-close, unload, readonly Summary and valid/conflicting
  cross-row draft cases. Native runner build passes with zero warnings/errors
  after correcting its focused-element API call to the installed FlaUI version.
- Diff whitespace review passes; 108 local documentation/guidance links resolve.
  Native ValueExplanation/ReferencesRename acceptance on VS2022 has started;
  VS2026 follows sequentially. No iteration-15 acceptance or commit is claimed yet.
- Initial native pair fails in TestResults/a8486f9a2e684366909a53b8b1309967.
  ValueExplanation stops before its first menu: failure.png shows de-DE instead
  of the requested en-GB. The shared selector used UIA SelectionItem.Select then
  Collapse, which can cancel the native shell's pending choice. It now commits
  through a real choice click and awaits the selected culture, retaining the
  original resolved-value assertion. ReferencesRename exercises Menu-key search,
  preview/Apply and linked Undo/Redo, then stops looking for the former inline
  identity Rename name. Identity menus now preserve that accessible name and the
  helper recognizes it. Designer menu fixtures hide obstructing floating panes;
  offscreen values are revealed before input. The explanation fixture additionally
  tests a different-field menu during a private draft without a focus-commit.
- MenuFinal passes 13 affected host checks, zero failures/skips (10 seconds).
  The corrected native pair is running on VS2022; both IDE gates remain pending.
- The second native pair (TestResults/68c65b41cdf046a98227632aaa671743) gets through
  culture fallback, source freshness, readonly Summary, parameter explanation and
  cross-field private-draft assertions. It stops at a clipped procedure title:
  WPF reports usable offscreen coordinates and the old helper right-clicks the
  taskbar (retained failure screenshot). The next fixture's UIA focused-element
  readiness check then fails. The helper now hit-tests points inside the owned
  window, scrolls the actual ancestor viewport without focusing a draft, and
  reuses VisualStudioSession's Win32 process foreground check. No assertion or
  source-preservation gate was removed. Disabled marked controls also explicitly
  permit a context menu for readonly inspection. Runner build passes with zero
  warnings/errors; the third native pair is running sequentially on VS2022.
- The third pair stops at foreground guards before semantic actions
  (TestResults/c570ccd350bf4e1197722ffff8b825a9). The screenshot retains an extra
  console and the earlier shell/taskbar menu. Computer-use inventory was read,
  but Explorer app access timed out; no Explorer input was issued. The owned IDE
  launch now sets CreateNoWindow=true (its GUI remains explicitly shown). A
  focused explanation repeat still stops before culture input at the foreground
  guard (TestResults/feb11979dd50415db39b4e0ebc80bbe9); it cannot prove native
  acceptance. Desktop readiness has been requested. No guard was bypassed, user
  IDE closed, iteration committed or iteration 16 implementation started.
  Packaged extension checks are running independently while that gate waits.
- Packaged VSIX integration passes 14 tests on net472 and 14 on net8.0, zero
  failures/skips (Integration472 and Integration8 under the iteration directory,
  11 seconds each). Final affected-menu regressions pass 13 tests, zero
  failures/skips (MenuReviewed, 11 seconds), after preserving native/standalone
  menus when no semantic owner exists. Diff whitespace and 108 local links pass.
  The user reports all applications closed; native VS2022 acceptance is being
  repeated against that cleared desktop before the sequential VS2026 run.
- Cleared-desktop pair (TestResults/228011d2d2bc4f63bcd1436e99f573f2) passes
  ReferencesRename2022, including keyboard search, direct identity/Summary Rename,
  preview, unsaved Apply and linked Undo/Redo. ValueExplanation now passes the
  procedure-title menu and stops at the entry menu. Its screenshot shows the
  built-in TextBox editing menu: that class handler consumes opening when no
  explicit child menu exists. Marked value containers now prepare a real editing
  menu on such child text boxes before opening; existing explicit menus remain
  unchanged. Native repeats retain all original expectations.
- After that fix, both VS2022 cases pass, zero failures/skips, in
  TestResults/689f7092e20f450fa2dd051a0c5c5690 (3 minutes 18 seconds).
  This includes Shift+F10/Menu-key and pointer invocation, scalar/parameter/title/
  entry and readonly Summary actions, cross-field private-draft preservation,
  culture/input invalidation, physical-source navigation, identity/title
  distinctions, direct Rename, unsaved Apply and linked Undo/Redo. The matching
  VS2026 pair is running sequentially. Full host review after the final text-menu
  route fix and final diff/link review follow before the iteration commit.
- Both VS2026 cases also pass, zero failures/skips, in
  TestResults/6485193184904d71ad06a93b640df06e (2 minutes 53 seconds).
  The native menu gate is now passed on both supported IDEs. HostAccepted is
  running the full host suite against the final child-text-menu fix before commit.

### Iteration 15 — complete

Value/identity actions now live in context menus throughout the designer and
Alarm Summary. Explicit owner/target/context-generation capture preserves popup
bindings and refuses stale commands. Existing clipboard, literal, assignment,
inheritance and source menus remain usable. Keyboard and pointer invocation,
readonly Summary, valid/conflicting cross-row drafts, a different-field private
draft, unload and source/culture invalidation are covered. Ordinary compilation,
analysis contract 9, analyzer 5, library 3, export 1 and fingerprints are unchanged.

| Final gate | Actual result |
| --- | --- |
| Full extension host, final implementation | 936 passed, zero failures/skips; HostAccepted (2 minutes 19 seconds). |
| Packaged VSIX integration net472 / net8.0 | 14 / 14 passed, zero failures/skips; Integration472 / Integration8 (11 seconds each). |
| Native VS2022 ValueExplanation and ReferencesRename | 2 passed, zero failures/skips; TestResults/689f7092e20f450fa2dd051a0c5c5690 (3 minutes 18 seconds). |
| Native VS2026 same scenarios | 2 passed, zero failures/skips; TestResults/6485193184904d71ad06a93b640df06e (2 minutes 53 seconds). |
| Focused menu regressions after entry-route fix | 13 passed, zero failures/skips; MenuEntryFix (11 seconds), overlapping the full host suite. |
| Review | Diff whitespace passes; 108 local documentation/guidance links resolve. No unrelated changes, machine paths or generated artifacts are included. |

Host and integration commands: dotnet test on the corresponding VisualStudio
test project, --no-restore -v quiet -m:1 -nr:false --logger trx, with framework
net472/net8.0 for integration. Results are under
artifacts/log/Debug/Milestone14/Iteration15. Native commands use
eng/test-visualstudio.ps1 with the installed IDE path, -RootSuffix Alarmlist13Exp
and -Filter 'Area=ValueExplanation|Area=ReferencesRename', sequentially. The
retained progress log above records setup/input failures, actual product fixes
and successful repeats. No scoped gate is deferred. Broader generated-definition,
library/package and integrated tutorial/performance reacceptance remains in 22.
Iteration 16 (separate Rename lifetime/window and expansion-preserving pixel
scrolling) is next; its acceptance has not been claimed.

### Iteration 16 — in progress

Starting from clean dev/milestone-14 after aae945e. A Rename window owns its own
result/review and SolutionReferenceSession. A References action forks the frozen
immutable capture only after revalidation, installs independent watchers and runs
a new compiler identity search. Only a reference-counted source-intent lease is
shared; disposing References must not dispose that lease while Rename owns it.
Initial search cancellation remains separate from established source-context
intent. XML retains resolved request leases across subsequent commands and still
observes every leased source/context for invalidation. Hide/reopen preserves the
review; replacement is refused during Apply and cancels an old preview otherwise.
No iteration-16 acceptance is claimed yet.

- Added the independently registered Alarmlist Rename tool window, its View menu
  command, explicit compiler symbol chooser and References row context menu.
  References and Rename use separate watched sessions; replacing References or
  closing its result lease cannot cancel the retained Rename intent. XML keeps
  every resolved request alive only while an owning result lease exists.
- Preview expansion belongs to each change view model. The bounded virtualized
  list uses pixel scrolling; before/after text forwards wheel and Page Up/Down
  to that list. Selection validation preserves expansion, fresh preview resets
  it, and Cancel clears changes, approval and acknowledgement. Apply remains
  distinguishable until backend completion/rollback, preventing replacement.
- Product build passes with zero warnings/errors. Initial focused suite passes
  12 of 13: the fork test incorrectly expected false after catalogue invalidation,
  while the existing API deliberately throws cancellation. Corrected that
  assertion; the full host suite then passes 943 tests, zero failures/skips
  (Host, 2 minutes 22 seconds). A final reference-row identity/stale-command test
  and its neighboring suite pass 6 tests (ReferenceRows, overlapping the full
  suite). These are under artifacts/log/Debug/Milestone14/Iteration16.
- Packaged VSIX checks pass 14 tests on net472 and 14 on net8.0, zero
  failures/skips (Integration472, 14 seconds; Integration8, 11 seconds), including
  the new tool-window registration. Native fixture build passes. VS2022 native
  ReferencesRename, ReferenceTutorial, SharedReferencesRename and
  AuxiliaryReferencesRename are running sequentially before the VS2026 gate.
  They now address Rename independently and retain unsaved Apply/Undo assertions.
  The main scenario also checks dock/hide/reopen, a replacement Find while Rename
  is hidden, and native wheel scrolling over an oversized expanded preview.
- The VS2022 tutorial stops at its initial designer context-menu input because
  another process takes foreground focus after readiness/hit testing. The final
  guard refuses input (ReferenceTutorial/2022/e76da528b30a4e1b865a7a9d4aa2f10c).
  This is not native acceptance; retained evidence will be reviewed and that case
  repeated after the remaining sequential scenarios finish. No guard is bypassed.
- That batch passes SharedReferencesRename and AuxiliaryReferencesRename. Its
  tutorial screenshot identifies an inconsistent-line-endings dialog, so fixture
  XML extracted from Markdown now uses consistent LF endings. The new menu
  helper is also restricted to top-level windows of the owned IDE, matching the
  existing value helper rather than traversing unrelated applications.
  ReferencesRename subsequently stalls in fixture Solution.SaveAs, before any
  product operation. Stopped only its verified executable/PID/start-time instance
  to end the stalled case; its resulting RPC failure is retained in
  TestResults/bf433c98275c4d079bb86a76b363a52e. A first cleanup check refused a
  timezone-mismatched comparison; an exact UTC identity check passed before stop.
  Both failed cases require fresh acceptance; the successful resource/lookup cases
  do not establish the missing main-window/tutorial gates.
- Focused repeat (TestResults/235a5a404ded4e3b9e5be887ccd2ea5d) times out in
  tutorial IDE automation discovery before executing its corrected input. The main
  scenario reaches dock/hide and the independent replacement Find, then identifies
  a command-registration mismatch at View.AlarmlistRename. Its VSCT canonical
  strings now follow the existing AlarmlistPreview naming pattern (plain
  AlarmlistRename), so the shell supplies the View prefix. This production fix and
  the remaining scrolling/Apply assertions require native repeats on both hosts.
- VS2022 focused repeat passes both cases, zero failures/skips, in
  TestResults/c1cf5ded20c24881800d8d965e1a9635 (1 minute 38 seconds). This verifies
  References row/menu identity selection, separate dockable Rename, retained
  preview through dock/hide/reopen and a replacement Find, the corrected View
  command, oversized expanded before/after presentation, native wheel over the
  header and readonly text without folding or changing inclusion, Cancel, direct
  designer/Summary/XML Rename, unsaved Apply and linked Undo/Redo. The tutorial
  reads its normalized XML examples and passes navigation, preview, Undo/Redo,
  Save and Build. All four VS2026 scenarios are now running sequentially.
- The VS2026 batch passes the main ReferencesRename and auxiliary lookup/group
  case. Shared-key discovery correctly resolves Resource:Caption, then disables
  it after a late catalogue publication; the fixture must explicitly recapture
  that token instead of waiting for stale results to become editable. The shared
  fixture now verifies unchanged source/saved bytes before any such recapture.
  The tutorial pointer failure reveals a stretched heading's transparent glyph-only
  hit testing: blank space hits its unmarked parent. Marked TextBlock headings now
  have a transparent hit-test background, preserving explicit authored backgrounds.
  A real visual hit test plus the related menu/render/review suite pass 10 tests
  (HeadingHitTest, 4 seconds). The retained VS2026 batch is
  TestResults/78b4cce9175c41f4baaaae52f259ff34. Native repeats follow this fix.
- Final full host suite after the heading fix passes 944 tests, zero
  failures/skips (HostFinal, 2 minutes 20 seconds). Final packaged checks and
  sequential native repeats follow; no iteration-16 commit has been made.
- Resumed on November 14 on the same dev branch and uncommitted iteration. The
  previous package process handle is terminal/missing; its saved net472 report
  proves 14 passes. Final net8.0 package checks pass 14, zero failures/skips
  (Integration8Final, 17 seconds). Both installed IDE versions remain unchanged.
  All four VS2026 scenarios pass after the heading and explicit shared recapture
  fixes, zero failures/skips (TestResults/050e9cb68ab545abbb8c022cc9af67ee,
  2 minutes 47 seconds).
- Final row-routing review finds that a References command must also retain its
  row's consumer index and compiler symbol projection. A declaration discovered
  in another project must not be searched through the original project's SDK
  context. Rows now index their family's compiler declarations once, preserve that
  exact symbol/context through menu invocation and fork Rename into that context.
  Group commands choose a compiler-provided context represented by their rows.
  Added coverage for owning/read-only projections and different consumer cultures;
  the signature and native menu path are being revalidated before commit.
- RowContextsComplete passes 8 tests, zero failures/skips (8 seconds), including
  a real packaged-compiler library/consumer family. Its child declaration is
  unavailable in the original library context, resolves in its actual consumer,
  and produces all four coordinated edits through an independent fork. View-model
  coverage preserves each culture/consumer's exact readonly/owning projection.
  Full host and affected native/package checks follow this routing correction.
- HostRowContextsFinal passes the full 945-test host suite, zero failures/skips,
  in 2 minutes 22 seconds. RowOrderingFinal passes all 7 reference presentation
  tests (74 milliseconds), including the readonly consumer appearing first.
- Final routing revision passes the packaged VSIX suites on net472 and net8.0:
  14 tests each, zero failures/skips (Integration472RowContexts, 11 seconds;
  Integration8RowContexts, 14 seconds). All four VS2022 native scenarios pass,
  zero failures/skips, in TestResults/adb9a51b6b8549a58d3ec8db4b499462
  (2 minutes 52 seconds). The two VS2026 References-menu workflows affected by
  that routing revision are being repeated; its direct XML/resource/lookup
  scenarios passed in the preceding four-case run and their path is unchanged.

### Iteration 16 — complete

Alarmlist Rename is a separate reusable dockable window with a compiler symbol
chooser and its own watched solution session. Designer, Summary, XML and References
menus open it. References rows retain the exact compiler declaration and consumer
context. Independent source-intent leases preserve Rename when References is
replaced; actual context/input changes still invalidate both. Hide/reopen preserves
the review, replacement cancels preview work, Apply/rollback blocks replacement,
and Cancel clears approval and acknowledgement. Changes own expansion state in a
pixel-scrolling virtualized list. Oversized before/after text scrolls with the list,
and selection validation, recycling and resize preserve expansion and inclusion.
Marked identity headings also own their full pointer hit area.

| Final gate | Actual result |
| --- | --- |
| Full host suite | 945 passed, zero failures/skips; HostRowContextsFinal (2 minutes 22 seconds). |
| Packaged compiler/context routing | 8 passed; RowContextsComplete (8 seconds), including a child declaration unavailable in the original context and all four edits in its actual consumer. |
| Final reference-row ordering | 7 passed; RowOrderingFinal (74 milliseconds), overlapping the full suite. |
| Packaged VSIX net472 / net8.0 | 14 / 14 passed, zero failures/skips; Integration472RowContexts / Integration8RowContexts (11 / 14 seconds). |
| Native VS2022 all four scenarios | 4 passed, zero failures/skips; TestResults/adb9a51b6b8549a58d3ec8db4b499462 (2 minutes 52 seconds). |
| Native VS2026 all four scenarios | 4 passed, zero failures/skips; TestResults/050e9cb68ab545abbb8c022cc9af67ee (2 minutes 47 seconds). |
| Native VS2026 affected menu workflows after final routing correction | 2 passed, zero failures/skips; TestResults/1cf28877e5fd4120bcf893922906e2f9 (1 minute 5 seconds). |
| Review | Local documentation/guidance links resolve (33 checked); whitespace diff passes. No generated outputs or unrelated edits are included. |

Host/package results are under artifacts/log/Debug/Milestone14/Iteration16;
commands use dotnet test on the corresponding test project with --no-restore
-v quiet -m:1 -nr:false --logger trx. Package runs select net472/net8.0. Native
runs use eng/test-visualstudio.ps1, the selected installation, RootSuffix
Alarmlist13Exp and the Area filters ReferencesRename, ReferenceTutorial,
SharedReferencesRename and AuxiliaryReferencesRename. Native runs are sequential;
the final VS2026 repeat selects the two affected ReferencesRename/ReferenceTutorial
cases. ALARMLIST_VS_TIMEOUT_SECONDS=600 bounds only each owned fixture lifetime.
Earlier setup failures, actual fixes and successful repeats remain above.

Native checks cover dock/float/hide/reopen, independent searches, direct actions,
source/context staleness, resource and partial project edits, unsaved Apply,
linked Undo/Redo and the documented Save/Build tutorial. Host checks cover ignored
cancellation, lease disposal, fork isolation, expansion/inclusion/acknowledgement,
pixel wheel/Page Up/Page Down scrolling and recycling. Analysis contract 9,
analyzer 5, library 3, export 1 and semantic fingerprints remain unchanged.
No scoped gate is deferred. Theme states remain iteration 17; integrated
library/generated-definition/performance reacceptance remains iteration 22.

### Iteration 17 — in progress

Starting from clean dev/milestone-14 after fa5e72f. References/Rename will reuse
Visual Studio's public themed-control style keys, preserving their selection,
hover, focus and disabled templates. Shared fallback styles support isolated WPF
rendering with injected Light/Dark/High Contrast palettes. Overlay styles retain
bindings, commands, automation and virtualization while native styles resolve or
change through dynamic resources; no brush snapshots or global theme subscriptions
are retained. Reference tree items, rename rows, labels, inputs and popup menus
must all receive the theme, including recycled and inactive selected rows.
Required native theme checks remain on both IDE hosts, run sequentially.

References: the pinned VSSDK public VsResourceKeys documentation describes themed
control styles and its default-style dictionary; WPF dependency-property
precedence explains why a themed parent cannot fix child default-style colors.
See [VsResourceKeys](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.vsresourcekeys?view=visualstudiosdk-2022)
and [WPF property precedence](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/properties/dependency-property-value-precedence).
No iteration-17 acceptance is claimed yet.

- Added shared styles and a dynamic native-style overlay using the public VSSDK
  keys. It preserves the IDE's actual templates and triggers, keeps only layout/
  behavior setters above them and drops isolated fallback state rules once native
  resources resolve. It aborts queued work on unload and has no global event
  subscriptions. Lists use the ListView variants of the public native row styles,
  retaining ListBox selection/automation contracts, recycling and pixel scrolling.
- The first build catches SelectionTextBrush being unavailable in the net472
  reference surface. Isolated textbox selection now uses the matching system
  highlight pair; production retains native TextBox styling. RenderingInitial
  passes the three existing reference/review/scrolling checks (2 seconds).
- New palette tests initially referenced Shell.15.0 compile-time types, while this
  test project deliberately copies that assembly only for BAML/runtime keys. They
  now use the neighboring tests' public-resource-property lookup without adding a
  compiler reference. Palettes passes 7 tests, zero failures/skips (4 seconds):
  Light/Dark/High Contrast nested text, active/inactive/disabled rows, open menus,
  live brush swaps, expansion/inclusion/acknowledgement through recycling, late
  native-style replacement/removal and unload/reload behavior. The native fixture
  build passes with zero warnings/errors; actual Light/Dark IDE changes, restored
  settings and visual evidence on both hosts remain required.
- The first native VS2022 case successfully changes to Light and restores the
  original theme, then fails in its screenshot helper because the DTE tool-window
  HWND is unavailable after the theme transition. Capture now targets the stable
  named WPF surface. Its restored-Dark screenshot already shows readable nested
  reference labels and selected text, but this is not the full native gate.
  Retained result: TestResults/80669da5c8854c308df034e44612efeb.
- Explicit virtualizing panels protect layout from native theme defaults.
  PaletteVirtualization passes 10 palette/render/review tests, zero failures/skips
  (5 seconds), including a native-style substitute that attempts to install a
  non-virtualized panel. Native theme acceptance is being repeated.
- Stable-frame capture avoids recording WPF/DWM window fade transitions as theme
  defects. VS2022 theme and Rename regression pass two cases in
  TestResults/961184777c7a473aab1e735564975e36 (1 minute 24 seconds). Visual review
  still identifies light disabled/inactive state backgrounds, so this is not the
  final theme gate. The adapter now also reads the public default-style dictionary
  directly by control type without replacing our implicit layout overlays.
  NativeDefaults passes 11 focused theme/render tests (5 seconds); the repeated
  native pair passes in TestResults/15bc43dd3d544f1989c95830b1fb6d12
  (1 minute 18 seconds). Further review retains the live-theme state check.
- Added initial-disabled and implicit-style invalidation coverage. The first
  initial-disabled test incorrectly searches a not-yet-created visual tree;
  it now obtains the original implicit style from the control's public resources.
  DisabledInitialFixed passes all eight palette/native-adapter tests (4 seconds).
  The adapter also observes its own Style property through a WPF binding so a
  theme's implicit-style invalidation cannot silently discard the composed native
  style while retaining the same native resource identity. There are no global
  event subscriptions. StyleInvalidation passes all 11 focused theme/render tests
  (5 seconds). Both native hosts and final regressions remain required.
- The style-invalidation correction removes the light disabled Apply button in
  the actual Dark capture. VS2022 passes both final theme/interaction cases in
  TestResults/b3882aa40786408a96474aca8502c6ae (1 minute 45 seconds). Selection
  fallback colors now use the public TreeViewColors active/inactive pairs rather
  than window-caption colors. TreePalette passes the same 11 focused tests
  (5 seconds); native verification of this final palette adjustment follows.
  Public [TreeViewColors](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.platformui.treeviewcolors?view=visualstudiosdk-2022)
  keys are verified against the pinned assembly's exported properties as well.
- The first VS2026 batch passes ReferencesRename but its theme case stops before
  input because an external console covers the IDE. The foreground guard remains
  intact (TestResults/49f98aa6f62d4eee9125bc553d26c1df). A theme-only repeat reaches
  Rename, then times out looking for a modal Options dialog; retained automation
  shows this installation uses the newer nonmodal SettingsWindowControl
  (TestResults/e6c6c88a47b04b199db7e82e4b40454a). The fixture now supports both
  public UI surfaces, hides its floating panes while changing settings and captures
  the original theme before any mutation. Modern Options switches to Light, Dark
  and back to the original theme in the next attempt; that attempt then exposes a
  missing presenter-readiness wait after showing Rename, now added
  (TestResults/7015dcfb48be4a45adbc211489ddf5ed).
- Paired system selection aliases are needed for stock WPF templates that color
  their internal border directly. A first Style.Resources color-based alias has
  no owning element context in isolated rendering (TemplatePalette: eight pass,
  three fail). Aliases now resolve live VS brushes in each row's own scope,
  without snapshots or global events. ScopedTemplatePalette and LiveTemplatePalette
  each pass all 11 focused tests (5 seconds), including live alias replacement.
  Native acceptance on both hosts and the final full suites remain pending.
- VS2026 ReferencesRename again passes, but the theme case still loses accessible
  child discovery on the second transition (TestResults/d003a9f3d04c4ac2bdcf30201b4212bb).
  A stable pre-query capture proves the expanded preview itself remains visible
  and readable; ordinary virtualization Realize/ScrollIntoView does not restore
  discovery (TestResults/71e54b118a934ebca6deacb56ceb210a). Native acceptance is
  still incomplete. The adapter now invalidates existing WPF peers and their
  EventsSource after loading/composing styles, without creating automation peers
  or querying UI Automation clients. AutomationRefresh passes the 11 focused
  tests (5 seconds); native verification remains required. See the public
  [InvalidatePeer](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationpeer.invalidatepeer)
  and [EventsSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationpeer.eventssource)
  contracts. No private reflection or automation-based production behavior is used.
- The public peer refresh resolves the native accessibility regression. VS2026
  ReferenceThemes passes, zero failures/skips, in
  TestResults/daccae18fea049519aeeeb659a292073 (39 seconds). Final VS2022 theme
  and ReferencesRename both pass, zero failures/skips, in
  TestResults/29369d05082f4661ad7b0513bcccf411 (1 minute 39 seconds). Reviewed
  stable Light/Dark captures for nested/active/inactive text, disabled controls,
  expanded before/after text and popup menus: colors remain readable and the
  inactive selection uses the VS palette. Both fixtures restore the original
  theme and leave saved source bytes and the proposed name intact. The final
  VS2026 interaction repeat, full host suite and packaged checks follow.

### Iteration 17 — complete

References and Rename use shared dynamic VS styles, including nested labels,
inputs, buttons, checkboxes, selected/inactive/disabled rows and popup menus.
The adapter composes layout above the public native control templates, handles
late resources and implicit-style invalidation, resolves paired selection brushes
in each row's scope and refreshes existing accessibility peers after presentation
changes. Explicit virtualizing panels, pixel scrolling, commands and review intent
survive theme changes. No global theme handlers or brush snapshots are retained.
The native fixture supports classic and modern Options, captures settled frames
and restores the original IDE theme even if subsequent acceptance fails.

| Final gate | Actual result |
| --- | --- |
| Full host suite | 953 passed, zero failures/skips; HostFinal (2 minutes 18 seconds). |
| Focused palette/render/review checks | 11 passed; AutomationRefresh (5 seconds), overlapping the full suite. Light/Dark/High Contrast injection, nested active/inactive/disabled text, live resource and template aliases, initial-disabled native styles, implicit-style invalidation, unload/reload, recycling and review intent. |
| Packaged VSIX net472 / net8.0 | 14 / 14 passed, zero failures/skips; Integration472Final / Integration8Final (11 seconds each). |
| Native VS2022 theme and Rename | 2 passed, zero failures/skips; TestResults/29369d05082f4661ad7b0513bcccf411 (1 minute 39 seconds). |
| Native VS2026 theme | 1 passed, zero failures/skips; TestResults/daccae18fea049519aeeeb659a292073 (39 seconds). |
| Native VS2026 final Rename repeat | 1 passed, zero failures/skips; TestResults/634ded8e89204cd687d9c144387994d0 (53 seconds). |
| Review | Whitespace diff passes; 127 local documentation/guidance links resolve. No generated outputs or unrelated changes are included. |

Host/package results are under artifacts/log/Debug/Milestone14/Iteration17.
Commands use dotnet test on the corresponding test project with --no-restore
-v quiet -m:1 -nr:false --logger trx; package runs select net472/net8.0.
Native commands use eng/test-visualstudio.ps1 with the selected installation,
RootSuffix Alarmlist13Exp and Area=ReferenceThemes/ReferencesRename, sequentially.
Stable native Light/Dark captures on both hosts were visually reviewed, including
readable expanded previews, disabled buttons, selected/inactive text and menus.
High Contrast is covered through injected palettes; no operating-system theme
change is claimed. Earlier failures, their actual causes and successful repeats
remain above. No iteration-17 gate is deferred. Analysis 9/analyzer 5/library 3/
export 1 and semantic fingerprints remain unchanged. Iteration 18 starts the
compiler's formal expression syntax; the broader integrated gates remain 22.

### Iteration 18 — in progress

Starting from clean dev/milestone-14 after 5ac54d6. Introduce internal immutable
expression roots, typed lexical tokens, reference/argument/placeholder nodes and
explicit missing/invalid syntax. The current compiler grammar remains authoritative:
ASCII parameter/resource names, Unicode lookup keys, case-sensitive expression
prefixes, one non-localization reference per formatted argument, context-gated
item references and exactly-once escapes. Lossless UTF-16 spans stay compatible
with the existing XML decoded-to-source map. Ordinary evaluation accepts only
fully valid syntax; recovery is inspectable but cannot execute or create proven
references. All expression consumers use this one compiler parser. Public bridge
contract activation remains iteration 19; analysis 9 and existing fingerprints
remain unchanged here. Required gates include both-framework compiler/downstream
regressions and generated definition/collection variants.

- Replaced the mutable/string-kind expression parts with immutable typed syntax.
  The sole parser owns lexical punctuation/keywords, argument and placeholder
  boundaries, exact source spans, iterative invalid-call recovery and missing
  tokens. Token text is a slice of the authored value rather than one retained
  string per punctuation mark. Strict PropertyExpression construction rejects
  every recovered root with errors. Evaluation, localization, lookup binding,
  provenance, references and exchange protection use the shared compiler parser;
  no host parser or public contract change is introduced.
- Initial8 passes 316 existing expression/reference/explanation tests (171 ms).
  The new xUnit theory initially exposes an internal enum in a public method's
  signature; its data now passes the enum's integer value without making compiler
  syntax public. Syntax8Fixed passes all 33 new tests (92 ms), covering all kinds,
  nested/quoted arguments, lossless typing prefixes and 300 malformed delimiter
  combinations, immutable collections, exact escapes/placeholders, Unicode/XML
  mapping and rejection of recovered semantic uses. Full compiler suites pass
  883 on net472 (1 second) and 885 on net8.0 (333 ms), zero failures/skips
  (Compiler472/Compiler8). The existing CA1416 test-helper warning is unchanged.
  Results are under artifacts/log/Debug/Milestone14/Iteration18.
- Generated scalar lifecycle/type/required-value checks pass on both frameworks
  (AlarmDefinitionGeneration/07ca9bd0bf074478806427c6b29bc2df). Collection runtime
  checks pass procedure-enabled, procedure-disabled and minimum-count variants on
  both frameworks, including identical JSON hashes
  (AlarmCollections/5a3ee7ecf2f94aa4844a63f6cb05a2a3). MSBuild task tests pass
  124 per framework, task integration passes 4 per framework and zenon passes
  9 net472 / 10 net8.0, zero failures/skips. The downstream launcher then uses an
  incorrect SDK project filename and stops before running that suite. It is
  corrected to AlarmList.MSBuild.SDK.Tests.csproj; both full SDK suites are running.
  No SDK acceptance is claimed from the failed launcher.
- Full SDK suites pass 97 on net472 (4 minutes 10 seconds) and 94 on net8.0
  (4 minutes 7 seconds), zero failures/skips. Final review replaces a list scan
  for duplicate arguments with an ordinal hash set, retains the original linear
  name validator and groups contiguous invalid recovery text. A 1,024-argument
  case verifies distinct names, duplicate rejection and losslessness without a
  timing threshold. Final compiler suites pass 884 on net472 (1 second) and
  886 on net8.0 (317 ms), including all 34 new cases. The full SDK runs precede
  this complexity-only correction; final packaged desktop/bridge checks repeat
  the affected behavior against the corrected compiler. Generated scalar and
  collection runs above precede that correction as well.
- Final focused packaged checks pass 17 tests on each installed desktop MSBuild
  host, zero failures/skips: Desktop-2022 (45 seconds) and Desktop-18 (48 seconds).
  They cover package isolation and the analysis-9 snapshot API, compiler inputs,
  formatted lookups and analyzer package loading against the corrected compiler.
  The full extension-host suite and packaged VSIX checks are running sequentially.

### Iteration 18 — complete

The compiler now owns immutable ExpressionSyntaxKind/ExpressionSyntaxToken data
and structured reference, argument, literal and placeholder nodes. The single
parser preserves authored text, UTF-16 spans, whitespace, quoted/escaped literals,
context-gated record references and all existing grammar/case rules. Incomplete
syntax has explicit missing tokens and diagnostics; invalid calls recover without
recursive call expansion. Only error-free roots enter PropertyExpression,
evaluation or proven reference capture. All compiler consumers use the typed
nodes; XML source mapping still protects entities, CDATA, comments and surrogates.
Duplicate-name validation remains linear in argument count. There is no host
semantic parser, new dependency, public API or contract/fingerprint transition.

| Gate | Actual result |
| --- | --- |
| Final compiler net472 / net8.0 | 884 / 886 passed, zero failures/skips; CompilerFinal-net472 / CompilerFinal-net8.0 (1 second / 317 ms), including 34 new syntax/recovery cases. |
| MSBuild task net472 / net8.0 | 124 / 124 passed; Task-net472 / Task-net8.0 (1 second / 918 ms). |
| Task integration net472 / net8.0 | 4 / 4 passed; TaskIntegration-net472 / TaskIntegration-net8.0 (1 second each). |
| Zenon net472 / net8.0 | 9 / 10 passed; Zenon-net472 / Zenon-net8.0 (492 / 157 ms). |
| Full SDK net472 / net8.0 | 97 / 94 passed; SDK-net472 / SDK-net8.0 (4 minutes 10 seconds / 4 minutes 7 seconds), before the final complexity-only correction noted above. |
| Final desktop SDK checks, VS2022 / VS2026 | 17 / 17 passed; Desktop-2022 / Desktop-18 (45 / 48 seconds), against the corrected compiler. |
| Final full extension-host suite | 953 passed, zero failures/skips; HostFinal (2 minutes 41 seconds). |
| Final packaged VSIX net472 / net8.0 | 14 / 14 passed, zero failures/skips; VSIX-net472 / VSIX-net8.0 (13 seconds each). |
| Generated scalar/runtime checks | Passed both frameworks, all scalar types and required/default variants; AlarmDefinitionGeneration/07ca9bd0bf074478806427c6b29bc2df. |
| Generated collection/runtime checks | Passed both frameworks, procedures enabled/disabled and minimum-count variants; AlarmCollections/5a3ee7ecf2f94aa4844a63f6cb05a2a3. |
| Review | Whitespace diff passes; 123 local documentation/guidance links resolve. Only the iteration's source, tests and guidance are included. |

Tests use dotnet test on the corresponding project with --no-restore, the selected
framework, -v quiet -m:1 -nr:false --logger trx and results under
artifacts/log/Debug/Milestone14/Iteration18. Desktop repeats select PackageTests,
CompilationTests and LookupTests, setting/restoring ALARMLIST_TEST_MSBUILD_PATH
for the selected installation. Generator commands are
eng/test-alarm-definition-generation.ps1 and eng/test-alarm-collections.ps1;
their evidence is under artifacts/log/Debug with the identifiers above. Full
downstream and generated runs precede the final complexity-only correction;
the final compiler, desktop and host/package checks validate that correction.
All executed test groups have zero failures/skips after the documented fixture
and launcher corrections. The pre-existing CA1416 shared-test-helper warning
remains. This iteration changes compiler internals, with no new native UI action;
native classification/editor acceptance remains in 20–22. No iteration-18 gate
is deferred. Analysis 9, analyzer 5, library 3, export 1 and semantic fingerprints
remain unchanged. Iteration 19 exposes detached classification and activates 10.

### Iteration 19 — in progress

Starting from clean dev/milestone-14 after b37f56c. Add an opt-in classification
request over frozen compiler inputs/context and one versioned document or value.
Return detached classifications, original spans/XML projections, diagnostics,
target/context stamps and explicit unavailable/literal states. The compiler alone
selects expression-capable source values through generated metadata and source
syntax. Raw private values may receive lexical classifications; resolved values,
literal assignments/defaults and escaped expression-like text do not become
semantic targets. Saved dependency/resource inputs remain separate from displayed
draft text. Classification creates no solution reference index and retains no
ordinary-analysis cache. Reuse the project-owned SDK worker and freshness gate;
cancel obsolete calls and revalidate inputs before publication. Activate analysis
10 across compiler, SDK and host only with the completed transport. Analyzer 5,
library 3, export 1 and semantic fingerprints remain unchanged. Required gates
include both compiler frameworks, both packaged task runtimes/desktop hosts,
compatible/incompatible toolsets, isolation and cancellation.

Implementation and validation in progress (2026-11-14):

- ClassifyExpressionsSnapshots accepts frozen original inputs and one separately
  versioned Almx/Resx/Value presentation target. Immutable results carry context,
  snapshot/view stamps, lexical roles, containing expression ranges, diagnostics
  and exact physical XML projections. Generated metadata selects expression fields,
  procedure values and simple/record collection fields. Default/assignment text,
  resolved displays and escaped references remain literal. Private drafts have
  lexical data without a physical edit projection or saved-input substitution.
- The single syntax parser supplies incomplete/recovered classifications; these
  never confer semantic identity or rename permission. RESX requires an explicit
  captured consumer, uses saved project/library bindings for template roles and
  reports mixed formatted/plain usage rather than guessing. Literal lookup/enum
  captions stay literal. Document mapping preserves entities, CDATA, comments and
  UTF-16 offsets. Repeated containing-expression projections are shared within
  the request instead of rescanning the outer expression for every argument token.
- Analysis 10 is activated across compiler, SDK package verification and host
  reader/worker. All classification objects crossing the SDK AppDomain are copied
  into serializable host records; internal syntax and compiler objects stay inside.
  Classification skips analyzer loading/evaluation and creates no reference index.
  ProjectAnalysisService reuses its owned worker under the analysis gate, validates
  complete inputs before/after, and cancels obsolete requests before disposal.
- Focused bridge tests pass 2 (ClassificationHostInitial, 793 ms), including an
  invalid captured analyzer image that must never be loaded, domain unload,
  immutable results, escaped/quoted roles, private drafts and saved RESX isolation.
  Four service tests pass (ClassificationServiceInitial, 102 ms): worker reuse,
  replacement buffer identity, silent post-worker input changes, project stop and
  caller cancellation followed by a valid later request. Existing bridge compatibility
  tests pass 82 (BridgeInitial, 48 seconds), including explicit rejection of 9.
- New compiler classification tests pass 14 (CompilerClassificationExtended,
  net8.0, 135 ms). They cover all mapped roles, malformed/incomplete text, exact
  entities/CDATA/comments/Unicode, source/draft/default states, RESX context and
  mixed templates, stable procedure entry targeting, case-insensitive group IDs,
  immutability and cancellation during a long expression request. The group test
  exposed an ordinal comparison inconsistent with existing procedure identities;
  classification now follows their established case-insensitive rule.
- Generated scalar lifecycle/runtime checks pass both frameworks
  (AlarmDefinitionGeneration/6d96b541fb014179b0d661f5b438724a). Generated collection
  checks pass both frameworks with procedures enabled/disabled and minimum-count
  variants (AlarmCollections/b1301c5f3a4f487ea6aec950e843c76d). They now assert typed
  scalar and record-sibling classification plus detached contract-10 transport.
- Full compiler suites pass 898 net472 (1 second) / 900 net8.0 (377 ms), zero
  failures/skips, Compiler-net472 / Compiler-net8.0. The pre-existing CA1416 shared
  test-helper warning remains. Full SDK/package acceptance is running; no completion
  is claimed yet. All 150 reviewed local documentation links resolve and the
  whitespace diff passes. Other results are under
  artifacts/log/Debug/Milestone14/Iteration19 unless separately identified above.
- Full SDK suites pass 97 net472 (4 minutes 11 seconds) and 94 net8.0 (4 minutes
  7 seconds), zero failures/skips, SDK-net472 / SDK-net8.0. Packaged verification
  invokes the new API directly, checking contract/context stamps, typed roles,
  quoted/escaped literals, immutable results, cancellation and no source-path I/O.
  Review then makes cancellation polling count parser work rather than absolute
  offsets so escaped pairs cannot skip the polling interval; an additional long
  escaped-string case covers that path. The net472 full SDK run predates this
  cancellation-only refinement; net8.0 and final desktop/host checks use it.
  Final generated collection acceptance additionally checks imported-library raw
  values classify without inventing physical edit projections. Ordinary saved
  analysis is asserted to produce actual non-null compiled XML before and after
  private resource/value classification. Final acceptance is running sequentially.

### Iteration 19 — complete

Opt-in expression classification is available through the public compiler API and
detached SDK transport. Analysis contract 10 is activated together across compiler,
SDK and extension; analyzer 5, library 3, export 1 and semantic fingerprints remain
unchanged. The compiler's shared parser supplies typed lexical roles and recoverable
syntax diagnostics; exact display/source projections retain XML entities, CDATA,
comments and Unicode. Classification is presentation data, never a proven semantic
reference. Private raw drafts remain separate from captured/saved inputs. Literal
defaults/assignments, resolved output and escaped text remain literal. No evaluator,
analyzer loading, solution search, ordinary-analysis cache or retained reference
index participates. Project-owned workers enforce cancellation and complete input
validation before/after publication, including buffer identity and saved dependencies.

| Gate | Actual result |
| --- | --- |
| Final compiler net472 / net8.0 | 899 / 901 passed, zero failures/skips; CompilerFinal-net472 / CompilerFinal-net8.0 (1 second / 333 ms), including 15 classification cases. |
| MSBuild task net472 / net8.0 | 124 / 124 passed; Task-UnitTests-net472 / Task-UnitTests-net8.0 (1 second / 582 ms). |
| Task integration net472 / net8.0 | 4 / 4 passed; Task-IntegrationTests-net472 / Task-IntegrationTests-net8.0 (1 second each). |
| Zenon net472 / net8.0 | 9 / 10 passed; Zenon-net472 / Zenon-net8.0 (516 / 128 ms). |
| Full SDK net472 / net8.0 | 97 / 94 passed; SDK-net472 / SDK-net8.0 (4 minutes 11 seconds / 4 minutes 7 seconds); net472 precedes the cancellation polling refinement recorded above. |
| Final desktop packaged SDK, VS2022 / VS2026 | 17 / 17 passed, zero failures/skips; Desktop-2022 / Desktop-18 (44 / 48 seconds), including direct contract-10 classification verification. |
| Final full extension-host suite | 960 passed, zero failures/skips; HostFinal (2 minutes 42 seconds), including detached isolation, draft/saved-input separation, stale buffers, cancellation and incompatible toolsets. |
| Final packaged VSIX net472 / net8.0 | 14 / 14 passed, zero failures/skips; VSIX-net472 / VSIX-net8.0 (11 / 10 seconds). |
| Generated scalar/runtime checks | Passed both frameworks; AlarmDefinitionGeneration/6d96b541fb014179b0d661f5b438724a, before the cancellation-only polling refinement. |
| Final generated collection/runtime checks | Passed both frameworks, procedures enabled/disabled and minimum-count variants; AlarmCollections/12b6df5e9b17490c875bddc505e160bb. Includes record siblings, detached transport and library raw values without invented physical ranges. |
| Documentation/review | 150 local links resolve; whitespace diff passes; current contracts and saved-input/lexical boundaries are documented without rewriting historical acceptance. |

Tests use dotnet test on each corresponding project with --no-restore, the chosen
framework, -v quiet -m:1 -nr:false --logger trx and results under
artifacts/log/Debug/Milestone14/Iteration19. Desktop repeats select PackageTests,
CompilationTests and LookupTests and set/restore ALARMLIST_TEST_MSBUILD_PATH for
the selected installation. Generated commands are eng/test-alarm-collections.ps1
and eng/test-alarm-definition-generation.ps1, with evidence locations above. All
test groups run sequentially; no native/WPF/desktop MSBuild overlap occurs. The
existing CA1416 shared-test-helper warning remains. There is no new UI action in
this iteration; actual classifications, XML/designer commands and the essential
native RESX-table feasibility/synchronization gate remain in iterations 20–22.
No iteration-19 required gate is deferred. Only this iteration's changes are included.

### Iteration 20 — complete under the revised XML/designer scope

Starting from clean dev/milestone-14 after 43a172f. First verify the essential
standard RESX Resource Explorer integration gate on both installed IDEs through
public shell/selection/document services. Metadata inspection of both installed
Resource Explorer assemblies finds public model/undo helper types but no exported
selection, editor view/model or pending-edit service. This inspection alone does
not establish an integration blocker: native capability checks must inspect
standard ISelectionContainer/IVsMonitorSelection, RDT persistence and text-buffer
access while an actual resource row and a pending cell edit are active. Keep
diagnostic automation outside the VSIX; no production UI automation or private
reflection is authorized. XML-only support cannot substitute for this gate.
The first VS2022 probe stops safely before input because its fixture omitted
explicit foreground activation of the owned main window. No resource capability
or transaction was tested in that run (TestResults/7ef81aeb5b0043dd915576a53c92a3e1).
Add the same explicit SetForeground/readiness check used by existing native
resource fixtures, then repeat; this setup failure is not an integration result.
The second run also stops before input while another transient window owns
foreground (TestResults/05e1dae313974027a52bb99f472fbde2). Adopt the existing
bounded foreground-readiness loop, activate the owned DTE main window and hide
only that fixture's persisted Rename/Summary tool windows before repeating.
That bounded retry still cannot obtain foreground (TestResults/8837b8ef6c234907b3af6e0556899d48).
After the user reconnects/unlocks the desktop, both native probes reach the actual
selected row and pending cell. VS2022 fails the essential selection gate in
32 seconds (TestResults/e936322027254a8a9beca75c99d67da2;
ResourceTableContracts/2022/2c34028e210543d8ba0964a0da762c61). VS2026 fails the
same gate in 47 seconds (TestResults/8919fdc30ba8425c9c0c97a2a873bf8d;
ResourceTableContracts/2026/ecd75a4db3ce42ef8ba28d76c8cb9c40). Each captures
resource-table-contracts.json/png for selected, pending-edit and canceled states.
IVsMonitorSelection succeeds but returns no ISelectionContainer for the selected
key. RDT/DTE expose the resource text document, but IsDocDataDirty remains false
and Document.Saved remains true while the cell contains an uncommitted draft.
Escape cancels the draft and disk bytes remain unchanged. Both owned IDEs exit
normally. The tested public shell/document services therefore supply neither
selected key identity nor pending-cell state; metadata inspection finds no
published editor-selection/pending-edit service to bridge that gap. No claim is
made that Apply/linked Undo synchronization passes: it cannot be accepted without
those prerequisites. The standard-table integration is an essential unresolved
blocker, not an XML-only implementation or a successful iteration. Preserve the
requested table scope pending a concrete user decision; continue independent XML
classification/token work meanwhile.

Scope decision (2026-11-14): the user explicitly replies, "Defer standard-table
commands and finish XML/designer support." The table prerequisite is now a
separately deferred vendor gate and no longer blocks completion of the revised
iteration 20. Preserve its actual failed results above. Keep its external fixture
opt-in via ALARMLIST_RUN_RESOURCE_TABLE_GATE=1; ordinary native suites skip that
deferred acceptance instead of claiming it passes. No standard-table commands ship
under the revised scope. Direct RESX XML, XML token/classification work, the
embedded designer editor and both-IDE integrated acceptance remain required.

The XML classifier uses one per-view/snapshot cache, compiler-owned detached
ranges, inherited editor theme colors and nine independent Fonts and Colors roles.
GetTags performs only bounded range projection, with no parser or solution search.
Initial host projection tests need the same Text.Logic/UI/UI.Wpf editor contract
assets used by the VSIX; add them as test-only downloads/references without
altering the CPS/MSBuild dependency family. Eleven focused tests pass
(ClassificationProjectionFixed, 146 ms): target/version/contract freshness,
malformed/overflowing/overlapping ranges, viewport queries and configurable
native-color inheritance. Native coloring and lifecycle acceptance remain pending.

Compiler token commands now observe each reference node's own punctuation/keyword
and its existing chosen binding. Quoted argument text and escaped expressions
receive no guessed identity; nested arguments keep independent ownership. Precise
identifier spans and name-end carets win over enclosing syntax, while syntax-end
positions do not capture following XML/literal text. The request-owned batch index
and single-target path share this selection rule. No new occurrences/rename edits,
ordinary-analysis index, semantic grammar or compatibility fingerprint are added.
Seven new tests cover parameter/resource prefixes, nested arguments, lookup result
versus path/member/metadata segments, unbound inheritance, entities/CDATA/comments
and unchanged rename edits. Initial fixtures use the wrong resource-kind label
and invalid lookup XML; corrected fixtures pass all 32 focused cases
(ExpressionTokenTargetsFixed, net8.0, 179 ms). Full compiler suites pass 906 net472
(1 second) and 908 net8.0 (349 ms), CompilerToken-net472 / CompilerToken-net8.0.

Go to Definition reuses frozen solution capture and existing physical/package
navigation. Compiler source-token identities deduplicate imported projections;
one physical declaration opens directly, while multiple declarations receive a
declaration-only choice with retained consumer/culture/coverage evidence. Generated
or unavailable declarations remain explicit. Original captured inputs, source
intent and private drafts are checked before navigation. Five new host cases plus
affected projection/reference/routing tests pass 26
(DefinitionNavigationInitial, 152 ms). Ordinary reference/rename presentation
remains unchanged when definition-only mode is not requested.

Native XmlExpressionNodes acceptance is in progress. The first VS2022 run stops
before keyboard input on lost foreground (TestResults/9137c31782ed4ca589c552858ae38c0b).
The next run proves prefix Go to Definition, nested-argument Find All References
and culture-choice publication, then hits a fixture null access before the new
choice tree is realized (TestResults/34529b7bace640a099e951c458397e48;
XmlExpressionNodes/2022/1e208a70978e4d6ab2597abe0ed6318a). Wait explicitly for
the realized source row. Its native UIA text attributes show normal/escaped text
RGB 13158600 / weight 400, parameter RGB 14064726 / weight 700 and resource key
RGB 8756694 / weight 700. These are actual inherited editor formats; GDI PNGs from
the occluded/hardware-rendered editor are blank and are not visual-review evidence.
The first menu probe accidentally includes the persistent menu bar; require an
actual popup Menu before collecting its items. Classification accepts the editor's
IClassificationTag interface as well as ClassificationTag and defers context/RDT
creation until after provider/GetTags initialization. UI-thread checks stay behind
the asynchronous switch; a VSTHRD109 build failure is corrected before repeating
native acceptance. No complete native XML gate is claimed yet.

Subsequent fixture corrections preserve explicit editor ownership and focus:
the visible XML view can transiently reject UIA Focus during template switching,
so retry that readiness check without issuing input; DTE OpenFile's second
argument is a logical view, not an editor factory, so direct RESX checks use
OpenDocumentViaProjectWithSpecific and verify the native XML editor and unchanged
document-data identity. An intermittent Shift+F10 popup timeout is retained in
TestResults/b9111dab2c3047cfa1ec3ffb86520413. The corrected pointer check obtains
the exact token's UIA text rectangle, hit-tests the owned IDE, opens its native
menu and invokes Go to Definition through that menu. Existing native XML menu
items include Go to Definition, Find All References, Rename and editing commands;
no replacement menu is introduced.

XmlExpressionNodes passes on VS2022 in 46 seconds
(TestResults/488890f9f80040cc8ff2ab17c87c226f). It proves actual parameter/resource
colors with plain/escaped text unchanged, native pointer menu navigation, nested
reference targeting, culture declaration choice/navigation, direct saved RESX XML
key discovery and coordinated ALMX/en/de rename. All three buffers change
unsaved, one Undo/Redo covers the set, a final Undo restores exact originals and
all disk bytes remain unchanged. Unsupported literal Go to Definition preserves
native fallback. VS2026 and broader regressions remain pending.

The matching VS2026 XML/RESX scenario passes in 43 seconds
(TestResults/1ff886eb9b8049d19c54c22c7584495c). Full host/WPF regression passes
976 tests, zero failures/skips, in 2 minutes 15 seconds (Iteration20/HostFull).
Final native repeats additionally require all three native token-menu actions
enabled and exercise the Menu key on the nested dollar-prefix target, alongside
the existing shared-consumer and lookup/procedure-group fixtures on both IDEs.

The combined VS2022 repeat passes SharedReferencesRename and
AuxiliaryReferencesRename but times out after UIA Invoke on the native Go to
Definition menu item (TestResults/37a2de02351249c4b1a9b8b46c944977). The token
and buffer remain unchanged; no Apply occurs. Repeat using a real click on the
enabled native item rather than UIA Invoke. The earlier isolated XML passes do
not replace this final keyboard/pointer repeat gate. A readable captured dark
editor view also confirms the native UIA color results; earlier blank GDI images
remain excluded from visual evidence.

The real-click fixture initially cannot observe a stable popup
(TestResults/b60abbc3de1e413baf5ffc8d54e8ccdd). Its final readiness helper
hit-tests native items and retries only an absent menu-opening gesture after
checking owned foreground, the intended document and unchanged source bytes;
semantic commands still execute once. Per-item enabled states/bounds are retained
in xml-expression-menu-state.json. With these guards the final VS2022
XmlExpressionNodes passes in 1 minute 5 seconds
(TestResults/b83c8d5ea45741dbbc45edddb877c83f), including all three enabled native
actions, an actual pointer click, dollar-prefix navigation, Menu-key nested search
and the full direct RESX XML rename/Undo/Redo/disk-preservation sequence. The
VS2022 shared/auxiliary passes from the combined run remain valid: production
code has not changed since that run; only fixture readiness has changed.

Final VS2026 XmlExpressionNodes, SharedReferencesRename and
AuxiliaryReferencesRename all pass, 3/3 in 1 minute 52 seconds
(TestResults/a87ae6b929ac4ca3a69fce5c54cc102e). This completes the revised
both-IDE native XML/RESX/shared/culture/lookup/procedure gate. The separate
standard-table probe remains explicitly deferred and failed, not counted here.
Packaged runtime/desktop regressions and final diff/documentation review follow.

Packaged SDK bridge cases pass 3/3 on net472 and 3/3 on net8.0 (4 seconds each),
Iteration20/SDK-net472 and SDK-net8.0. Package/compilation/lookup regressions pass
17/17 under each desktop MSBuild: VS2022 in 42 seconds and VS2026 in 45 seconds,
Iteration20/Desktop-VS2022 and Desktop-VS2026. All runs are sequential after native
acceptance. No runtime/analysis contract, analyzer/library/export version or
semantic definition fingerprint changes in this iteration. VSIX package checks
and final reviewed commit remain pending.

Final VSIX integration passes 14/14 on net472 and 14/14 on net8.0, 10 seconds
each (Iteration20/VSIX-net472 and VSIX-net8.0). Reviewed the compiler binding/probe
changes, per-view classification lifecycle, detached target validation, definition
navigation, native command fallback and external-only fixtures. Diff whitespace
and changed local documentation links pass. User guides describe implemented XML
behavior and the explicitly deferred standard table; no table acceptance is
claimed. Iteration 20's revised gates pass. Embedded designer editor acceptance
remains iteration 21; integrated library/generated-definition/tutorial/performance
reacceptance remains iteration 22. Record the focused commit before beginning 21.

### Iteration 21 — completed 2026-11-15

Started from clean dev/milestone-14 at iteration-20 commit 53f0464. The designer's
ExpressionTextBox now composes an explicitly injected native VS buffer/view with
a dedicated content type. Standalone WPF/design-test hosts use their own surface;
a failed installed native factory never silently falls back. Existing raw/resolved
binding, drafts, completion, validation, source navigation, multiline behavior and
source transactions remain in the surrounding control. Views, subscriptions,
classification requests and token requests are released on hide/unload; reopening
recreates presentation without committing a draft.

The compiler adds TargetAtValueToken using its existing parser/binder's decoded
UTF-16 token probes and explicit alarm consumer. The request-owned index shares
single/batch target precedence with XML, preserves independent inherited child
bindings and refuses literal assignments, escapes, ambiguous and unbound targets.
The SDK transports the detached target through the completed analysis-10 API;
analyzer 5, library 3, export 1 and semantic fingerprints remain unchanged. No host
semantic parser or ordinary retained reference index was added.

Native values share compiler classifications and Text Editor formats with XML.
Resolved output stays literal; lexical draft colors do not enable semantic
commands. F12 and captured token menus use compiler-owned targets/navigation;
Explain stays a containing-value action. A popup retains its actual source editor
when a containing grid owns the value-action anchor. Opening menus on Menu-key
or right-button release avoids release dismissing an early popup and preserves
other private drafts. Captured context/caret/input changes cancel obsolete
requests; published reference intent remains independent of later caret movement.

Two native integration corrections matter for future work. Buffer change handlers
read TextBuffer.CurrentSnapshot rather than an older view-layout snapshot. Native
insertion can publish its final caret during layout without a separate usable
caret notification, so snapshot/caret/selection changes notify completion from
layout too, suppressing duplicates. Private mirror-buffer Undo recording is
disabled: synchronization must not discard linked source Redo. Source Undo first
cancels a private draft, then operates on committed XML. These rules are documented
in repository guidance.

Validation commands use dotnet test with --no-restore, the framework shown below,
--logger trx and results under artifacts/log/Debug/Milestone14/Iteration21. Host
and native/WPF checks ran sequentially. Native commands use
eng/test-visualstudio.ps1, the respective installed IDE and idle Alarmlist13Exp
profile; existing user IDEs were not closed.

| Required check | Actual result |
| --- | --- |
| Compiler suite, net472/net8.0 | 914/916 passed; 1 second/395 ms; Compiler-net472 and Compiler-net8.0. Eight new authored-target cases cover decoded XML, nested/prefix ownership, literals/invalid/unbound targets, inherited consumer separation and exact coordinated preview edits. |
| Full VS host suite, net472 | 981 passed, no failures/skips, 2 minutes 14 seconds; HostFull. The earlier affected-editor pass was 417 in 2 minutes 24 seconds. Composite focus/composition assertions address the actual input child while retaining every source/draft assertion. |
| SDK PackageTests, net472/net8.0 | Three passed per runtime, 4/3 seconds; SDK-net472 and SDK-net8.0. The packaged consumer independently verifies an entity-encoded value prefix and its two-edit rename with captured inputs only. |
| Desktop SDK PackageTests/CompilationTests/LookupTests | 15 passed per host, VS2022 29 seconds and VS2026 31 seconds; Desktop-2022 and Desktop-2026. ALARMLIST_TEST_MSBUILD_PATH selects each desktop MSBuild and is restored afterward. |
| VSIX integration, net472/net8.0 | 14 passed per framework, 10/11 seconds; VSIX-net472 and VSIX-net8.0. |
| Native DesignerExpressionNodes, VS2022/VS2026 | Passed in 30/38 seconds; exact native colors/text patterns, raw/resolved focus, F12, Menu-key nested references, right-pointer token Rename and unsaved three-edit Apply/one-step Undo, draft colors with disabled semantic actions, Escape, Unicode/multiline Copy/Paste, draft-first Undo and source Undo/Redo. Clipboard and navigation preference are restored. |
| Native existing completion/explanation/reference regressions, both IDEs | LanguageAndSavedCompletion, LookupCompletion, ListCompletion, ValueExplanation and ReferencesRename all have passing results, including saved-only resources/lookups, scoped arguments, context menus, independent Rename review and two-file linked Undo/Redo. |

Native TRX evidence is under artifacts/log/Debug/VisualStudioUI/TestResults.
VS2022 DesignerExpressionNodes (30 seconds) and ReferencesRename (72 seconds)
pass within eef923de5f3f429494cbb2c8d684a501; that mixed run's four other cases
fail and are not counted as successes. Corrected ValueExplanation (77 seconds)
and LanguageAndSavedCompletion (27 seconds) pass in
6e8daa77ccd84a20b209279314916167, whose list case still fails. LookupCompletion
then passes in 9ded70ae340842b1bf1fabf40db17a45 (33 seconds), and ListCompletion in
5057a50e17454785bc97fc2727e0af61 (54 seconds). VS2026's corresponding five existing
scenarios pass in c548f54e5f7742e9aae6f4bff6662b83: language 21 seconds, lookup 18,
list 16, explanation 63 and references 66. Its initial new-editor F12 check fails;
the focused repeat verifies native command readiness and the XML preference
before physical F12 and passes in 0f208a2b6fd44301945f1b8944a73818 (38 seconds).
No production behavior was changed between those two VS2026 runs.

Resolved failures and limits remain explicit:

- Initial Keyboard failures (72092fbfa2c4435ca9616bd2934f9355) expose the stale
  view-snapshot read. Corrected ScalarCommitAndUndo passes on VS2022/2026 in
  2bd5cb03a42a4808874256566c8b9aff and d76556a506c04582a43dc551ffe73739.
  Initial host regression passes 415/fails 14 on former wrapper-focus/composition
  assumptions; updating actual input targets yields the complete 981-test pass.
- Early token popup/definition runs f5c8c60e1f1d44db9be8d09f9961319f,
  16b5239f43cc4eca95247ddfb8060371 and 92b3c9bbd7914bf19ff2d70810d9324a stop
  before complete acceptance. Source-editor ownership, native Text Editor
  appearance, hit-tested glyph input and release-time opening correct the actual
  issues. Expanded early passes 5488c511e49d429e9d6cd7d2e0ebe8b9 (VS2022) and
  9542099be974485aa5a1f6773a2142d3 (VS2026) precede the final regression fixes.
- Existing regressions initially fail culture selection, linked Redo and completion
  (1c5ac0d6f7024e18a02f5e67d4222406, 49112733c4c94e51a9ed95314029fd62 and
  9bee1d88fb514f39b62132aaed4c7566). Tracing b7041d98f8b34198983ac30f5d192c22
  proves validation succeeds but captures caret 28 before final caret 29. A queued
  callback alone fails in b929c8e726794ddbbeaf6b8b6b6e84f6; layout notification
  resolves it. Private-buffer Undo suppression restores complete linked Redo.
- Fixture readiness now hides obstructing persisted panes, uses the captured main
  HWND, retries only absent menus, waits for visible/committed culture choices and
  contributor layout, and explicitly unfocuses values before resolved-text
  assertions. Native range.Select/synthetic positioning did not reliably place
  argument carets; use actual hit-tested glyph input. Original source/draft/output
  assertions remain. Readiness/caret/selection changes are never permission to
  reuse a stale preview. WindowsTerminal/RDP focus losses stop before input.
- An earlier diagnostic repeat (0c59ae92e68440599701911adacd9f6a) terminates the
  owned IDE during a UIA menu query. Windows .NET/Application records identify
  ItemAutomationPeer.GetNameCore and PresentationFramework. Later complete native
  runs pass without that failure. All temporary production diagnostics are removed.

Diff whitespace and changed local documentation links pass (133 checked after the
final backlog update). Feature/contributor/native-runner documentation describes
the implemented behavior. Iteration 21's required gates pass; record its focused
commit before starting iteration 22. Integrated shared/library/package/generated
coverage, tutorial and performance evidence remain iteration 22. Standard RESX
Resource Explorer table commands remain separately deferred by the user's explicit
decision; XML/designer successes do not imply that vendor gate passed.

### Iteration 22 — complete (2026-11-15)

Start from clean dev/milestone-14 at iteration-21 commit 6f72650. Complete the
accepted XML/designer scope without revisiting the separately deferred standard
RESX table. No merge, branch creation or push is authorized by this iteration.

Deliverables and gates:

1. Add a guided expression-editing/navigation tutorial using the native scenario's
   exact parameter/localization/escaped-text sample. Exercise its unsaved Rename,
   Undo and final Save/Build outcome through a native tutorial mode on both IDEs;
   retain source/output preservation and explicit focus/context readiness.
2. Reaccept shared consumers/all cultures, direct RESX XML, project and NuGet
   libraries, the prepared shared example and procedure-enabled/disabled generated
   definitions. Reuse their existing native/isolated fixtures on both hosts,
   sequentially. Add focused compiler/packaged coverage for authored token targets
   in imported library values where current tests do not prove that path.
3. Extend the existing large-project measurement fixtures with ordinary analysis
   versus document/value classification and private-draft classification, including
   SDK-domain allocation totals, cancellation/recovery and bounded retained view
   presentation. Reuse reference/rename/explanation measurements for comparison.
   Record native typing observations separately from isolated API/UI timings and
   do not claim outer counters measure compiler allocations or retained memory.
   Verify typing/classification never launches solution reference searches.
4. Run compiler and affected downstream regressions on both frameworks, packaged
   bridge checks on both desktop MSBuild hosts and integrated native checks on both
   IDEs. Keep failures and separately deferred/vendor limits visible. Review all
   examples/links, update guidance/backlog/coverage, record actual measurements and
   commit only after these remaining gates pass.

Iteration 21 already supplies the complete per-control native editor, six passing
native scenarios per IDE, 914/916 compiler, 981 host, both-runtime SDK and both-host
desktop evidence. Do not rerun unrelated checks without a new change, failure or
unresolved concern; the final regression selection must cover iteration-22 changes
and the required integrated acceptance. No iteration-22 acceptance is claimed yet.

Implementation/evidence in progress (2026-11-15): the imported-library authored
token case passes with its read-only contribution and acknowledged partial
consumer edit. Full compiler suites pass 915 net472 (1 second) and 917 net8.0
(327 ms), Iteration22/Compiler-net472 and Compiler-net8.0. Project analysis passes
57 tests (1 second), including successive draft versions reusing one worker and
original inputs without any reference-search bridge; ProjectAnalysis. Native
tutorial/typing code and the performance project build without warnings/errors.
The tutorial fixture reads its four checked-in XML examples, validates unsaved
Rename/Undo/Redo and final saved three-culture output. Native typing adds 1,000
background alarms and separate input/color-observation metadata; those gates are
still running, so no successful tutorial/native-typing result is claimed yet.

Initial integrated VS2022 run 9743b2e5231d4e6e96a6224fb7cc1e5d passes the package
definition and edited prepared-example cases, fails SharedReferencesRename,
SavedLibraryAnalysis, XmlExpressionNodes and ReferenceThemes. The theme image
explicitly reports catalogue invalidation; the fixture now checks unchanged
source/disk, leaves stale Apply disabled and recaptures the same alarm only after
that reported change. Shared XML adds native command/input readiness. XML pointer
checks retain process hit testing and stop rather than send input through an
overlaid foreign window; another run still fails the initial hit test, so that
gate remains open. External menu diagnostics are added for the library failure;
no production semantic freshness guard has been relaxed. Record successful
repeats before accepting any of these four cases.

Performance run 87d8da7a7ee448968dedcd9236e486f2 passes all 22 selected cases in
7 minutes 35 seconds. Command: eng/measure-visualstudio-analysis.ps1 -Job Short
with Classifications, ClassificationAllocations, ClassificationCorrectness,
ReferenceAllocations, ExplanationAllocations and ReferenceCorrectness areas.
Follow-up 726e46c78aee4963a5778c0a2f729d9f passes two expression-rich single-file
cases in 3 minutes 29 seconds (FullyQualifiedName~LargeExpressionDocument).
Both use Release/x64, .NET Framework 4.8.1, two warmups/five samples on an
i7-11700K. Raw TRX/BenchmarkDotNet JSON/CSV/Markdown and environment/revision
metadata are beneath artifacts/log/Release/AnalysisPerformance/<run-id>.

BenchmarkDotNet warm end-to-end bridge means, milliseconds (outer remoting and
detached transport included; ordinary baseline is the same fixture):

| Scenario | Ordinary analysis | Document classification | Authored value | Private draft |
| --- | ---: | ---: | ---: | ---: |
| 1,000 alarms, 100/file, literal | 351.46 | 2.98 | 2.96 | 1.41 |
| 10,000 alarms, 100/file, literal | 3920.50 | 15.71 | 15.85 | 6.10 |
| 10,000 alarms, one literal file | 3935.55 | 337.66 | 394.76 | 50.45 |
| 10,000 alarms, one expression-rich file | 6851.22 | 1934.04 | 427.06 | 61.73 |

The literal document/value targets intentionally have zero lexical spans. The
expression-rich document returns 160,000 spans, its selected value four, and the
incomplete draft nine plus one recovery diagnostic. The large single-file costs
are explicit limitations, not hidden by a latency threshold. Separate parameter
and localization scenarios also pass; retain their full reports. Short single-call
samples have broad confidence intervals and BenchmarkDotNet minimum-iteration
warnings; they are descriptive observations, not a speed guarantee.

SDK-domain allocation probes include compiler, captured-request and detached
reader work, excluding outer remoting/UI. Means of five samples for the
10,000-alarm/100-per-file fixture:

| Operation | SDK-domain milliseconds | Cumulative allocated MiB |
| --- | ---: | ---: |
| Ordinary analysis (classification probe) | 2571.10 | 823.21 |
| Document classification | 14.97 | 16.50 |
| Authored-value classification | 12.07 | 16.29 |
| Private draft classification | 8.15 | 14.12 |
| Explanation | 1037.91 | 574.12 |
| Find references | 1597.13 | 1100.59 |
| Rename preview | 4447.16 | 2797.43 |

The expression-rich single-file probe separately reports ordinary analysis
4142.56 ms/1282.70 MiB, document 1276.92 ms/671.45 MiB, value
518.21 ms/347.32 MiB and draft 53.18 ms/41.97 MiB. These monitoring-enabled
domain-local timings are a separate measurement method from BenchmarkDotNet.
Allocation totals are not retained heap size or collectibility evidence; empty
outer Allocated cells are not allocation-free compiler work.

Classification correctness covers a 220,000-UTF-16-unit private value with 40,000
spans and only 15 intersecting its 80-unit viewport, stale display-version
rejection, pre-cancelled calls and subsequent worker recovery. Both in-flight
classification calls cancel (observed totals 302.30/54.77 ms after a scheduled
25 ms trigger); reference and rename calls also cancel and recover. These are
request outcomes, not latency limits. Ordinary analysis still succeeds afterward.
Source review confirms the lexical classifier calls no evaluator, analyzer or
reference index; host draft requests use only the classification bridge. No
native typing performance or whole-IDE retained-memory claim is made by these
isolated results. Remaining integrated/fork/native tutorial gates stay open.

Second integrated VS2022 run c58bd11b331846488d4a0579dc536854 passes shared
Rename (48 seconds) and native expression typing (32 seconds). The latter observes
22 characters with 1,001 alarms: input/draft confirmation 952.33 ms and color
observation 109.39 ms, including focus/UIA/polling and diagnostic I/O. No semantic
results window opens automatically. These are actual input observations, separate
from the isolated API measurements above.

Four cases fail in that run. Tutorial reaches final Save All but exact source
comparison finds the final newline required by .editorconfig; the starting sample
now includes that newline, preserving exact assertions. XML pointer hit testing
finds WindowsTerminal PID 10956 over the token and refuses input; its initial
pointer action now waits for an unobstructed owned-IDE hit, as its retry already
did. Theme capture still needs native command readiness and reported-invalidation
recapture; the fixture retains unchanged-source checks and disabled stale Apply.
SavedLibraryAnalysis still cannot open the resolved Name menu; external logs show
no custom popup, so no speculative production semantic change is made. Its DTE
shutdown then blocks until the 900-second case watchdog. Cleanup now has its own
30-second watchdog over the retained owned process handle, including an explicit
failure artifact; this bounds test teardown, not product operation latency.
Corrected fixtures build with zero warnings/errors; successful repeats remain
required. Desktop-clear/idle input is pending before another native run.

The isolated heap diagnostic LibraryCleanupDiagnostic/heap-39242e5c2aee44d28c3e060451f088f5.json
captures that owned IDE before watchdog termination. Main-thread native frames
cannot be resolved; no managed Alarmlist frame identifies a classifier deadlock.
Five NativeExpressionSurface objects are unreachable at capture. This does not
prove collectibility or diagnose the synchronous shell/DTE shutdown stall.

eng/test-alarm-collections.ps1 passes enabled, disabled and minimum-cardinality
variants on net472/net8.0, including matching cross-framework JSON hashes;
AlarmCollections/d3dc5ee572794c009c85686d9ff4dfe8. Native generated-definition
acceptance is still separate and open. Iteration 22 remains uncommitted.

Further menu investigation reproduces an independent lifecycle defect in WPF:
a read-only text child added after its marked anchor loads has no explicit menu
before the TextBox class handler. Both pointer and Menu-key cases fail before the
correction (LateValueMenuBeforeFix, two failures). The anchor now repeats existing
child-menu preparation during context-invocation preview input, without handling
input, opening a popup early or changing drafts/source. Both cases plus existing
read-only/freshness/requery checks pass (LateValueMenuAfterFix, five tests,
6 seconds). This is not yet proof that the native SavedLibraryAnalysis failure
is resolved. Full host and native repeats are required after this production fix.

After that correction, the full host suite passes 984 tests, zero failures/skips,
in 2 minutes 39 seconds (HostFullAfterMenuFix). Packaged VSIX checks pass 14 each
on net472/net8.0 (10 seconds each); SDK PackageTests pass three each (4/3 seconds).
Focused PackageTests/CompilationTests/LookupTests pass 15 each on desktop
MSBuild 2022/2026 (29/31 seconds), sequentially. Evidence directories beneath
Iteration22 are VSIX-net472, VSIX-net8.0, SDK-net472, SDK-net8.0, Desktop-2022 and
Desktop-2026. The final UI acceptance project builds with zero warnings/errors.
These results validate the corrected host/package combination but do not replace
native library, tutorial, XML pointer, theme and generated-definition acceptance.

Resume run e2eeabad5f824513bdce2013e0a84c7b (VS2022) passes
SavedLibraryAnalysis, including the formerly failing resolved Name menu,
explanation/source navigation, project-library Rename/linked Undo and unloaded
library discovery. Three cases fail: tutorial foreground readiness, theme result
invocation and XML keyboard-menu focus. Desktop inventory has no Terminal window;
quser reports the console session active. A stale-window activation fails, while
refreshing to the current IDE restores native editor focus; no source edits or
security UI are used for that recovery.

The XML failure screenshot shows the actual native keyboard context menu, but
the fixture's pointer hit test rejects its References item and then waits for
editor focus while the menu remains open. Keyboard invocation now uses the
visible owned MenuItem's public Invoke pattern; the independent first pointer
menu still requires actual owned-process hit testing and a real click. Retry
dismisses the obsolete popup and explicitly focuses the XML editor before
reopening. Theme action invocation likewise uses the native MenuItem Invoke
pattern; unchanged-input/invalidation/stale-Apply assertions remain intact.
The next native repeat must validate these fixture corrections.

Run 91a71d78269d40b7b0dc56349fdb3440 passes VS2022 ReferenceThemes (60 seconds)
and XmlExpressionNodes (80 seconds), including direct multi-culture RESX XML
Rename/linked Undo and restored original theme. Dark Rename and Light selected
References images are visually reviewed: nested text, input/preview backgrounds,
disabled controls and selected text remain readable. Tutorial still fails initial
F12 navigation. A focused run records the expected Global::F12 binding and actual
native/value focus with SourceView=XML. Another input diagnostic records no
Control/Shift/Alt key held. The guarded single idempotent F12 retry passes the
base DesignerExpressionNodes scenario in a3fe29b28d1a4383bbceed101e68c93c,
but tutorial navigation remains intermittent. No production command routing or
semantic freshness requirement is changed on this evidence alone.

Tutorial run fbf134ad998d4f9b9de04f8632b7e665 gets past navigation, reference search,
Rename and linked Undo/Redo, then fails because an injected typing burst stops at
"draft $(P) " instead of including the escaped suffix. Its new draft helper sends
each character through the native keyboard and confirms insertion before the next;
it never replaces text through UIA. Measurements identify this confirmed-input
mode and include its focus/UIA/polling work. The next repeat
153171d6ba844730b82e1da9e44dc5e4 stops at F12 before reaching that helper, so the
typing adjustment and final tutorial Save/Build are not yet accepted. VS2026
integrated acceptance proceeds independently; iteration 22 remains open.

VS2026 integrated run 6e03128944b2484ca803129d14571bd4 passes seven of nine
cases in 9 minutes 5 seconds: ExpressionTutorial (67 s), DesignerExpressionNodes
(40 s), ExpressionTyping (44 s), XmlExpressionNodes (66 s), ReferenceThemes
(44 s), PackageDefinition (45 s) and DefaultSharedExample (144 s). Tutorial
verifies its final saved source and all three culture outputs. With 1,001 alarms,
confirmed character input observes 2539.79 ms and color observation 73.61 ms;
no semantic window opens. This input mode includes UIA/polling and differs from
the earlier VS2022 burst observation, so do not compare those totals as editor
throughput. SharedReferencesRename stops on WindowsTerminal PID 17552 taking
foreground; SavedLibraryAnalysis times out opening the resolved Name menu.
Both failed cases still require repeats.

Further fixture review finds that value-menu retries unconditionally activate the
main IDE HWND even when an owned floating tool window already has foreground.
The helper now preserves any already-owned foreground frame, while retaining
process ownership and pointer hit guards; it only activates the main window when
another process has foreground. This avoids resetting a floating Summary's
context on every popup retry. SelectCaret additionally waits for the UIA selection
range to reach the authored prefix before sending commands, instead of treating
focus alone as a caret-readiness proof. These final fixture corrections require
native validation; an idle-desktop question is pending after the observed foreign
foreground interruption.

Build-only preparation completes both generated-definition forks:
artifacts/col13/9a546456 (procedures enabled) and artifacts/col13/15176660
(disabled). An ignored preparation copy uses the existing collection-fork source
copy/definition/build steps while removing its native-only block and explicitly
reporting that acceptance has not run. Both UI/SDK/extension variants build
successfully. Their unmodified native block is prepared separately for four
MetadataCollections checks (two variants by two IDEs), including temporary
withdrawal/restoration of the private template sources. No native generated-
definition result is claimed; desktop availability remains pending. The previously
observed Terminal PID 17552 has exited, but that alone does not establish an idle
interval or answer the outstanding availability question.

A fresh guarded VS2022 repeat eab9060bb1d44d0d8e085044d8f56ebb stops both
tutorial and typing at foreground readiness before reaching the new caret
assertion. The external activation helper now attempts supported UIA focus when
Windows refuses SetForeground alone, while preserving any already-owned floating
frame and requiring the correct process again before input. The root fixture
build passes with zero warnings/errors; this helper is not yet natively accepted.
Automatic approval review rejects the next native run because idle-desktop
readiness remains explicitly pending after repeated foreground interference.
No indirect native run is attempted. Remaining native execution requires user
confirmation; noninteractive prepared-fork builds may continue. Iteration 22
remains uncommitted and the final acceptance gate is open.

Noninteractive preparation after that rejection completes: both existing forks
receive the final external fixture helpers and rebuild successfully
(build-final-fixtures.log in each fork); no IDE is launched. Documentation review
resolves all 74 local links across the changed guides and instructions, and
git diff --check passes. The concrete remaining native set is VS2022 tutorial/
typing, VS2026 shared/project-library repeats, four generated MetadataCollections
cases and standard-extension restoration with DesignerExpressionNodes on both
experimental hosts. This is ten cases, run sequentially after desktop readiness
and execution confirmation. No iteration-22 commit or goal completion is claimed.

### Iteration 22 correction — owned console/process cleanup

The user identifies leftover console windows after failed UI tests. Treat the
observed Terminal focus losses as ownership-unknown until investigated, rather
than attributing them to user/RDP input. Read-only inventory finds many surviving
console hosts created during the native runs. Investigation found root-only
cleanup in several paths and a DefaultSharedExample preparation timeout without
helper termination. Windowless PowerShell native-command invocation is also
a console-allocation risk, so CreateNoWindow on its initial launch is insufficient.

1. Add a shared test-only owned-process launcher using an unnamed kill-on-close
   Windows job, assigned atomically at creation. Retain exact process handles,
   restrict inherited handles, terminate descendants even after root exit, and
   fail closed if containment cannot be established. Apply it to the functional
   IDE/session, deployment and auxiliary helper lifetimes.
2. Run example-preparation native tools directly through redirected windowless
   processes instead of PowerShell native invocation. Preserve arguments, working
   directories, inherited package environments, logs and exit-code behavior.
3. Add headless regression coverage on both runtimes for normal/nonzero exit,
   timeout, exception rollback, surviving descendants, abrupt owner exit, large
   output, arguments/environment and unrelated-process isolation. Verify a
   windowless preparation shell remains console-free. These tests open no IDE
   and send no desktop input; native acceptance remains separately gated.
4. Update guidance, record actual evidence and limitations, and retain the
   outstanding native acceptance requirement. Never sweep processes by executable
   name or stop an unidentified existing Terminal session.

Implemented correction and headless evidence (2026-11-15):

- Shared `OwnedTestProcess` assigns a private kill-on-close job atomically using
  the process creation job-list attribute and inherits only explicit stdio
  handles. Functional IDEs, deployment, live-driver and auxiliary commands use
  it. Normal/nonzero root exit and disposal terminate remaining descendants;
  failure to establish containment fails the launch. Job ownership stays in the
  external test runner and adds no VSIX dependency.
- Example preparation uses `eng/Invoke-HeadlessTool.ps1` instead of PowerShell
  native invocation. It preserves argument quoting, working directory,
  environment, both redirected streams and exit status, including LASTEXITCODE.
  Preparation shells are noninteractive; all tools remain inside the fixture job.
- `dotnet test` on UIIntegrationTests with
  `--filter 'Area=ProcessCleanup|Area=ProcessCleanupPreparation' --no-restore
  -v quiet -m:1 -nr:false` passes **10/10**. TRX:
  `Iteration22/ConsoleCleanupFinal-net8/*13_20_59_net8.0.trx`. Nine launcher cases
  cover normal/failing roots, timeout, exception, abrupt supervisor death,
  surviving children, unaffected independent scopes, large output and exact
  arguments/environment. Console handles stay zero before/after native tools.
- The tenth case runs actual example preparation twice on edited isolated
  North/South/Zenon/Shared copies per host: dotnet, VS2022 desktop MSBuild and
  VS2026 desktop MSBuild. All three application restores exist, Shared has no
  build pipeline, source/project/membership/solution bytes remain unchanged and
  both preparation-console probes are zero. Host selection and logs:
  `Iteration22/ConsolePreparation/b795b009aa8b4ff6bf7b2935bc3ae75f/`.
- PerformanceTests with
  `--filter 'Area=ProcessCleanup|FullyQualifiedName~VisualStudioTestDeploymentTests|FullyQualifiedName~LiveVisualStudio'`
  and the same build options passes **62**, with **6** opt-in live cases skipped,
  on net472 (one second). TRX:
  `Iteration22/ConsoleCleanupFinal-net472/*13_26_04_net472.trx`.
  The nine cleanup cases pass on this runtime too; existing deployment/report
  coverage remains green. Both final builds emit no warnings.
- Headless tests launch no IDE and send no desktop input. No unidentified existing
  console/Terminal was terminated. Brokered historical console ownership is not
  proven by timestamps. Native failed-scenario cleanup and both prepared-example
  repeats are still required after execution approval; these results do not
  complete iteration 22 or its native gates. Refresh the prepared definition-fork
  fixture copies with this launcher/probe/helper before their native runs.

Resumed preparation (2026-11-15): the new `Area=ProcessCleanupNative` cases
inject a scenario exception through `UiTestRun` on each IDE, preserve the expected
failure artifacts and require the retained IDE/helper/descendant handles to exit.
Only then is `expected-failure-cleanup.txt` recorded. The root functional runner
compiles with zero warnings/errors; no native cleanup result is claimed yet.
Both existing definition forks receive the current iteration source changes,
including the shared launcher, helper, probe project and native regression.
Their build-only repeats both pass with zero warnings/errors: enabled 10.94
seconds and disabled 16.23 seconds (`build-console-cleanup.log` in each fork).
Both functional output directories contain the copied process-cleanup probe.
An initial enabled-build logging argument was unquoted, causing PowerShell to
interpret its semicolon suffix after the successful build; the quoted repeat
above exits successfully. This was a command-line preparation error, not native
acceptance evidence.

The remaining native batch is now **14 cases**: the earlier ten plus two
`ProcessCleanupNative` cases and two `DefaultSharedExample` repeats affected by
the preparation change. Run them sequentially, using their existing isolated
profiles/copies; the native cleanup cases intentionally fail an inner scenario
and pass only after teardown is proven. The automatic approval rejection remains
in force until explicit desktop readiness/execution confirmation. Resuming the
goal alone does not establish that readiness. No native run has been attempted
as part of this preparation.

The user explicitly confirms the desktop connected/unlocked/idle and approves
the 14-case native batch. The initial VS2022 batch
`TestResults/8d5ba9e50bd7427995ec9f070b27ecb4` exposes a launcher regression:
typing, tutorial and prepared-example cases time out before solution startup.
Read-only native inspection shows devenv treating quoted `/RootSuffix` and `/Log`
as file arguments, opening its ordinary profile with a missing-files dialog.
The owned processes are stopped by fixture cleanup/cancellation; read-only CIM
then confirms no devenv/testhost remains. No user IDE is terminated.
`OwnedTestProcess.Quote` now matches ArgumentList's minimal quoting, preserving
simple switches unquoted. The probe reports the raw Windows command line and a
new both-runtime regression checks the exact switch/path representation. Repeat
the affected native cases after those headless checks pass; the earlier batch
does not supply acceptance. The user's execution approval persists for these
corrected sequential runs.

Corrected launcher evidence: `ConsoleCleanup-Switches-net8/*13_42_07*` passes
10/10 cleanup cases; `ConsoleCleanup-Switches-net472/*13_42_42*` passes 63 checks
with six opt-in live cases skipped. Both include the raw switch serialization
regression. VS2022 native repeat `TestResults/021bd5dd1d15475e9a5c62176f8a056e`
passes injected-failure cleanup (21.78 seconds) and prepared-example/reopening
acceptance (226.78 seconds). Expected cleanup evidence:
`ProcessCleanupNative/2022/d28fed2c1bc744ec95035fbeca8343f0/expected-failure-cleanup.txt`;
prepared example `DefaultSharedExample/2022/154295904ec44342b83ee5a9d1c0e7d9`.
Typing/tutorial stop before keyboard input at foreground readiness.

The fixture now requests main-window activation through the owning DTE only
after external activation fails, and reports the actual foreign foreground PID
on timeout. Repeat `TestResults/5643f8db125044748255b071c1bd6039` identifies
WindowsTerminal PID 21356. Read-only CIM identifies an existing embedding broker
started 09:29:29; its tabs' ownership is not proven. The functional PowerShell
entry point also now invokes dotnet through the direct windowless helper, and
CLI observations during native runs avoid PowerShell native invocation.
Repeat `TestResults/4168b670b75547aca7f758f2e28ed9eb` confirms the old broker
still holds foreground; no keyboard input is sent there. Manual Terminal
minimization is requested because the computer-use skill prohibits Terminal UI
automation. No existing Terminal is stopped and keyboard scenarios stay pending.

VS2026 native cleanup passes independently through the windowless entry point:
`TestResults/88dc35eea5814b77ba9a5f8bbfb052ce` (20.89 seconds), expected-failure
fixture `ProcessCleanupNative/2026/b296d6063d86467db2e6f0559a06a023`.
Both IDEs now prove failed-scenario teardown of the exact owned IDE, helper and
descendant. Post-run CIM finds no devenv/testhost. This acceptance is separate
from keyboard behavior; no Terminal interaction is attempted.

Both definition forks rebuild the final corrected launcher/fixtures through
windowless native execution with zero warnings/errors: enabled 6.13 seconds and
disabled 6.16 seconds (`build-final-windowless.log`). Their ignored native batch
also uses the headless helper; it has not run. **Eleven native cases remain**:
VS2022 tutorial/typing, VS2026 shared/project-library/prepared-example, four
generated MetadataCollections and two standard-extension restoration checks.
Those cases await the requested manual Terminal minimization. Iteration 22
remains uncommitted; no final acceptance or completion is claimed.

Final source/documentation review while native input is pending resolves all
75 local links across eight changed guides/instructions. SHA-256 comparison
confirms both prepared forks match the current launcher, native declarations,
headless helper, functional entry point, probe and native/designer fixture
sources. Review of the production late-child menu change, its two regressions,
imported-library token coverage, successive-draft service coverage and the
classification measurement harness finds no additional implementation change
needed. Repository scope wording now consistently names direct RESX XML commands
and the separately deferred standard-table proposal. No unchanged suite is
repeated for this documentation-only clarification. The eleven native gates
and iteration-22 commit still await Terminal minimization.

The user minimizes Terminal and explicitly resumes the checks. Foreground
readiness now succeeds. The initial resumed VS2022 repeat
`TestResults/ed7130b8b2a64b1db2951388974830ad` exposes a fixture race during
F12: its guarded retry tries to reposition the caret while the first navigation
is replacing the designer with XML. The retry now only reads the original
caret/selection and retries once at the unchanged token; it never repositions
or retargets during navigation. The exact declaration-line assertion remains.
Corrected repeat `TestResults/3aa26d5be7324a35bc83ca2c11ef5185` passes both
VS2022 cases (typing 53.41 seconds; tutorial 67.87 seconds), including normal
owned-IDE cleanup. Tutorial fixture
`ExpressionTutorial/2022/b01b5f487d3e4381b5c493491cbe5bfe` completes the
checked-in sample's navigation, unsaved Rename, Undo/Redo and three-culture
Save/Build workflow. Typing fixture
`ExpressionTyping/2022/4cebd819edd74aefac9af209d5a79d47` records 1,001 alarms,
22 per-character-confirmed native keystrokes in 2492.14 ms and subsequent color
observation in 75.94 ms; no semantic window opens. These native observations
include focus/UIA/polling and remain separate from isolated API timings.
Nine native cases remain; VS2026 standard-profile checks are running next.

VS2026 repeat `TestResults/f914823764cf4c8bb84575421c06876f` passes shared
consumer Rename (68.36 seconds) and prepared-example/reopening acceptance
(177.15 seconds). Fixtures:
`SharedReferencesRename/2026/3bbbde60e7c64f1db51c4b351e642c06` and
`DefaultSharedExample/2026/115115ad835a4933986280c183291edf`.
The project-library case stops at actual pointer hit testing: its screenshot
shows a persisted floating empty Rename frame covering Summary. Its initial
setup now explicitly hides old semantic tool windows, as does the generated-
collection fixture; pointer guards and semantic assertions are unchanged.
That library case is being repeated. Seven native cases remain.

The first library setup correction alone still leaves a persisted floating
Summary behind the main editor. The fixture now gives that Summary an explicit
active 1000x650 viewport. Value-action readiness reacquires the current UIA
value element when analysis replaces the template, keeping physical hit tests
and final foreground checks. Subsequent attempts sometimes stop on new transient
Terminal PIDs, including an isolated run with no concurrent shell observations;
their origin is not established. Temporary WPF lifecycle logging is removed.
An optional process-start CIM subscription is canceled by WMI and supplies no
origin evidence; its cleanup also reports a null job ID. These optional trace
errors are separate from the following actual native result.

VS2026 project-library acceptance passes **1/1**, 76.94 seconds, in
`TestResults/22f4eb69d71b46c1b8510bbfd32567e2` and fixture
`SavedLibraryAnalysis/2026/32da69951e8948db921102bd5c799c90`. The supported
test entry point and its TRX checks finish successfully; only the surrounding
optional diagnostic cleanup errors afterward. Physical hit/click evidence names
the actual Resolved Name edit at (529,441). Explanation and physical source
navigation, coordinated library Rename/linked Undo, loaded-library saved-input
behavior and unloaded-library discovery all pass with owned-IDE cleanup. No
foreign Terminal is manipulated or stopped. Six generated/restoration native
checks remain; the final fixture sources are being rebuilt for both variants.

### Iteration 22 final generated-definition and restoration evidence

The generated fixture initially stops while revealing off-screen Count and
inherited Tags values. Its old whole-window UIA name crawl also encounters a
replaced element; a later initial Tags probe races template replacement. Actual
failed reports remain under the two isolated forks. Readiness now selects the
intended enabled designer mode through its supported selection pattern, expands
Checks explicitly and performs the existing absent-default focus/source check
before explaining Count. It reacquires Tags once per probe, tolerates absent
templates and retries UIA_E_ELEMENTNOTAVAILABLE only inside bounded readiness
waits whose predicates must be safe to repeat. The error code's UIA meaning is
documented by [Microsoft](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-error-codes).

Pointer reveal uses the outer visible content viewport, clipped by enclosing
scroll viewports, without assigning focus. Diagnostics establish that the old
small-step scroll advances only 16 pixels per poll, leaving distant inherited
values hidden under a sticky header before timeout. Page steps reveal distant
values; fine steps center nearby ones. Physical hit testing, final foreground
checks, private-draft/source preservation, exact explanations and reference
assertions remain required. Temporary production menu logging is removed; the
only production change in iteration 22 is the tested late-child menu preparation.

All four generated cases pass with definition-relative preview sources withdrawn
and restored by the fixture's finally block. Each report contains exactly one
executed/passing test and its fixture records `passed.txt` after owned-IDE cleanup:

| Definition / host | Report beneath its isolated fork | Duration |
| --- | --- | ---: |
| Procedures enabled / VS2022 | `9a546456/results-2022-7e79b8f1` | 146.50 s |
| Procedures enabled / VS2026 | `9a546456/results-2026-3ee341f0` | 142.06 s |
| Procedures disabled / VS2022 | `15176660/results-2022-edac34cd` | 152.93 s |
| Procedures disabled / VS2026 | `15176660/results-2026-310c74b8` | 138.43 s |

The first disabled/VS2026 attempt stops before value input at foreground
readiness; its focused repeat above passes with the same guards. These cases
cover generated metadata, defaults and sibling/inherited/Clear explanations,
immutable definition references, draft/collapse/edit/ordering preservation,
saved three-culture output, compiled collection templates without source XAML,
localized enum captions and optional-procedure presence/absence.

Standard extension restoration uses the root `eng/test-visualstudio.ps1` with
`-RootSuffix AlarmlistDefinitionExp -Filter Area=DesignerExpressionNodes` on each
host. VS2022 passes in `TestResults/aabe1a8749254c1f98bd59e457a8fd1d` (50.84 s).
VS2026's first repeat encounters an invalidated UIA text range during the optional
F12 retry. That read-only probe now declines the retry for a replaced range;
the exact declaration-navigation assertion remains. VS2026 then passes in
`TestResults/cc9caebb7cff4f2eb69ef5096447948b` (47.52 s). Both profiles contain
the standard matching extension again; post-run CIM finds no devenv/testhost.

### Iteration 22 completion audit

All accepted-scope implementation and acceptance gates pass. Audited TRX counters:
compiler **915 net472 / 917 net8.0**, project-analysis **57**, full host **984**,
SDK package **3 per framework**, desktop package/compilation/lookup **15 per host**,
and VSIX **14 per framework**. Generated collection runtimes pass both frameworks;
all **24** selected performance/allocation/cancellation cases pass, with actual
measurements and large single-document limitations above. Native tutorial,
typing, shared/culture, direct RESX XML, project/NuGet library, themes, edited
example preparation/reopening and generated-definition scenarios have passing
both-IDE evidence. Failed-test cleanup passes headless on both runtimes and
through injected native failures on both IDEs.

The tutorial consumes the checked-in four XML examples and completes unsaved
Rename/Undo/Redo/Save/Build. Source review confirms the production menu change
matches the already accepted 984-test host run; no later tracing or semantic
change remains. Analysis **10**, analyzer **5**, library **3**, export **1** and
semantic definition fingerprints are preserved. No compiler, exporter contract
or provider behavior changes are introduced in this iteration. No accepted-scope
gate remains open. Standard RESX-table commands stay deferred by the user's
2026-11-14 decision, with the failed public-API feasibility evidence retained;
XML/designer success is not table acceptance. Final review verifies 139 local
links across nine changed documents and passes `git diff --check`. The work
remains on dev/milestone-14; no merge or push is included.

## Iteration 23 — Restore designer Backspace (complete, 2026-11-15)

The user reports Backspace failing in designer value fields on VS2026. The XML
completion report is resolved by selecting a project context in the shared
HmiInstructions.almx input; no completion behavior change is authorized here.

Deliverable: reproduce the native Backspace failure before changing production
code, then give focused embedded expression editors the Text Editor keybindings
already used by XML. Follow focus transitions between XML, native value editors
and ordinary WPF controls, detach subscriptions on pane disposal, and route
deletion through the native command chain. Preserve private drafts, read-only
protection, completion, Unicode/multiline editing and document Undo/Redo. No
compiler/public API/contract change, merge or push is included.

Required validation: native beginning/middle/end and selection Backspace, Delete,
typing, scalar/parameter/procedure values, Unicode/multiline deletion, draft
cancellation and commit, source Undo/Redo, ordinary inputs, read-only values and
XML/designer focus round trips. Run focused checks and the affected host suite;
run native acceptance sequentially on VS2026 first and VS2022 afterward, including
XML expression completion with an explicit shared consumer and HMI input.
Record actual evidence separately below, then commit this iteration only after
its required checks pass.

### Iteration 23 actual evidence

Area=DesignerKeyboard adds real keyboard deletion through the installed
extension, with source/disk/output preservation and no direct command
substitution for the reported Backspace. The initial sandboxed launch is canceled
during preparation, without native results. The first unrestricted build finds a
missing test namespace import, corrected before the native run.

VS2026 red-before-fix: TestResults/63405276538847f3954cab6ecc2c498e fails 1/1 in
1m42s at the first physical Backspace: `abc $(P) xyz` stays unchanged. Fixture
DesignerKeyboard/2026/0a68226bb0844e8ea04d8966259bffa3 retains the failure and
normal owned-IDE cleanup. Production code was unchanged for that run.

The frame now inherits Text Editor bindings for a focused native expression
surface owned by this pane as well as XML. Handled focus events update ownership
between embedded editors, ordinary WPF inputs and external controls; disposal
detaches both subscriptions. Existing focused-surface command forwarding and
private-draft Undo remain unchanged. Required green checks are pending.

The first fix repeat passes the initial deletion checks, then fails typing after
UIA-only selection (31e8634cd2934c809bf97b7cdc90913d). The fixture now uses physical
pointer placement, extended-arrow selection and exact UIA caret/selection checks;
UIA Select alone is insufficient evidence of the native caret. Production is
temporarily returned to HEAD and verified unchanged for a second red run.
TestResults/2f543087dda244f48b36da4c252db64b fails the same first Backspace in
1m41s, after the actual end caret is verified, fixture
DesignerKeyboard/2026/b27d0e02140b463a8dba7a19c04d6d38. The focused ownership fix
is reapplied with explicit UI-thread guards in its event handlers.

Repeat 4426e4fb49aa48cbbfe01dc663f74e6d passes scalar deletion and fast typing,
then stops at the focused-field guard after pointer selection in an ordinary WPF
parameter cell. The fixture uses that cell's End key instead; native expression
fields retain physical pointer/caret checks. No production change is needed for
that fixture correction. TestResults/59b099435567494e9273a90a11f7daec then passes
VS2026 DesignerKeyboard 1/1 in 33 seconds: scalar start/middle/end and selection
deletion, forward Delete and typing, parameter/procedure deletion, protected
inherited values, Unicode and multiline drafts, cancellation/commit, source
Undo/Redo and WPF/XML/designer focus transitions. Source/disk/output assertions
and owned-IDE cleanup pass. Full host and both-host remaining acceptance follow.

The first full host run (Iteration23/Host) passes 983/984 with one timeout in the
existing ClosingDesignerDrainsQueuedNotificationsWithoutJoiningItsOwnTask WPF
fixture. Its isolated unchanged repeat passes 1/1 in 206 ms
(Iteration23/HostShutdownRepeat). The complete suite is repeated without
assertion/time-limit changes; no cause is attributed to user or RDP input.

The full unchanged repeat passes **984/984**, zero skips, in 2m26s
(Iteration23/HostRepeat). Packaged extension/consumer checks then pass **14/14**
on each of net472 and net8.0, in 11/10 seconds
(Iteration23/VSIX-net472 and VSIX-net8.0). Builds have no remaining threading
warnings. Final native keyboard/editor/shared-example checks run sequentially,
with the new HmiInstructions test selecting each consumer explicitly and checking
Ctrl+Space without changing source text. Both-host final results are pending.

VS2026 batch TestResults/898fb2ad0fea4c02a1971177d3230fe4 passes 3/4: native
designer expression nodes (38.46s), scalar commit/Undo (7.50s), and the edited
default example including HmiInstructions Ctrl+Space under both explicit consumers
(216.27s). XmlKeyboard fails its Split baseline: expected Name Before, source
still After XML. The fixture selected the first WpfTextView, which can now be an
embedded designer editor. Its source-focus helper now identifies the actual XML
surface by complete current document text, retaining exact edit/source assertions.
The new DesignerKeyboard pointer helper also verifies that the intended field,
not just its process, owns the pointer hit. Focused repeats of both fixtures on
both hosts remain required; no production behavior change follows these findings.

VS2026 focused repeat TestResults/cdacc7ca01e348b1a8a81e8aeee8d232 passes **2/2**
in 47 seconds: DesignerKeyboard with intended-field pointer hit checks and
XmlKeyboard with actual XML surface selection in XML/Split. All original source,
draft, Save/reopen, Undo/Redo and keyboard assertions remain. Together with the
three passing scenarios in the preceding batch, required VS2026 native acceptance
passes. The matching five-case VS2022 batch is running next, sequentially.

VS2022 batch TestResults/f9f4bcb86db14fcaa7318387df20dba6 passes 4/5: XML/Split
keyboard (15.31s), scalar commit/Undo (8.77s), default example with both explicit
HMI consumers (224.89s), and native expression nodes (70.55s). DesignerKeyboard
passes initial Backspace cases, then safely stops on its focused-field guard
while selecting a token. Diagnostic repeat 75742e8ca42e4ea7a5af4cbc8b52eba9 passes
all scalar checks and stops when UIA cannot focus a clipped parameter cell.
Captures show initial analysis pending and the alarm pane scrolled past that row.
The fixture now waits for the resolved project value and reveals controls in the
owning alarm viewport before focus, without editing or weakening input guards.

Repeat 999a84888621472b98547929a60521d9 stops before deletion when WindowsTerminal
PID 11280 takes foreground. Post-run CIM finds neither that Terminal nor an owned
IDE/testhost; its origin is not established and no Terminal is manipulated or
stopped. The fixture uses the existing bounded owned-IDE UIA/DTE activation check
before field input, retaining the final foreground/caret checks. Final focused
repeats on both IDEs remain open; production code is unchanged.

The readiness/viewport fixture passes VS2022 1/1 in 40.44s
(TestResults/e7fa23162a31468aa5940a7815f4c67c). Its VS2026 repeat
94c9876b66ea435190a4127d4de837d9 passes the initial deletion cases, then fails a
later repeated pointer-caret setup before forward Delete. The initial hit-tested
pointer regression remains; subsequent positions/selections now use physical
extended Ctrl+Home/Right/Shift keys and freshly acquired UIA ranges. No UIA Select
or direct command/buffer replacement substitutes for native editing.

Final focused cases pass with all assertions and owned-IDE cleanup:

| Host | Final DesignerKeyboard report | Duration |
| --- | --- | ---: |
| VS2026 | TestResults/6f094609940d45ef8eaa978da8619701 | 57.74s |
| VS2022 | TestResults/7979c5beba534ca1997ca58bdb158ad5 | 41.55s |

The final audit verifies each of the ten required native cases' passing TRX
entry and `passed.txt` cleanup marker: dedicated designer keyboard, XML/Split
keyboard, scalar commit/Undo, native expression nodes and edited default-example
preparation/reopening with HMI completion, on both hosts. The affected full host
suite passes **984/984** and packaged extension checks pass **14/14 per framework**.
Earlier failures and successful repeats remain above; no assertion or foreground
guard is relaxed. No required iteration-23 gate remains open.

The production change is limited to document-frame keybinding ownership and its
focus subscriptions. Compiler, SDK, completion-provider behavior and public
contracts are unchanged: analysis 10/analyzer 5/library 3/export 1 and semantic
fingerprints are preserved. Compiler suites are not rerun for this host-only fix.
Documentation covers deletion/draft behavior and explicit shared-file consumers;
contributor guidance records native field keybindings and exact source-view
selection in Split fixtures. Native foreground/UIA sensitivity and the separately
deferred standard RESX-table capability remain existing limitations. No merge or
push is included.

Validation commands use `dotnet test` for the host UnitTests project (full run,
then the isolated shutdown filter and unchanged `--no-build --no-restore` full
repeat) and the IntegrationTests project with `--framework net472` and
`--framework net8.0`, sequentially. Native entry point:
`eng/test-visualstudio.ps1 -VisualStudioPath $installation -Filter <selection>`,
with the discovered VS2026/VS2022 installations and selections
`Area=DesignerKeyboard`, `Area=Keyboard|Area=DesignerExpressionNodes|Area=DefaultSharedExample`
and the focused `Area=DesignerKeyboard|FullyQualifiedName~XmlKeyboardTests` repeat.
Each command writes the TRX artifacts recorded above. Final review passes
`git diff --check`, resolves 32 local links across five changed documents, verifies
all selected passing-case cleanup markers and finds no remaining IDE/testhost in
the final read-only process inventory. Only documentation changes follow these
checks; unchanged suites are not repeated.
