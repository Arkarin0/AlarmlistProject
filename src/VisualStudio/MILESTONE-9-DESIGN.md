# Milestone nine design contract

Status: owner-approved decisions, recorded 2026-10-13; implementation/validation
status belongs to [the iteration plan](MILESTONE-9.md).

## Artifact and public interfaces

- Add OutputType=Library; retain current final XML as the default. Library Build
  produces one deterministic culture-independent `.almlib` artifact.
- Use versioned normalized XML containing library identity, dependency identities,
  ordered source scopes, definitions, declarations/assignments, resources, lookups
  and portable provenance. Preserve references and expressions, not flattened
  inheritance. Do not package original formatting, machine paths or analyzers.
- Identity uses PackageId/PackageVersion, defaulting to project name/1.0.0.
  Package artifacts live under `alarmlist/`; dependencies remain separate packages.
- Compiler APIs accept immutable captured library inputs and create/read library
  artifacts without filesystem or project discovery. Hosts supply the dependency
  graph. Deterministic dependency-first traversal preserves first-encounter order;
  source/declaration order is retained and diamond dependencies appear once.
- Advance analysis contract 4 to 5 and analyzer contract 2 to 3 in one coordinated
  compiler/bridge/sample/consumer iteration. Expose ownership, dependency origins,
  deferred-value availability and navigation data. Incompatible contracts fail
  explicitly. Keep compiler objects outside the VS host AppDomain.

## Validation and evaluation

- Libraries contain abstract templates and concrete alarms. Abstract definitions
  are referenceable; final application output includes every concrete definition
  once, including those imported transitively.
- Library builds validate syntax, structure and references against their own
  dependency closure. Missing consumer-supplied parameters, translations and lookup
  values may remain deferred. Final application compilation remains strict, with
  existing keyword-mode behavior. Deferred preview values are unavailable.
- Alarm identities remain globally unique. Consumer alarms cannot replace imported
  alarms by reusing their identity. References from a library to downstream
  consumer-only definitions are invalid. Reject graph cycles and conflicting
  library identities/payloads; deduplicate diamonds.
- Concrete library alarm parameter values resolve: own alarm, own source file,
  final consumer project, owning library project defaults. Explicit empty stops
  fallback. Consumer-defined alarms inheriting templates use their own alarm/file/
  project context; library project defaults and base alarm/file assignments do not
  follow inheritance. Raw expressions and parameter declarations still inherit.
- Project identity expressions describe the final consumer. Library source identity
  uses stable relative names. Physical source-path expressions are unavailable for
  library definitions for both project and package consumption. Keep physical host
  navigation separate from portable expression identity.
- Preserve current literal substitution, escaping, group titles/order/Clear,
  localization argument handling and compiler-owned provenance.

## Resources and lookup overrides

- Bundle RESX string data and lookup catalogues. A project/library can override its
  dependencies. Incomparable sibling definitions conflict unless a consuming owner
  explicitly overrides that identity. Duplicates within one owner remain errors.
- Translation override identity is key plus culture. Resolve precedence per
  culture before the existing parent-culture fallback; retain explicit empty and
  whitespace translation behavior.
- Lookup override units are an entire path, one named-list entry, or one completion
  binding. Do not implicitly merge individual path members or metadata. Retain
  case-sensitive resource/lookup identities and existing validation.
- Library analyzers and EditorConfig policy validate their owning project; they
  do not propagate automatically. Consumer analyzers see the combined final model.
- XLIFF continues to operate only on selected resources owned by the current
  project. Imported resources are read-only.

## Build, restore and IDE behavior

- Support ProjectReference and PackageReference, full transitive mixed graphs,
  native Add Project Reference and native NuGet management on VS2022/VS2026.
  Consumer projects require no TargetFramework. Use NuGet's resolver/version rules.
- Project library references obey MSBuild configuration/build ordering and
  BuildProjectReferences=false. GetTargetPath discovers without building. Preserve
  transactional output installation, previous outputs on failure and input-safe
  Clean. Consumers may contain only references and no local ALMX inputs.
- Pack represents project-library references as package dependencies using their
  package identity. Reject dependencies that cannot be represented; do not silently
  embed or omit them. Fresh restore and warm-cache offline use are required.
- Consumer IDE analysis captures referenced projects' saved configuration and
  source/resource/lookup inputs without Build, including unloaded/out-of-solution
  references. Watch saves, imports, membership, deletion, configuration and restore
  changes. Generate equivalent library definitions in memory; write no outputs.
- Unsaved referenced ALMX changes remain private to that project's editor.
  Consumer-local unsaved ALMX behavior remains unchanged. Invalidate immediately,
  cancel obsolete work and revalidate the whole captured graph before publication
  or user actions. Invalid saved dependencies must not leave stale previews valid.
- Completion/reference search identifies library origin. Project previews include
  imported concrete alarms. Imported definitions cannot be edited from a consumer.
  Project navigation opens actual local sources; package navigation opens generated
  read-only definitions with portable origin information, not original formatting.
- Add library creation templates and output/package property controls. Native UI,
  source/definition navigation, reopen/restore recovery and disposal require actual
  host evidence, not only capability/rule tests.

## Boundaries

No Shared Projects, XML IntelliSense, syntax generation, automatic publication or
legacy bridge adapters. No private NuGet client SDK assemblies in devenv. Preserve
the pinned Arcade imports and both compiler/task frameworks; the extension remains
net472. Native integration is an essential first-iteration gate, not permission to
drop a requested workflow if integration proves difficult.
