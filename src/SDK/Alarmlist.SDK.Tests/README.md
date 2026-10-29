# Alarmlist SDK package tests

The structure follows [Microsoft.DotNet.Arcade.Sdk.Tests](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Arcade.Sdk.Tests),
especially its [TestProjectFixture](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestProjectFixture.cs)
and [TestApp](https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.Arcade.Sdk.Tests/Utilities/TestApp.cs).

- `testassets/` contains named `.almproj` scenarios and shared boilerplate. ALMX
  samples are linked from `src/MSBuild/SampleData` and copied at build time.
- The test project builds the SDK package, copies it into the test output, and
  generates a `global.json` pinned to that package's version. Package-consumer tests require
  only the built test output, the selected MSBuild host, and the test runtimes.
- `TestProjectCollection` shares a fixture across the feature tests.
  `CreateTestApp` copies a scenario and its boilerplate to a unique temporary
  directory, with its own NuGet cache. The fixture cleans up those copies.
- `TestApp` runs `dotnet msbuild`, reports its output through `ITestOutputHelper`,
  and retains text and binary logs under
  `artifacts/log/<Configuration>/SDKTests/<TargetFramework>/<Test>/<Instance>/`.
  Each invocation receives a separate log, including expected failures.
- Tests cover package contents, compilation, conditional inputs, diagnostics,
  framework selection, and Clean/Rebuild behavior.
- `LibraryPackTests` and `PackageRestoreTests` build real library artifacts and
  packages in isolated local feeds. They cover exact project/package dependency
  mapping, mixed graphs, equivalent compiled XML, warm-cache restore against an
  empty feed, updates/removal, missing artifacts, version conflicts, non-packable
  dependencies and prior-output preservation. The same tests can use desktop
  MSBuild through `ALARMLIST_TEST_MSBUILD_PATH` below.
- `ExamplePlaygroundTests` builds isolated copies of the example's current source
  inputs with the packaged SDK and both prepared sample-analyzer runtimes. It
  checks project/shared configuration scope, warning controls, source preservation,
  output preservation on promotion, and the missing-preparation message. It does
  not build or modify the persistent playground's outputs.
- `LibraryExampleTests` builds an isolated copy of `example/Libraries/Libraries.sln`,
  checks both consumers' transitive parameter/resource/lookup overrides, packs
  the libraries and verifies equivalent output after switching one consumer to
  PackageReference. It never changes the persistent example sources.
- `AnalyzerLifecycle` builds a disposable analyzer against the restored SDK's
  compiler for the selected MSBuild runtime. It checks one disposal after a
  multi-culture batch, output/manifest preservation on disposal failure, and
  recovery on the following build. Desktop coverage requires the matching .NET
  Framework developer pack, as well as desktop MSBuild.
- `AnalyzerPackageTests` consumes the real sample analyzer NuGet package using
  a direct PackageReference. It verifies both runtime payloads, host selection,
  diagnostics/configuration, version update, missing-version restore failure,
  invalid-image output preservation/recovery, uninstall and owner-only tooling
  across project/package library consumption. Run with desktop MSBuild as well
  as the default .NET MSBuild to exercise both analyzer binaries.
- `AnalysisContract` loads the compiler from the restored SDK's selected task
  runtime and calls its public snapshot analysis API through reflection. It checks
  contract version, field/procedure origins, Clear, result isolation, opt-in XML
  preview and error gating, on-demand explanations (inherited/library contributors,
  detached values and cancellation), and absence of disk writes without a compiler project
  reference. The ordinary test run
  exercises the net8.0 payload; the desktop-MSBuild run below exercises net472.
- Evaluation tests check that `AlarmlistAnalysisCompilerAssembly` points to the
  restored package's net472 compiler under either MSBuild runtime, including when
  the build-task path is overridden, without running Build or writing output.
- `ProjectReferenceTests` additionally exercises the test project's actual SDK
  reference with Visual Studio build settings and `BuildProjectReferences=false`.
  These repository-build regression tests require the checkout and its restored
  dependencies. They verify that MSBuild can query `GetTargetPath` for both
  frameworks without building the referenced project.

From the repository root:

```powershell
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj
```

The suite targets both `net472` and `net8.0`. To select a test runtime, use
`--framework net8.0` or `--framework net472`. Both use `dotnet msbuild` by default;
the test assembly's framework and the MSBuild host are independent.

To verify desktop MSBuild's `net472` task loading:

```powershell
$env:ALARMLIST_TEST_MSBUILD_PATH = '<Visual Studio installation>/MSBuild/Current/Bin/MSBuild.exe'
dotnet test src/SDK/Alarmlist.SDK.Tests/AlarmList.MSBuild.SDK.Tests.csproj --framework net472
Remove-Item Env:ALARMLIST_TEST_MSBUILD_PATH
```

To add coverage, add a named asset directory and a feature test in the collection.
Use `_fixture.CreateTestApp("AssetName")` and `app.Build(_output)` for a successful
build, or `app.Run(_output, "/t:Build")` to assert a failure. Assertions should
check build behavior and outputs rather than the literal spelling of targets.

Export acceptance uses ExportProviderTests, JsonExportTests and ZenonExportTests.
The test feed includes the production zenon package for both runtimes. The isolated
verification provider compiles against extracted SDK assemblies and demonstrates
declared dependencies plus multiple binary/text artifacts. Repeat these cases
against both installed desktop MSBuild hosts, restoring the previous environment
value afterward. Custom-definition package acceptance uses
`eng/test-alarm-collection-fork.ps1 -Exports -DesktopMSBuildPath ...`; consumer-only
repeats use `eng/test-alarm-definition-fork-consumers.ps1 -Collections -Exports`.
See the [milestone evidence](../../VisualStudio/MILESTONE-13.md).
