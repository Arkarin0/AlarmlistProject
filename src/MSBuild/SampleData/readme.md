# SampleData Folder
This folder contains a small Alarmlist MSBuild project and sample ALMX files.

## Files
|File(s)|Description|
|---|---|
|*.almx|Alarm source files consumed by the compiler.|
|SampleData.almproj|MSBuild project that invokes `AlarmlistBuildTask`.|

## How to use

Build the task assembly first, then build the sample project:

```powershell
dotnet build ..\AlarmList.MSBuild\AlarmList.MSBuild.csproj
dotnet msbuild .\SampleData.almproj /t:Build
```

The compiled output is written to `bin\SampleData.Alarmlist.xml`.

This sample invokes the task directly. For SDK-style projects, default source
globbing, and packaged task resolution, see the
[Alarmlist.MSBuild.SDK guide](../../SDK/Alarmlist.SDK/README.md).
