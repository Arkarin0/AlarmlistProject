# Sample analyzer

`CodePrefixAnalyzer` implements analyzer contract 2. SAMPLE001 warns when a concrete
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

The assemblies are under `artifacts/bin/Alarmlist.SampleAnalyzer/<Configuration>/`
for net472 and net8.0. Reference the appropriate DLL using `AlarmlistAnalyzer` in
your `.almproj`; see the [SDK guide](../../SDK/Alarmlist.SDK/README.md#diagnostic-configuration-and-analyzers).
The sample is not shipped as an automatic or mandatory rule.

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
