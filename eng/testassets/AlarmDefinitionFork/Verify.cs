// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
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
            var analysis = new AlarmCompiler().AnalyzeSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Code>+008</Code></Alarm></Alarmlist>", "fixture.almx", 1) });
            var code = analysis.Alarms.Single().Fields.Single(field => field.Name == "Code");
            Check(analysis.Success && code.TypedValue is long number && number == 8 && code.CanonicalValue == "8", "Packaged analysis typed value.");
            Console.WriteLine("Packaged fork values verified: " + args[0]);
        }
    }
}
