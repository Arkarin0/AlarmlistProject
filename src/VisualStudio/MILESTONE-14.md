# Milestone 14 — Explain this value

Status: original iterations 1–7 complete with required acceptance gates passed on
2026-10-29. Follow-up iterations 8–14 (Find All References and Rename) authorized;
iterations 8–12 complete, with UI/integrated acceptance iterations 13–14 pending.
Original evidence and performance limits remain below.

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
