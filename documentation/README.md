# User documentation

Feature descriptions explain what an Alarmlist capability does, when to use it,
and its supported behavior. Tutorials guide a user through numbered actions to
achieve a concrete, verifiable result.

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

## Template iteration 1

Deliverable: both templates, this authoring guide, and repository instructions
requiring their use. This documentation-only iteration has no implementation
dependencies. Completion requires review of the diff, headings, filenames, links,
and consistency; builds and tests are not required.

Validation record: reviewed both templates for the agreed structure, explicit
expected results, optional sections, and placeholder instructions. Relative links
and changed-file whitespace were checked. No product examples or API changes were
introduced. Builds and tests were not run (documentation only). No remaining
validation blockers.
