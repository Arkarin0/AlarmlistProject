# Milestone twelve: XML IntelliSense and native Shared Projects

Status: planning only, 2026-10-26. The owner confirmed the two topics and the
file/project scope below. Implementation has not been requested or started.
Iteration 0 records this plan; iterations 1–8 are proposed implementation work.
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

The remaining interaction and implementation choices are proposals in this plan;
the scope confirmation does not imply approval of every detail below.

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
| 8 — Combined acceptance and usage docs | Persistent small shared-project example, user documentation and final regression evidence. | 2–7 | Repeat packaged command-line/desktop tests and both-host end-to-end shared XML/designer workflow. Record completion latency/cancellation and lifecycle measurements against a reproducible baseline; agree acceptance thresholds in iteration 1. Record residual manual/vendor/release gates separately. |

## Open decisions and implementation questions

1. XML-only consumer selector: proposed reuse of the existing editor project
   selection where available, with a native-editor-compatible UI for standalone
   XML views. Decide placement, default selection and whether selection persists.
   Multiple consumers must never be selected by arbitrary enumeration order.
2. Shared Project creation: proposed dedicated Alarmlist Shared Project template.
   Prove whether native Shared Project infrastructure can host the item types
   directly or requires an Alarmlist-specific project factory/flavor. Do not
   declare generic import-only support equivalent to native support.
3. Imported-item duplicates: choose a documented physical-file policy consistent
   with existing MSBuild/compiler behavior, without weakening declaration checks.
4. Determine whether existing detached metadata is sufficient for structural and
   semantic completion. If contract additions are necessary, plan a coordinated
   compiler/SDK/host transition and package tests; do not invent a version now.
5. Identify supported MSBuild completion extension points and XML schema
   association on both hosts. Keep the implementation choice open until the
   native feasibility results exist. Also choose measured latency thresholds.

Resolve required product decisions before their dependent iterations. Technical
choices can be settled through the feasibility work within the confirmed scope.

## Actual evidence and remaining gates

### Iteration 0 — planning

- Reviewed existing editor XML hosting, designer completion context, project
  context registry, SDK item/build configuration, prior milestones and feature
  proposals. Working tree was clean before this documentation change.
- Owner confirmed all three XML file types and native Shared Projects.
- Validation: documentation diff, relative links and consistency reviewed.
  Tests/builds were not run because this iteration changes documentation only.
- Implementation iterations 1–8 have not started. Their planned validation is
  acceptance criteria, not evidence of passing tests or available native support.
- Remaining gates: required design decisions, both-host feasibility, all scoped
  implementation/acceptance checks, and separately recorded release gates.
