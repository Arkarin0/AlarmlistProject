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
