# Milestone five implementation contract

This contract implements the confirmed choices in [milestone five](MILESTONE-5.md).
It freezes the core localization behavior for implementation; it does not claim
that the later compiler, SDK, or designer iterations are complete. The owner has
confirmed the XLIFF tool targets, source-language workflow, and whole-import
conflict policy below.

The [planned generated HMI lookup extension](MILESTONE-5.md#planned-extension-generated-hmi-lookups)
records the owner's confirmed additional requirements: reusable named-placeholder
templates, externally generated XML lookup files with an included XSD, individual
label translations, literal keyword display, reference-resolution errors, and
explicit metadata references. It retains the existing single-culture preview and
build model; mixed-language output and documentation/HMI culture pairs are outside
scope. The [lookup specification](HMI-LOOKUPS.md) defines its XML and expression
grammar; the milestone log records implementation progress. The core contract below describes the
existing RESX behavior and does not yet add argument or lookup syntax.

## Expressions and values

`$(loc:Key)` is supported in Name, Code, Category, Description, and procedure
text. Identity and reference fields remain literal. The `loc:` prefix is
case-sensitive. Keys match `[A-Za-z_][A-Za-z0-9_.-]*` and compare ordinally and
case-sensitively; `Pump`, `pump`, `Alarm.Inspection`, and `alarm-inspection` are
distinct valid keys. Whitespace inside a token is invalid. Existing parameter
identifier rules remain unchanged.

`$$(loc:Key)` produces the literal `$(loc:Key)` without lookup. Escaping and
substitution remain single-pass. In `keyword` mode, an unescaped localization
expression also preserves its full literal token, even when the key has not been
declared in any resource file. Other property expressions still evaluate normally.

A real culture resolves a key from that culture, then successive
`CultureInfo.Parent` cultures, stopping before the invariant culture. For example,
`de-DE` can use `de`. Never use unqualified RESX values, an unrelated language,
or keyword mode as fallback. An empty string counts as missing and continues the
parent search. If the chain supplies no nonempty value, compilation fails.
Preserve whitespace in values; a string containing spaces is not an empty string.
This rule concerns stored translation values, before parameter evaluation.

Translations can contain ordinary property expressions. Resolve those expressions
in the consuming alarm's scope and field-dependency graph. `Inspection` with
value `Inspect $(par1)` and a consuming assignment `par1=001` produces
`Inspect 001`. Parameter replacement strings remain literal and are not rescanned.
If a nonempty translation evaluates to empty through an empty parameter, that is
its evaluated value; do not restart culture lookup. Explicit-empty parameter
assignments retain milestone four's stop-lookup behavior.

Initially reject nested unescaped localization tokens inside a translation.
Escaped tokens are literal text. Validate translation expression syntax when
that translation is used; unused language values do not require alarm parameters.
Resource structure, keys, and duplicate entries are validated independently of
the selected language, including keyword mode. Abstract templates do not require
contextual translations until a concrete alarm consumes their content. Cleared
procedure entries do not contribute translation or parameter dependencies.

## Resource format and capture

Support string-only `.resx` documents with an unqualified `root` element and
direct `data` children. Each data element has a valid `name`, one `value`, and
optional `comment`. Support standard `resheader` and embedded XML schema metadata
without executing it. Preserve values and comments, including Unicode, line
breaks, and XML escaping. A missing value, duplicate value/comment, invalid key,
or unsupported resource structure is an error.

Accept plain string entries and an explicit `System.String` type, optionally
assembly-qualified. Reject other types, `mimetype` serialization, file references,
and unexpected nested value markup. Never deserialize objects, load assemblies,
resolve external resources, or permit DTDs. File read/encoding failures are host
input errors; malformed captured XML produces a located compiler diagnostic.

Each resource has a captured culture identity. The empty identity designates an
optional unqualified key catalogue, not a translation culture. Multiple resource
files contribute to one project-wide catalogue. Duplicate keys in the same
culture are errors, including repeated empty entries and repeated source items.
The same key in different cultures is expected. Preserve duplicate inputs for
diagnosis rather than silently deduplicating them.

Add `ResxTextSnapshot(text, filePath, version, culture)` alongside the existing
ALMX snapshot type. It contains immutable text, path, a nonnegative integer
version, and the explicit culture identity. Captured parsing never reads its path.
Add resource snapshots and the selected culture to `CompilationContext` through
optional constructor inputs and read-only copied collections. Omitted/empty
selected culture means keyword. Invalid nonempty culture names are diagnostics.
The compiler owns parsing, catalogue validation, parent lookup, and evaluation.

The resource-aware overload is `CompilationContext(resources, culture: null,
projectPath: null, configuration: null, platform: null, properties: null)`.
Resources are optional through the original constructor. Contract three uses the
resource-aware overload in the bridge. Culture validation uses the runtime's
supported `CultureInfo.GetCultures(AllCultures)` catalogue, ignoring case; it does
not accept arbitrary names just because a runtime can construct a CultureInfo.

MSBuild and IDE adapters read saved RESX files and pass captured contents to the
compiler. Unsaved resource-editor buffers never enter compilation, preview,
completion, localization diagnostics, or export. A dirty editor does not invalidate
otherwise usable saved resources. Saving or externally changing a file invalidates
affected results. A newly added resource without a saved file produces the normal
missing-input error. Existing unsaved ALMX snapshot analysis remains supported.

The native Resource Explorer may save immediately when a cell edit is committed.
That saved edit is eligible for analysis; an unfinished cell draft is not. Do not
intercept the editor's own save behavior or require an additional explicit Save.
Alarmlist itself never saves pending resource edits to obtain a result.

Resource snapshots and locations carry versions independently of ALMX versions.
Retain requested and actual supplying cultures, resource path, key, raw value,
comment, and source location. Invalid or ambiguous entries cannot supply effective
values. Dependency tracking and result publication compare captured contents and
membership as well as identities; timestamps alone are insufficient.

## SDK resource items and cultures

Add `AlarmlistResource` items, enabled by default for `**/*.resx`. Apply existing
output/intermediate/hidden-directory exclusions. `EnableDefaultAlarmlistResourceItems`
disables the default glob. Allow explicit Include, Remove, Update, conditional
items, linked files, and `Culture` metadata. Include the item type and culture
metadata in the packaged CPS rules; no generated resource classes or managed
resource compilation are required.

An explicitly present `Culture` takes precedence, with an empty value identifying
an unqualified catalogue. Otherwise infer culture from the filename's final
dotted component before `.resx`: `Translations.de-DE.resx` means `de-DE`, while
`Translations.resx` is unqualified. An invalid dotted suffix is an error; use
explicit empty Culture for a dotted unqualified catalogue name. Reject `keyword`
as a resource culture because it is a display/output mode. Normalize valid culture
names through `CultureInfo`, retaining ordinal comparison for resource keys.

`AlarmlistCultures` is a semicolon-separated build list, defaulting to `keyword`
when missing or whitespace-only. Trim entries; reject empty entries inside a
nonempty list, invalid cultures, and duplicate normalized cultures. The reserved
keyword selection is accepted ignoring case and normalized to `keyword`. Culture
selection never silently falls back after an invalid name.

`AlarmlistCulture` independently selects the active project's designer language,
also defaulting to keyword. Persist changes through CPS in the shared project.
Build cultures are not changed by preview selection. Supply available resource
cultures and explicitly configured build cultures in the selector, plus keyword.

The designer writes an unconditional shared property through CPS and saves only
the project. Global overrides disable the selector; read-only projects or later
overrides report the failure and retain the evaluated selection. Choosing a
language does not commit an ALMX field draft. Keep context and culture choices
stable during semantic refreshes so an open dropdown and keyboard focus survive.

Localization completion uses ordinal key matching and inserts the complete raw
token. Show the SDK's evaluated value only when the current alarm has a matching
valid use and no draft; otherwise show its saved translation template, comment
and requested/supplying culture. Revalidate captured inputs before displaying or
inserting suggestions, and abandon late results after text, focus, selection,
context or lifetime changes. Resource diagnostic navigation explicitly opens the
XML editor because Resource Explorer also handles the Code logical view.

## Build outputs and failure behavior

Keep `OutputPath` as the output root and `CompiledAlarmlistFileName` as the base
filename. Emit `<OutputPath>/<culture>/<CompiledAlarmlistFileName>`, including a
`keyword` directory. Validate names and normalized destinations before compilation
so cultures cannot collide or escape the intended output directory. Preserve
the existing ability to choose an absolute OutputPath; the filename must be a
filename, not a second output path.

Expose `@(CompiledAlarmlistFiles)` with Culture metadata; Build and GetTargetPath
return that list. The task exposes `OutputFiles`. Update consumers of the old
singular output contract together in iteration four; unreleased compatibility
aliases are not needed. Design-time builds do not write outputs.

Capture ALMX and resource inputs once for the requested batch. Evaluate and
serialize every selected culture before installing any output. Compilation or
validation failure preserves all previous output bytes. Stage temporary files
beside their destinations and retain rollback copies during installation. On an
ordinary write failure or cancellation, restore replaced outputs and remove
newly created outputs; report restoration failures explicitly. This is not a
guarantee against process termination or power loss between filesystem operations.

Track exact owned outputs in a project/configuration-specific manifest under the
intermediate directory. Update it only after a successful batch. Clean removes
only configured/tracked outputs and their owned staging files; never glob-delete
culture directories. On a successful build, retire outputs for removed cultures
using that ownership record. Preserve unrelated files. Rebuild performs Clean
first, so failed Rebuild cannot promise to preserve files explicitly removed by
Clean. GetTargetPath evaluates expected paths without requiring a prior build.

## Diagnostics and analysis contract

Preserve `ALM0001` through `ALM0012`. Reserve the following localization IDs:

| ID | Meaning |
| --- | --- |
| ALM0013 | Invalid localization culture. |
| ALM0014 | Malformed or unsupported RESX content. |
| ALM0015 | Duplicate resource key in the same culture. |
| ALM0016 | Missing nonempty translation after the permitted parent search. |
| ALM0017 | Unsupported nested localization expression. |
| ALM0018 | Invalid localization key. |

Use the existing invalid-expression diagnostic for malformed token syntax and
existing parameter/context/cycle diagnostics for translated property evaluation.
Include the key and requested culture in missing-translation messages. Resource
errors point to captured RESX locations; usage errors point to the ALMX expression
with related resource locations when available. Keep one-based positions and
exclusive end columns. Input I/O failures are not assigned invented ALM IDs.

Coordinate analysis contract 3 across compiler results, package consumer tests,
isolated worker, bridge, and host-owned DTOs in iteration five. Include captured
resource identities/versions, selected culture, catalogue entries for completion,
and per-use translation provenance. Preserve existing raw/effective alarm and
procedure fields, abstract state, and compiled XML error gating. The SDK compiler
remains outside the VS host AppDomain; no host compiler reference is introduced.

Contract-three result names are Culture, Resources, ResourceEntries and Translations.
Resources retain captured path, version and input culture. ResourceEntries preserve
raw declarations, comments, locations and ambiguity. Translations contain the
compiler-selected requested/supplying culture and raw value for each catalogue key;
IsAvailable describes lookup, not expression evaluation. Keyword records expose
literal tokens without fabricated resource origins. Invalid selection yields null
Culture and its diagnostic. Each alarm's LocalizationUses stores actual uses,
usage locations and evaluated values/availability in that alarm's scope. Excluded
procedure content and abstract templates have no evaluated uses. These read-only
records must survive later analyses and SDK AppDomain unloads unchanged.

Native RESX editing remains Visual Studio-owned. Watch saved files rather than
retaining resource-editor graphs. Revalidate saved input versions before using
completion or navigating resource diagnostics. Continue existing generation,
project-context, open-ALMX-buffer, and disposal protections.

CPS supplies resource membership, ordered duplicates and metadata; the extension
does not discover resources independently. Capture one saved byte sequence per
physical path in each analysis pass, retaining separate item culture metadata.
Disk snapshot versions are zero within a project generation; compare captured
text, path, order and culture again before publication or navigation, including
same-timestamp rewrites. File watchers invalidate saved-resource changes. Open
document notifications affect selected ALMX inputs only, so resource drafts and
editor close cannot make otherwise valid saved translations unavailable. Resource
diagnostics navigate only when the saved capture and opened text still match.

## Iteration-one feasibility boundary

The native-editor scenario creates an isolated `.almproj`, explicitly includes a
RESX file using the existing project-item API, opens the native resource table,
edits a value with keyboard input, checks that disk still contains the saved
version during the draft, undoes and cancels drafts, commits a new value, explicitly
saves, and reopens. Current Resource Explorer saves when a cell edit is committed;
the test verifies that native save boundary rather than assuming document-wide
pending changes until Ctrl+S.
This verifies the prerequisite native authoring/save behavior, not localization
preview refresh, which is delivered and tested in iteration six.

Run separate VS2022 and VS2026 cases using the repository's owned-IDE harness.
Record installed versions; a pass on VS2022 17.14 does not verify the minimum
17.9 editor surface. Keep that older-host acceptance limitation explicit.

The design follows the existing ALMX captured-input boundary and Roslyn's
[AdditionalText](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/AdditionalText.cs)
separation, with task/SDK responsibility following the
[Arcade SDK organization](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk).
It introduces no upstream runtime dependency or change to the pinned Arcade SDK.

## XLIFF exchange choices

Target Trados Studio, memoQ, and Phrase TMS using an XLIFF 2.0 subset. The
[milestone compatibility decision](MILESTONE-5.md#exchange-compatibility-decision-2026-10-07)
records the industry survey and vendor format references behind this choice.
Verify real round trips before claiming interoperability with each tool.
AlarmlistXliffSourceCulture selects the source language and defaults to en; a
command/export override can select another supported culture. This is independent
of build and preview selection and never introduces default translation fallback.

RESX remains the master. Export saved values only. Preflight the entire import;
any stale source or conflicting target edit rejects all changes and produces a
conflict report. Do not apply nonconflicting entries from a rejected batch.
Preserve explicit dry-run/apply and dirty-resource-editor protection.

## XLIFF export profile version one

The compiler's XliffExport.Create consumes captured saved RESX snapshots and
returns detached XLIFF/baseline strings. The task owns capture and transactional
file installation; the SDK exposes ExportAlarmlistXliff as an explicit target.
This follows the existing captured-text and task/output boundaries. It adds no
compiler file access, IDE evaluator, dependency or normal Build input.

Export one XLIFF 2.0 document per exact source/target culture pair, with one file
and one segment per key. Keys are ordinal-sorted stable unit IDs. Preserve source
and target comments as categorized unit notes, and whitespace through xml:space.
Property expressions use originalData and non-copyable/non-deletable ph elements
with distinct occurrence IDs; existing target expressions may reorder but must
match the source's occurrence multiset exactly. Escaped dollar text remains raw
text. Invalid expressions and nested localization fail export. Segmentation is
disabled with canResegment=no. Inline markup, segmentation and additional modules
are outside this initial profile; import rejects unsupported structures.

Source defaults to en and must have a nonempty exact-culture value for every
catalogue key. No parent/default substitution occurs during exchange. A missing
target has no target element; an explicit empty value has an empty target. Both
start untranslated. Whitespace-only values remain values. Export uses source and
target comments; unqualified catalogue values never become source translations.

Keep a local AlarmlistXliffBaseline version-one sidecar with each key's source and
target presence, raw value and optional comment. It contains no file paths,
timestamps or unsaved text. The XLIFF file ID includes a SHA-256 digest of the
deterministic baseline bytes to pair the returned document with the retained
sidecar; this is a consistency check, not authentication. The sidecar stays out
of translator tools so vendor metadata rewriting cannot remove conflict data.
Do not regenerate it when translations return. Import compares all
baseline entries with current saved resources before applying any change, rejects
conflicts for the whole batch, and resolves destinations only from current project
resource selection. For a previously missing key, multiple possible target files
require explicit destination selection rather than a guessed file.

The SDK exposes AlarmlistXliffSourceCulture, required AlarmlistXliffTargetCultures,
AlarmlistXliffOutputPath and AlarmlistXliffOverwrite. The default output folder is
xliff under the project; filenames are Alarmlist.<source>.<target>.xlf with
.baseline.xml appended for the sidecar. Reject source/target equality, keyword,
empty/duplicate target selections, protected input/output collisions and implicit
overwriting of an existing job. Compile and Clean do not own exchange files.

Translator setup: select the XLIFF 2.0 filter, retain one segment per unit,
preserve whitespace and notes, preserve inline codes, and return the translated
XLIFF with the same IDs. In Phrase, disable resegmentation, HTML/ICU subfilters,
target-language overrides and automatic copying of source into empty targets.
These settings follow vendor documentation; actual Trados, memoQ and Phrase
round trips must still be recorded before claiming verified interoperability.

## XLIFF import profile version one

`XliffImport.Preview` compares captured exchange/baseline text with current saved
resource snapshots, returning immutable changes, conflicts and proposed resource
texts. All source/target baseline entries are checked; any conflict removes all
writable files from the result. A target already equal to the returned value is
accepted for repeat-import idempotency only when its comment still matches.
Additional resource keys are preserved. Missing returned targets do not delete
entries; explicitly empty targets remain distinct. Notes are informational;
RESX comments are retained. Changed RESX serialization retains XML content and
whitespace nodes but can normalize lexical spelling/line endings and uses UTF-8.

The reader accepts the export's single-file, single-segment XLIFF 2.0 subset,
including translated/reviewed/final states and reordered protected occurrences.
It rejects unknown structures/attributes, duplicate or missing units, changed
source/cultures/protected data, new unprotected expressions, and external DTDs.
It reuses the export parser and code mapping rather than an IDE evaluator.
The baseline digest pairs files; it is not authentication. Resource destinations
come only from evaluated project items, with explicit selection for ambiguous
new-key destinations. No new file path is accepted from translation content.

The task/SDK owns saved-byte capture, default dry-run, explicit apply, collision
checks, input revalidation and rollback. Apply is a terminal workflow, independent
of Build/Clean. Read-only Windows COM automation checks open VS documents without
referencing VS assemblies in the task or compiler. All affected editors must be
closed: native resource-cell drafts can precede the document dirty flag. This is
a deliberate conservative implementation of dirty-editor protection, rather than
assuming Document.Saved accounts for unsealed edits. An inaccessible IDE prevents
apply; the importer never saves/closes it. The compiler remains filesystem- and
host-independent. See the SDK README for limits concerning other editor apps,
encoding normalization, concurrent changes and non-crash-atomic rollback.
