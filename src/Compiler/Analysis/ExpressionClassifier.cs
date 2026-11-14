// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.Evaluation;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    // Presentation-only compiler request. No evaluator, analyzer, reference index,
    // solution search or persistent cache participates in classification.
    internal sealed partial class ExpressionClassifier
    {
        private readonly ExpressionClassificationRequest _request;
        private readonly CancellationToken _token;
        private readonly List<ExpressionClassificationSpan> _spans = new List<ExpressionClassificationSpan>();
        private readonly List<ExpressionClassificationIssue> _diagnostics = new List<ExpressionClassificationIssue>();
        private ExpressionClassificationTarget Target => _request.Target;
        private ExpressionClassifier(ExpressionClassificationRequest request, CancellationToken token)
        { _request = request; _token = token; }

        internal static ExpressionClassificationResult Classify(ExpressionClassificationRequest request, CancellationToken token)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            token.ThrowIfCancellationRequested();
            var classifier = new ExpressionClassifier(request, token);
            try
            {
                string unavailable;
                switch (request.Target.Kind)
                {
                    case "Almx": unavailable = classifier.AlmxDocument(); break;
                    case "Resx": unavailable = classifier.ResourceDocument(); break;
                    case "Value": unavailable = classifier.Value(); break;
                    default: unavailable = "This document kind has no Alarmlist expression language."; break;
                }
                token.ThrowIfCancellationRequested();
                return classifier.Finish(unavailable);
            }
            catch (XmlException error)
            {
                token.ThrowIfCancellationRequested();
                return classifier.Finish("Parsing prevents reliable classification: " + error.Message);
            }
        }
        private ExpressionClassificationResult Finish(string unavailable)
            => new ExpressionClassificationResult(_request, _request.Inputs.SnapshotId,
                unavailable == null ? _spans.OrderBy(span => span.Start).ThenBy(span => span.Length) : Enumerable.Empty<ExpressionClassificationSpan>(),
                unavailable == null ? _diagnostics : Enumerable.Empty<ExpressionClassificationIssue>(), unavailable == null, unavailable);

        private string AlmxDocument()
        {
            var originals = _request.Inputs.Sources.Where(source => source.FilePath == Target.Text.FilePath).ToArray();
            if (originals.Length > 1 || originals.Length == 1
                && (originals[0].Version != Target.Text.Version || originals[0].Text != Target.Text.Text))
                return "The displayed ALMX snapshot differs from its captured input.";
            var document = new Document(Target.Text, _token);
            if (document.Map.Document.Root.Name.LocalName != "Alarmlist") return "The document is not an ALMX source.";
            foreach (var value in Values(document.Input.Parse()))
            {
                _token.ThrowIfCancellationRequested();
                var mapped = document.FindValue(value.Location);
                if (mapped == null || mapped.Value != value.Raw) return "A compiler value cannot be mapped to the captured document.";
                Emit(value.Raw, value.Record, false, value.Location, mapped, true);
            }
            return null;
        }

        private string Value()
        {
            var target = Target.ValueTarget;
            if (Target.IsResolved || target.Kind == "Parameter" || target.Kind == "SourceParameter"
                || target.Kind == "WellKnown" || target.Kind == "SourceWellKnown") return null;
            bool record;
            switch (target.Kind)
            {
                case "Field":
                    if (!Definition.AlarmDefinition.Fields.Any(field => field.Name == target.Name)) return "The field is not in this compiler definition.";
                    record = false; break;
                case "ProcedureTitle": case "ProcedureEntry": case "ExcludedProcedureEntry":
                    if (!Definition.AlarmDefinition.HasTestProcedure) return "Procedures are disabled in this compiler definition.";
                    record = false; break;
                case "CollectionField": case "ExcludedCollectionField":
                    var descriptor = Definition.AlarmDefinition.Collections.FirstOrDefault(collection => collection.Name == target.Name);
                    if (descriptor == null || !descriptor.Fields.Any(field => field.Name == target.FieldName))
                        return "The collection field is not in this compiler definition.";
                    record = descriptor.Kind == "Complex"; break;
                default: return "This value kind has no Alarmlist expression language.";
            }
            var owner = _request.Inputs.Sources.Where(source => source.FilePath == target.AlarmLocation.FilePath).ToArray();
            if (owner.Length == 1)
            {
                if (owner[0].Version != target.AlarmLocation.SourceVersion) return "The value's owning source has changed.";
                if (!owner[0].Parse().Alarms.Any(alarm => SameLocation(alarm.Location, target.AlarmLocation)))
                    return "The value's owning alarm is unavailable.";
            }
            else if (owner.Length != 0 || Target.IsDraft) return "The value's owning source is unavailable or ambiguous.";
            // Unmaterialized/default values are literal. A private draft may
            // introduce source text, but gains no physical/semantic projection.
            if (!Target.IsDraft && Target.ValueLocation == null) return null;
            XmlMappedValue mapped = null;
            if (!Target.IsDraft && Target.ValueLocation != null)
            {
                var sources = _request.Inputs.Sources.Where(source => source.FilePath == Target.ValueLocation.FilePath).ToArray();
                if (sources.Length > 1) return "The defining source is ambiguous.";
                if (sources.Length == 1)
                {
                    var source = sources[0];
                    if (source.Version != Target.ValueLocation.SourceVersion) return "The defining source has changed.";
                    var document = new Document(new ReferenceTextSnapshot(source.Text, source.FilePath, source.Version), _token);
                    var candidates = Values(source.Parse()).Where(value => SameLocation(value.Location, Target.ValueLocation)
                        && value.Kind == NormalizeKind(target.Kind) && string.Equals(value.Name, target.Name,
                            value.Kind == "ProcedureTitle" || value.Kind == "ProcedureEntry" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                        && (value.Kind != "CollectionField" || value.FieldName == target.FieldName)).ToArray();
                    if (candidates.Length != 1 || candidates[0].Raw != Target.Text.Text) return "The displayed value differs from its authored source.";
                    if (target.EntryLocation != null && !SameLocation(candidates[0].EntryLocation, target.EntryLocation))
                        return "The defining entry has changed.";
                    mapped = document.FindValue(Target.ValueLocation);
                    if (mapped == null || mapped.Value != Target.Text.Text) return "The authored value has no reliable source projection.";
                }
                // Library definitions can carry original-source locations without
                // an open physical ALMX buffer. Lexical data remains useful, but
                // semantic commands must independently resolve the compiler target.
            }
            Emit(Target.Text.Text, record, false, Target.ValueLocation, mapped, false);
            return null;
        }

        private static string NormalizeKind(string kind) => kind == "ExcludedProcedureEntry" ? "ProcedureEntry"
            : kind == "ExcludedCollectionField" ? "CollectionField" : kind;
        private static bool SameLocation(SourceLocation left, SourceLocation right) => left != null && right != null
            && left.FilePath == right.FilePath && left.SourceVersion == right.SourceVersion
            && left.LineNumber == right.LineNumber && left.ColumnNumber == right.ColumnNumber && left.EndColumnNumber == right.EndColumnNumber;

        private IEnumerable<ValueSource> Values(AlarmSyntaxTree tree)
        {
            foreach (var alarm in tree.Alarms)
            {
                _token.ThrowIfCancellationRequested();
                foreach (var field in Definition.AlarmDefinition.Fields)
                {
                    string raw = alarm.GetLocalFieldValue(field.Name);
                    if (raw != null) yield return new ValueSource(raw, alarm.GetFieldLocation(field.Name), "Field", field.Name);
                }
                if (Definition.AlarmDefinition.HasTestProcedure)
                    foreach (var group in alarm.TestProcedure.Groups)
                    {
                        if (group.Title != null) yield return new ValueSource(group.Title, group.TitleLocation, "ProcedureTitle", group.Name);
                        foreach (var entry in group.Entries.OfType<TestProcedureStepSyntax>())
                            yield return new ValueSource(entry.Text, entry.Location, "ProcedureEntry", group.Name, entryLocation: entry.Location);
                    }
                foreach (var collection in alarm.LocalCollections)
                    foreach (var entry in collection.Entries.OfType<AlarmCollectionItemSyntax>())
                        foreach (var field in collection.Descriptor.Fields)
                        {
                            string raw = entry.GetFieldValue(field.Name);
                            if (raw != null) yield return new ValueSource(raw, entry.GetFieldLocation(field.Name), "CollectionField",
                                collection.Descriptor.Name, field.Name, entry.Location, collection.Descriptor.Kind == "Complex");
                        }
            }
        }

        private sealed class ValueSource
        {
            internal ValueSource(string raw, SourceLocation location, string kind, string name, string fieldName = null,
                SourceLocation entryLocation = null, bool record = false)
            { Raw = raw; Location = location; Kind = kind; Name = name; FieldName = fieldName; EntryLocation = entryLocation; Record = record; }
            internal string Raw { get; }
            internal SourceLocation Location { get; }
            internal string Kind { get; }
            internal string Name { get; }
            internal string FieldName { get; }
            internal SourceLocation EntryLocation { get; }
            internal bool Record { get; }
        }
        private sealed class Document
        {
            internal readonly AlmxTextSnapshot Input;
            internal readonly XmlValueSourceMap Map;
            private readonly Dictionary<(int Line, int Column), XObject> _nodes;
            internal Document(ReferenceTextSnapshot input, CancellationToken token)
            {
                Input = new AlmxTextSnapshot(input.Text, input.FilePath, input.Version);
                Map = XmlValueSourceMap.Parse(input.Text, token);
                _nodes = Map.Document.Root.DescendantsAndSelf().SelectMany(element => new XObject[] { element }.Concat(element.Attributes()))
                    .ToDictionary(node => (((IXmlLineInfo)node).LineNumber, ((IXmlLineInfo)node).LinePosition));
            }
            internal XmlMappedValue FindValue(SourceLocation location)
                => location != null && location.FilePath == Input.FilePath && location.SourceVersion == Input.Version
                && _nodes.TryGetValue((location.LineNumber, location.ColumnNumber), out var node) && Map.Values.TryGetValue(node, out var mapped) ? mapped : null;
        }
    }
}
