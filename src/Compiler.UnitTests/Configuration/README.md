# Captured EditorConfig tests

`EditorConfigUpstreamTests` uses an xUnit `IClassFixture` to load the upstream
[editorconfig-core-test](https://github.com/editorconfig/editorconfig-core-test)
corpus at revision `895b3a65d0d823dbd0acf2bc402376381995d1b1`. The fixture first
checks `artifacts/testassets/editorconfig/<revision>.zip`. If missing, it downloads
that revision from GitHub, validates its pinned SHA-256, and installs it atomically.
The fixture then reads the archive in memory. It never executes upstream CMake
or writes archive-controlled paths. The archive retains the upstream license.

Run through the ordinary compiler xUnit project, on both target frameworks:

```powershell
dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj --filter FullyQualifiedName~EditorConfigUpstream
```

The first run needs HTTPS access to `codeload.github.com`; later runs work offline.
A missing cache and failed download fail setup with an actionable error, rather
than silently skipping validation. A checksum mismatch also fails; remove the
reported invalid cache to retry. Cache behavior has separate network-free tests.
Updating the corpus requires reviewing the revision, checksum, declaration counts
and adaptations together. No downloaded corpus is checked into this repository.

The adapter reads the pinned simple test declarations in `glob`, `parser`, and
`filetree` (130, 34 and 23 cases). It captures the corresponding `.in` files as
`.editorconfig` snapshots at their relative directory levels and compares the
resolved options. Windows excludes the same two impossible literal-backslash
filenames as upstream. A separate test covers native source-path separators.
No configuration or source file is read by the compiler itself.

This is an adapted compatibility suite, not a claim of full EditorConfig core
certification. CLI/version switches, formatting-property defaults and upstream
harness self-tests are outside Alarmlist's captured diagnostic-options API.
The following differences are explicit in `AdaptExpected`, rather than hidden
as skipped cases:

| Cases | Expected Alarmlist behavior |
| --- | --- |
| `brackets_range`, `brackets_nrange2`, `brackets_range_and_choice` | Literal bracket characters, including `-`, per [specification 0.17.2](https://spec.editorconfig.org/#glob-expressions); upstream retains older character-range expectations. |
| `brackets_slash_inside2`, `brackets_slash_inside3` | `[e/]` can select `e`; it does not make the entire bracket expression literal. |
| `braces_numeric_range8` | Preserve the existing ALMX contract accepting leading-zero numeric values; upstream expects canonical number spelling. |
| Seven `unset_*` cases | The captured API returns effective options with unset keys removed; upstream CLI output retains the literal `unset` marker and synthesizes formatting defaults. |

On Windows the `spaces_in_section_name` input adds `.almx` after its terminal
space, and updates the corresponding section. This still checks meaningful spaces
inside a section without Windows path normalization removing a terminal space.
All other supported expectations and original fixture text are unchanged.
