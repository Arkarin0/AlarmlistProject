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
            + "<TestProcedure><Group Name='Instructions'><Step>Base step</Step></Group></TestProcedure></Alarm></Alarmlist>";
        const string childText = "<Alarmlist><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName><Name/>"
            + "<TestProcedure><Group Name='Instructions'><Note>Local step</Note></Group></TestProcedure></Alarm></Alarmlist>";
        Array snapshots = Array.CreateInstance(snapshotType, 2);
        snapshots.SetValue(Activator.CreateInstance(snapshotType, baseText, basePath, 17), 0);
        snapshots.SetValue(Activator.CreateInstance(snapshotType, childText, childPath, 23), 1);
        object result = analyze.Invoke(compiler, new object[] { snapshots });
        Require((int)Value(result, "ContractVersion") == 9, "Unexpected analysis contract version.");
        Require(((string)Value(result, "SchemaFingerprint")).Length == 64 && ((string)Value(result, "ToolingFingerprint")).Length == 64,
            "Packaged definition fingerprints are missing.");
        Require(Items(result, "FieldDefinitions").Select(field => (string)Value(field, "Name")).SequenceEqual(new[] { "Name", "Code", "Category", "Description" }),
            "Packaged field metadata does not match the standard definition.");
        Require((bool)Value(result, "Success"), "Valid unsaved inputs did not resolve.");
        object creation = Value(Items(result, "Sources")[1], "ProcedureCreation");
        Require(Value(creation, "Error") == null && Items(creation, "Groups").Select(group => (string)Value(group, "Name"))
            .SequenceEqual(new[] { "Instructions", "Reset" }), "Detached creation defaults were not provided by the packaged compiler.");
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
        Require((string)Value(name, "Type") == "String" && Value(name, "State").ToString() == "Valid"
            && (string)Value(name, "CanonicalValue") == "Inherited" && (string)Value(name, "TypedValue") == "Inherited", "Packaged typed field state was not retained.");
        object valueLocation = Value(name, "ValueLocation");
        Require((string)Value(valueLocation, "FilePath") == basePath && (int)Value(valueLocation, "SourceVersion") == 17,
            "The inherited field lost its defining source snapshot.");
        object[] entries = Items(Items(child, "ProcedureSections").Single(group => (string)Value(group, "Name") == "Instructions"), "EffectiveEntries");
        Require(entries.Length == 2 && (bool)Value(entries[0], "IsInherited") && !(bool)Value(entries[1], "IsInherited"),
            "Inherited and local procedure origins were not preserved.");

        snapshots.SetValue(Activator.CreateInstance(snapshotType, childText.Replace("<Note>Local step</Note>", "<Clear/>"), childPath, 24), 1);
        object cleared = Items(analyze.Invoke(compiler, new object[] { snapshots }), "Alarms")[1];
        object section = Items(cleared, "ProcedureSections").Single(group => (string)Value(group, "Name") == "Instructions");
        Require(Items(section, "EffectiveEntries").Length == 0 && Items(section, "ExcludedEntries").Length == 1,
            "Clear did not exclude the inherited sequence.");
        Require(Items(Items(child, "ProcedureSections").Single(group => (string)Value(group, "Name") == "Instructions"), "EffectiveEntries").Length == 2, "A later analysis mutated an earlier result.");
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
        MethodInfo libraryAnalysis = compilerType.GetMethod("AnalyzeLibrarySnapshots", new[] { snapshotsType, contextType, typeof(System.Threading.CancellationToken) });
        Require(libraryAnalysis != null, "The packaged compiler has no library analysis API.");
        Array deferredSources = Array.CreateInstance(snapshotType, 1);
        deferredSources.SetValue(Activator.CreateInstance(snapshotType,
            "<Alarmlist><Alarm><FullyQualifiedName>Deferred</FullyQualifiedName><Properties><Property Name='required'/></Properties><Name>$(required)</Name></Alarm></Alarmlist>",
            childPath, 29), 0);
        object deferredResult = libraryAnalysis.Invoke(compiler, new[] { deferredSources, context, (object)System.Threading.CancellationToken.None });
        Require((bool)Value(deferredResult, "Success") && Value(deferredResult, "CompiledXml") == null,
            "Library analysis must defer consumer values without generating application XML.");
        object deferredName = Items(Items(deferredResult, "Alarms").Single(), "Fields").Single(field => (string)Value(field, "Name") == "Name");
        Require(!(bool)Value(deferredName, "IsValueAvailable") && (string)Value(deferredName, "EffectiveExpression") == "$(required)",
            "Deferred library values lost availability or their expression.");
        Require(!(bool)Value(contextual.Invoke(compiler, new[] { deferredSources, context, (object)true }), "Success"),
            "Application analysis unexpectedly deferred missing values.");
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
        Type resourceType = assembly.GetType("Alarmlist.Text.ResxTextSnapshot", true);
        Array resources = Array.CreateInstance(resourceType, 1);
        string resourcePath = Path.Combine(ProjectDirectory, "Captured.en.resx");
        resources.SetValue(Activator.CreateInstance(resourceType,
            "<root><data name='Inspection'><value>Inspect $(p)</value><comment>Operator</comment></data></root>", resourcePath, 47, "en"), 0);
        object localizedContext = Activator.CreateInstance(contextType, resources, "en-US", null, null, null, properties);
        scoped.SetValue(Activator.CreateInstance(snapshotType, "<Alarmlist><Alarm><Name>$(loc:Inspection)</Name></Alarm></Alarmlist>", childPath, 48), 0);
        object localized = contextual.Invoke(compiler, new[] { scoped, localizedContext, (object)true });
        Require((bool)Value(localized, "Success") && (string)Value(localized, "Culture") == "en-US", "Captured localization failed.");
        Require((int)Value(Items(localized, "Resources").Single(), "Version") == 47, "Captured resource version was lost.");
        object translation = Items(localized, "Translations").Single();
        Require((string)Value(translation, "SupplyingCulture") == "en" && (string)Value(translation, "RequestedCulture") == "en-US",
            "Parent translation provenance was lost.");
        Require((string)Value(translation, "RawValue") == "Inspect $(p)" && (string)Value(translation, "Comment") == "Operator",
            "Translation content was not retained.");
        object use = Items(Items(localized, "Alarms").Single(), "LocalizationUses").Single();
        Require((string)Value(use, "Value") == "Inspect 001" && (bool)Value(use, "IsValueAvailable"), "Alarm localization ignored parameter scope.");
        Require((int)Value(Value(translation, "ValueLocation"), "SourceVersion") == 47, "Resource location has the wrong version.");
        Require(((string)Value(localized, "CompiledXml")).Contains("<Name>Inspect 001</Name>"), "Localized preview did not match evaluation.");
        object missingContext = Activator.CreateInstance(contextType, resources, "de", null, null, null, properties);
        object missingTranslation = contextual.Invoke(compiler, new[] { scoped, missingContext, (object)true });
        Require(!(bool)Value(missingTranslation, "Success") && Value(missingTranslation, "CompiledXml") == null,
            "Missing translation exposed compiled XML.");
        Require(!File.Exists(resourcePath), "Analysis read or wrote a captured resource path.");
        Type lookupType = assembly.GetType("Alarmlist.Text.LookupTextSnapshot", true);
        Array lookups = Array.CreateInstance(lookupType, 1);
        lookups.SetValue(Activator.CreateInstance(lookupType,
            "<Lookup xmlns='urn:alarmlist:lookup:1'><List Key='Wires'><Entry Key='W1' Value='=EC-120W1'/></List>"
            + "<Template Key='Inspection'><Argument Name='wire' List='Wires'/></Template></Lookup>", "captured.almlookup", 51), 0);
        object listContext = Activator.CreateInstance(contextType, resources, lookups, "en", null, null, null, properties);
        scoped.SetValue(Activator.CreateInstance(snapshotType, "<Alarmlist><Alarm><Name>$(list:Wires#W1)</Name></Alarm></Alarmlist>", childPath, 52), 0);
        object lists = contextual.Invoke(compiler, new[] { scoped, listContext, (object)true });
        Require((bool)Value(lists, "Success"), "Captured named list did not resolve.");
        object listEntry = Items(Items(lists, "LookupLists").Single(), "Entries").Single();
        Require((string)Value(listEntry, "Value") == "=EC-120W1" && (string)Value(listEntry, "Expression") == "$(list:Wires#W1)", "Literal list identity was lost.");
        Require((int)Value(Value(listEntry, "Location"), "SourceVersion") == 51, "List provenance was lost.");
        Require((bool)Value(Items(lists, "ArgumentBindings").Single(), "IsAvailable"), "Completion binding was lost.");
        Require(((string)Value(lists, "CompiledXml")).Contains("<Name>=EC-120W1</Name>"), "List preview was incorrect.");
        Type identityType = assembly.GetType("Alarmlist.Libraries.LibraryIdentity", true);
        Type librarySnapshotType = assembly.GetType("Alarmlist.Libraries.LibraryTextSnapshot", true);
        Type referencesType = assembly.GetType("Alarmlist.Libraries.LibraryReferenceSet", true);
        object identity = Activator.CreateInstance(identityType, "PackagedLibrary", "1.0.0");
        Array librarySources = Array.CreateInstance(snapshotType, 1);
        librarySources.SetValue(Activator.CreateInstance(snapshotType,
            "<Alarmlist><Alarm><FullyQualifiedName>Imported</FullyQualifiedName><Name>$(p)</Name></Alarm></Alarmlist>", "Library.almx", 1), 0);
        object emptyContext = contextType.GetProperty("Empty").GetValue(null, null);
        object builtLibrary = compilerType.GetMethod("CreateLibrary").Invoke(compiler,
            new object[] { identity, librarySources, emptyContext, System.Threading.CancellationToken.None, null });
        Require((bool)Value(builtLibrary, "Success") && Items(builtLibrary, "DeferredDiagnostics").Length != 0,
            "Packaged compiler did not preserve deferred library values.");
        Array roots = Array.CreateInstance(identityType, 1);
        roots.SetValue(identity, 0);
        Array libraryInputs = Array.CreateInstance(librarySnapshotType, 1);
        libraryInputs.SetValue(Activator.CreateInstance(librarySnapshotType, Value(builtLibrary, "ArtifactText"), "Captured.almlib", 2), 0);
        object libraryContext = contextType.GetMethod("WithLibraries").Invoke(context,
            new[] { Activator.CreateInstance(referencesType, roots, libraryInputs) });
        object imported = contextual.Invoke(compiler, new object[] { Array.CreateInstance(snapshotType, 0), libraryContext, true });
        Require((bool)Value(imported, "Success") && ((string)Value(imported, "CompiledXml")).Contains("<Name>001</Name>"),
            "Packaged library consumption did not resolve consumer values.");
        Require((string)Value(Value(Items(imported, "Libraries").Single(), "Identity"), "Id") == "PackagedLibrary",
            "Library ownership was not detached into analysis.");
        Require((string)Value(Value(Value(Items(imported, "Alarms").Single(), "Origin"), "Library"), "Id") == "PackagedLibrary",
            "Imported alarm ownership was lost.");
        Type targetType = assembly.GetType("Alarmlist.Analysis.ValueExplanationTarget", true);
        MethodInfo explain = compilerType.GetMethod("ExplainSnapshots");
        Require(explain != null, "The packaged compiler has no explanation API.");
        object target = Activator.CreateInstance(targetType, Value(Value(child, "Origin"), "Location"), "Field", "Name", null, null);
        object staleExplanation = explain.Invoke(compiler, new object[] { snapshots, emptyContext, target, System.Threading.CancellationToken.None, false });
        Require(!(bool)Value(staleExplanation, "IsAvailable"), "A stale source target was retargeted.");
        snapshots.SetValue(Activator.CreateInstance(snapshotType, childText, childPath, 23), 1);
        object explanation = explain.Invoke(compiler, new object[] { snapshots, emptyContext, target, System.Threading.CancellationToken.None, false });
        Require((bool)Value(explanation, "IsAvailable") && (int)Value(explanation, "ContractVersion") == 9,
            "Packaged explanation unavailable: " + Value(explanation, "Message"));
        object root = Items(explanation, "Nodes").Single(node => (int)Value(node, "Id") == (int)Value(explanation, "RootId"));
        Require((string)Value(root, "Value") == "Inherited" && Items(explanation, "Nodes").Any(node => (string)Value(node, "Kind") == "Inheritance"),
            "Packaged explanation lost inherited contributors.");
        object importedTarget = Activator.CreateInstance(targetType, Value(Value(Items(imported, "Alarms").Single(), "Origin"), "Location"), "Field", "Name", null, null);
        object importedExplanation = explain.Invoke(compiler, new object[] { Array.CreateInstance(snapshotType, 0), libraryContext,
            importedTarget, System.Threading.CancellationToken.None, false });
        Require((bool)Value(importedExplanation, "IsAvailable") && Items(importedExplanation, "Nodes").Any(node =>
            (string)Value(node, "Kind") == "Field" && (string)Value(node, "Value") == "001" && Value(node, "Library") != null),
            "Packaged explanation lost library ownership or consumer parameters.");
        using (var cancellation = new System.Threading.CancellationTokenSource())
        {
            cancellation.Cancel();
            bool cancelled = false;
            try { explain.Invoke(compiler, new object[] { snapshots, emptyContext, target, cancellation.Token, false }); }
            catch (TargetInvocationException exception) { cancelled = exception.InnerException is OperationCanceledException; }
            Require(cancelled, "Packaged explanation ignored cancellation.");
        }
        VerifyReferences(assembly, compilerType, compiler, snapshots, emptyContext, basePath, baseText);
        Log.LogMessage(MessageImportance.High, "Packaged analysis contract 9 passed: origins, Clear, creation defaults, immutable results, unsaved inputs, libraries, compiled XML preview, on-demand explanations, reference search and rename preview.");
        return true;
    }

    private static void VerifyReferences(Assembly assembly, Type compilerType, object compiler, Array snapshots,
        object context, string sourcePath, string sourceText)
    {
        Type requestType = assembly.GetType("Alarmlist.Analysis.ReferenceCompilationSnapshot", true);
        MethodInfo find = compilerType.GetMethod("FindReferencesSnapshots", new[] { requestType,
            assembly.GetType("Alarmlist.Analysis.ReferenceSymbolTarget", true), typeof(System.Threading.CancellationToken) });
        MethodInfo rename = compilerType.GetMethod("PreviewRenameSnapshots", new[] { requestType,
            assembly.GetType("Alarmlist.Analysis.ReferenceSymbolTarget", true), typeof(string), typeof(System.Threading.CancellationToken), typeof(IReadOnlyCollection<string>), requestType });
        Require(find != null && rename != null, "The packaged reference/rename APIs are missing.");
        object request = Activator.CreateInstance(requestType, snapshots, context, null, null, System.Threading.CancellationToken.None);
        object target = requestType.GetMethod("TargetAtToken").Invoke(request, new object[] { sourcePath, 17, sourceText.IndexOf("Base", StringComparison.Ordinal) });
        object found = find.Invoke(compiler, new object[] { request, target, System.Threading.CancellationToken.None });
        Require((bool)Value(found, "IsAvailable") && (int)Value(found, "ContractVersion") == 9, "Packaged reference search is unavailable.");
        Require(!(bool)Value(found, "IsUnsupportedTarget"), "A bound packaged reference was classified as unsupported.");
        object unsupported = requestType.GetMethod("TargetAtToken").Invoke(request, new object[] { sourcePath, 17, 0 });
        Require((bool)Value(find.Invoke(compiler, new object[] { request, unsupported, System.Threading.CancellationToken.None }), "IsUnsupportedTarget"),
            "Packaged XML caret classification lost native-command fallback for unsupported tokens.");
        object nameEnd = requestType.GetMethod("TargetAtToken").Invoke(request, new object[] { sourcePath, 17, sourceText.IndexOf("Base", StringComparison.Ordinal) + 4 });
        Require((bool)Value(find.Invoke(compiler, new object[] { request, nameEnd, System.Threading.CancellationToken.None }), "IsAvailable"),
            "A packaged caret at the end of a bound name did not resolve.");
        Require((string)Value(Value(found, "Symbol"), "Kind") == "Alarm" && Items(found, "Occurrences").Length == 2,
            "Packaged search lost the bound inheritance reference.");
        string identity = (string)Value(Value(found, "Symbol"), "DeclarationId");
        object rebound = requestType.GetMethod("TargetIdentity").Invoke(request, new object[] { identity });
        Require((bool)Value(find.Invoke(compiler, new object[] { request, rebound, System.Threading.CancellationToken.None }), "IsAvailable"),
            "A compiler-issued declaration identity could not be resolved.");
        object preview = rename.Invoke(compiler, new object[] { request, target, "RenamedBase", System.Threading.CancellationToken.None, null, null });
        Require((bool)Value(preview, "IsApplicable") && Items(preview, "Edits").Length == 2 && Items(preview, "Groups").Length == 2,
            "Packaged rename did not coordinate the definition and inheritance use.");
        Require(Items(preview, "Edits").All(edit => (string)Value(edit, "Text") == "RenamedBase")
            && (string)Value(Value(preview, "References"), "SnapshotId") == (string)Value(found, "SnapshotId"),
            "Packaged rename lost text replacements or its target snapshot.");
        object excluded = rename.Invoke(compiler, new object[] { request, target, "RenamedBase", System.Threading.CancellationToken.None, new string[0], null });
        Require(!(bool)Value(excluded, "IsApplicable") && (bool)Value(excluded, "RequiresAcknowledgement") && Items(excluded, "Edits").Length == 0,
            "Excluded groups were silently restored to the approved edit set.");
        bool readOnly = false;
        try { ((IList)Value(preview, "Edits")).Clear(); }
        catch (NotSupportedException) { readOnly = true; }
        Require(readOnly, "Packaged rename results are mutable.");
        object protectedRequest = Activator.CreateInstance(requestType, snapshots, context, null, null,
            System.Threading.CancellationToken.None, new[] { sourcePath });
        object protectedTarget = requestType.GetMethod("TargetAtToken").Invoke(protectedRequest,
            new object[] { sourcePath, 17, sourceText.IndexOf("Base", StringComparison.Ordinal) });
        object protectedFound = find.Invoke(compiler, new object[] { protectedRequest, protectedTarget, System.Threading.CancellationToken.None });
        Require((bool)Value(protectedFound, "IsAvailable") && (bool)Value(Value(protectedFound, "Symbol"), "IsReadOnly"),
            "Captured package/SDK source ownership was not retained by the compiler.");
        object protectedPreview = rename.Invoke(compiler, new object[] { protectedRequest, protectedTarget, "RenamedBase", System.Threading.CancellationToken.None, null, null });
        Require(Items(protectedPreview, "Edits").All(edit => (string)Value(edit, "Path") != sourcePath)
            && (bool)Value(protectedPreview, "RequiresAcknowledgement"), "A captured read-only source was included in rename edits.");
        Require(!(bool)Value(find.Invoke(compiler, new object[] { protectedRequest, target, System.Threading.CancellationToken.None }), "IsAvailable"),
            "A target remained current after its captured source ownership changed.");
        using (var cancellation = new System.Threading.CancellationTokenSource())
        {
            cancellation.Cancel();
            foreach (MethodInfo method in new[] { find, rename })
            {
                bool cancelled = false;
                try { method.Invoke(compiler, method == find ? new object[] { request, target, cancellation.Token }
                    : new object[] { request, target, "RenamedBase", cancellation.Token, null, null }); }
                catch (TargetInvocationException exception) { cancelled = exception.InnerException is OperationCanceledException; }
                Require(cancelled, "Packaged reference/rename request ignored cancellation.");
            }
        }
        Require(!File.Exists(sourcePath), "Reference search or rename accessed the captured source path.");
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
