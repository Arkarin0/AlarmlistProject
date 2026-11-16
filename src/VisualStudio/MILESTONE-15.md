# Milestone 15 — Semantic comparison and Git tools

Status: authorized implementation, iterations 1–13. Work on dev/milestone-15.
Iteration 1's documentation and integration gates pass. Later iterations remain
planned until their individual evidence and completion gates are recorded.

## Outcome and confirmed scope

One compiler-owned comparison/merge engine serves a dockable Visual Studio
Alarmlist Changes window, an independent Windows x64 desktop application and a
thin local VS Code Git/tool extension. A dedicated comparison playground supplies
project-pair and three-way merge examples, expected results and native acceptance.
The interactive UI concepts reviewed by the user show selectable alarm changes,
authored/resolved/source/coverage views, contributing sources, culture selection,
Base/Current/Incoming choices, private result editing and explicit VS Code launch
commands. They are design examples, not evidence of implemented product behavior.

Compare one selected project and its reachable dependencies, not an entire
solution. Include all cultures configured on either side. Visual Studio uses its
active configuration/platform; standalone shows each evaluated default and offers
explicit overrides. Match the logical pair of root projects even when their
names/directories differ. Compiler identities and existing case rules govern
member correspondence. Identity renames are additions/removals, never similarity
guesses. Unkeyed ordered entries must not be matched by effective index.

Keep authored changes, resolved effects, diagnostics and coverage distinct.
Ignore XML indentation/attribute ordering in semantic results, retaining significant
value text, expressions, empty/absent/deferred/invalid states and collection order.
Show effects of inherited templates, parameters, localization fallback and lookup
inputs. Historical navigation opens immutable captured text; live navigation
requires current input/source checks.

Both sides use one current compatible toolset, normally the current/right-hand
project SDK, explicitly labelled. Preserve each side's declared inputs and
dependency versions. Missing/unverifiable dependencies and incompatible semantic
definitions produce unavailable coverage, not a false unchanged result. Do not
restore, download, build or execute historical targets during capture/comparison.
Do not load exporters or third-party analyzers for this operation. Diagnostic
changes refer to compiler diagnostics and captured compiler diagnostic policy.
Normalize capture environment paths so temporary directories cannot invent value
changes, without rewriting authored strings. Freeze imports, membership/globs,
project libraries, immutable package inputs and tooling bytes before evaluation.

Direct editor comparisons capture supported committed unsaved XML-backed source,
resource, lookup and project/import buffers through a comparison-only overlay.
Ordinary analysis retains its saved dependency/resource rules. Never implicitly
commit designer drafts or save a document. If authoritative pending text cannot
be captured through supported services, report unavailable coverage. Standard
RESX-table commands/private-cell capture remain deferred; no production private
reflection/UI automation is authorized.

Full VS Code language authoring, embedded VS Code comparison, remote/virtual
workspaces, other operating systems and ARM64 are excluded from this first release.
Unknown external library consumers and reconstruction of historical incompatible
toolsets/definition formats are not promised. Project/build files may affect
resolved comparisons, but their conflicts have no semantic MSBuild merge engine.
File rename/delete, binary and other unsupported filesystem conflicts remain
explicitly unsupported. Plain text/XML manual resolution is separately labelled.

## Architecture and compatibility

Follow the existing compiler/Text → Syntax → Binding → Evaluation → Analysis
boundary, Roslyn's separation of workspace contracts/services from IDE hosts, and
the repository's pinned Arcade props/targets/artifact conventions. References:
[Roslyn Workspaces](https://github.com/dotnet/roslyn/tree/main/src/Workspaces),
[Arcade SDK](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk).
These guide layering; they do not add Roslyn runtime dependencies or replace the
existing Arkarin0.DotNet.Arcade.Sdk build policy.

Introduce host-neutral comparison contracts/orchestration beneath src/Comparison,
targeting the existing net472/net8.0 pair. Copy immutable primitive/collection data
across host boundaries, never compiler/MSBuild/WPF/editor/reflection objects.
Host adapters supply input capture, SDK invocation, process ownership, navigation
and publication. Preserve VS's project-owned SDK isolation; standalone uses an
isolated worker and local SDK discovery, without Visual Studio assembly/runtime
dependencies. Shared code must not obtain semantics through XML/text matching.

Add opt-in public compiler APIs CompareSnapshots(ComparisonRequest,
CancellationToken) and PreviewMergeSnapshots(MergePreviewRequest,
CancellationToken). Requests carry frozen inputs, explicit before/after or
base/current/incoming contexts, document correspondence and snapshot stamps.
Merge requests include explicit choices or edited proposal text. Results detach
differences, source origins, coverage, diagnostics and exact versioned source
replacements/proposed-result validation. No Git/filesystem/editor dependency is
permitted in compiler APIs; normal analysis retains no comparison/merge graphs.

Keep the foundation internal through iteration 7. Iteration 8 exposes the complete
APIs and activates analysis contract 11 across compiler, SDK and VS together.
Preserve analyzer 5, library format 3, export 1 and semantic definition fingerprints.
Retain current tooling/schema matching and XML-only recovery for incompatible
tools. Standalone/editor session protocol 1 is independently versioned and
negotiated; its capabilities must not advertise incomplete compiler support.

The standalone host targets net10.0-windows and ships as a self-contained portable
directory/ZIP with Alarmlist.Compare.exe, dependencies and license notices.
Do not retarget existing compiler/SDK/VS projects. Use AvalonEdit for standalone
source/result editing and DiffPlex for textual differences, pinning reviewed
versions. Both are presentation dependencies, not alternate semantic parsers.
Visual Studio retains native text/diff services. Standalone project evaluation
requires available compatible SDK tooling; file-only operation can remain
explicitly source-only when tooling is absent. No Visual Studio installation is
required. A clean deployment environment is an acceptance gate, not an inference
from running on this developer PC.

## Entry points, Git and application

Stable public executable modes:

```text
projects <left.almproj> <right.almproj>
revisions --project <project.almproj> --left <endpoint> --right <endpoint>
diff-files --left <file> --right <file> --path <original-path>
merge-files --base <file> --ours <file> --theirs <file> --output <file>
configure-git --scope repository|user
```

Endpoints distinguish commit:<ref>, index and worktree. Resolve refs to fixed
object IDs; do not silently retarget a displayed historical comparison. Launch
without arguments opens the project-pair chooser. Project comparison is read-only
and needs neither Git nor VS. Source inspection remains explicit when semantic
capture is unavailable.

Git difftool supplies temporary file pairs, not whole revision projects. Its
adapter shows authored file differences; an explicitly selected optional project
context may evaluate file variants against that common captured context. Never
label that result as two complete historical project snapshots. Git mergetool
uses the provided Base/Local/Remote inputs and writes only MERGED. Respect its
actual stage roles, including rebase cases, without guessing branch identities.
References: [difftool](https://git-scm.com/docs/git-difftool),
[mergetool](https://git-scm.com/docs/git-mergetool).

Exit 0 means normal completed comparison (differences are not an error), or a
merge explicitly accepted and saved. Exit 1 means cancelled/unresolved work;
exit 2 means execution/configuration failure. Keep a Git-launched process alive
through review. Preserve all input files. Revalidate destination identity/bytes
before atomic replacement; failed/cancelled review must not overwrite output.
The tool does not stage/commit. The calling git mergetool may stage successful
resolutions normally; documentation and tests must distinguish these actions.

Semantic merge supports ALMX, string RESX and lookup XML, preserving comments,
entities, CDATA, encoding and untouched formatting. Compose independent keyed
definitions/named fields conservatively. Same-value, parent/delete/Clear and
ambiguous ordered-entry conflicts require explicit resolution. Full project mode
requires reliable validation and resolved structural conflicts. New compiler
diagnostics require acknowledgement. Unavailable validation cannot be ignored.

Source-only mode is a deliberate exception for external file workflows with no
usable project. Offer reviewed manual source resolution, clearly acknowledge that
project results were not validated, and never guess semantic merges/schema rules.
Require well-formed XML for XML results. Unsupported binaries are unavailable.
Source-only acceptance is not evidence for full-project validation.

Git setup previews precise writes and defaults to repository scope. Register only
the named Alarmlist diff/merge definitions and merge exit-code handling. Defaults
change only by explicit selection; preserve existing tools and unrelated settings.
Apply accepted settings completely with freshness checks/rollback. Do not install
drivers, change config automatically or add machine-specific paths to the repo.

Visual Studio provides Compare alarms… and Resolve alarm conflicts… project
commands, a dockable Changes window, private result editing and native immutable
source diffs. Apply a complete approved set through Query Edit and one linked Undo
transaction, rolling back all touched buffers on failure. Leave files unsaved.
Saving/staging/branch operations remain with existing Git/editor workflows.

VS Code is a thin local extension: project/revision comparison, configured Git
diff/merge launch and Git setup through Command Palette and relevant menus. Use
the built-in Git extension's public getAPI(1) repository/executable discovery and
honor effective computer Git configuration, including other tools. Preserve native
Open Changes. Use argument-vector process execution; let Git interpret its own
configured commands. Git operations use disk/index inputs: no autosave and no
merge launch over a dirty target. Direct project comparisons can include frozen
unsaved buffers with versions/identities through protocol 1. Changes invalidate
requests/previews. Find the separately installed application through a machine
setting or PATH and verify its capability handshake; do not auto-download it.

## Dedicated comparison playground

Create independent example/Comparison with its own solution, local build/restore
boundary and README, leaving default North/South/Zenon unchanged. Two complete
Before/After applications use standard definitions, XML exports and cultures
keyword;en;de-DE, initially selecting en. Each emits three concrete alarms per
culture; abstract templates do not emit.

| Scenario | Before | After |
| --- | --- | --- |
| Common identities | Demo.Pump.Ready, Demo.Pump.Fault | Same |
| Removal/addition | Demo.Legacy.Sensor | Demo.Safety.Door |
| Project value | Site=North | Site=South |
| Local Code | Ready P-001 | Ready P-010 |
| Shared template Category | Utilities | Safety; both pump alarms inherit |
| Localization | English/German captions, one German key falling back | Changed caption and completed German key |
| Lookup label | Enable | Start |
| Procedure | Original ordered instructions | Changed sequence |
| Formatting | XML entities/text | Equivalent CDATA/formatting |
| Escaped text | $$(Site) | Same literal |

Check in an expected-results manifest for identities, representative resolved
values, fallback/diagnostic changes and ignored formatting noise. Verify it;
never regenerate expected values from actual output during preparation/tests.

Provide Base/Ours/Theirs source fixtures with independent named-field edits, a
competing alarm value, translation, lookup label and procedure sequence. Document
the deterministic resolution: both independent changes, Ours for the alarm value
and procedure, Theirs for translation/lookup. Preparation may create an ignored
private Git repository with local fixture commits/branches. Never mutate the real
repository/branches/remotes/configuration or fetch/push. Starting the example merge
is an explicit tutorial/test action. Use only local fixture identity/settings.

eng/prepare-comparison-example.ps1 prepares matching SDK packages with a content-
derived private cache and ignored local config, without optional analyzers/exporter
packages. Preserve edited sources/projects/membership/solutions on repeated runs;
new Git scenarios use fresh directories. Native/tutorial acceptance reads these
assets, rather than maintaining divergent illustrative samples. English guides
cover standalone, VS, VS Code Git-tool launch and resolution through the templates.

## Iteration plan and completion gates

Each row depends on completed prior rows. Commit successful iterations before
starting the next, including code, tests, docs and actual evidence; summarize its
commit/checks/blockers. Failed essential gates remain open. Only initial setup
pushes are authorized; later pushes need a separate request.

| Iteration | Deliverable and completion gate |
| --- | --- |
| 1 | Refresh origin; integrate/push feature 14 to main; create feature/dev 15; commit this plan/backlog/instructions; push both initial branches. Recheck clean baseline/history before publication. Documentation review only. |
| 2 | Neutral contracts, adapters and capabilities; prove standalone/editor independence, native VS diff and coordinated transactions, VS Code Git discovery and cancellable owned process cleanup. Early essential feasibility gates on supported hosts. |
| 3 | Frozen project/revision/index/worktree capture, imports/globs/dependencies/cultures/editor overlays. Prove path consistency, freshness and no automatic restoration/build execution. |
| 4 | Dedicated Before/After playground, merge fixtures, expected manifest and preparation. Verify builds/three-alarm counts and preparation twice on edited copies using supported SDK/build hosts. |
| 5 | Internal compiler comparison for identities, scalars, parameters, defaults and inheritance. Test significant values, unavailable states and project/library origins on both frameworks. |
| 6 | Complete comparison for localization/fallback/lookups/procedures/Clear/generated collections, cultures and diagnostics. Validate definition and procedure variants. |
| 7 | Source-preserving compiler merge proposals, conservative units, explicit resolutions and private edited-result validation. Validate dedicated merge fixtures and interacting edits. |
| 8 | Complete public APIs and coordinated analysis-11 compiler/SDK/VS bridge, standalone worker and protocol. Pass packaged compatibility/isolation/cancellation on both task runtimes and desktop hosts. |
| 9 | Standalone project/revision/file comparison UI, diagnostics/coverage/navigation and portable packaging. Validate project pair on Windows without VS. |
| 10 | External Git file diff/merge, manual source-only fallback, atomic output/exit codes and opt-in config setup. Real isolated Git success/cancellation/stale/config-preservation acceptance. |
| 11 | Dockable VS experience, native/private editors, acknowledgement, Query Edit and linked Undo/rollback; themes/accessibility/keyboard on VS2022 and VS2026. |
| 12 | Thin VS Code package, configured computer Git tools, dirty-target guards, project/revision sessions and unsaved overlays. Dedicated playground native/extension-host acceptance. |
| 13 | Integrated parity/library/package/shared/fork scenarios, English tutorials, performance and final regressions; record actual limitations before acceptance commit. |

Run compiler/affected downstream on net472 and net8.0; standalone on its target
runtime; packaged bridge checks on both desktop MSBuild hosts. Run desktop builds,
WPF and native VS2022/VS2026/standalone/VS Code checks sequentially under the shared
desktop mutex, retaining actual focus/input checks and owned-process cleanup.
RDP interference is not inferred from a covering Terminal: inspect ownership.

Verify independent app deployment without VS, plus file mode with absent SDK;
selected computer Git/config; quoted/Unicode/temp paths; request lifetime, exact
output and caller Git index state; missing dependencies and fingerprints; all
cultures and typed states; immutable historical/package navigation; stale buffers,
repository/index changes, Query Edit reloads, cancellation, rollback and linked
Undo. Keep default example's 12/8 alarm regression. Measure existing large fixtures
for ordinary analysis versus comparison/merge, SDK-domain allocations, cold/warm
startup, cancellation and bounded presentation; no arbitrary latency thresholds.

## Actual evidence

### Iteration 1 — planning and setup

Started from clean dev/milestone-14 at 08989ef. Origin was refreshed; local main
matched origin/main and was 24 commits behind feature/milestone-14, with no
divergence. Fast-forwarded main to 08989ef and successfully pushed origin/main.
Created feature/milestone-15 and dev/milestone-15 at that baseline and switched to
dev. The completed milestone-14 branches were not changed. No source changes or
builds were required for integration. Initial milestone-15 branch pushes follow
the reviewed planning commit.

Reviewed existing SDK capture/bridge/transactions, project build conventions,
documentation/templates, standalone/native editor and Git APIs during planning.
Consulted the architectural references linked above. This contract preserves
existing compiler layering and adapts host-neutral workspace services instead of
introducing a second semantic parser or replacing repository build policy.
Final review passes git diff --check and verifies 91 local links across this
plan, AGENTS.md and FUTURE-FEATURES.md. Reviewed paths, planned capabilities,
contract staging and preservation of existing milestones. Builds/tests were not
run: this iteration changes only documentation and branch references. The focused
planning commit and initial branch publication complete iteration 1; their object
IDs are retained in Git and summarized to the user before iteration 2.
