# Milestone twelve: XML IntelliSense and native Shared Projects

Status: implementation authorized on 2026-10-27; iterations 1–6 complete.
Iterations 7–8 remain to be implemented and validated.
The owner confirmed the scope and recommended defaults and activated the goal
on `dev/milestone-12`. Maintain at least 15% remaining account usage allowance;
check usage throughout and stop before crossing that threshold. Do not merge or
push without instruction. Iteration 0 records planning; iterations 1–8 are authorized.
Existing milestone acceptance and outstanding release gates remain unchanged.

## Outcome and confirmed scope

Provide useful IntelliSense while editing `.almx`, `.almlookup` and `.almproj`
XML, and support native Visual Studio Shared Projects (`.shproj`/`.projitems`).
Each `.almproj` consumer compiles shared ALMX sources, resources and lookups in
its own context. Shared Projects produce no independent alarm-library output.
Support Visual Studio 2022 17.9+ and Visual Studio 2026 x64, with command-line
consumer builds on both existing task frameworks (`net472` and `net8.0`).

Confirmed by the owner on 2026-10-26:

- XML IntelliSense includes all three file types, rather than ALMX alone.
- Shared-project support means native Visual Studio Shared Projects, rather than
  only hand-authored imports or linked files.

The owner accepted the recommended defaults on 2026-10-27. Technical integration
choices remain subject to iteration 1's feasibility evidence.

## Existing implementation and architectural boundaries

- `Editor/AlmxEditorPane.cs` already creates a native XML code window over the
  shared document buffer. `AlmxContentType.cs` maps ALMX to XML; the standard XML
  editor is also registered through `Alarmlist.XmlEditor.pkgdef`. Preserve native
  coloring, formatting, outlining, XML commands and the existing Undo history.
- Designer completion lives in the existing view models and expression controls,
  including `UI/ExpressionCompletionContext.cs` and
  `UI/EditFileControlViewModel.Properties.cs`. Reuse candidate semantics and
  detached analysis; separate lexical XML caret context from compiler resolution.
  Do not duplicate inheritance, localization or expression evaluation in a host
  completion provider.
- `Analysis/ProjectAnalysisRegistry.cs` discovers multiple owning projects for a
  physical file. Extend the existing editor project selection and buffer capture
  lifecycle rather than assigning a shared document an arbitrary consumer.
- SDK `tools/Alarmlist.props`, `Alarmlist.DesignTime.targets`, item rules and build
  targets already govern evaluated source/resource/lookup inputs. Imported shared
  items must flow through those same compiler inputs and analysis capture.
- [Milestone 9](MILESTONE-9-DESIGN.md) defines library dependencies and saved
  dependency capture. Shared-source imports belong to each consumer's own inputs;
  they must not accidentally inherit the saved-only library-document policy.
- [Milestone 11](MILESTONE-11.md) governs generated fields, collections, optional
  procedures, definition compatibility and detached metadata. Completion must
  follow the rebuilt definition, including procedure-disabled forks.

Follow the repository's Roslyn direction: compiler-owned semantics, immutable
snapshots and editor services operating on versioned inputs. Follow Arcade's
existing SDK/package-consumer test organization; preserve the pinned build SDK,
root imports and target frameworks. No new Roslyn dependency is assumed.

Reference starting points:

- [Visual Studio Shared Project references](https://learn.microsoft.com/en-us/visualstudio/ide/managing-references-in-a-project#shared-project-references).
- [IVsSharedProjectReference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.ivssharedprojectreference?view=visualstudiosdk-2022).
- [Roslyn editor features](https://github.com/dotnet/roslyn/tree/main/src/EditorFeatures).
- [Arcade SDK tests](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.Tests).

Microsoft's reference confirms shared sources compile into each consumer without
separate binary output. Upstream Roslyn/Arcade directory retrieval was unavailable
during planning; inspect relevant implementations before introducing a structural
pattern in iteration 1. The local source observations above are the planning basis.

## Proposed behavior and design decisions

### XML IntelliSense

- ALMX: suggest legal elements and attributes at the current position, supported
  literal values (including enum tokens), alarm references and expression
  candidates. Cover parameters, localization keys and named arguments, lookups,
  named lists, generated scalar/collection fields and record sibling expressions
  only where valid. Use stored enum tokens for insertion; captions are display
  information. Do not insert inherited values or definition defaults implicitly.
- `.almlookup`: offer structure, attributes and constrained values from the
  authoritative `src/Compiler/Lookups/AlarmlistLookup.xsd`. Preserve its namespace
  and literal metadata rules; do not treat literal catalogue values as expressions.
- `.almproj`: preserve native MSBuild XML completion and add Alarmlist SDK item,
  property and metadata guidance, including source/resource/lookup items and
  shared imports. Do not replace general MSBuild completion or promise arbitrary
  evaluated MSBuild property completion. Identify the native provider and any
  supported extension mechanism in iteration 1.
- Cover embedded Split/XML modes and Open With > XML Editor where supported.
  Keep ordinary unrelated XML files unaffected. Do not require authored namespace
  or schema-location changes solely to activate completion.
- Offer explicit completion invocation and appropriate automatic triggers.
  Completion replaces only the intended token/span, escapes XML correctly, avoids
  duplicate quotes/delimiters and is one Undo operation. Escape dismisses the list;
  Enter/Tab commit behavior must coexist with native XML editing and be documented.
- Tolerate incomplete tags, attributes and expressions. Suppress inappropriate
  suggestions in comments and unsupported literal contexts. Respect read-only
  buffers, cancellation and version changes between request and insertion.
- Context-free structure remains available when analysis is pending, invalid or
  no project owns the file. Semantic candidates require a current compatible
  selected consumer; never silently combine candidates from different projects.
  Do not introduce a new competing diagnostic engine or network schema downloads.

### Shared Projects and consumer context

- Provide a native Shared Project creation path for Alarmlist inputs, Add/Remove
  Shared Project reference integration and Solution Explorer browsing/editing.
  Iteration 1 must prove the required project factory/capability, reference-manager
  and template integration on both hosts before a full design is committed.
- `.projitems` contains consumer imports with paths relative to its own location.
  Consumers use normal evaluated items, conditions and metadata. Standalone
  consumer builds must not require the Shared Project to be loaded in the IDE or
  execute `.shproj` as a compiler/build dependency. Record desktop tooling
  prerequisites separately from command-line consumer prerequisites.
- Shared ALMX, RESX and lookup files participate in each consumer's parameters,
  cultures, resources, references and diagnostic policy. Retain physical-source
  EditorConfig discovery. Shared and consumer resources/lookups obey existing
  duplicate/key rules; shared imports do not introduce override precedence.
- Define canonical physical input identity and duplicate-inclusion behavior before
  implementation, including overlap with default globs and repeated imports.
  Preserve compiler diagnostics for genuinely conflicting declarations; do not
  silently discard distinct content or change alarm ordering.
- One physical document has one text buffer/Undo history. Its unsaved ALMX text
  updates every owning consumer's analysis independently. Existing saved-only
  RESX/lookup rules continue to apply. Designer drafts remain private until the
  existing document transaction commits them to that buffer.
- Reuse the existing consumer selector in the designer and provide an equivalent
  discoverable context selection for XML-only editing. Switch completion,
  effective values, navigation and diagnostics consistently; preserve drafts.
  Alarm Summary stays pinned to its selected project. Attribute Error List
  diagnostics to their consumer, even when physical locations coincide.
- Watch shared imports and included inputs through edits, rename/removal,
  configuration changes and failed recapture. Discard stale results on context
  switch/unload; release subscriptions on close. Removing a reference removes
  the import, not source files. Revalidate before source edits/navigation.

## Exclusions

No runtime per-project alarm definitions, new expression syntax, compiler
inheritance semantics, nested collections or keyed record overrides. No standalone
shared binary/package format, implicit library conversion or NuGet distribution of
shared source projects. No general-purpose XML/MSBuild language server, VS Code,
TwinCAT or other IDE host. No expanded RESX designer or XLIFF editor IntelliSense.
No automatic source rewriting, default materialization or mandatory schema markers.

## Numbered iterations and planned acceptance

Each successful implementation iteration includes focused tests, documentation,
actual evidence and a commit before the next begins. Essential native checks must
pass on both hosts; unavailable checks block the iteration's completion. Run native
fixtures sequentially, use idle experimental profiles and never close a user's IDE.

| Iteration | Deliverable | Dependencies | Validation and completion criteria |
| --- | --- | --- | --- |
| 0 — Planning | Record confirmed scope, proposed behavior, questions and gates; update feature/instruction links. | None | Review diff, links and consistency. Documentation only; no implementation claim. |
| 1 — Native feasibility and design contract | Minimal isolated ALMX completion integration and native Shared Project prototype; identify `.almproj` native completion support and `.almlookup` schema association. Resolve project factory, templates, references and context selection design; record upstream inspection and prerequisites. | 0; resolve required product questions | On VS2022 and VS2026, open shared files, add/remove a shared reference, invoke completion in embedded and standalone XML views and build a consumer. Verify native XML/MSBuild completion coexistence. Update this plan with findings before expanding implementation; failure is an explicit blocker, not a reduced-scope success. |
| 2 — ALMX structural completion | Definition-aware legal elements, attributes and literal values; tolerant caret/span handling and native command integration. | 1 | Unit coverage for incomplete XML, escaping, enum insertion and invalid contexts; native invocation, commit, dismissal and Undo in both XML surfaces on both hosts. Alternate definition fixture includes generated collections and disabled procedures. No behavior regression in designer/split editing. |
| 3 — ALMX semantic completion | Reuse detached candidates for references and context-appropriate expressions; share candidate logic with designer where appropriate. | 2 | Scope, localization arguments, lookup/list and record sibling tests; stale/cancelled requests and invalid analysis. Native unsaved edits refresh suggestions; project switches isolate candidates. Existing designer completion suites remain passing. |
| 4 — Lookup and project XML completion | `.almlookup` schema completion and Alarmlist `.almproj` guidance extending available native facilities. Document exact supported contexts. | 1; 2–3 integration patterns | Schema drift/packaging checks, ALMX-independent lookup editing, item/property/metadata suggestions and ordinary MSBuild coexistence on both hosts. Packaged extension and SDK tests verify required assets ship. No source schema-marker requirement. |
| 5 — Shared input build contract | Consumer imports, paths, evaluated conditions, duplicate policy and source/resource/lookup capture; no independent shared output. | 1; duplicate policy resolved | Packaged SDK tests on net472/net8.0 and desktop MSBuild: two consumers produce different expected outputs from shared parameters/resources; condition changes, spaces/relative paths, duplicate overlap, missing/restored imports, build/rebuild/clean and library consumers. Analysis/build inputs agree. |
| 6 — Native Shared Project management | Supported project/template creation, reference-manager Add/Remove, tree/item management and imported item ownership. | 5 | Both hosts: create, save/reopen, add/remove reference and item, rename, unload/reload; source preservation and persistence checks. Consumer builds also pass with Shared Project unloaded. Existing library/NuGet reference tests remain passing. |
| 7 — Shared document context and lifecycle | Common consumer selection across designer/XML, unsaved ALMX fan-out, consumer-specific diagnostics/navigation and completion, import watches and cleanup. | 3–6 | Two-consumer automated unit/native scenarios on both hosts: different candidates/effective values, shared buffer/Undo, drafts surviving view/context changes, save/rename/import loss/recovery, configuration switches and unload during pending analysis. Summary remains project-pinned; no stale insertion or cross-consumer results. |
| 8 — Combined acceptance and usage docs | Persistent small shared-project example, user documentation and final regression evidence. | 2–7 | Repeat packaged command-line/desktop tests and both-host end-to-end shared XML/designer workflow. Record completion latency/cancellation and lifecycle measurements against a reproducible baseline, without machine-dependent timing assertions. Record residual manual/vendor/release gates separately. |

### Iteration execution details

The original iteration numbers above remain stable. Execute 1–8 in order;
dependencies in the table identify the technical prerequisites. The following
breakdown makes each commit boundary reviewable without postponing behavior tests
to iteration 8. Implementation was subsequently authorized on 2026-10-27.

#### 1 — Native feasibility and design contract

- Inspect existing native XML command routing, project selection and reference
  management, plus relevant upstream Roslyn/Arcade patterns. Inspect installed
  Shared Project support and completion APIs on each supported IDE host.
- Build the smallest isolated test fixture proving ALMX completion, lookup schema
  association, MSBuild completion coexistence and native shared-reference
  integration. Use one shared source and two consumers; keep prototype-only assets
  out of production registration unless they form a supported vertical slice.
- Record the chosen integration points, required installed components, deployment
  assets and any compiler/SDK/host contract changes. Resolve consumer-selector,
  template and duplicate-policy decisions before dependent implementation.
- Define repeatable completion/lifecycle measurements and report interpretation
  from the baseline. Identify exact native scenarios and automation entry points.
- **Exit:** both-host feasibility evidence and an updated design contract in this
  document. Existing packaged extension remains usable. If an essential feature
  cannot be proved, record the blocker and revise the plan before proceeding.

#### 2 — ALMX structural completion

- Implement tolerant XML caret-context and replacement-span handling, then wire
  completion to the existing XML text view and command chain.
- Supply valid structural names, attributes and literal candidates from fixed
  ALMX grammar plus generated descriptors. Include collections, record fields and
  optional procedure capability without hard-coding the standard four fields.
- Cover prefixes, incomplete tags/quotes, comments, namespaces, escaped values,
  read-only content and document-version changes. Insert only the selected text;
  opening or dismissing completion must never change source.
- **Validation:** focused editor tests, generated-definition checks and sequential
  native tests on both hosts for Split/XML and standalone XML views, keyboard
  invocation/commit/dismissal, Undo/Redo and ordinary XML editing coexistence.
- **Exit:** useful structure completion with no project analysis dependency;
  native XML behavior and designer drafts remain intact. Document triggers and
  commit keys with the feature.

#### 3 — ALMX semantic completion

- Extract reusable detached candidate construction from existing designer logic
  only where required; retain compiler-owned semantics and existing public
  contracts unless iteration 1 established a coordinated contract transition.
- Map XML position to file/alarm/record scope. Offer applicable references,
  properties/parameters, localization keys/arguments, lookups and named lists.
  Record sibling suggestions are restricted to their record context.
- Tie requests to document version, selected consumer and compatible analysis
  generation. Cancel or reject stale requests and insertions; structure completion
  remains available while semantic analysis is unavailable.
- **Validation:** scope and candidate parity tests against existing designer
  behavior, unsaved ALMX refresh, selected-project isolation, escaped/nested
  expression cases and native insertion/Undo on both hosts.
- **Exit:** semantic XML completion and designer completion use consistent input
  semantics; invalid or stale analysis cannot supply another consumer's candidates.

#### 4 — Lookup and project XML completion

- Associate lookup completion with the authoritative packaged lookup XSD using
  the supported mechanism proven in iteration 1. Do not alter authored files to
  activate it or interpret literal lookup metadata as expressions.
- Extend native MSBuild completion with the supported Alarmlist items, properties
  and metadata, including shared-import authoring. Record the supported contexts
  explicitly; retain native general MSBuild suggestions.
- Include required schema/completion assets in packaging and add checks that
  prevent drift between shipped assets and authoritative definitions.
- **Validation:** editor and package-consumer tests, both-host lookup/project XML
  keyboard scenarios, incomplete/invalid XML and unrelated XML/MSBuild regression
  checks. Verify installed artifacts rather than only project output directories.
- **Exit:** all three confirmed XML file types have documented, installed
  completion behavior; any platform limitation is an explicit unresolved gate.

#### 5 — Shared input build contract

- Implement and document `.projitems` inclusion of ALMX, resources and lookups,
  import-relative paths, consumer conditions and existing item metadata.
- Apply the agreed physical-file duplicate policy consistently to build and
  analysis capture, preserving declaration diagnostics and deterministic order.
  Test conflicting metadata instead of silently choosing whichever import wins.
- Capture imported items and discovery/watch inputs using existing SDK/analysis
  boundaries. Do not execute shared project targets during IDE input capture.
- **Validation:** isolated packaged consumers on net472/net8.0 plus desktop
  MSBuild; two consumers with different parameters/cultures/resources, default-glob
  overlap, repeated imports, missing/restored imports, conditional inclusion,
  paths with spaces, library output and Build/Rebuild/Clean output preservation.
- **Exit:** evaluated input sets and resolved output agree between build and
  analysis. Consumers build independently of the `.shproj` being loaded; shared
  sources produce no separate artifact. Include SDK usage documentation.

#### 6 — Native Shared Project management

- Add the supported Alarmlist Shared Project creation/template path and native
  project registration selected in iteration 1.
- Connect Add/Remove Shared Project reference to persisted consumer imports.
  Display owned shared items and their consumer projections in Solution Explorer.
- Support adding, opening, renaming and removing items with correct ownership;
  reference removal must preserve the physical shared files.
- **Validation:** packaged VSIX/template tests and sequential native scenarios on
  both hosts: create two consumers, add/remove shared reference, edit items,
  save/reopen solution and unload/reload. Run existing library/NuGet reference
  regressions; verify command-line builds after IDE-authored reference changes.
- **Exit:** native Shared Project workflows persist correctly and support all
  three shared input types. Include user-facing creation/reference instructions.

#### 7 — Shared document context and lifecycle

- Implement the agreed consumer selection across designer, embedded XML and
  standalone XML views; show unavailable/ambiguous context explicitly.
- Fan out committed buffer changes and unsaved ALMX edits to every owning
  consumer while retaining one physical buffer/Undo history and private drafts.
  Keep resource/lookup saved-input behavior and Summary project pinning.
- Attribute diagnostics/navigation to the consumer; recover watches after import
  failure, reference removal, item rename and configuration changes. Cancel pending
  work and detach subscriptions when documents/projects close or unload.
- **Validation:** unit concurrency/lifecycle tests and both-host two-consumer
  scenarios with differing candidates, effective values and diagnostics. Verify
  context switching with drafts, shared Undo/Redo, pending-analysis unload,
  failed-capture recovery and stale completion/navigation rejection.
- **Exit:** no arbitrary consumer choice, cross-consumer result leakage or stale
  edit; drafts survive view/context operations. Document context selection rules.

#### 8 — Combined acceptance and usage docs

- Add a small persistent Shared Project example with two consumers and shared
  ALMX, translations and lookups; preparation must preserve user-edited sources.
- Exercise the installed SDK/extension together: create/reference shared inputs,
  use completion in all three file types, edit unsaved ALMX, switch consumers,
  inspect diagnostics/Summary, save, build, reopen and remove a reference.
- Run the relevant compiler/task/SDK downstream suites for any changed contracts
  on both frameworks, extension unit/package suites and the combined native
  scenarios on both hosts. Reuse prior passing evidence unless integration changes
  require repetition; record exact commands and artifact versions.
- Measure completion responsiveness, cancellation and lifecycle cleanup using
  iteration 1's baseline, without machine-dependent timing assertions. Retain separately deferred release gates.
- **Exit:** all scoped essential gates pass with evidence, examples/docs agree
  with shipped behavior, and remaining limitations are explicit. No iteration is
  called complete solely because its implementation is present.

### Accepted defaults — 2026-10-27

The owner authorized the following recommended defaults with implementation:

- Use the opening consumer as the initial shared-file context when it is known;
  retain an explicit selection while the document stays open. For ambiguous opens,
  require a choice and keep context-free structural completion available.
- Provide an **Alarmlist Shared Project** template for ALMX, resources and lookups.
- Include the same physical file once per consumer for equivalent repeated
  imports. Distinct-file declaration conflicts remain compiler errors. The handling
  of conflicting item metadata must be decided with the build-contract evidence.

Iteration 1 investigates technical feasibility; dependent production behavior
must follow these agreed decisions.

## Decision disposition

1. XML-only consumer selector: reuse existing project selection where available,
   with a native-editor-compatible UI for standalone XML views. Initial selection
   and document-lifetime retention follow the accepted defaults; placement remains
   an iteration 7 integration detail. Never choose by arbitrary enumeration order.
2. Shared Project creation: dedicated Alarmlist Shared Project template accepted.
   Native CodeSharing hosting and consumer reference management are exercised in
   iteration 1; final Alarmlist template/item registration belongs to iteration 6.
   Do not declare generic import-only support equivalent to native support.
3. Imported-item duplicates: equivalent physical inputs are included once per
   consumer; conflicting compiler-relevant metadata is an explicit error. Add
   build/capture parity coverage in iteration 5 without weakening declaration checks.
4. Existing detached metadata is sufficient for the currently identified
   candidates; no version change is planned. If a concrete missing semantic fact
   requires additions, coordinate compiler/SDK/host contracts and package tests.
5. Native MSBuild/namespaced lookup baselines coexist with the ALMX completion
   provider. Automatic installed schema/candidate discovery is iteration 4 work;
   timing acceptance is defined in the integration decisions below.

Product defaults are resolved. Technical implementation follows the integration
decisions below and each iteration's own validation gates.

## Iteration 1 integration decisions

- Use the existing XML text view and legacy `ICompletionSourceProvider` /
  `ICompletionBroker` integration, with an ALMX-specific command filter and native
  completion presenter. The native XML host supports this API on both installed
  hosts. Roslyn's async completion source was inspected for snapshot/cancellation
  layering; adopting its provider wholesale would replace a different language
  host. This local API choice preserves native XML services and adds no Roslyn
  dependency. The current vertical slice completes only the ALMX root name.
- Explicit invocation handles List Members, Complete Word and Auto Complete.
  Completion insertion uses a custom edit transaction, preserves source on disk,
  and rejects a stale/read-only snapshot. Full grammar, automatic triggers and
  incremental-session refresh remain iteration 2 work.
- Native lookup completion recognizes the authoritative namespaced XSD when that
  schema is open in the IDE. Native MSBuild completion works in namespaced
  `.almproj` XML. These are coexistence baselines, not automatic association for
  installed lookup inputs or namespace-free ALMX/project files. Iteration 4 must
  supply automatic extension-local schema/candidate discovery, without modifying
  machine-global schema caches or requiring source markers. Preserve the native
  providers for contexts they already support.
- Existing host descriptors and detached analysis expose scalar/collection types,
  procedure capability, resources, references and lookup/argument catalogues.
  No contract version change is currently required. Extend host lexical context
  and candidate adapters in iterations 2–3; re-evaluate only if a concrete missing
  compiler-owned fact is found.
- CPS `SharedImports` and `SharedProjectReferences` enable native shared-reference
  management. The isolated fixture adds them to its consumer, imports ordinary
  `.projitems` and uses installed CodeSharing targets for the `.shproj`. Production
  SDK capabilities/template registration remain iterations 5–6. The prototype
  reuses the installed C# Shared Project container and sets
  `SharedProjectAppliesTo=Alarmlist`; it does not compile ALMX as C#. The installed
  Shared Project tooling is an IDE prerequisite, not a consumer-build prerequisite.
  A dedicated Alarmlist template must retain native Shared Project behavior.
- The accepted consumer context will be buffer/document scoped, initially supplied
  by the opening hierarchy when unambiguous, and shown in the existing designer
  selector plus a native editor-compatible selector for standalone XML. Losing
  membership clears the selected consumer; it never selects the next enumerated
  project. View changes preserve private drafts. Implement in iteration 7.
- Duplicate identity is the normalized absolute physical path, compared using
  existing Windows path semantics. Equivalent repeated items compile once per
  consumer, preserving first inclusion order. Conflicting compiler-relevant
  metadata for the same item/path is an explicit capture/build error; different
  input kinds are not silently merged. Do not deduplicate alarm/key declarations
  across distinct files. Implement/test in iteration 5, including default globs.
- Performance validation: in iteration 8, measure at least 20 warm explicit
  completion requests per XML surface with a 1,000-alarm input and retain p50/p95
  reports. Follow AGENTS.md: do not introduce machine-dependent timing assertions.
  This supersedes the initial iteration-1 1,000 ms p95 proposal, which had not
  been implemented as an assertion. Record first-use latency separately, retain
  the 100 ms native polling resolution in the report, and test
  cancellation/stale insertion separately from timing. Candidate acquisition must
  not build projects or perform synchronous dependency capture on the UI thread.
  Record lifecycle cleanup via view/session detachment and functional unload/
  reopen checks; whole-process memory alone is not proof of collectibility.

No additional product decision is required to continue under the accepted defaults.
These choices do not waive any later iteration's essential gates.

## Actual evidence and remaining gates

### Iteration 0 — planning

- Reviewed existing editor XML hosting, designer completion context, project
  context registry, SDK item/build configuration, prior milestones and feature
  proposals. Working tree was clean before this documentation change.
- Owner confirmed all three XML file types and native Shared Projects.
- Validation: documentation diff, relative links and consistency reviewed.
  Tests/builds were not run because this iteration changes documentation only.
- At this planning checkpoint implementation iterations 1–8 had not started. Their planned validation is
  acceptance criteria, not evidence of passing tests or available native support.
- Remaining gates: required design decisions, both-host feasibility, all scoped
  implementation/acceptance checks, and separately recorded release gates.

### Iteration 0 — planning refinement, 2026-10-26

- Expanded iterations 1–8 into concrete work, per-iteration validation and exit
  criteria while retaining their existing identifiers and dependencies.
- Recorded the three recommended defaults as unconfirmed proposals, separating
  product decisions from technical feasibility work.
- Validation: documentation diff, local Markdown links and iteration/status
  consistency reviewed. Tests/builds were not run (documentation-only change).
- At this refinement checkpoint implementation remained unstarted.

### Iteration 1 — native feasibility and design contract, 2026-10-27

Implemented the ALMX root-completion vertical slice using the native completion
presenter and an explicit insertion transaction, with lexical span tests. Added
isolated native Shared Project and lookup/MSBuild schema coexistence probes.
Accepted the owner's recommended defaults and recorded the 15% remaining-allowance
stop. No production SDK capabilities, shared template, compiler contract or
library-format changes are included in this iteration.

Validation (commands run from the repository root):

- `dotnet build src/VisualStudio/Alarmlist.VisualStudio.UIIntegrationTests/Alarmlist.VisualStudio.UI.IntegrationTests.csproj --no-restore -v minimal`: passed.
- `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj --no-restore -v minimal --logger trx`: **656 passed**, no failures/skips, net472. Includes 11 new lexical context cases.
- `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj --no-restore -v minimal --logger trx`: **13 passed per framework**, net472/net8.0, no failures/skips.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng/test-visualstudio.ps1 -VisualStudioPath '<selected installed host>' -Filter 'Area=XmlCompletion|Area=SharedProjectFeasibility'`: **3 passed per host** in final sequential VS2022/VS2026 runs, no failures/skips. Installed hosts: 17.14.36930.0 and 18.10.12224.181. The fixture deployed/verified the built VSIX in idle AlarmlistExp profiles and closed only its owned IDEs.
- Shared-reference tests verify native selection, saved Add/Remove imports,
  consumer builds, North/South resolution from one shared physical source and
  retained shared files. Root-completion tests verify embedded/standalone XML,
  one Undo and unchanged disk bytes. Schema baselines verify native lookup Path
  and MSBuild PropertyGroup suggestions and insertion.
- Native artifacts under `artifacts/log/Debug/VisualStudioUI/`: VS2022
  SharedProjectFeasibility `375adc94ae9a44fbad42eb1017745337`, XmlCompletion
  `8e9e58afe78c4d14a279ef4c1eea1060`; VS2026 SharedProjectFeasibility
  `e62462d514494a898c94a0a5a0ffbaac`, XmlCompletion
  `54b647b05eb7445b9287b7c8b883075d`, NativeSchemaBaseline
  `6dbb9d63eb2f488aa639eeff681638ec`.
- First-use command-to-visible-item measurements: VS2022 embedded 103 ms /
  standalone 808 ms; VS2026 embedded 717 ms / standalone 373 ms. These two
  samples per host establish a baseline, not iteration 8's warm distribution report.
- Earlier exploratory runs exposed a completion readiness check matching the
  toolbar rather than the popup, a decorated native accessible name and an OK
  button outside the reference-manager content subtree. Corrected the probes;
  final runs above pass. A concurrent test-project rebuild was also blocked by
  its running testhost; subsequent native runs/builds were sequential. Initial
  source build threading warnings were fixed before final validation.
- Documentation links and `git diff --check`: passed. Working tree started clean.

No remaining scoped iteration-1 blocker. Full structural/semantic completion,
automatic installed lookup/project support, production shared-input integration,
context/lifecycle checks and final performance acceptance remain iterations 2–8.
Prior milestones' manual/vendor/release gates remain unchanged. Latest allowance
check during iteration 1: 70% remaining, above the owner's 15% stop.

### Iteration 2 — ALMX structural completion, 2026-10-27

Implemented a tolerant lexical context scanner and definition-driven host grammar.
ALMX completion now offers scoped element/closing names, supported attributes,
boolean/enum tokens, generated scalar fields, simple/record collections and optional
procedure content. It skips comments, CDATA, DTDs, foreign namespaces and unsupported
contexts. Existing singleton children/attributes are suppressed. Literal completion
preserves surrounding whitespace and XML delimiters; a missing attribute quote
cannot make subsequent markup part of the replacement span. Defaults are never
offered or materialized implicitly. No compiler/SDK contract or format changes.

The native provider supports explicit invocation and automatic tag/attribute/value
triggers, refreshes after typing/deletion, keeps native editing for unmatched Enter,
rejects stale/read-only edits, and commits through one Undo transaction. Added
physical-key tests for XML/Split/standalone views, Escape, Undo/Redo, automatic
completion, concurrent text changes, read-only buffers and existing XML editing.

Validation evidence:

- Focused lexical/grammar tests initially passed 45, then 47 cases as quote-boundary
  coverage was added. Final full extension suite passed **694**, no failures/skips,
  net472, including **49** lexical/grammar cases and both whitespace regressions.
  Command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj --no-restore -v minimal --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration2/Unit`.
- VSIX/package integration: **13 passed per framework**, net472/net8.0, no failures/
  skips. Command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj --no-restore -v minimal --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration2/Package`.
- Final standard-definition native run: **5 passed per host**, VS2022/VS2026,
  no failures/skips. Filter: `Area=XmlCompletion|FullyQualifiedName~XmlKeyboardTests`
  through `eng/test-visualstudio.ps1` with each installed host. Source-preserving
  structural edits include 18 edit/cancel/Undo/Redo combinations per host across
  XML, Split and standalone views. Existing physical XML editing remains passing.
  TRX run directories: `4c7e51411da049f29d131dc88e66b189` (2022),
  `ba3c92607d5d4bacba1854929d40651a` (2026), under
  `artifacts/log/Debug/VisualStudioUI/TestResults/`.
- Extended `eng/test-alarm-collection-fork.ps1` with `-XmlCompletion`, selecting
  five native cases per host and definition. Both alternate generated definitions
  built with zero warnings/errors. Procedure-enabled fork `artifacts/col13/a8cca2bd`
  passed **5 per host**. Procedure-disabled fork `artifacts/col13/2e246438` passed
  four VS2022 cases initially; after correcting its input-processing wait, the
  failed automatic case passed in `recovery-2022` and all **5 VS2026 cases** passed
  in `recovery-2026`. Thus all 20 scoped host/definition/case combinations passed.
  The recovery rebuilt only the test probe after verifying that all three
  production completion source files matched the checkout. Later whitespace-only
  span refinement is covered by the final standard native runs and full unit suite.
- Alternate-definition acceptance verifies added fields and collections, removed
  Name/Category, stored enum tokens, boolean choices, absent numeric defaults and
  procedure-enabled/disabled suggestions. Popup browsing leaves source unchanged.
  The fixture retains its normal `MetadataCollections` selection without the new
  switch; ordinary XML tests and opt-in `XmlDefinitionCompletion` remain separate.
- Exploratory fork `41caaf28` exposed a virtualized-list test assumption; the
  corrected probe queries prefixes instead of requiring off-screen native rows.
  A later disabled-fork probe read before injected keys arrived; observable word/
  newline waits and the normal input-processing wait corrected that test. The
  failed reports are retained and are not reported as successful fixture runs.
- Final review aligned performance reporting with AGENTS.md: the initial proposed
  fixed p95 limit is superseded by descriptive warm-distribution measurements and
  functional cancellation/stale-result checks. Added active-document/editor or
  native-popup focus guards before injected keys. Guarded root/automatic reruns
  passed **2 per host** on VS2022/VS2026, with no failures/skips.
- Documentation links, PowerShell parsing and `git diff --check` passed. The first
  restricted final unit invocation could not start its build processes; the same
  scoped run succeeded with the required execution access. No checks were skipped
  to work around the environment.

No remaining scoped iteration-2 blocker. Iterations 3–8 remain unstarted. Existing manual/vendor/release gates
are unchanged. Latest allowance check: 68% remaining, above the 15% stop.

### Iteration 3 — completed semantic XML completion

Shared candidate construction and filtering now serve the designer and XML
completion from detached analysis data. XML caret mapping distinguishes literal
identity/assignment fields from expression fields and maps XML entity offsets
without resolving expressions. A per-view service borrows the embedded designer
context or owns a standalone registry adapter, validates saved inputs before
display and insertion, and owns cancellation and shutdown work.

The shared factory preserves designer matching rules: case-insensitive parameter
prefixes, key-only localization substring search with prefix priority, compiler
template signatures, case-sensitive lookup/list identities, bound-list searches,
record-only sibling expressions and reference eligibility. XML insertion escapes
only markup, preserves surrounding text/entities, and selects empty template
arguments without resolving them. Parameter assignments and identities remain
literal. No compiler/SDK contract or fingerprint change is needed.

The native semantic set owns an observable candidate collection and implements
recalculation; the default legacy set does not refresh an asynchronously prepared
list itself. Updates retain the selected token when possible. Pending rows cannot
commit. Every accepted semantic insertion checks saved bytes again, then checks
the view snapshot, caret, selection, context generation and read-only span before
one Undo transaction. Escape, changed inputs, new requests and view close cancel
owned work. Embedded views borrow the designer context without disposing it;
standalone views dispose only their own registry adapter. Document versions are
compared against their persisted buffer owner; the XML view is stamped separately.

Validation so far:

- Initial focused semantic suite: **26 passed**. Expanded full extension suite:
  **724 passed**, no failures/skips. Final suite with an additional XML record-scope
  case passed **725**, no failures/skips. It covers entity spans, literal/unsupported fields, explicit
  selection, escaped/nested expressions, localization matching/template arguments,
  bound-list descriptions, lookup casing, record siblings and invalid references.
- Extension build passed with **zero warnings/errors** after the UI-thread guard
  correction. Native final builds also succeeded.
- VS2022 final selection: **7 passed**, no failures/skips, through
  `eng/test-visualstudio.ps1` with filter
  `Area=XmlSemanticCompletion|Area=XmlCompletion|FullyQualifiedName~LanguageAndSavedCompletion`.
  TRX directory: `2257902ca6ac4d24935311e765013ea7`.
- VS2026: **6 passed** in `f187b464aee148c09c407f37ef33ece2`; its semantic case
  stopped at a focus guard before keyboard injection after the test's deliberate
  DTE edit. After explicitly restoring source focus, the expanded semantic case
  passed in `a05a02ba26b5491a96f30f01af8e2e0b`. Thus all seven unique scoped cases
  passed against the final production behavior. The successful recovery also
  checks Escape cancellation. Both hosts check buffer/resource stale insertion
  rejection, saved-only resources, unsaved declarations, native insertion/Undo,
  embedded/standalone views and selected-project isolation in Split mode.
- Earlier exploratory native failures exposed the missing completion-set refresh,
  the base list's update behavior and a remembered XML-only test view. They are
  retained in artifacts and are not counted as passes. An observable list and
  explicit test view/focus setup resolved these issues. Temporary lifecycle
  diagnostics were removed before the final native checks.

- Final unit command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj --no-restore -v quiet --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration3/FinalUnit`.
- Packaged VSIX/consumer integration: **13 passed per framework**, net472/net8.0,
  no failures/skips. Command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj --no-restore -v quiet --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration3/Package`.
- Documentation, links, paths and `git diff --check` reviewed. All native/WPF
  suites ran sequentially. No compiler contract, source format or SDK build
  behavior changed. Temporary diagnostic tracing is absent from the final code.

No remaining scoped iteration-3 blocker. Automatic lookup/project XML completion
and native Shared Project production support remain iterations 4–8. Existing
manual/vendor/release gates are unchanged. Latest allowance: 63% remaining.

### Iteration 4 — completed lookup and project XML completion

Lookup hints are read from the authoritative XSD embedded into the extension and
also shipped as `Schemas/AlarmlistLookup.xsd`. XML namespace bindings select its
element/attribute model, including prefixed lookup documents. Literal catalogue
values do not receive expression suggestions. Schema loading prohibits DTDs and
external resolution and reads no project content. Compiler validation remains
authoritative; this adds completion, not a diagnostic engine.

Project hints read embedded SDK CPS rules for public project settings, input
items and editable metadata, with documented build-setting additions. Namespace-
free `.almproj` files get common structural MSBuild hints; default MSBuild namespace
documents retain native general structure commands and get SDK-specific hints.
Unmatched SDK prefixes fall through to native XML/MSBuild completion. Prefixed
MSBuild documents remain with native namespace tooling. No project evaluation,
target execution, restore or path enumeration occurs when requesting hints.

The extension-local candidate approach follows iteration 1's accepted discovery
direction. Microsoft's [schema-cache documentation](https://learn.microsoft.com/en-us/visualstudio/xml-tools/schema-cache?view=visualstudio)
describes machine-wide catalog installation; this implementation leaves those
catalogs and authored schema markers alone. Common MSBuild contexts follow the
[MSBuild XML reference](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-project-file-schema-reference).
The ordinary XML editor, native validation and user schema choices remain in place.

Initial focused suite: **56 passed**. Final full suite passed **784**, including
**59** catalog cases with prefix declarations, nested extension-payload exclusion
and deterministic ordering coverage. No failures/skips.
Build passed with zero warnings/errors. The VSIX contains the linked schema at
the expected path. Native probes verify no schema needs to be opened first and
compare native MSBuild Undo behavior with ordinary `.csproj` editing.

Native validation evidence:

- Final completion selection passed **8 per host**, no failures/skips, with
  `Area=XmlCatalogCompletion|Area=XmlCompletion|Area=XmlSemanticCompletion|FullyQualifiedName~LanguageAndSavedCompletion`
  through `eng/test-visualstudio.ps1`. TRX directories:
  `79fe9dc24aff4c8e9e0c4cc6ec0a467e` (VS2022) and
  `c63a2a2cdbaa42be9cddad875dceecc5` (VS2026).
- The catalog case covers fourteen edit/cancel/Undo probes per host: lookup
  default/prefixed namespaces and namespace declarations, schema attributes,
  namespace-free project structure, SDK settings/items/metadata/enum values,
  shared-import labels and native MSBuild structure in `.almproj`/`.csproj`.
  Extension insertions undo exactly once; native insertions retain their existing
  two Undo units on both file types and hosts. Source files remain unsaved.
- Independent native-schema regression was moved to ordinary `.xml`, preventing
  the lookup provider from satisfying it. Final reruns passed **1 per host**:
  `4ad2dcad54044bb5819883266e1b5cc7` (VS2022),
  `8fe2ebcb43484ffeb9d6c5f8a1074baa` (VS2026).
- The deployment fixture verifies all installed VSIX payload bytes, including the
  linked schema, before running the owned IDE. No global schema cache was changed.
- Exploratory failures exposed candidate-order dependence and a native Undo
  assumption; stable ordering and comparison with ordinary MSBuild corrected the
  probes. A broader run exposed cancellation from unchanged selected-context
  notifications during startup. The service now ignores those notifications;
  real selected-state changes and saved-input validation still cancel stale work.
  The semantic fixture builds its valid project before editing, and both final
  hosts pass buffer/resource stale rejection and context switches. Failed reports
  are retained separately and are not counted as successful selections.

- Full unit command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj --no-restore -v quiet --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration4/Unit`.
- Packaged extension/SDK consumer checks passed **13 per framework**, net472 and
  net8.0, no failures/skips. The package test compares VSIX and SDK lookup schemas
  exactly; embedded SDK rule hints are checked against authoritative rule assets
  in the unit suite. Command: `dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj --no-restore -v quiet --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration4/Package`.
- Documentation, links, paths and diff review passed. Native/WPF suites were
  sequential. No compiler/SDK contracts or runtime build behavior changed.

No remaining scoped iteration-4 blocker. Native Shared Project production work
and combined acceptance remain iterations 5–8. Existing manual/vendor/release
gates remain unchanged. Latest allowance: 60% remaining, above the 15% reserve.

### Iteration 5 — completed shared input build contract

Task and evaluated-project capture now share a small lexical input-set helper.
Equivalent normalized absolute physical paths appear once in first evaluated order;
Windows uses case-insensitive identity, other platforms case-sensitive identity.
Filesystem links are not resolved. The first item and its metadata are retained,
without mutating task input arrays. This boundary does not change compiler raw
snapshot APIs or suppress duplicate declarations inside/across source files.

Repeated resources compare effective raw Culture: absent metadata uses the same
filename inference as the compiler; explicit empty remains a catalogue. Culture
identity is case insensitive. Library inputs additionally compare portable names
(Link or project-relative default), slash normalized and case sensitive. Conflicts
produce explicit capture/build errors before output installation. Application Link
metadata stays presentation only. XLIFF resource capture follows the same policy.
The filename helper was extracted without changing its public compiler API or
culture validation/fallback. No contract, format or fingerprint version changed.
Use matching rebuilt SDK/extension; old SDKs retain their earlier behavior.

Shared import tests use MSBuildThisFileDirectory for ALMX/RESX/lookup paths,
ordinary consumer conditions and missing/restored optional/required imports.
Saved capture retains existing discovery hints without executing targets. Two
consumer builds use different parameter/culture contexts; default-glob overlap,
spaces, library names, Build/Rebuild/Clean and previous-output preservation are
covered. An actual packaged-compiler bridge test checks one evaluated shared
alarm and its consumer-specific translated value. Native management and ongoing
context/watch lifecycle acceptance remain iterations 6–7.

Architecture references consulted before introducing the helper:
[Roslyn PathUtilities](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/FileSystem/PathUtilities.cs)
for platform path handling, and
[Arcade TestProjectFixture](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestProjectFixture.cs)
for shared fixture/private consumer isolation. Helpers remain at host/task capture
boundaries; the compiler still owns parsing, binding, diagnostics and cultures.

Validation to date (TRX under artifacts/log/Debug/Milestone12/Iteration5):

- Compiler full suite: **674 net8.0 / 672 net472 passed**, no failures/skips.
- Task full unit suite: **99 per framework passed**, no failures/skips.
- Direct-task integration: **4 per framework passed**, no failures/skips.
- Packaged SharedInputTests: **6 per framework passed** using dotnet MSBuild
  (SharedSDKFinal). Initial runs exposed an incorrect keyword-mode test assertion;
  keyword output correctly retains lookup tokens, while translated output resolves
  them. Failed reports are retained separately.
- The same six cases pass under **VS2022 and VS2026 desktop MSBuild**, net472,
  in Desktop2022/Desktop2026. Each command restored the previous
  ALARMLIST_TEST_MSBUILD_PATH. These --no-build runs reused the freshly rebuilt
  matching SDK package and test assets.
- Extension full unit run: **794 passed / 1 failed**. The failure was an older
  saved-library assertion expecting repeated physical inputs. After correcting it,
  **7 focused tests passed**, including all CapturedLibraryProjectTests and the
  new packaged-compiler parity test (UnitRecovery). No production change followed
  the full run; all 795 unique cases have passing final evidence.
- Extension package integration: **13 per framework passed**, no failures/skips.
- Existing CA1416 warnings in shared TestHelper.SetApartmentState were emitted
  by compiler/task test builds; no target framework or unrelated helper changed.
- Full SDK regression: **88 net8.0 / 91 net472 passed**, no failures/skips (SDK).

Commands use dotnet test <project> --no-restore -v quiet --logger trx
--results-directory artifacts/log/Debug/Milestone12/Iteration5/<suite> for the
compiler, tasks, direct-task integration, SDK and extension unit/package projects
listed in AGENTS.md. SharedSDKFinal and both desktop runs filter
FullyQualifiedName~SharedInputTests; desktop runs also select --framework net472.
UnitRecovery filters FullyQualifiedName~CapturedLibraryProjectTests or
FullyQualifiedName~EvaluatedSharedInputsAnalyzeOnce. Native/WPF tests remain
sequential. SDK usage and durable repository guidance document the new policy.

No remaining scoped iteration-5 blocker. Final diff, paths and documentation were
reviewed. Latest allowance: 57% remaining, above the 15% reserve. Native creation,
shared context lifecycle and combined acceptance remain iterations 6–8.

### Iteration 6 — completed native Shared Project management

The SDK advertises SharedProjectReferences and SharedImports for consumers. A
new Alarmlist Shared Project template uses the installed native C# sharing
container, setting SharedProjectAppliesTo=Alarmlist and the existing Alarmlist
item-template GUID. It imports only SDK Alarmlist.Shared.targets file rules, not
consumer build targets or the Alarmlist analysis capability. Shared ownership
stays in .projitems with import-relative Compile/resource/lookup items. The
container has no compiler output. Consumer CLI builds require no loaded .shproj.

The template follows the installed native CSharp/SharedProject template and
CodeSharing props/targets on both supported hosts. It retains their shared GUID,
HasSharedItems, native project registration and source-container lifecycle.
Alarmlist item schemas replace C# item defaults so .resx and .almlookup additions
use the correct compiler input kinds. Packaging copies the authoritative SDK
rules, pins the matching SDK version and generates definition-aware ALMX samples
through the existing template generator. No new project factory was introduced.

An initial creation probe used the Project-level SDK name/version shorthand on
an Import. Corrected to separate Sdk and Version attributes following the
[MSBuild SDK import contract](https://learn.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk).
The corrected installed template passes native creation. A package-catalog test
also initially assumed a separate catalog for CSharp; VSSDK retains one project
catalog, so the assertion now checks two catalogs with three project templates.
Failed reports remain distinct from passing evidence.

Validation so far:

- Installed SharedProjectManagement passes on VS2022 and VS2026. It creates the
  actual shipped template, checks owned sample items, adds/removes native shared
  references, verifies persisted imports, builds two independent consumers and
  checks no physical source loss. Expanded cases cover all three input types:
  Add Existing, XML open, correct .projitems build action, rename and Remove
  preserving files, plus ALMX Add From Template. It builds with the container
  unloaded, reloads it, saves/reopens the solution and builds IDE-authored consumer
  imports from the command line. Final report identifiers will be recorded below.
- SDK DesignTimeTests/SharedInputTests: **8 per framework passed**, no failures or
  skips. Command: dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
  --no-restore --filter 'FullyQualifiedName~DesignTimeTests|FullyQualifiedName~SharedInputTests'
  -v quiet --logger trx --results-directory artifacts/log/Debug/Milestone12/Iteration6/SDK.
- Extension package suite: **13 passed / 1 failed per framework**; the failure
  was the catalog-count assumption above. After its correction, that case passes
  on both frameworks (PackageRecovery). All **14 unique cases** now have passing
  final evidence, including the new shipped shared-template consumer build and
  SDK version/schema payload checks. Reports: Iteration6/Package, PackageRecovery.
- Native library/NuGet regressions exposed remembered UI state: the Reference
  Manager retained Shared Projects and package-tree inspection activated a project
  preview. Tests now select Projects and reactivate NuGet before their respective
  actions. Captured screenshots/UI trees support those corrections. Reruns pending.

The scoped iteration remains open until its final native regression/projection
checks pass. Existing compiler/task and full SDK evidence from iteration 5 remains
applicable; this iteration changes design-time capabilities, templates and rules.

Projection follow-up: expanded native tree inspection showed that the general
Shared Project capabilities did not graft source nodes into this custom project's
tree. The existing dependency-tree provider now adds Shared projects from evaluated
ProjectInstance.ImportPaths and DefiningProjectFullPath item metadata. Conditions,
hidden items and first physical membership are retained; captions use Link or an
import-relative path. Imported source nodes open the same physical file and cannot
remove membership from the consumer. Item changes remain native owner operations.
This follows the existing project/package graft pattern rather than enabling
unrelated Managed capabilities to obtain private providers. No build target or
source directory scan is used for browsing. Missing-source nodes show warnings.

The source-projection probe initially selected the new Shared reference node
instead of the actual Shared project for unload. It now distinguishes the
project-root node from projection children; projection display/opening had passed
before that test-only failure. SDK/template defaults also supply conditional
portable Link names for consumers (not the owning .shproj), so the shipped samples
work in a library outside its source directory. Packaged application and library
consumer checks pass on both frameworks after this addition.

- Full extension unit suite after projection code: **796 passed**, no failures or
  skips, Iteration6/Unit. The new capture test covers all input kinds, imported
  ownership, conditions, hidden items, duplicate paths, Link captions and target-
  free membership. Focused dependency/rule checks: **20 passed**, TreeUnit.
- Final full extension package suite: **14 per framework passed**, no failures or
  skips, PackageFinal. Expanded shared-template application/library test: **1 per
  framework passed**, SharedLibraryPackage. The latter is the only test expanded
  after PackageFinal; production projection code is unchanged after that run.
- Native library/NuGet references: **2 passed VS2022** in
  a4a72580db364edea81b4e0b4b1d9faf; **3 passed VS2026**, including pre-projection
  shared management, in de9e170306b74ea5a70117ed8ea368ba. These regressions cover
  the final SDK capabilities/template/rules. The later projection branch activates
  only for .projitems imports; existing project/package paths retain their behavior.
- First source-projection management recovery passed **1 VS2022** in
  027e4ddea1b648828c1f48be02f5a8d0, including projected source opening and disabled
  consumer Delete. Final metadata-caption/template-link runs on both hosts remain
  to be recorded before completing this iteration.

Final iteration-6 acceptance supersedes the pending notes above:

- Final SharedProjectManagement: **1 passed per host**, no failures/skips,
  ec0e04c4628240ccb8616e13ba25e693 (VS2022) and
  39ee622b5a924abcae37ada01a297e52 (VS2026). These runs include final conditional
  portable template links, Link-aware projections, source opening, disabled
  consumer membership deletion, owner item creation/open/rename/remove, two
  consumer builds, shared unload/reload, solution reopen, native reference removal
  and command-line Rebuild of the IDE-authored consumer. Both verify installed
  payload bytes through the standard deployment fixture.
- Native command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File
  eng/test-visualstudio.ps1 -VisualStudioPath '<selected installed host>'
  -Filter 'Area=SharedProjectManagement'. Reference regression filters are
  Area=LibraryReferences|Area=LibraryPackages. All native and WPF suites ran
  sequentially; only fixture-owned IDEs were closed.
- Final unit command: dotnet test
  src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
  --no-restore -v quiet --logger trx --results-directory
  artifacts/log/Debug/Milestone12/Iteration6/Unit. PackageFinal uses the matching
  IntegrationTests project; SharedLibraryPackage filters FullyQualifiedName~PackagedSharedTemplate.
- Source, template assets, commands, paths, links and final diff reviewed. Build
  passed with zero warnings/errors. No compiler/analysis/library contract changed.
  Latest allowance: 55% remaining, above the 15% reserve.

No remaining scoped iteration-6 blocker. Shared context selection and lifecycle
remain iteration 7; combined acceptance, persistent example and performance
reports remain iteration 8. Existing manual/vendor/release gates stay visible.