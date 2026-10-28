# Export provider contract

SDK builds invoke selected built-in XML/JSON and external providers. The packaged
zenon sample lives in [src/Exporters](../../Exporters/Alarmlist.Exporters.Zenon/README.md).

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
Absent and explicit empty remain distinct.

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

## Implement and package a provider

Implement a public concrete IAlarmlistExportProvider with a public parameterless
constructor. Keep its ID stable, return ContractVersion=1, and use a null required
definition fingerprint only when all supported fields/collections are handled
through metadata. Call request.ValidateCompatibility(this), check cancellation
between work units, and reject unsupported options explicitly. Preserve compiler
ordering, absent/default/deferred distinctions and typed values. Do not evaluate
source, resolve resources again, discover dependencies or publish files yourself.

Return one or more ExportArtifact values. Each path must be relative to the culture
root and unique across every provider selected in that build. Bytes can represent
any format; ExportArtifact.FromText provides strict BOM-free UTF-8. Nested paths
are supported in returned artifacts. Request.FileName is a suggested filename;
the registration's OutputFileName override accepts a filename without a directory.
All artifacts must be detached from provider types so they remain usable after
disposal/unloading. A provider may implement IDisposable; disposal failure prevents
publication just like export failure. Whole-process crashes are not rollback gates.

Build runtime assets for net472 and net8.0 against the matching compiler contract.
Do not ship Alarmlist.Core.dll or privately bind another copy. Put entry/dependency
DLLs under tools/<framework> and register the explicit entry type from ordinary
build props/targets. Use MSBuildRuntimeType to select the task-host asset. Avoid
buildTransitive and automatic export selection; the consuming project chooses its
IDs. Explicitly list dependency DLLs in Dependencies. The production
[zenon package project](../../Exporters/Alarmlist.Exporters.Zenon/Alarmlist.Exporters.Zenon.csproj)
demonstrates packaging; its build targets demonstrate runtime selection.

Validate consumers against the extracted packaged SDK contract. The isolated
[verification provider](../../SDK/Alarmlist.SDK.Tests/testassets/Exports/Provider)
and [SDK tests](../../SDK/Alarmlist.SDK.Tests/ExportProviderTests.cs) cover DLL/NuGet
registration, declared dependencies, multiple binary/text artifacts, failure
preservation and nonexecution during discovery/Clean/design-time builds. Providers
are trusted build code; collectible loading controls lifetime rather than permissions.

JSON format version 1 uses the platform JsonReaderWriterFactory writer available
on both frameworks, with no new package dependency. It writes detached canonical
numbers/booleans directly and canonical temporal/enum text as strings. Field names
are values in ordered scalar-record arrays, avoiding reserved JSON member names.
See the [wire format](../../../documentation/features/json-export-format.md).
JSON returns exact UTF-8 bytes without BOM and has no provider options in version 1.

XmlAlarmlistExportProvider also accepts scalar/text projection delegates. Its
parameterless constructor preserves legacy bytes; projections reuse metadata and
XML escaping without duplicating the output structure or evaluating source.
The external Alarmlist.Exporters.Zenon package uses this surface to render keys as
@Key@. It rejects literal delimiters, adjacent keys without literal separators and
RequiresSubstitution segments. Its package contains only provider runtime assets,
ordinary build registration and a README; it relies on the host's compiler contract.
