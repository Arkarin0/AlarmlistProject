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
        public const int CurrentContractVersion = 4;
        public string SchemaFingerprint => Definition.AlarmDefinition.SchemaFingerprint;
        public IReadOnlyList<Definition.AlarmFieldDescriptor> FieldDefinitions => Definition.AlarmDefinition.Fields;
        public IReadOnlyList<AnalyzedAlarm> Alarms { get; }
        public IReadOnlyList<AnalyzedLibrary> Libraries { get; }
        public string ProjectPath { get; }
        public string Culture { get; }
        public AnalyzerConfiguration Options { get; }

        internal AlarmAnalyzerContext(IEnumerable<AnalyzedAlarm> alarms, CompilationContext context, string culture,
            IEnumerable<AnalyzedLibrary> libraries = null)
        {
            Alarms = Array.AsReadOnly(alarms.ToArray());
            Libraries = Array.AsReadOnly((libraries ?? Enumerable.Empty<AnalyzedLibrary>()).ToArray());
            ProjectPath = context.ProjectPath;
            Culture = culture;
            Options = context.DiagnosticOptions.Configuration;
        }
    }
}
