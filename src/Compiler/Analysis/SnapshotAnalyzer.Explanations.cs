// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Alarmlist.Binding;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Evaluation;
using Alarmlist.Libraries;
using Alarmlist.Syntax;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    internal sealed partial class SnapshotAnalyzer
    {
        internal static ValueExplanationResult Explain(AlmxTextSnapshot[] inputs, CompilationContext context,
            ValueExplanationTarget target, CancellationToken token, bool allowDeferred)
        {
            token.ThrowIfCancellationRequested();
            var binder = new Binder();
            binder.AddSourceTexts(inputs.Select(input => input.CreateSourceText()));
            var binding = LibraryCompilation.Include(binder.Bind(), context);
            bool sourceTarget = target.Kind == "SourceParameter" || target.Kind == "SourceWellKnown";
            var matches = binding.SyntaxTree.Alarms.Where(alarm => SameLocation(alarm.Location, target.AlarmLocation)).ToArray();
            var selected = !sourceTarget && matches.Length == 1 ? matches[0] : null;
            var capture = new ValueExplanationCapture(alarm => ReferenceEquals(alarm, selected), token);
            var analyzer = new SnapshotAnalyzer(binding, context, allowDeferred || selected?.IsAbstract == true, capture);
            var compilation = analyzer.CompleteCompilation(binding, token);
            var analyzed = analyzer.GetAnalyzedAlarms(binding);
            var rawDiagnostics = DiagnosticPolicy.Apply(compilation.Diagnostics.Concat(analyzer._evaluation.DeferredDiagnostics), context.DiagnosticOptions);
            var diagnostics = rawDiagnostics.Select(diagnostic => new AnalysisDiagnostic(diagnostic)).ToArray();
            ValueExplanationResult Unavailable(string message) => new ValueExplanationResult(target, context, analyzer._evaluation.Culture,
                null, Array.Empty<ValueExplanationNode>(), diagnostics.Where(diagnostic =>
                    diagnostic.Location == null || diagnostic.Location.FilePath == target.AlarmLocation.FilePath), message);
            AlarmExplanationCapture recorder;
            string key;
            if (sourceTarget)
            {
                // The binder preserves input order after imported library sources.
                var sourceMatches = inputs.Where(item => item.FilePath == target.AlarmLocation.FilePath
                    && item.Version == target.AlarmLocation.SourceVersion).ToArray();
                if (sourceMatches.Length != 1) return Unavailable("The owning source has changed or is unavailable.");
                var input = sourceMatches[0];
                var file = binding.SyntaxTree.Files
                    .Skip(binding.SyntaxTree.Files.Count - inputs.Length).ElementAt(Array.IndexOf(inputs, input));
                recorder = new AlarmExplanationCapture(null, token);
                CaptureProperties(recorder, null, file, context, true, input.FilePath, allowDeferred);
                key = (target.Kind == "SourceParameter" ? "Parameter:" : "WellKnown:") + target.Name;
            }
            else
            {
                if (selected == null) return Unavailable("The alarm target has changed or is ambiguous. Select the value again.");
                var alarm = analyzed.Single(item => item.Origin.Index == analyzer._alarms[selected].Index);
                if (!alarm.IsResolved) return Unavailable("Binding prevents a reliable explanation for this alarm.");
                recorder = capture.ForAlarm(selected);
                CaptureProperties(recorder, selected, selected.SourceFile, context, true, null, allowDeferred || selected.IsAbstract);
                key = ExplanationKey(target, alarm);
            }
            token.ThrowIfCancellationRequested();
            var graph = recorder.Freeze();
            if (key == null || !graph.Roots.TryGetValue(key, out int root))
                return Unavailable("The value target is unavailable or has changed. Select the value again.");
            var byId = graph.Nodes.ToDictionary(node => node.Id);
            var reachable = new HashSet<int>();
            var pending = new Stack<int>(); pending.Push(root);
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                int id = pending.Pop();
                if (!reachable.Add(id)) continue;
                foreach (int dependency in byId[id].Inputs) pending.Push(dependency);
            }
            var nodes = graph.Nodes.Where(node => reachable.Contains(node.Id)).ToArray();
            var locations = nodes.Where(node => node.Location != null).Select(node => node.Location).ToArray();
            var relevant = rawDiagnostics.Where(diagnostic => locations.Any(location => SameLocation(location, diagnostic.Location)
                || diagnostic.RelatedLocations.Any(related => SameLocation(location, related)))
                || (SameLocation(diagnostic.Location, selected?.Location) && diagnostic.Id == DiagnosticWellKnownIds.InvalidFieldValue
                    && diagnostic.Arguments.Length > 1 && nodes.Any(node => node.Kind == "Field" && node.Location == null
                        && string.Equals(node.Name, diagnostic.Arguments[1] as string, StringComparison.OrdinalIgnoreCase))))
                .Select(diagnostic => new AnalysisDiagnostic(diagnostic));
            return new ValueExplanationResult(target, context, analyzer._evaluation.Culture, root, nodes, relevant, null);
        }

        private static void CaptureProperties(AlarmExplanationCapture recorder, AlarmSyntaxNode alarm, AlarmSyntaxTree file,
            CompilationContext context, bool resolved, string filePath, bool allowDeferred)
        {
            foreach (var binding in PropertyResolver.Resolve(alarm, file, context, resolved))
                recorder.Property(binding.Declaration.Definition.Name, binding, binding.Assignment?.Definition.Value, allowDeferred);
            foreach (var property in WellKnownProperties.Capture(context, alarm, filePath, file))
                recorder.WellKnown(property.Key, property.Value,
                    property.Key.StartsWith("AlarmlistThisFile", StringComparison.OrdinalIgnoreCase) ? alarm?.Location : null);
        }

        private static string ExplanationKey(ValueExplanationTarget target, AnalyzedAlarm alarm)
        {
            switch (target.Kind)
            {
                case "Field": return "Field:" + target.Name;
                case "Parameter": return "Parameter:" + target.Name;
                case "WellKnown":
                    var field = Definition.AlarmDefinition.Fields.FirstOrDefault(item =>
                        string.Equals("AlarmlistAlarm" + item.Name, target.Name, StringComparison.OrdinalIgnoreCase));
                    return field == null ? "WellKnown:" + target.Name : "Field:" + field.Name;
                case "ProcedureTitle": return "ProcedureTitle:" + target.Name;
                case "ProcedureEntry":
                case "ExcludedProcedureEntry":
                    var group = alarm.ProcedureSections.FirstOrDefault(item => string.Equals(item.Name, target.Name, StringComparison.OrdinalIgnoreCase));
                    if (group == null || target.EntryLocation == null) return null;
                    var entries = target.Kind == "ProcedureEntry" ? group.EffectiveEntries : group.ExcludedEntries;
                    var steps = entries.Select((entry, index) => new { entry, index })
                        .Where(item => !item.entry.IsClear && SameLocation(item.entry.Origin.Location, target.EntryLocation)).ToArray();
                    if (steps.Length != 1) return null;
                    var step = steps[0];
                    return target.Kind == "ProcedureEntry" ? "ProcedureEntry:" + group.Name + ":" + step.index.ToString(CultureInfo.InvariantCulture)
                        : AlarmExplanationCapture.ExcludedKey("Procedure", group.Name, step.entry.Origin.Alarm.Index, step.entry.Origin.Index);
                case "CollectionField":
                case "ExcludedCollectionField":
                    var collection = alarm.Collections.FirstOrDefault(item => string.Equals(item.Name, target.Name, StringComparison.OrdinalIgnoreCase));
                    if (collection == null || target.EntryLocation == null) return null;
                    var items = target.Kind == "CollectionField" ? collection.EffectiveEntries : collection.ExcludedEntries;
                    var matches = items.Select((entry, index) => new { entry, index }).Where(item => !item.entry.IsClear
                        && SameLocation(item.entry.Origin.Location, target.EntryLocation)).ToArray();
                    if (matches.Length != 1) return null;
                    var match = matches[0];
                    var fieldName = match.entry.Fields.FirstOrDefault(item => string.Equals(item.Name, target.FieldName, StringComparison.OrdinalIgnoreCase))?.Name;
                    if (fieldName == null) return null;
                    return target.Kind == "CollectionField" ? AlarmExplanationCapture.CollectionKey(collection.Name, match.index, fieldName)
                        : AlarmExplanationCapture.ExcludedKey("Collection", collection.Name, match.entry.Origin.Alarm.Index, match.entry.Origin.Index, fieldName);
                default: return null;
            }
        }

        private static bool SameLocation(SourceLocation left, SourceLocation right) => left != null && right != null
            && string.Equals(left.FilePath, right.FilePath, StringComparison.Ordinal) && left.SourceVersion == right.SourceVersion
            && left.LineNumber == right.LineNumber && left.ColumnNumber == right.ColumnNumber && left.EndColumnNumber == right.EndColumnNumber;
    }
}
