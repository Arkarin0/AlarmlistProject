# Agent instructions

## Scope and working approach

These instructions apply to the entire repository. Follow any more specific
`AGENTS.md` in a directory you change, and honor the user's task instructions.

- Check `git status --short` before editing and preserve unrelated changes.
- Read the implementation, nearby tests, and relevant project files before
  changing behavior. Keep changes focused; avoid unrelated formatting or renames.
- Treat the checked-in source and build configuration as authoritative. Update
  this guide when project structure or supported workflows change.
- Never add credentials, machine-specific paths, or generated build outputs.
- Report what changed, which checks ran, and any checks that could not run.

## Project map

Alarmlist compiles XML-based `.almx` source files into a resolved XML alarm list.
It provides a compiler library, MSBuild tasks, and a NuGet-distributed MSBuild SDK
for `.almproj` projects. The solution is `AlarmlistProject.slnx`.

| Location | Responsibility |
| --- | --- |
| `src/Compiler/` | `Alarmlist.Core`: source parsing, syntax, binding, diagnostics, resolved models, and output writers. |
| `src/Compiler.UnitTests/` | Compiler tests organized by text parsing, syntax, binding, and output. |
| `src/MSBuild/AlarmList.MSBuild/` | MSBuild task implementation and logging. |
| `src/MSBuild/AlarmList.MSBuild.UnitTests/` | Task unit tests and mocked build-engine behavior. |
| `src/MSBuild/AlarmList.MSBuild.IntegrationTests/` | Integration tests invoking MSBuild against sample projects. |
| `src/MSBuild/SampleData/` | Shared ALMX samples and a project that invokes the task directly. |
| `src/SDK/Alarmlist.SDK/` | SDK packaging project, `sdk/` entry points, and `tools/` props/targets. |
| `src/SDK/Alarmlist.SDK.Tests/` | Tests of the packaged SDK, isolated consumer projects, and build lifecycle. |
| `src/VisualStudio/Alarmlist.VisualStudio/` | Visual Studio 2022/2026 CPS/VSSDK extension, project and item templates, and XML editor registration. |
| `src/VisualStudio/Alarmlist.VisualStudio.UnitTests/` | `net472` CPS build-check and MSBuild XAML schema tests. |
| `src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/` | Packaged VSIX and template-consumer tests on both frameworks. |
| `src/shared/Unittesting/` | Shared compiler/task test helpers linked through build configuration. |
| `eng/` | Repository build settings, version properties, and common build tooling. |
| `scripts/` | Batch launchers that deploy the extension and start the Visual Studio 2022 or 2026 experimental instance. |
| `template/` | Reference XML implementation/tests outside the current solution; active implementation lives in `src/`. |

Read `src/SDK/Alarmlist.SDK/README.md` for SDK usage and
`src/SDK/Alarmlist.SDK.Tests/README.md` for the package-test workflow.
Read `src/VisualStudio/README.md` for extension packaging and experimental IDE testing.

## Reference repositories and architectural direction

The project owner has explicitly identified **Arcade** and **Roslyn** as lookup,
template, and architectural reference repositories. Use them when deciding how
to organize and extend this project. They establish the intended direction for
the repository's structure, build infrastructure, and compiler design.

| Reference | How it guides this repository |
| --- | --- |
| [dotnet/arcade](https://github.com/dotnet/arcade) | Repository engineering and build infrastructure: shared root props/targets, `eng/` tooling and version configuration, consistent artifact layout, MSBuild SDK organization, packaging, and SDK integration-test patterns. Apply this guidance to the root build files, `eng/`, and `src/SDK/`. |
| [dotnet/roslyn](https://github.com/dotnet/roslyn) | Compiler architecture and code organization: clear boundaries between source text, syntax, binding/reference resolution, diagnostics, compilation, and output, with corresponding compiler tests and separate build integration. Apply this guidance to `src/Compiler/`, `src/Compiler.UnitTests/`, and the boundary with `src/MSBuild/`. |

Useful starting points:

- [Arcade SDK](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk)
  for SDK entry points, props/targets organization, and shared build conventions.
- [Arcade SDK tests](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.Tests)
  for package-consumer tests, especially the `TestProjectFixture` and `TestApp`
  patterns already referenced by this repository's SDK test README.
- [Roslyn compiler sources](https://github.com/dotnet/roslyn/tree/main/src/Compilers)
  for compiler layers, diagnostics, API responsibilities, and test organization.

When making structural changes:

- Consult the relevant reference implementation before introducing a new
  architectural pattern, and adapt it to the ALMX domain and this project's size.
- Keep the compiler independently usable, the MSBuild task focused on invoking
  it and reporting results, and the SDK focused on consumer build integration.
  Keep shared repository build policy in the root configuration and `eng/`.
- Treat this alignment as design guidance. It does not imply that every upstream
  feature is implemented here or that Roslyn packages must become dependencies.
- Preserve this repository's supported frameworks, public contracts, and
  `Arkarin0.DotNet.Arcade.Sdk` integration when adapting upstream patterns.
  Check the pinned version before copying current upstream build configuration.
- Explain significant departures from these reference patterns in the change
  description, including the local requirement that motivates the choice.

## Build environment and dependencies

- The shared configuration targets `net472;net8.0` and sets C# 12. Keep code and
  dependencies compatible with both frameworks; language support does not imply
  that a newer runtime API is available on .NET Framework.
- The in-process Visual Studio extension and its CPS unit tests target only
  `net472`; the VSIX supports Visual Studio 2022 17.9+ and 2026 x64. Its package-consumer
  integration tests still target both repository frameworks. Do not propagate
  this host-specific target to the compiler, tasks, or SDK.
- Use a .NET SDK that supports the `.slnx` solution format. `global.json` declares
  `tools.dotnet` as `9.0.302` for the repository tooling and pins
  `Arkarin0.DotNet.Arcade.Sdk` to `1.0.0-Preview`. The `tools.dotnet` entry is not
  the standard `sdk.version` pin; check `dotnet --info` when diagnosing selection.
- Running `net472` tests requires Windows and .NET Framework. Running `net8.0`
  tests requires a compatible test runtime. Report missing prerequisites rather
  than changing target frameworks to bypass them.
- Root `Directory.Build.props` and `Directory.Build.targets` import the Arcade
  SDK. Preserve these imports and inspect shared settings before overriding them
  in an individual project.
- `NuGet.config` uses nuget.org and the repository-local `.packages/` cache.
  Restore may require network access on a fresh checkout.
- Check `Directory.Packages.props`, `eng/Versions.props`, and the affected project
  when changing dependencies. `ManagePackageVersionsCentrally` is currently
  `false`, and projects also contain explicit versions; editing a central entry
  alone may not change the resolved dependency. Follow the existing convention
  and verify the effective version.
- Treat `artifacts/`, `.packages/`, `.vs/`, `bin/`, and `obj/` as generated/local
  state. Do not edit or commit them as implementation changes.

## Build and validation commands

Run commands from the repository root. Build and test restore dependencies by
default.

```powershell
dotnet build AlarmlistProject.slnx
dotnet test AlarmlistProject.slnx
```

For focused validation, choose the relevant suite:

```powershell
dotnet test src/Compiler.UnitTests/Alarmlist.Core.UnitTests.csproj --framework net8.0
dotnet test src/MSBuild/AlarmList.MSBuild.UnitTests/AlarmList.MSBuild.UnitTests.csproj --framework net8.0
dotnet test src/MSBuild/AlarmList.MSBuild.IntegrationTests/AlarmList.MSBuild.IntegrationTests.csproj --framework net8.0
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj --framework net8.0
dotnet test src/VisualStudio/Alarmlist.VisualStudio.UnitTests/Alarmlist.VisualStudio.UnitTests.csproj
dotnet test src/VisualStudio/Alarmlist.VisualStudio.IntegrationTests/Alarmlist.VisualStudio.IntegrationTests.csproj
```

- Add or update regression tests for behavior changes using existing xUnit
  conventions and helpers. Assert observable behavior, diagnostics, and output,
  rather than duplicating implementation details.
- Start with the affected suite. For shared compiler, build configuration, task,
  or packaging changes, also validate the affected downstream suites and both
  target frameworks when the environment supports them. Omitting `--framework`
  runs both; a `net8.0`-only pass does not verify `net472` compatibility.
- Documentation-only changes normally need a diff and path/command review, not
  a full build. Do not claim tests passed unless they were actually run.
- The SDK package is generated during Build under
  `artifacts/packages/<Configuration>/NonShipping/`. Avoid `--no-build` when
  validating changed SDK contents unless that package and the test assets have
  already been rebuilt.
- SDK tests use an isolated local feed and cache. Add scenarios in
  `src/SDK/Alarmlist.SDK.Tests/testassets/` and use the existing
  `TestProjectCollection`, `_fixture.CreateTestApp(...)`, and `TestApp` helpers.
  Shared ALMX samples are copied from `src/MSBuild/SampleData/` at build time.
- SDK test MSBuild logs are retained under
  `artifacts/log/<Configuration>/SDKTests/<TargetFramework>/<Test>/<Instance>/`.
  Inspect them when a package-consumer build fails.
- Both SDK test frameworks use `dotnet msbuild` by default. To verify desktop
  MSBuild task loading, set `ALARMLIST_TEST_MSBUILD_PATH` to the installed Visual
  Studio `MSBuild.exe` and run the SDK suite with `--framework net472`, as shown
  in its README. Restore any previous environment-variable value afterward.

## Compiler and file-format contracts

- Keep the existing flow: `Text/AlmxFile` reads source XML into `Syntax` models;
  `Binding/Binder` merges sources and resolves references; `AlarmCompiler`
  produces a `CompilationResult`; `Output` writers serialize resolved alarms.
  Keep compiler behavior independent of MSBuild-specific APIs.
- ALMX source serialization and compiled output serialization are distinct.
  Source XML uses an `Alarmlist` root and supports references and procedure
  directives; compiled XML uses an `AlarmList` root and resolved values. Preserve
  element names and casing unless the task explicitly changes the format.
- Preserve reference inheritance, local overrides, procedure ordering, and
  `Clear` behavior. Extend relevant parser, binder, and syntax tests when
  changing these semantics, including references across multiple files.
- Preserve DTD prohibition and unknown-element skipping in XML parsing.
- Keep diagnostic IDs stable: `ALM0001` duplicate alarm name, `ALM0002` missing
  reference, `ALM0003` self-reference, and `ALM0004` circular reference. Use the
  existing diagnostic descriptors/bag and preserve error propagation through
  `CompilationResult` and MSBuild logging.
- Compilation errors must fail the MSBuild task without writing a new compiled
  output. Test failure paths as well as successful compilation.

## MSBuild SDK contracts

- Keep consumer `.almproj` projects usable without a `TargetFramework`.
  Task assembly selection depends on `MSBuildRuntimeType`: .NET MSBuild loads
  `tasks/net8.0`, while desktop MSBuild loads `tasks/net472`.
- The SDK package must contain both task runtimes, the compiler, and runtime
  dependencies. MSBuild supplies `Microsoft.Build.*`; do not bundle those DLLs.
  Preserve the SDK package layout and its package-content tests.
- Preserve default `**/*.almx` inclusion, exclusions for output/intermediate
  and hidden directories, explicit item selection, conditional items, and user
  overrides of output paths and names.
- Builds currently recompile selected inputs on each invocation so removed or
  conditional inputs are reflected. Do not add incremental skipping without
  covering these cases. Preserve the design-time-build guard.
- `Clean` deletes the configured compiled file and preserves unrelated files.
  Keep `Build`, `Clean`, `Rebuild`, `Restore`, `GetTargetPath`, custom build hooks,
  and consumer `Directory.Build.props/targets` imports working.
- The SDK test project's conditional `ProjectReference` metadata handles
  command-line package builds versus Visual Studio and
  `BuildProjectReferences=false`. Keep its `ProjectReferenceTests` passing when
  changing project-reference or target-framework handling.
- Update the SDK README and consumer tests when changing public SDK properties,
  defaults, package layout, or lifecycle behavior.
- Keep CPS capabilities and XAML rules in the SDK's `tools/` directory. The VSIX
  templates must use the built SDK version; do not add private compiler copies
  or hard-coded local package paths. Keep the CPS up-to-date provider requesting
  builds until removed/conditional input handling has an alternative guarantee.
- Release the authoritative SDK package to NuGet.org before distributing its
  matching VSIX. `eng/release-visualstudio.ps1` separates candidate preparation,
  explicit SDK publication, and fresh-cache verification of the published SDK.
  Do not bundle a private SDK in the VSIX or modify users' NuGet sources on install.
- Command-line builds create a VSIX without deploying it. Visual Studio builds
  deploy to `AlarmlistExp`, which is also the F5 launch instance. Use Visual Studio
  2022 or 2026 for this workflow. Use desktop MSBuild for explicit experimental deployment
  and `eng/test-visualstudio.ps1` for the IDE smoke test. Package tests alone do not
  verify Visual Studio UI behavior.

## Coding style

- Follow `.editorconfig`: UTF-8, a final newline, spaces, four-space C#
  indentation, two-space XML/MSBuild/PowerShell indentation, and LF for shell
  scripts. Preserve surrounding line endings and avoid whole-file reformatting.
- Use the existing block-style namespaces and braces on new lines. Keep using
  directives outside namespaces with `System` imports first.
- Prefer explicit types where inference is unclear, C# keyword type names,
  `_camelCase` private/internal instance fields, `s_camelCase` private/internal
  static fields, and PascalCase constants, following `.editorconfig`.
- Preserve existing license headers and use the configured header for new C#
  files. Avoid renaming public symbols, package IDs, XML elements, or paths to
  normalize the repository's existing `AlarmList`/`Alarmlist` casing.
