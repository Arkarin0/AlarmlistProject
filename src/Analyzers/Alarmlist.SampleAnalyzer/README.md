# Sample analyzer

`CodePrefixAnalyzer` implements analyzer contract 3. SAMPLE001 warns when a concrete
alarm's resolved `Code` does not start with `AL-`. Configure the prefix per source:

```ini
[*.almx]
alarmlist_sample.code_prefix = PLANT-
alarmlist_diagnostic.SAMPLE001.severity = warning
```

Build from the repository root:

```powershell
dotnet build src/Analyzers/Alarmlist.SampleAnalyzer/Alarmlist.SampleAnalyzer.csproj
```

Build also produces `Alarmlist.SampleAnalyzer.<version>.nupkg` in the repository's
package output. Add that directory to your NuGet sources, then reference the
package directly in your `.almproj` (replace the version with the built version):

```xml
<ItemGroup>
  <PackageReference Include="Alarmlist.SampleAnalyzer" Version="1.0.0" PrivateAssets="all" />
</ItemGroup>
```

Restore before building. The package's ordinary `build` targets register an
`AlarmlistAnalyzer` from `tools/net472` for desktop MSBuild/Visual Studio or
`tools/net8.0` for .NET MSBuild. No consumer TargetFramework is needed. The package
does not bundle the compiler contract or expose managed `lib` assets. It is opt-in
tooling, not a mandatory rule or a transitive alarm-library dependency.

Direct DLL registration remains supported. Assemblies are under
`artifacts/bin/Alarmlist.SampleAnalyzer/<Configuration>/` for both runtimes;
see the [SDK guide](../../SDK/Alarmlist.SDK/README.md#diagnostic-configuration-and-analyzers).

For your own analyzer package, ship both runtime payloads and an unqualified
`build/<PackageId>.targets` registering the host-appropriate `AlarmlistAnalyzer`
relative to `MSBuildThisFileDirectory`. Keep private managed dependencies beside
each payload; use separate runtime directories. Do not ship another Alarmlist.Core
or use `buildTransitive` to activate rules in downstream projects. Ordinary Roslyn
analyzer assets are not Alarmlist analyzers. Package version updates require restore;
invalid DLLs/contracts report ALM0025 and preserve previous compiled outputs.

An analyzer exposes its contract version, immutable `SupportedDiagnostics`, and
`Analyze(AlarmAnalyzerContext, CancellationToken)`. Use unique letter-led IDs
containing letters, digits or underscores outside the reserved `ALM` prefix.
Duplicate or undeclared IDs are analyzer failures. A warning descriptor is
configurable by default; an error descriptor must opt in with `isConfigurable: true`
to permit suppression or demotion.

The context supplies detached resolved alarms, raw/effective fields, generic
procedure sections, provenance, culture and captured file options. Check inherited
values in the consuming alarm's context. The sample reports that alarm as primary
and the inherited declaration as related, so the consuming file's policy applies.
Do not reparse ALMX or infer inheritance from equal strings.

Check cancellation inside long loops. Return diagnostics only for declared rules;
the compiler freezes their messages and applies policy. An exception discards that
analyzer's partial results and emits ALM0025. Do not save inputs, write build output,
retain context objects, or assume execution on the UI thread. Keep dependencies
compatible with the selected runtime and the authoritative SDK compiler contract.
