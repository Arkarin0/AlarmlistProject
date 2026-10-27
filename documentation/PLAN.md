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
- Iteration 1 complete: added 13 feature guides and 10 workflow tutorials using
  the established templates. Checked current SDK targets, standard definition,
  output writer, scoped-property/list/procedure tests, example configuration and
  preparation scripts, and custom-definition/sample-analyzer guides. A PowerShell
  review verified 79 local article links and parsed 20 XML fragments; no article
  placeholders remained. `git diff --check` passed. Builds/tests were not run
  (documentation only); workflows were reviewed against source, not replayed.
- Iteration 2: pending.

## Validation limits

Documentation review does not rerun IDE acceptance, package/toolset builds or
translation-vendor round trips. Vendor interoperability remains unverified.
