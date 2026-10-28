# Export provider contract

Milestone 13 is in progress. These APIs are not yet activated by SDK builds.

`IAlarmlistExportProvider` receives an `ExportRequest` and cancellation token and
returns artifacts. Providers must use the resolved document and metadata rather
than parse source expressions. `ExportDocument.Create` rejects unsuccessful
compilations and invalid cultures and captures the mutable alarm list. Scalar
values are immutable primitives/temporal values; generated enums become their
canonical string tokens. Metadata and ordered collection/procedure data are
copied into read-only collections. Absent scalar values remain distinguishable
from empty strings. Abstract/library analysis is not an export input.

The provider reports contract version 1. A metadata-driven provider returns null
for RequiredDefinitionFingerprint; a definition-specific provider returns its
required semantic fingerprint. The host calls ValidateCompatibility before export
and reports mismatch with rebuild guidance. Assembly/dependency loading is part
of the later SDK integration, not this in-memory contract.

Artifacts contain relative names and defensively copied bytes. FromText uses
strict UTF-8 without BOM; binary exporters specify exact bytes themselves. The
XML provider preserves the legacy writer's encoding/preamble and serialization.
Only the publication host may validate destinations and install artifacts.
Cancellation and provider failures must prevent publication of the whole batch.

Compiler evaluation now captures text segments for fields, collection entries,
procedure titles and steps. Field/sibling references keep their original key
boundaries; parameter assignments and defaults remain literal. Localization
segments contain their key and, in concrete cultures, their translated Expansion.
RequiresSubstitution marks formatted or expression-dependent translations, which
cannot be represented by a plain HMI keyword lookup without further support.
Lookup label keys remain distinct; metadata and named-list values remain literal.
Keyword-mode ResolvedText preserves the original ALMX token while segments can
carry the known lookup label keys or literal metadata/list value. Providers must
use segments, not scan ResolvedText, to produce keyword-preserving output.

IsDeferred distinguishes keyword-mode typed expressions from concrete typed
values; their canonical text is preserved but their typed Value can be null.
Absent and explicit empty remain distinct. SDK export activation and the zenon
provider are still pending.