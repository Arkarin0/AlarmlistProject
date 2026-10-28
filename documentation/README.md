# User documentation

Feature descriptions explain what an Alarmlist capability does, when to use it,
and its supported behavior. Tutorials guide a user through numbered actions to
achieve a concrete, verifiable result.

## Start here

Follow [Build your first alarmlist](tutorials/build-your-first-alarmlist.md) for a
small command-line project, or [Edit and inspect alarms](tutorials/edit-and-inspect-alarms.md)
to explore the existing Visual Studio playground. The references below describe
implemented behavior; custom fields and collections require a rebuilt toolset.

## Feature reference

| Topic | What it covers |
| --- | --- |
| [Project builds](features/project-builds.md) | Inputs, output paths, cultures and Build/Clean behavior |
| [Export providers](features/export-providers.md) | Select outputs, register external providers and manage owned artifacts |
| [JSON export format](features/json-export-format.md) | Versioned resolved JSON, typed values and collection/procedure shapes |
| [zenon keyword XML](features/zenon-keyword-export.md) | External provider package, @Key@ projection and supported text |
| [Inheritance and expressions](features/alarm-inheritance-and-expressions.md) | Abstract templates, references, parameters and scope |
| [Test procedures](features/test-procedures.md) | Named groups, titles, entry ordering, Clear and creation defaults |
| [Localization](features/localization.md) | RESX strings, culture fallback and independent summary language |
| [XLIFF exchange](features/xliff-translation-exchange.md) | Export, baselines, dry run, apply and conflicts |
| [Lookups and instruction templates](features/lookups-and-instruction-templates.md) | Named lists, HMI catalogues and translated arguments |
| [Alarm libraries](features/alarm-libraries.md) | Project/NuGet references, transitive dependencies and packing |
| [Diagnostics and analyzers](features/diagnostics-and-analyzers.md) | EditorConfig, severity policy and opt-in rules |
| [Shared projects](features/shared-projects.md) | Shared membership and consumer-specific analysis |
| [Visual Studio editing](features/visual-studio-editing.md) | Designer/Split/XML, drafts, inheritance and navigation |
| [XML completion](features/xml-completion.md) | Structure, expressions, references and context selection |
| [Alarm Summary](features/alarm-summary.md) | Resolved tables, details, XML and column preferences |
| [Custom fields and collections](features/custom-fields-and-collections.md) | Rebuilt definitions, typed values, collections and optional procedures |

## Tutorials

The first-build, translation and lookup exercises share a small FirstAlarm
project. Other tutorials use the named checked-in playground and can be followed
independently after preparing its tools.

1. [Build your first alarmlist](tutorials/build-your-first-alarmlist.md).
2. [Author reusable alarms and procedures](tutorials/author-reusable-alarms.md).
3. [Translate alarms](tutorials/translate-alarms.md).
4. [Exchange translations with XLIFF](tutorials/exchange-translations.md).
5. [Use lookups in translated instructions](tutorials/use-lookups-in-instructions.md).
6. [Edit and inspect alarms in Visual Studio](tutorials/edit-and-inspect-alarms.md).
7. [Reuse alarm libraries](tutorials/reuse-alarm-libraries.md).
8. [Share sources between projects](tutorials/share-sources-between-projects.md).
9. [Configure diagnostic policy](tutorials/configure-diagnostics.md).
10. [Explore custom fields and collections](tutorials/explore-custom-definitions.md).
11. [Export resolved alarms](tutorials/export-resolved-alarms.md).

## Create an article

1. Copy the [feature template](features/TEMPLATE.md) or
   [tutorial template](tutorials/TEMPLATE.md) into its corresponding directory.
2. Give the file a lowercase, hyphen-separated name ending in `.md`, such as
   `alarm-references.md` or `create-your-first-alarm.md`.
3. Replace every `{{placeholder}}` with finished English content. Follow the
   author instructions in HTML comments, then remove those comments.
4. Remove unused optional sections and tables. Remove the optional labels from
   sections you keep. Keep required sections concise and useful to project users.
5. Check examples against the current implementation and describe expected
   results explicitly. Use repository-relative paths and links; never include
   credentials or machine-specific paths.
6. Link related feature descriptions and tutorials using relative Markdown links.
   Verify all links before publishing.

Describe current supported behavior. Keep implementation plans and historical
acceptance evidence in the milestone documents rather than user articles.

Documentation coverage and review evidence are maintained in the
[documentation plan](PLAN.md).
