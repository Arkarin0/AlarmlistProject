# Milestone 14 — Explain this value

Status: authorized on 2026-10-29; implementation in progress.

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
