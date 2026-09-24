// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
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
        MethodInfo analyze = compilerType.GetMethod("AnalyzeSnapshots");
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
        Require((int)Value(result, "ContractVersion") == 1, "Unexpected analysis contract version.");
        Require((bool)Value(result, "Success"), "Valid unsaved inputs did not resolve.");
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
        Require(!File.Exists(basePath) && !File.Exists(childPath), "Analysis wrote snapshot text to disk.");
        Log.LogMessage(MessageImportance.High, "Packaged analysis contract 1 passed: origins, Clear, immutable results, and unsaved inputs.");
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
