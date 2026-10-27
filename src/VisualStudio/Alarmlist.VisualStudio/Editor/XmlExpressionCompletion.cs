// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;
using Alarmlist.VisualStudio.UI;

namespace Alarmlist.VisualStudio.Editor
{
    internal enum XmlSemanticKind { None, Expression, Reference, ParameterName }

    internal sealed class XmlExpressionRequest
    {
        public XmlCompletionEdit Edit { get; }
        public XmlValueText Value { get; }
        public int Caret { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
        public bool Explicit { get; }
        public XmlSemanticKind Kind { get; }
        public string SourceText { get; }
        public XmlExpressionRequest(XmlCompletionEdit edit, XmlValueText value, int caret, int selectionStart, int selectionLength,
            bool explicitInvocation, XmlSemanticKind kind, string sourceText)
        { Edit = edit; Value = value; Caret = caret; SelectionStart = selectionStart; SelectionLength = selectionLength; Explicit = explicitInvocation; Kind = kind; SourceText = sourceText; }
    }

    internal sealed class XmlSemanticResult
    {
        public int Start { get; }
        public int Length { get; }
        public IReadOnlyList<XmlCompletionCandidate> Candidates { get; }
        public XmlSemanticResult(int start, int length, IEnumerable<XmlCompletionCandidate> candidates)
        { Start = start; Length = length; Candidates = Array.AsReadOnly(candidates.ToArray()); }
    }

    internal static class XmlExpressionCompletion
    {
        public static XmlExpressionRequest Request(string text, int caret, int selectionStart, int selectionLength, bool explicitInvocation)
        {
            XmlCompletionEdit edit = XmlCompletionContext.Find(text, caret);
            if (edit == null || edit.Ancestors.Any(node => node.Name.Contains(":")
                || node.Attributes.TryGetValue("xmlns", out string ns) && ns.Length != 0)) return null;
            string[] path = edit.Ancestors.Select(node => node.Name).ToArray();
            if (path.Length == 0 || path[0] != "Alarmlist") return null;
            XmlSemanticKind kind = XmlSemanticKind.None;
            if (edit.Kind == XmlCompletionKind.Text && path.SequenceEqual(new[] { "Alarmlist", "Alarm", "ReferenceName" })) kind = XmlSemanticKind.Reference;
            else if (edit.Kind == XmlCompletionKind.AttributeValue && edit.Opening?.Name == "Property" && edit.AttributeName == "Name"
                && (path.SequenceEqual(new[] { "Alarmlist", "Properties" }) || path.SequenceEqual(new[] { "Alarmlist", "Alarm", "Properties" }))) kind = XmlSemanticKind.ParameterName;
            else if (edit.Kind == XmlCompletionKind.Text && IsExpressionField(path)) kind = XmlSemanticKind.Expression;
            if (kind == XmlSemanticKind.None) return null;
            XmlValueText value = XmlValueText.Decode(text.Substring(edit.Start, edit.Length));
            if (value == null || !value.TryFromRaw(caret - edit.Start, out int decodedCaret)) return null;
            if (selectionLength == 0) selectionStart = caret;
            if (!value.TryFromRaw(selectionStart - edit.Start, out int decodedStart)
                || !value.TryFromRaw(selectionStart + selectionLength - edit.Start, out int decodedEnd)) return null;
            if (kind == XmlSemanticKind.Expression && !explicitInvocation
                && ExpressionCompletionContext.FindStart(value.Text, decodedCaret, out _) < 0
                && ExpressionCompletionContext.FindArgument(value.Text, decodedStart, decodedEnd - decodedStart) == null) return null;
            return new XmlExpressionRequest(edit, value, decodedCaret, decodedStart, decodedEnd - decodedStart, explicitInvocation, kind, text);
        }

        private static bool IsExpressionField(string[] path)
        {
            if (path.Length < 3 || path[1] != "Alarm") return false;
            if (path.Length == 3 && AlarmDefinition.Fields.Any(field => field.Name == path[2])) return true;
            AlarmCollectionDescriptor collection = AlarmDefinition.Collections.FirstOrDefault(item => item.Name == path[2]);
            if (collection?.Kind == "Simple" && path.Length == 4 && path[3] == "Item") return true;
            if (collection?.Kind == "Complex" && path.Length == 5 && path[3] == "Item" && collection.Fields.Any(field => field.Name == path[4])) return true;
            return AlarmDefinition.HasTestProcedure && path.Length == 5 && path[2] == "TestProcedure" && path[3] == "Group"
                && new[] { "Title", "Step", "Hint", "Note", "Warning" }.Contains(path[4]);
        }

        public static XmlSemanticResult Complete(XmlExpressionRequest request, AnalysisSnapshot snapshot, AnalysisAlarm alarm, AnalysisSource source)
        {
            if (request == null || snapshot == null) return null;
            XmlCompletionEdit edit = request.Edit;
            string value = request.Value.Text;
            if (request.Kind == XmlSemanticKind.Reference)
            {
                string query = value.Substring(0, request.Caret);
                return Result(0, value.Length, ExpressionCompletionCandidates.ReferenceAlarms(snapshot, alarm)
                    .Select(item => new ReferenceChoice(item)).Where(item => item.Identifier.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(item => new XmlCompletionCandidate(XmlValueText.Escape(item.Identifier, edit.Quote), item.Name)));
            }
            IEnumerable<AnalysisProperty> properties = edit.Ancestors.Any(node => node.Name == "Alarm")
                ? alarm?.IsResolved == true ? alarm.Properties : null : source?.Properties;
            if (properties == null) return null;
            if (request.Kind == XmlSemanticKind.ParameterName)
            {
                string query = value.Substring(0, request.Caret);
                return Result(0, value.Length, properties.Where(property => !property.IsWellKnown && property.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    .Select(property => new XmlCompletionCandidate(XmlValueText.Escape(property.Name, edit.Quote), "Declared parameter")));
            }
            PropertyCompletion[] suggestions = ExpressionCompletionCandidates.Create(snapshot, alarm, properties, currentValues: true).ToArray();
            string[] path = edit.Ancestors.Select(node => node.Name).ToArray();
            if (path.Length == 5 && path[1] == "Alarm" && path[3] == "Item")
                suggestions = suggestions.Concat(ExpressionCompletionCandidates.RecordSiblings(
                    snapshot.CollectionDefinitions.FirstOrDefault(item => item.Name == path[2]), path[4])).ToArray();
            int start = ExpressionCompletionContext.FindStart(value, request.Caret, out bool argument);
            string prefix = start < 0 ? null : value.Substring(start + 2, request.Caret - start - 2);
            if (prefix != null && !ExpressionCompletionContext.IsPrefix(prefix)) prefix = null;
            ArgumentEdit slot = prefix == null ? ExpressionCompletionContext.FindArgument(value, request.SelectionStart, request.SelectionLength) : null;
            bool bound = slot != null && snapshot.ArgumentBindings.Any(binding => binding.IsAvailable
                && binding.TemplateKey == slot.TemplateKey && binding.ArgumentName == slot.ArgumentName);
            int end;
            PropertyCompletion[] matches;
            if (bound)
            {
                if (!request.Explicit && slot.Query.Length == 0) return null;
                start = slot.Start; end = slot.End;
                matches = suggestions.Where(item => item.IsBoundTo(slot.TemplateKey, slot.ArgumentName) && item.MatchesListSearch(slot.Query)).ToArray();
            }
            else
            {
                if (prefix == null)
                {
                    if (!request.Explicit) return null;
                    start = request.SelectionStart; end = start + request.SelectionLength; prefix = "";
                }
                else
                {
                    end = request.Caret;
                    while (end < value.Length && ExpressionCompletionContext.IsTokenCharacter(value[end])) end++;
                    if (end < value.Length && value[end] == ')') end++;
                }
                matches = ExpressionCompletionCandidates.FindMatches(suggestions, prefix);
                if (argument) matches = matches.Where(item => !item.IsLocalization).ToArray();
            }
            return Result(start, end, matches.Select(item =>
            {
                string insertion = XmlValueText.Escape(item.Insertion, edit.Quote);
                int selectionOffset = item.ArgumentOffset < 0 ? -1 : XmlValueText.Escape(item.Insertion.Substring(0, item.ArgumentOffset), edit.Quote).Length;
                return new XmlCompletionCandidate(insertion, item.Detail, insertion, selectionOffset,
                    selectionOffset < 0 ? 0 : XmlValueText.Escape("\"\"", edit.Quote).Length);
            }));

            XmlSemanticResult Result(int decodedStart, int decodedEnd, IEnumerable<XmlCompletionCandidate> candidates)
                => new XmlSemanticResult(edit.Start + request.Value.ToRaw(decodedStart), request.Value.ToRaw(decodedEnd) - request.Value.ToRaw(decodedStart), candidates);
        }
    }
}
