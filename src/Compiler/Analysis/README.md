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

## Internal reference discovery and rename preview

Milestone 14 follow-up iteration 10 adds request-owned ReferenceCapture for alarm
and custom parameter symbols. This is an internal foundation; no public reference
API or SDK transport is exposed yet, and analysis contract 8 remains active.
Follow iterations 11–14 for resource/lookup/group coverage, contract 9, solution
coordination, commands and integrated acceptance. Ordinary analysis does not
construct or retain a reference index.

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

Internal Preview recomputes binding, validates names/collisions, deduplicates
physical edits and compiles a temporary ALMX overlay. It returns versioned edits
and proposed diagnostics without mutating input snapshots. Unavailable/read-only
findings, conflicting shared tokens and new diagnostics require acknowledgement
before any eventual host Apply. Invalid names, declaration collisions and
overlapping replacements cannot apply. Selection/exclusion, resource/project
overlays, all-culture coordination and UI acknowledgement are later gates;
do not expose this internal prototype as a completed rename feature.
