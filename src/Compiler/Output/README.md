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

Text-segment primitives exist, but compiler capture is the next iteration. Do not
infer localization boundaries by scanning CanonicalText or treat resolved text
as keyword-preserving input. Zenon support is not available yet.
