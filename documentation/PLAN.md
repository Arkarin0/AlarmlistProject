# User documentation coverage

## Outcome and scope

Document currently implemented user capabilities using the feature and tutorial
templates. Cover SDK builds, source semantics, procedures, localization/exchange,
lookups, libraries, diagnostic policy, shared projects, IDE editing/completion,
Alarm Summary, and fork-time fields/collections. Exclude planned features,
contributor API reference, and claims of new runtime or vendor acceptance.

## Iteration 1 — Feature references and linked workflows

Deliverable: a feature article for each capability above and its linked workflow
tutorials, with supported syntax, examples, observable results and limitations. Depends on
current source, tests and checked-in examples. Validate template structure,
examples against their source of truth, local links and whitespace. Commit once
these checks pass. No product build or test run is required for prose changes.

## Iteration 2 — Navigation and coverage audit

Deliverable: navigable documentation index and completed coverage audit. Depends
on iteration 1. Validate steps, starting state, commands, expected outcomes, links, placeholders
and feature coverage against current implementation. Record evidence separately
from planned acceptance and commit the completed iteration.

## Evidence

- Planning: inspected both templates, SDK targets and guide, standard definition,
  output writer, parameter regression tests, extension guide and sample/analyzer
  documentation. Existing SDK prose contains historical implementation statements;
  current targets and executable sample definitions take precedence.
- Planning adjustment: feature references link directly to tutorials. Bundle
  those articles into iteration 1 to keep committed links usable; iteration 2
  adds navigation and audits coverage rather than introducing the linked files.
- Iteration 1 complete (`abb318b`): added 13 feature guides and 10 workflow tutorials using
  the established templates. Checked current SDK targets, standard definition,
  output writer, scoped-property/list/procedure tests, example configuration and
  preparation scripts, and custom-definition/sample-analyzer guides. A PowerShell
  review verified 79 local article links and parsed 20 XML fragments; no article
  placeholders remained. `git diff --check` passed. Builds/tests were not run
  (documentation only); workflows were reviewed against source, not replayed.
- Iteration 2 complete: indexed all 13 feature guides and 10 tutorials, linked the
  index from SDK/extension guides, and recorded the documentation location in the
  repository map. Verified every article against required template headings,
  index reachability and local links including heading anchors. The final review
  resolved 107 documentation links and both SDK/IDE guide entry links. Historical
  template evidence now lives here rather than in the user index.
  `git diff --check` passed. Builds/tests were not run (documentation only).

## Coverage audit

| Current capability | Documentation and source evidence |
| --- | --- |
| SDK compilation and outputs | Project builds + first-build tutorial; SDK Alarmlist.targets and XML output writer |
| References, expressions and templates | Inheritance guide + reusable-alarm tutorial; scoped-property regression tests and standard definition |
| Procedures and creation defaults | Procedure guide + reusable-alarm tutorial; procedure-group tests and SDK creation-setting documentation |
| RESX and translation exchange | Localization/XLIFF guides + translation/exchange tutorials; SDK exchange targets, XliffExport and import profile |
| Lists, HMI catalogues and instruction arguments | Lookup guide + instruction tutorial; list-expression tests, embedded lookup schema and HMI specification |
| Libraries and distribution | Library guide + project/package tutorial; SDK reference/Pack targets and independent library sample |
| Diagnostic policy and analyzers | Diagnostic guide + policy tutorial; DiagnosticPolicy, sample analyzer and physical-source example configuration |
| Shared membership and contexts | Shared-project guide + sharing tutorial; shared imports/sample and current extension documentation |
| Designer, completion and resolved inspection | Editing/completion/summary guides + IDE tutorial; current extension and default/custom playground guides |
| Fork-time typed fields and collections | Custom-definition guide + isolated-playground tutorial; definition generator and capability-paired sample definitions |

All articles have requirements, examples or starting inputs, expected outcomes,
troubleshooting and related links. Conditional template sections were omitted
where inapplicable. No placeholders remain in published articles. The examples
use repository-relative locations and current source formats. No planned feature
is presented as available without its required custom toolset.

## Earlier template iteration

Template iteration 1 was completed in `12de066`: feature/tutorial templates,
authoring guide and repository instructions. Diff, links and template structure
were reviewed; builds/tests were not run (documentation only). This historical
record was moved here from the user index without changing its completed status.

## Validation limits

Documentation review does not rerun IDE acceptance, package/toolset builds or
translation-vendor round trips. Vendor interoperability remains unverified.

## Milestone 13 export documentation

The export-provider feature guide, project-build updates and SDK/index links ship
with Milestone 13 iterations 5b/6. The packaged consumer fixture verifies the XML
output-name example, explicit DLL and NuGet registration, runtime asset selection,
last-success discovery and Clean. Implementation/validation evidence and remaining
JSON/zenon/native acceptance gates are recorded in
[src/VisualStudio/MILESTONE-13.md](../src/VisualStudio/MILESTONE-13.md).

Milestone 13 iteration 7 adds the JSON format reference and index links. Its
packaged examples cover JSON-only and XML+JSON selection, two cultures,
transactional failure preservation and both desktop task hosts. Generated scalar
and collection fixtures cover wire types and identical bytes across frameworks;
see the milestone record for commands and results.

Milestone 13 iteration 8 adds the zenon keyword-XML feature reference and packaged
README examples. The SDK fixture verifies private NuGet and direct DLL selection,
the exact mixed-keyword example, filename overrides, both task runtimes, later-
culture failure preservation and discovery/design-time/Clean nonexecution with an
invalid provider image. The scalar/collection fixtures verify custom definitions.
Iteration 9 supplies the guided export tutorial and integrated native acceptance;
no vendor import certification is claimed.

Milestone 13 iteration 9 completes the export tutorial, provider-authoring notes
and SDK/native testing links. Executed the tutorial's actual XML code blocks in
an isolated local-feed directory: XML+JSON, zenon, delimiter-failure hash
preservation, JSON-only switching and Clean all pass. Separate shared consumers
verify independent selection and cultures. Custom-definition project/package
consumers verify all formats against the extracted SDK contract on both desktop
hosts. Native export builds and installed XML hints pass on both supported IDEs;
full SDK, VSIX and IDE unit regressions pass. Changed local links, paths,
placeholders and diff were reviewed. Vendor import certification
remains excluded from the keyword-XML feature.

Milestone 13 follow-up iteration 10 updates the default playground to North XML,
South JSON and a standalone Zenon keyword example. Its README documents outputs,
preparation, resource fallback and keyword-only scope; existing shared localization
exercises stay unchanged. Focused SDK tests pass on both frameworks and desktop
hosts; native DefaultSharedExample passes on both IDEs. The checked-in playground
is prepared and builds all three formats. Validation evidence is recorded in the
milestone plan.

Milestone 14 iteration 6 adds the Explain this value feature reference and links
from Alarm Summary and Visual Studio editing. It covers chosen contributors,
source navigation, invalid/deferred states, saved/unsaved input rules, private
drafts, freshness and bounded step pages. Compiler/bridge API reference remains
in contributor documentation. Full host unit checks (844) and native explanation
checks on both IDE versions pass; actual evidence is in MILESTONE-14.md. A guided
explanation tutorial and integrated shared/library/custom-definition acceptance
remain iteration 7 work.
