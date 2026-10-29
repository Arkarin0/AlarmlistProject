// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Compiler;
using Alarmlist.Text;

namespace Alarmlist.Analysis
{
    /// <summary>
    /// Identifies a value in captured inputs. Alarm and entry locations include snapshot versions;
    /// collection/procedure entries are identified by their defining source, never an effective index.
    /// Hosts must also revalidate captured dependencies and context before using the result.
    /// </summary>
    public sealed class ValueExplanationTarget
    {
        public ValueExplanationTarget(SourceLocation alarmLocation, string kind, string name,
            string fieldName = null, SourceLocation entryLocation = null)
        {
            AlarmLocation = alarmLocation ?? throw new ArgumentNullException(nameof(alarmLocation));
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            FieldName = fieldName; EntryLocation = entryLocation;
        }
        /// <summary>For SourceParameter/SourceWellKnown, a location in the captured owning source.</summary>
        public SourceLocation AlarmLocation { get; }
        /// <summary>Field, Parameter, WellKnown, ProcedureTitle, ProcedureEntry, CollectionField,
        /// ExcludedProcedureEntry, ExcludedCollectionField, SourceParameter or SourceWellKnown.</summary>
        public string Kind { get; }
        public string Name { get; }
        public string FieldName { get; }
        public SourceLocation EntryLocation { get; }
    }

    /// <summary>Immutable explanation for one target and context. Unavailable results contain an explicit reason.</summary>
    public sealed class ValueExplanationResult
    {
        internal ValueExplanationResult(ValueExplanationTarget target, CompilationContext context, string culture,
            int? rootId, IEnumerable<ValueExplanationNode> nodes, IEnumerable<AnalysisDiagnostic> diagnostics, string message)
        {
            Target = target; ProjectPath = context.ProjectPath; Configuration = context.Configuration;
            Platform = context.Platform; Culture = culture; RootId = rootId;
            Nodes = Array.AsReadOnly(nodes.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray()); Message = message;
        }
        public int ContractVersion => AnalysisResult.CurrentContractVersion;
        public string ToolingFingerprint => Definition.AlarmDefinition.ToolingFingerprint;
        public ValueExplanationTarget Target { get; }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Culture { get; }
        public int? RootId { get; }
        public IReadOnlyList<ValueExplanationNode> Nodes { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public string Message { get; }
        public bool IsAvailable => RootId.HasValue;
    }
}
