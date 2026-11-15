// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.PerformanceTests.Infrastructure
{
    internal static class ClassificationMeasurement
    {
        internal static AnalysisClassificationTarget[] Targets(AnalysisInput[] inputs, AnalysisResponse analysis)
        {
            var alarm = analysis.Snapshot.Alarms.First(item => !item.IsAbstract && item.Fields.Any(field => field.Name == "Name" && field.IsValueAvailable));
            var field = alarm.Fields.Single(item => item.Name == "Name");
            var value = new AnalysisExplanationTarget(alarm.Origin.Location, "Field", "Name");
            string path = field.ValueLocation?.FilePath ?? alarm.Origin.Location.FilePath;
            return new[]
            {
                new AnalysisClassificationTarget("document", "Almx", inputs[0]),
                new AnalysisClassificationTarget("value", "Value", new AnalysisInput(field.EffectiveExpression, path, 1), value, field.ValueLocation),
                new AnalysisClassificationTarget("draft", "Value", new AnalysisInput("draft $(AlarmlistAlarmCode) $$(Escaped) $(loc:Missing", path, 2), value, field.ValueLocation, isDraft: true)
            };
        }

        internal static void Validate(ClassificationResponse response, AnalysisClassificationTarget target)
        {
            if (response?.Availability != AnalysisAvailability.Available || response.Result?.IsAvailable != true
                || response.Result.Target.Id != target.Id || response.Result.Target.Text.Version != target.Text.Version
                || (target.IsDraft && (!response.Result.IsLexicalOnly || response.Result.Spans.Count == 0)))
                throw new InvalidOperationException("Measured classification lost its target, lexical draft or recovery diagnostics: " + response?.Message + " " + response?.Result?.Message);
        }
    }
}
