// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Definition;

namespace Alarmlist.VisualStudio.Editor
{
    // A view-owned detached projection, never a parser or semantic symbol index.
    internal sealed class ExpressionClassificationData
    {
        private readonly AnalysisClassificationSpan[] _spans;
        private ExpressionClassificationData(IEnumerable<AnalysisClassificationSpan> spans) { _spans = spans.ToArray(); }
        internal static ExpressionClassificationData Create(AnalysisClassificationTarget target, AnalysisClassificationResult result)
        {
            if (result == null || !result.IsAvailable || !result.IsLexicalOnly || result.ContractVersion != AnalysisContractReader.CurrentVersion
                || result.ToolingFingerprint != AlarmDefinition.ToolingFingerprint || string.IsNullOrEmpty(result.SnapshotId)
                || !SameTarget(target, result.Target)) return null;
            int end = 0;
            foreach (var span in result.Spans)
            {
                if (span == null || !ExpressionClassifications.IsKnown(span.Kind) || span.Start < end || span.Length <= 0
                    || span.Start > target.Text.Text.Length - span.Length || span.ExpressionStart < 0
                    || span.ExpressionLength <= 0 || span.ExpressionStart > span.Start
                    || span.ExpressionStart > target.Text.Text.Length - span.ExpressionLength
                    || span.Start + span.Length > span.ExpressionStart + span.ExpressionLength) return null;
                end = span.Start + span.Length;
            }
            return new ExpressionClassificationData(result.Spans);
        }
        internal IEnumerable<AnalysisClassificationSpan> Intersecting(int start, int length)
        {
            if (start < 0 || length <= 0 || start > int.MaxValue - length) yield break;
            int low = 0, high = _spans.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (_spans[middle].Start + _spans[middle].Length <= start) low = middle + 1;
                else high = middle;
            }
            for (int index = low; index < _spans.Length && _spans[index].Start < start + length; index++) yield return _spans[index];
        }
        private static bool SameTarget(AnalysisClassificationTarget left, AnalysisClassificationTarget right)
            => left != null && right != null && left.Id == right.Id && left.Kind == right.Kind
            && left.IsDraft == right.IsDraft && left.IsResolved == right.IsResolved
            && left.Text.Text == right.Text.Text && left.Text.FilePath == right.Text.FilePath && left.Text.Version == right.Text.Version
            && SameLocation(left.ValueLocation, right.ValueLocation) && SameValue(left.ValueTarget, right.ValueTarget);
        private static bool SameValue(AnalysisExplanationTarget left, AnalysisExplanationTarget right)
            => left == null || right == null ? left == right : left.Kind == right.Kind && left.Name == right.Name
            && left.FieldName == right.FieldName && SameLocation(left.AlarmLocation, right.AlarmLocation) && SameLocation(left.EntryLocation, right.EntryLocation);
        private static bool SameLocation(AnalysisLocation left, AnalysisLocation right)
            => left == null || right == null ? left == right : left.FilePath == right.FilePath && left.SourceVersion == right.SourceVersion
            && left.LineNumber == right.LineNumber && left.ColumnNumber == right.ColumnNumber
            && left.EndLineNumber == right.EndLineNumber && left.EndColumnNumber == right.EndColumnNumber;
    }
}
