// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Analysis;
using Alarmlist.Compiler;
using Alarmlist.Configuration;

namespace Alarmlist.Analyzers
{
    /// <summary>Detached, immutable analyzer inputs; resolved fields retain raw text and declaration provenance.</summary>
    public sealed class AlarmAnalyzerContext
    {
        public const int CurrentContractVersion = 2;
        public IReadOnlyList<AnalyzedAlarm> Alarms { get; }
        public string ProjectPath { get; }
        public string Culture { get; }
        public AnalyzerConfiguration Options { get; }

        internal AlarmAnalyzerContext(IEnumerable<AnalyzedAlarm> alarms, CompilationContext context, string culture)
        {
            Alarms = Array.AsReadOnly(alarms.ToArray());
            ProjectPath = context.ProjectPath;
            Culture = culture;
            Options = context.DiagnosticOptions.Configuration;
        }
    }
}
