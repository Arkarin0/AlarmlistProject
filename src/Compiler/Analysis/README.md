# Value explanation capture

Milestone 14 adds internal opt-in provenance capture, not yet a public explanation
API or IDE feature. Follow its [iteration plan](../../VisualStudio/MILESTONE-14.md).

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

The foundation currently covers field/parameter selection. Expression detail,
localization/lookups, procedures/collections and a target-specific public API are
subsequent iterations. Analysis contract remains 7 until the coordinated API/SDK
transition to 8; analyzer, library and exporter contracts are unchanged.
