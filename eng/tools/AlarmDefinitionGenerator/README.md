# Alarm definition generator

This build tool validates the repository's versioned XML alarm definition and
generates compiler model members and detached Visual Studio field metadata.
It runs on net8.0; generated code supports the compiler's net472/net8.0 and the
extension's net472 targets. It adds no runtime compiler filesystem access.

The authoritative input is `src/Compiler/Definition/AlarmDefinition.xml`; its
schema is `AlarmDefinition.xsd` beside it. Ordinary field names use portable ASCII
C#/XML identifiers. Names are also expression suffixes and must not collide with
reserved members. Types and metadata are documented in
[milestone 11](../../../src/VisualStudio/MILESTONE-11.md).

Shared `eng/targets/AlarmDefinitionGeneration.targets` builds the tool and runs it
before CoreCompile, including fresh design-time builds. Generated files live in
each consumer's intermediate directory. Normal Clean removes those files. Atomic
installation and unchanged-content detection preserve complete outputs and avoid
rewriting unchanged source. A validation error fails the calling build; the last
complete generated file is not a fallback for a failed build.

Run focused checks from the repository root:

```powershell
dotnet test eng/tools/AlarmDefinitionGenerator.UnitTests/AlarmDefinitionGenerator.UnitTests.csproj
./eng/test-alarm-definition-generation.ps1
```

The lifecycle check exercises Clean, a fresh design-time compile, parallel and
no-op builds, invalid-definition failure and recovery. Evidence remains under
`artifacts/log/Debug/AlarmDefinitionGeneration/`. It does not edit tracked source.

The CLI accepts `<definition.xml> <output.cs> <compiler|host>`. An
`AlarmlistDefinitionFile` MSBuild override is available for isolated fork fixtures;
it is a repository build input, not an `.almproj` consumer option. The semantic
fingerprint excludes presentation metadata; the tooling fingerprint includes it.

Current iteration boundary: standard-schema model generation is integrated.
Alternate schemas require the later compiler, contract and designer iterations
before they are supported end to end. Do not treat generated type declarations
alone as evidence of typed compilation support.
