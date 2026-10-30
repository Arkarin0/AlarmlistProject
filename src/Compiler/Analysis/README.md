# Explanation and reference capture

Milestone 14 adds opt-in provenance capture and the public ExplainSnapshots API.
Follow its [iteration plan](../../VisualStudio/MILESTONE-14.md).

ValueExplanationCapture is supplied explicitly to CompilationEvaluator. It selects
the consuming alarm; ordinary evaluator construction retains no recorder or graph.
Recording observes the existing GetFieldSource and PropertyResolver selections.
It must never perform independent binding, expression evaluation or resource I/O.

The internal frozen graph copies ordered nodes and input IDs into read-only
collections. Field roots retain raw expression, evaluated/canonical value, type,
state and chosen definition origin. Inheritance edges follow only the effective
chain. Parameter nodes separate declaration from actual assignment, including
project/file/alarm/library ownership. Assignment text and definition defaults stay
literal. Null/unavailable, absent and explicit empty values remain distinct.

Only chosen contributors belong in a result. Do not add histories of shadowed
values. Repeated parameter/field dependencies share nodes; cycles terminate in a
Cycle leaf rather than an unbounded tree. Frozen results contain no syntax nodes,
delegates or mutable capture objects. Locations and library identities are existing
immutable compiler records; hosts will copy these through the detached SDK bridge.

Capture includes field/parameter dependencies, well-known context values,
localization/resource nodes with requested/supplying culture, formatted arguments,
lookup definitions/labels and compiler typed conversion results. Only the selected
resource appears; no skipped fallback alternatives are added. Keyword nodes stay
deferred without selecting translations. Label translations, list/metadata values
and quoted arguments retain their existing literal semantics.

Procedure titles/entries and simple/record fields now capture their existing
effective values and source ownership. Siblings share record-field nodes; defaults
remain literal. SnapshotAnalyzer observes its existing ordered Clear-resolution
results to record excluded entries and controlling directives without evaluating
discarded text. Collection and procedure ordinals in internal root keys are
snapshot-local addresses, never persistent entry identities. Public targeting and
host refresh must additionally validate source/context stamps.

ExplainSnapshots accepts snapshots, CompilationContext, ValueExplanationTarget,
cancellation and optional library deferral. Target kinds are Field, Parameter,
WellKnown, ProcedureTitle, ProcedureEntry, CollectionField,
ExcludedProcedureEntry, ExcludedCollectionField, SourceParameter and
SourceWellKnown. Entry kinds require the original entry location, including its
source version. Collection fields additionally require FieldName. No public API
target uses an effective-entry index. Source targets use an owning input location
and version. Unknown, ambiguous, stale or unbound targets return explicit
unavailable results. Abstract targets defer missing consumer inputs.

The immutable result contains only nodes reachable from the selected root, context,
relevant diagnostics and any unavailable reason. Node IDs need not be contiguous.
Hosts must revalidate their captured inputs/context before publication/navigation;
locations do not replace dependency or package byte validation. Analysis contract
8 is activated together with the isolated SDK bridge. Analyzer 5, library format
3, export contract 1 and semantic definition fingerprints are unchanged. IDE
actions and presentation follow in iteration 6.

## Reference discovery and rename preview

Milestone 14 follow-up iterations 10–11 add request-owned ReferenceCapture for
alarms, custom parameters, localization keys, lookups, groups and immutable
generated/built-in names. Iteration 12 exposes the complete compiler surface and
detached SDK transport with analysis contract 9. Solution coordination, commands
and integrated acceptance are tracked in iterations 12–14. Ordinary analysis does not
construct or retain a reference index.

Construct `ReferenceCompilationSnapshot` from captured ALMX, `CompilationContext`
and optional exact project/import `ReferenceTextSnapshot` inputs. A library owner
also supplies `ReferenceLibraryScope` with its identity and authoritative portable
source names. Creation accepts cancellation and stamps all captured inputs and
tooling. Use `TargetAtToken(path, version, offset)`, `TargetAtLocation(kind, location,
name)` or a compiler-issued `TargetIdentity(declarationId)` to obtain a target bound
to that request. `TargetAtValue(location, consumer)` resolves the symbols bound in
the value at an exact compiler-reported location, optionally restricted to its
consumer alarm. `TargetParameter(location, name, sourceScope)` uses the actual
parameter resolver for an alarm or source context, including inherited and project
declarations. The source context location is `(path, 1, 1, version)`.
`TargetBuiltIn(name)` selects an immutable well-known name or generated field alias.
Literal text and unsupported tokens remain unavailable. A target containing more
than one bound family returns `IsAmbiguous` with immutable `Candidates`; the caller
must obtain an explicit choice before using that candidate's declaration identity.

The six-argument snapshot constructor additionally accepts `readOnlyPaths`, an
explicit set of captured paths protected by the host's provider ownership policy.
This set participates in snapshot identity. These inputs retain exact searchable
spans and unavailable-edit findings, including references in a protected file to
an otherwise editable symbol. Rename never emits replacements for protected paths,
and a projected request cannot change their ownership to enable an edit. The
ordinary constructor preserves the behavior of independently supplied editable
inputs; the compiler does not inspect files or infer package-cache ownership.

`AlarmCompiler.FindReferencesSnapshots(request, target, cancellationToken)` returns
detached declarations, uses, exact UTF-16 spans, context, diagnostics and coverage.
The overload accepting multiple targets resolves them against one binding pass;
each target remains independently snapshot-bound and unambiguous. This supports
solution identity propagation and shared-token probes without retaining an index
in ordinary analysis.
`IsAmbiguous` distinguishes an ambiguous target from an unsupported/unbound one;
available symbols also carry their compiler-owned ambiguity state. Occurrence
`DeclarationId` and `SourceTokenId` preserve binding and source-token identity
between an owning library and its imported read-only projection. A solution
coordinator can use these identities to protect a shared contributing token when
one consumer's family cannot be renamed. It must not decode library paths or
match source names to infer that relationship.
`PreviewRenameSnapshots(request, target, newName, cancellationToken, includedGroups,
validationInputs)` returns versioned replacements, change groups, skipped findings
and proposed diagnostics. Null includedGroups selects all proven edits; an empty
selection selects none. Group IDs are snapshot/name-bound. A projected request
must describe precisely the approved overlay, including SDK-reevaluated project
metadata/membership. These APIs access no files and apply no edits.
`NonApplicableFamilies` identifies compiler-rejected families explicitly, including
collisions or ambiguous bindings, so callers need not interpret failure messages.

Binding uses Binder/LibraryCompilation, PropertyResolver, the expression parser,
effective scalar origins and existing procedure/collection resolution. Equal names
alone do not create a family. Actual declaration/assignment and inheritance edges
connect families; unrelated scopes remain separate. Case rules follow the compiler.
Literal parameter assignments, defaults, quoted arguments and escaped expressions
are not reference uses. A shared physical token retains every observed consumer
binding; incompatible bindings prevent an edit to that token.

XmlValueSourceMap maps decoded tokens to original UTF-16 source ranges after XML
validation. It never discovers references or defines binding. Leaf spans preserve
surrounding formatting, entities, CDATA and comments; a token fragmented across
markup is searchable but receives no guessed replacement. Carriage returns in new
names use character references, including a CDATA boundary when necessary, to
preserve their exact value through XML normalization.

Detached immutable search data includes identities/families, declarations,
occurrences, binding consumer names, coverage findings and compiler diagnostics.
Targets are bound to captured source/resource/lookup/library bytes and consumer
project/configuration/platform/culture. Recheck the wider solution/toolset stamps
at the host boundary in iteration 12. Imported library and uncaptured project
declarations remain searchable and read-only; project text overlay capture is
also part of iteration 12.

Preview recomputes binding, validates names/collisions, deduplicates
physical edits and compiles temporary ALMX/resource/lookup overlays. It returns versioned edits
and proposed diagnostics without mutating input snapshots. Unavailable/read-only
findings, conflicting shared tokens and new diagnostics require acknowledgement
before any eventual host Apply. Invalid names, declaration collisions and
overlapping replacements cannot apply. Requests support
snapshot-bound change groups: excluding a group revalidates the actual selected
overlay and reports remaining references and additional diagnostics. A supplied
SDK-evaluated project overlay must match exactly those selected text changes before
project edits become applicable. Ordinary analysis never sees this overlay.
Each compiler change group identifies one proven physical replacement, including
its version/span/text. The host can combine groups by file for presentation and
exclude an incompatible shared token without dropping other proven edits in the
same document. The multi-target rename overload compiles one joint selected overlay;
it retains every selected target in ReferenceSearches. Colliding families remain
unchanged, while proven edits elsewhere require partial-result acknowledgement.
Solution coordination and UI acknowledgement remain required host gates;
compiler API availability alone does not complete the end-to-end rename feature.

The internal multi-context search follows compiler-issued declaration identities
and each captured context's actual family edges to a fixed point. It retains every
project/configuration/platform/culture slice, including unavailable input coverage;
unrelated equal names do not connect. Physical tokens bound to another family in
a captured consumer receive an explicit conflict and no guessed replacement.
This helper does not discover solution projects, choose active configurations or
load toolsets; those remain host capture and SDK isolation responsibilities.

Resource and lookup constructors expose opt-in observation at their existing
provider registration/override selections. No observer/index is retained by
ordinary catalogue construction. Normal evaluation and capture share
LookupTokenBinding; the host must not reproduce these rules. Resource cultures
join within actual providers, and dependency overrides join through the existing
catalogue choices. Lookup lists preserve their additive entry selection rules.
Path/member/metadata, list/entry and template/argument namespaces retain their
actual ownership; unrelated member names do not collide across different paths.

Capture covers localization keys in expressions, lookup labels and template
bindings, selected translated expressions, formatted call arguments and parser-owned
placeholder spans across related resource cultures. Caption/metadata/list values
stay literal. Procedure groups join through actual inheritance and expose their
controlling Clear directives without rewriting entry text. Generated field names,
record sibling names, well-known names and definition caption keys are searchable
but immutable. Resource-key renames report immutable caption uses as skipped.

When the compiler rejects an alarm's reference chain, capture reports unavailable
consumer binding rather than converting a partial binder choice into a proven
expression use. Duplicate declarations and ambiguous owners propagate to their
members. Known physical spans remain available for navigation, with no replacement
for ambiguous identities. Imported definitions remain read-only; partial rename
requires explicit acknowledgement before any eventual host application.

Token targets accept UTF-16 caret offsets inside or immediately after a semantic
token. A containing token takes precedence over a token ending at that offset.
Opt-in parser/binder probes distinguish unsupported literal/markup positions
(`IsUnsupportedTarget`) from recognized but unbound tokens and unavailable parsing
or inheritance. Hosts may delegate unsupported positions to native tooling; they
must not classify expressions by text or treat unbound references as literals.
Probes retain the actual fragments of tokens separated by XML markup for search,
while replacement still requires one safe mapped token. This permits navigation
without deleting intervening comments or CDATA boundaries.
