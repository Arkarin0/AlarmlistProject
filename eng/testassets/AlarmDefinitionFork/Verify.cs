// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Analysis;
using Alarmlist.Analyzers;
using Alarmlist.Compiler;
using Alarmlist.Definition;
using Alarmlist.Diagnostics;
using Alarmlist.Text;

namespace AlarmDefinitionFork
{
    public sealed class ForkAnalyzer : IAlarmAnalyzer
    {
        private static readonly DiagnosticDescriptor s_success = new DiagnosticDescriptor("FORK001", "Fork verification", "Typed fork analyzer executed.", DiagnosticSeverity.Warning);
        private static readonly DiagnosticDescriptor s_failure = new DiagnosticDescriptor("FORK002", "Fork verification failure", "Fork metadata or typed values differ.", DiagnosticSeverity.Error);
        public int ContractVersion => 5;
        public IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; } = Array.AsReadOnly(new[] { s_success, s_failure });

        public IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool valid = context.FieldDefinitions.All(field => field.Name != "Name" && field.Name != "Category")
                && context.FieldDefinitions.Single(field => field.Name == "Code").Type == "Int64"
                && context.SchemaFingerprint == AlarmDefinition.SchemaFingerprint
                && context.Alarms.Count == 2;
            foreach (var alarm in context.Alarms)
            {
                var code = alarm.Fields.Single(field => field.Name == "Code");
                valid &= code.TypedValue is long value && value == (alarm.Origin.FullyQualifiedName == "Library" ? 8 : 7);
                valid &= code.IsDefault == (alarm.Origin.FullyQualifiedName == "Local");
                if (context.CollectionDefinitions.Count != 0 && alarm.Origin.FullyQualifiedName == "Library")
                {
                    var record = alarm.Collections.Single(collection => collection.Name == "Checks").EffectiveEntries.Single();
                    valid &= record.Fields.Single(field => field.Name == "Copy").TypedValue is long copy && copy == 8;
                    valid &= record.Origin.Alarm.Library != null && context.HasTestProcedure == AlarmDefinition.HasTestProcedure;
                }
            }
            yield return new Diagnostic(valid ? s_success : s_failure);
        }
    }

    internal static class Program
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Main(string[] args)
        {
            var libraryArtifact = XDocument.Load(args[1]);
            Check((string)libraryArtifact.Root.Attribute("SchemaFingerprint") == AlarmDefinition.SchemaFingerprint, "Library semantic fingerprint matches packaged compiler.");
            var alarms = XDocument.Load(args[0]).Root.Elements("Alarm").ToArray();
            Check(alarms.Length == 2, "Library and local alarms must both be emitted.");
            var library = alarms.Single(alarm => alarm.Element("FullyQualifiedName").Value == "Library");
            var local = alarms.Single(alarm => alarm.Element("FullyQualifiedName").Value == "Local");
            var expected = new Dictionary<string, string>
            {
                ["Code"] = "8", ["Description"] = "Consumer text", ["Price"] = "12.5",
                ["Ratio"] = "1000", ["Enabled"] = "true", ["Priority"] = "High",
                ["Day"] = "2024-02-29", ["Clock"] = "12:34:56.12", ["Instant"] = "2024-02-29T12:34:56.12+02:00"
            };
            foreach (var pair in expected) Check(library.Element(pair.Key)?.Value == pair.Value, "Canonical field: " + pair.Key);
            Check(local.Element("Code")?.Value == "7" && local.Element("Price") == null, "Default and optional absence.");
            Check(alarms.All(alarm => alarm.Element("Name") == null && alarm.Element("Category") == null), "Removed fields absent.");
            Check(alarms.All(alarm => (alarm.Element("TestProcedure") != null) == AlarmDefinition.HasTestProcedure), "Procedure capability respected.");
            if (AlarmDefinition.Collections.Count != 0)
            {
                Check(library.Element("Numbers").Element("Item").Value == "8" && library.Element("Checks").Element("Item").Element("Copy").Value == "8", "Packaged collection expressions and canonical output.");
                Check(libraryArtifact.ToString().Contains("$(item:Count)"), "Raw sibling expression retained in library.");
            }
            foreach (var alarm in alarms)
            {
                var expectedOrder = AlarmDefinition.Fields.Where(field => alarm.Element(field.Name) != null).Select(field => field.Name).ToArray();
                var actualOrder = alarm.Elements().Select(element => element.Name.LocalName).Where(name => expectedOrder.Contains(name));
                Check(expectedOrder.SequenceEqual(actualOrder), "Output respects definition field order.");
            }
            string jsonPath = Path.ChangeExtension(args[0], ".json");
            if (args.Length > 2 && args[2] == "exports")
            {
                XElement json;
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(File.ReadAllBytes(jsonPath), XmlDictionaryReaderQuotas.Max))
                    json = XDocument.Load(reader).Root;
                Check(json.Element("definitionFingerprint").Value == AlarmDefinition.SchemaFingerprint, "JSON semantic fingerprint matches fork.");
                Check(json.Element("hasTestProcedure").Value == (AlarmDefinition.HasTestProcedure ? "true" : "false"), "JSON procedure capability.");
                var jsonAlarms = json.Element("alarms").Elements("item").ToArray();
                Check(jsonAlarms.Length == 2, "JSON contains library/local alarms.");
                var jsonLibrary = jsonAlarms.Single(alarm => alarm.Element("fullyQualifiedName").Value == "Library");
                var fields = jsonLibrary.Element("fields").Elements("item").ToArray();
                foreach (var pair in expected) Check(fields.Single(field => field.Element("name").Value == pair.Key).Element("value").Value == pair.Value, "JSON canonical field " + pair.Key);
                Check((string)fields.Single(field => field.Element("name").Value == "Code").Element("value").Attribute("type") == "number", "JSON retyped Code remains numeric.");
                Check(fields.All(field => field.Element("name").Value != "Name" && field.Element("name").Value != "Category"), "JSON removed fields absent.");
                if (AlarmDefinition.Collections.Count != 0)
                    Check(jsonLibrary.Element("collections").Elements("item").Single(collection => collection.Element("name").Value == "Checks").Element("items").Element("item").Element("fields").Elements("item").Single(field => field.Element("name").Value == "Copy").Element("value").Value == "8", "JSON collection sibling typed value.");
                string zenonPath = Path.Combine(Path.GetDirectoryName(args[0]), "Consumer.Zenon.xml");
                Check(XNode.DeepEquals(XDocument.Load(args[0]), XDocument.Load(zenonPath)), "Zenon preserves literal/canonical fields and custom XML shape.");
            }
            var analysis = new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Code>+008</Code></Alarm></Alarmlist>", "fixture.almx", 1) });
            var code = analysis.Alarms.Single().Fields.Single(field => field.Name == "Code");
            Check(analysis.Success && code.TypedValue is long number && number == 8 && code.CanonicalValue == "8", "Packaged analysis typed value.");
            VerifyReferences();
            Console.WriteLine("Packaged fork values verified: " + args[0]);
        }

        private static void VerifyReferences()
        {
            string text = "<Alarmlist><Alarm abstract='true'><FullyQualifiedName>A</FullyQualifiedName><Code>+008</Code>"
                + (AlarmDefinition.Collections.Count == 0 ? "" : "<Checks><Item><Count>8</Count><Copy>$(item:Count)</Copy></Item></Checks>")
                + "</Alarm><Alarm><FullyQualifiedName>B</FullyQualifiedName><ReferenceName>A</ReferenceName><Description>Keep literal A</Description></Alarm></Alarmlist>";
            var source = new AlmxTextSnapshot(text, "references.almx", 3);
            var request = new ReferenceCompilationSnapshot(new[] { source }, CompilationContext.Empty);
            var compiler = new AlarmCompiler();
            var alarm = request.TargetAtToken(source.FilePath, source.Version, text.IndexOf(">A<", StringComparison.Ordinal) + 1);
            var found = compiler.FindReferencesSnapshots(request, alarm);
            Check(found.IsAvailable && found.Symbol.Name == "A" && found.Occurrences.Count == 2, "Packaged fork alarm declaration/inheritance references.");
            var rename = compiler.PreviewRenameSnapshots(request, alarm, "RenamedA");
            Check(rename.IsApplicable && rename.Edits.Count == 2 && rename.Edits.All(edit => edit.Text == "RenamedA"), "Packaged fork rename preserves literal text.");
            var generated = request.TargetBuiltIn("AlarmlistAlarmCode");
            var field = compiler.FindReferencesSnapshots(request, generated);
            Check(field.IsAvailable && field.Symbol.Kind == "DefinitionField" && field.Symbol.Name == "Code" && field.Symbol.IsReadOnly,
                "Retyped generated field remains searchable and immutable.");
            Check(!compiler.PreviewRenameSnapshots(request, generated, "NextCode").IsApplicable, "Generated field rename is blocked.");
            if (AlarmDefinition.Collections.Count != 0)
            {
                var sibling = request.TargetAtToken(source.FilePath, source.Version, text.IndexOf("item:Count", StringComparison.Ordinal) + 6);
                var references = compiler.FindReferencesSnapshots(request, sibling);
                Check(references.IsAvailable && references.Symbol.Name == "Collection:Checks.Count" && references.Symbol.IsReadOnly,
                    "Packaged record sibling binds to the generated collection field.");
                Check(!compiler.PreviewRenameSnapshots(request, sibling, "NextCount").IsApplicable, "Generated sibling rename is blocked.");
            }
            try { compiler.FindReferencesSnapshots(request, alarm, new CancellationToken(true)); throw new Exception("Pre-cancelled search ran."); }
            catch (OperationCanceledException) { }
            Check(compiler.FindReferencesSnapshots(request, alarm).IsAvailable, "Packaged compiler recovers after cancellation.");
        }
    }
}
