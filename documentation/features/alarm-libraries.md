# Reusable alarm libraries

Build reusable definitions into `.almlib` artifacts and share them through project
references or NuGet packages.

## When to use it

Use libraries for templates, common alarms, translations and lookups reused by
several independently built consumers. Use shared projects when you want the
same physical source included directly instead.

## Requirements and availability

Use matching compiler/SDK tools. Library artifacts must match the semantic alarm
definition; rebuild older or incompatible libraries. NuGet references require
restore and a feed containing dependency packages.

## How it works

Library Build validates reusable inputs, allowing consumer-supplied values to be
deferred. Applications resolve the dependency graph in their own parameter,
resource and lookup scope. Abstract alarms stay reusable but are not emitted.
References preserve dependency identities and reject conflicting versions.
Project-reference builds normally build dependencies first. Saved dependency
source edits refresh IDE consumers; unsaved dependency drafts remain private.
Package consumers need newly packed/restored artifacts to see changes.

Pack builds a library and creates a NuGet package containing one artifact under
`alarmlist/`; direct library references become exact package dependencies rather
than embedded artifacts. Pack does not publish. Applications cannot be packed as
alarm libraries, and `NoBuild=true` is not supported. Clean owns compiled outputs,
not distribution packages.

## Configuration or syntax

Set `OutputType` to `Library`. `PackageId` defaults to the project name,
`PackageVersion` to `1.0.0`, and `AlarmlistLibraryFileName` to `<PackageId>.almlib`.
`PackageOutputPath` defaults to OutputPath. Use ProjectReference for local libraries
or PackageReference for packages. In Visual Studio use Add Reference or Manage
NuGet Packages; the Dependencies tree shows selected direct/transitive references
and read-only declared package content. Browsing does not build or load analyzers.

## Minimal example

In `Common/Common.almproj`, with an available SDK pin inherited from its parent:

```xml
<Project Sdk="Alarmlist.MSBuild.SDK">
  <PropertyGroup><OutputType>Library</OutputType></PropertyGroup>
</Project>
```

Add ALMX definitions, then in a sibling application's `.almproj` add:

```xml
<ItemGroup>
  <ProjectReference Include="../Common/Common.almproj" />
</ItemGroup>
```

### Expected result

Building the application builds Common first and includes its concrete alarms.
`dotnet msbuild Common/Common.almproj /restore /t:Pack`, run from their parent,
creates a local package without publishing it.

## Troubleshooting

| Symptom | Cause | Resolution |
| --- | --- | --- |
| Artifact incompatible | Semantic definition differs | Rebuild the library with the consumer's matching definition. |
| Package change not visible | Old restored artifact | Pack a new version and restore the selected dependency. |
| Missing transitive artifact | Dependency feed/restore incomplete | Supply the full dependency graph and restore again. |

## Related features and tutorials

See [reuse libraries](../tutorials/reuse-alarm-libraries.md),
[shared projects](shared-projects.md), and the [library playground](../../example/Libraries/README.md).
