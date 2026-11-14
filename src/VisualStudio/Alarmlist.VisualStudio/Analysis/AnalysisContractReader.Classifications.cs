// Created/modified by Arkarin0 under one ore more license(s).

using System;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed partial class AnalysisContractReader
    {
        public AnalysisClassificationResult ReadClassifications(object result)
        {
            if (result == null || Value<int>(result, "ContractVersion") != CurrentVersion
                || Value<string>(result, "ToolingFingerprint") != Definition.AlarmDefinition.ToolingFingerprint
                || !Value<bool>(result, "IsLexicalOnly"))
                throw new NotSupportedException("The SDK returned incompatible expression classifications. Rebuild matching tools.");
            var target = Value<object>(result, "Target");
            var text = Value<object>(target, "Text");
            var valueTarget = Value<object>(target, "ValueTarget");
            return new AnalysisClassificationResult(CurrentVersion, Value<string>(result, "ToolingFingerprint"), new AnalysisClassificationTarget(
                Value<string>(target, "Id"), Value<string>(target, "Kind"), new AnalysisInput(Value<string>(text, "Text"), Value<string>(text, "FilePath"), Value<int>(text, "Version")),
                valueTarget == null ? null : new AnalysisExplanationTarget(Location(Value<object>(valueTarget, "AlarmLocation")), Value<string>(valueTarget, "Kind"),
                    Value<string>(valueTarget, "Name"), Value<string>(valueTarget, "FieldName"), Location(Value<object>(valueTarget, "EntryLocation"))),
                Location(Value<object>(target, "ValueLocation")), Value<bool>(target, "IsDraft"), Value<bool>(target, "IsResolved")),
                Value<string>(result, "SnapshotId"), Value<string>(result, "ProjectPath"), Value<string>(result, "Configuration"), Value<string>(result, "Platform"), Value<string>(result, "Culture"),
                List(result, "Spans", span => new AnalysisClassificationSpan(Value<string>(span, "Kind"), Value<string>(span, "SyntaxKind"),
                    Value<int>(span, "Start"), Value<int>(span, "Length"), Value<int>(span, "ExpressionStart"), Value<int>(span, "ExpressionLength"),
                    Location(Value<object>(span, "ValueLocation")), List(span, "SourceRanges", ClassificationRange))),
                List(result, "Diagnostics", issue => new AnalysisClassificationIssue(Value<string>(issue, "Message"), Value<int>(issue, "Start"), Value<int>(issue, "Length"),
                    Location(Value<object>(issue, "ValueLocation")), List(issue, "SourceRanges", ClassificationRange))),
                Value<bool>(result, "IsAvailable"), true, Value<string>(result, "Message"));
        }
        private static AnalysisClassificationRange ClassificationRange(object range)
            => new AnalysisClassificationRange(Value<int>(range, "Start"), Value<int>(range, "Length"));
    }
}
