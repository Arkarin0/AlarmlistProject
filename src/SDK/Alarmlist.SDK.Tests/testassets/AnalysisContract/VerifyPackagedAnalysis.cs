// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

public sealed class VerifyPackagedAnalysis : Task
{
    [Required]
    public string CompilerAssembly { get; set; }

    [Required]
    public string ProjectDirectory { get; set; }

    public override bool Execute()
    {
        // The consumer has no compiler project reference. Load only the compiler
        // selected by the restored SDK for this MSBuild runtime.
        Assembly assembly = Assembly.LoadFrom(CompilerAssembly);
        Require(string.Equals(Path.GetFullPath(CompilerAssembly), Path.GetFullPath(assembly.Location), StringComparison.OrdinalIgnoreCase),
            "A compiler outside the resolved SDK was loaded.");
        Type snapshotType = assembly.GetType("Alarmlist.Text.AlmxTextSnapshot", true);
        Type compilerType = assembly.GetType("Alarmlist.Compiler.AlarmCompiler", true);
        object compiler = Activator.CreateInstance(compilerType);
        Type snapshotsType = typeof(IEnumerable<>).MakeGenericType(snapshotType);
        MethodInfo analyze = compilerType.GetMethod("AnalyzeSnapshots", new[] { snapshotsType });
        Require(analyze != null, "The packaged compiler has no snapshot analysis API.");
        string basePath = Path.Combine(ProjectDirectory, "UnsavedBase.almx");
        string childPath = Path.Combine(ProjectDirectory, "UnsavedChild.almx");
        const string baseText = "<Alarmlist><Alarm><FullyQualifiedName>Base</FullyQualifiedName><Name>Inherited</Name>"
            + "<TestProcedure><Instructions><Step>Base step</Step></Instructions></TestProcedure></Alarm></Alarmlist>";
        const string childText = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Name/>"
            + "<TestProcedure><Instructions><Note>Local step</Note></Instructions></TestProcedure></Alarm></Alarmlist>";
        Array snapshots = Array.CreateInstance(snapshotType, 2);
        snapshots.SetValue(Activator.CreateInstance(snapshotType, baseText, basePath, 17), 0);
        snapshots.SetValue(Activator.CreateInstance(snapshotType, childText, childPath, 23), 1);
        object result = analyze.Invoke(compiler, new object[] { snapshots });
        Require((int)Value(result, "ContractVersion") == 2, "Unexpected analysis contract version.");
        Require((bool)Value(result, "Success"), "Valid unsaved inputs did not resolve.");
        Require(Value(result, "CompiledXml") == null, "Default analysis unexpectedly generated XML.");
        MethodInfo preview = compilerType.GetMethod("AnalyzeSnapshots", new[] { snapshotsType, typeof(bool) });
        Require(preview != null, "The packaged compiler has no in-memory preview API.");
        object previewResult = preview.Invoke(compiler, new object[] { snapshots, true });
        string xml = (string)Value(previewResult, "CompiledXml");
        Require(xml != null && xml.Contains("<AlarmList>") && xml.Contains("<Name>Inherited</Name>")
            && xml.Contains("Kind=\"Instruction\"") && !xml.Contains("<ReferenceName>"), "Compiled preview did not use the output format.");
        object child = Items(result, "Alarms")[1];
        object name = Items(child, "Fields").Single(field => (string)Value(field, "Name") == "Name");
        Require((bool)Value(name, "IsPresent") && (bool)Value(name, "IsInherited"), "An empty local element lost its inherited state.");
        Require((string)Value(name, "EffectiveValue") == "Inherited", "The inherited field value is incorrect.");
        object valueLocation = Value(name, "ValueLocation");
        Require((string)Value(valueLocation, "FilePath") == basePath && (int)Value(valueLocation, "SourceVersion") == 17,
            "The inherited field lost its defining source snapshot.");
        object[] entries = Items(Value(child, "Instructions"), "EffectiveEntries");
        Require(entries.Length == 2 && (bool)Value(entries[0], "IsInherited") && !(bool)Value(entries[1], "IsInherited"),
            "Inherited and local procedure origins were not preserved.");

        snapshots.SetValue(Activator.CreateInstance(snapshotType, childText.Replace("<Note>Local step</Note>", "<Clear/>"), childPath, 24), 1);
        object cleared = Items(analyze.Invoke(compiler, new object[] { snapshots }), "Alarms")[1];
        object section = Value(cleared, "Instructions");
        Require(Items(section, "EffectiveEntries").Length == 0 && Items(section, "ExcludedEntries").Length == 1,
            "Clear did not exclude the inherited sequence.");
        Require(Items(Value(child, "Instructions"), "EffectiveEntries").Length == 2, "A later analysis mutated an earlier result.");
        snapshots.SetValue(Activator.CreateInstance(snapshotType, childText.Replace(">Base<", ">Missing<"), childPath, 25), 1);
        object failed = preview.Invoke(compiler, new object[] { snapshots, true });
        Require(!(bool)Value(failed, "Success") && Value(failed, "CompiledXml") == null, "Failed compilation exposed project XML.");
        Require((string)Value(previewResult, "CompiledXml") == xml, "A later analysis mutated earlier XML.");
        Type contextType = assembly.GetType("Alarmlist.Compiler.CompilationContext", true);
        Type propertyType = assembly.GetType("Alarmlist.Compiler.PropertyDefinition", true);
        Array properties = Array.CreateInstance(propertyType, 1);
        properties.SetValue(Activator.CreateInstance(propertyType, new object[] { "p", "001", null }), 0);
        object context = Activator.CreateInstance(contextType, Path.Combine(ProjectDirectory, "Plant.almproj"), "Release", "AnyCPU", properties);
        MethodInfo contextual = compilerType.GetMethod("AnalyzeSnapshots", new[] { snapshotsType, contextType, typeof(bool) });
        Require(contextual != null, "The packaged compiler has no captured-context analysis API.");
        Array scoped = Array.CreateInstance(snapshotType, 1);
        scoped.SetValue(Activator.CreateInstance(snapshotType, "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName>"
            + "<Properties><Property Name='p' Value='ignored'/></Properties><Code>$(p)</Code>"
            + "<Description>Inspect $(AlarmlistAlarmCode):$(AlarmlistConfiguration)</Description></Alarm>"
            + "<Alarm><ReferenceName>A</ReferenceName></Alarm></Alarmlist>", childPath, 30), 0);
        object scopedResult = contextual.Invoke(compiler, new[] { scoped, context, (object)true });
        Require((bool)Value(scopedResult, "Success"), "Captured project properties did not resolve.");
        object scopedChild = Items(scopedResult, "Alarms")[1];
        object code = Items(scopedChild, "Fields").Single(field => (string)Value(field, "Name") == "Code");
        Require((string)Value(code, "EffectiveExpression") == "$(p)" && (string)Value(code, "EffectiveValue") == "001",
            "Raw and evaluated values were not kept separately.");
        object parameter = Items(scopedChild, "Properties").Single(property => (string)Value(property, "Name") == "p");
        Require((string)Value(Value(parameter, "DeclarationOrigin"), "Scope") == "Alarm"
            && (string)Value(Value(parameter, "ValueOrigin"), "Scope") == "Project", "Parameter provenance was lost.");
        Require((bool)Value(Items(scopedResult, "Alarms")[0], "IsAbstract") && (bool)Value(scopedChild, "HasEvaluatedValues"),
            "Template and evaluated availability were lost.");
        Require(((string)Value(scopedResult, "CompiledXml")).Contains("Inspect 001:Release"), "Preview ignored captured context.");
        Require(!File.Exists(basePath) && !File.Exists(childPath), "Analysis wrote snapshot text to disk.");
        Log.LogMessage(MessageImportance.High, "Packaged analysis contract 2 passed: origins, Clear, immutable results, unsaved inputs, and compiled XML preview.");
        return true;
    }

    private static object Value(object instance, string property)
    {
        return instance.GetType().GetProperty(property).GetValue(instance, null);
    }

    private static object[] Items(object instance, string property)
    {
        return ((IEnumerable)Value(instance, property)).Cast<object>().ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
