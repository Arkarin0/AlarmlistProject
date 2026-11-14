// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.Syntax;
using Alarmlist.Text;
using Alarmlist.Evaluation;

namespace Alarmlist.Analysis
{
    internal sealed partial class ReferenceCapture
    {
        private void Probe(Symbol symbol, SourceLocation location, string attribute, int start, int length, bool syntaxFallback = false)
        {
            if (location == null || !_sources.TryGetValue(location.FilePath, out var source) || location.SourceVersion != source.Input.Version) return;
            var node = source.Find(location);
            XObject value = attribute == null ? node : (node as XElement ?? (node as XAttribute)?.Parent)?.Attribute(attribute);
            if (value == null || !source.Map.Values.TryGetValue(value, out var mapped)) return;
            foreach (var range in mapped.SourceRanges(start, length, _token)) _tokenProbes.Add((symbol, location, range.Start, range.Length, syntaxFallback));
        }

        private void ProbeExpressionSyntax(Symbol selected, ExpressionReferenceSyntax reference, SourceLocation location)
        {
            // Only the reference's own punctuation/keyword belongs to its bound
            // result. Argument literals and nested references keep their own
            // ownership; no containing-expression text match is used.
            var arguments = new HashSet<int>(reference.ArgumentList.SelectMany(argument => argument.Tokens).Select(token => token.Start));
            foreach (var token in reference.Tokens)
            {
                _token.ThrowIfCancellationRequested();
                if (arguments.Contains(token.Start) || token.Length == 0 || token.Kind == ExpressionSyntaxKind.IdentifierToken
                    || token.Kind == ExpressionSyntaxKind.WhitespaceToken || token.Kind == ExpressionSyntaxKind.StringLiteralToken) continue;
                Probe(selected, location, null, token.Start, token.Length, syntaxFallback: true);
            }
        }

        // Binding failed for this consumer. Recognize authored semantic tokens
        // without accepting a binder's partial inheritance choice as a reference.
        private void ObserveUnavailableExpressions(AlarmSyntaxNode alarm)
        {
            void Value(string raw, SourceLocation location, bool record = false)
                => ObserveExpression(raw, location, alarm, null, record: record, bindingAvailable: false);
            foreach (var field in Definition.AlarmDefinition.Fields)
            {
                ObserveDefinitionUse(field.Name, alarm.GetFieldLocation(field.Name), alarm);
                Value(alarm.GetLocalFieldValue(field.Name), alarm.GetFieldLocation(field.Name));
            }
            foreach (var group in alarm.TestProcedure.Groups)
            {
                Value(group.Title, group.TitleLocation);
                foreach (var entry in group.Entries.OfType<TestProcedureStepSyntax>()) Value(entry.Text, entry.Location);
            }
            foreach (var descriptor in Definition.AlarmDefinition.Collections)
                foreach (var item in alarm.GetLocalCollection(descriptor.Name)?.Entries.OfType<AlarmCollectionItemSyntax>() ?? Enumerable.Empty<AlarmCollectionItemSyntax>())
                    foreach (var field in descriptor.Fields) Value(item.GetFieldValue(field.Name), item.GetFieldLocation(field.Name), descriptor.Kind == "Complex");
        }
    }
}
