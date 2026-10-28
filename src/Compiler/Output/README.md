# Export provider contract

Milestone 13 is in progress. SDK builds now invoke selected XML and external
providers; built-in JSON and the zenon sample remain subsequent iterations.

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
and reports mismatch with rebuild guidance. Assembly/dependency loading uses the captured provider loader; the in-memory
contract itself performs no filesystem discovery.

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
Absent and explicit empty remain distinct. The zenon provider remains a subsequent
iteration.

Provider loader foundation (iteration 5a): ExportProviderReference declares an ID,
assembly path, exact public entry type and immutable captured images. The loader
loads only that type, validates ID/version, and disposes providers and all loading
scopes on failure. It shares captured assembly loading with analyzers: collectible
contexts on .NET 8, captured byte loading with conflict/restart guidance on desktop
CLR. Paths are identity only after capture; the compiler loader reads no files.

Build configuration uses AlarmlistExportTypes and AlarmlistExportProvider items.
Metadata names are AssemblyPath, TypeName, Dependencies, OutputFileName and
Option_<name>. Dependencies are explicitly declared DLL paths, relative to the
provider assembly's directory unless absolute. The host contract DLL cannot be
replaced. Only selected external providers are captured. Empty selection defaults
to XML; semicolon-empty IDs and duplicate IDs are errors. These settings are
activated by SDK builds through the 5b/6 publication boundary.
NuGet providers register the same items through selected package build assets,
choosing their net472/net8.0 assembly by MSBuildRuntimeType, not TargetFramework.
