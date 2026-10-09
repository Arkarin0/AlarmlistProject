// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Analysis;
using Alarmlist.Analyzers;
using Alarmlist.Diagnostics;

namespace Alarmlist.SampleAnalyzer
{
    /// <summary>Example project rule: concrete alarms use a configurable alarm-code prefix.</summary>
    public sealed class CodePrefixAnalyzer : IAlarmAnalyzer
    {
        private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor("SAMPLE001", "Alarm code prefix",
            "Alarm '{0}' has code '{1}'; expected prefix '{2}'.", DiagnosticSeverity.Warning);
        public int ContractVersion => AlarmAnalyzerContext.CurrentContractVersion;
        public IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; } = Array.AsReadOnly(new[] { s_rule });

        public IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken)
        {
            foreach (AnalyzedAlarm alarm in context.Alarms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (alarm.IsAbstract) continue;
                IReadOnlyDictionary<string, string> options = context.Options.GetOptionsForSourcePath(alarm.Origin.Location?.FilePath);
                string prefix = options.TryGetValue("alarmlist_sample.code_prefix", out string configured) ? configured : "AL-";
                AnalyzedField code = alarm.Fields.Single(field => field.Name == "Code");
                if (code.IsValueAvailable && !(code.EffectiveValue ?? string.Empty).StartsWith(prefix, StringComparison.Ordinal))
                    yield return new Diagnostic(s_rule, alarm.Origin.FullyQualifiedName, code.EffectiveValue, prefix)
                        .WithLocation(alarm.Origin.Location, code.IsInherited ? new[] { code.ValueLocation } : null);
            }
        }
    }
}
