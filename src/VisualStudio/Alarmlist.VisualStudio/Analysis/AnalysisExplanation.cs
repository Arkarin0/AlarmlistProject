// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisExplanationTarget
    {
        public AnalysisExplanationTarget(AnalysisLocation alarmLocation, string kind, string name,
            string fieldName = null, AnalysisLocation entryLocation = null)
        { AlarmLocation = alarmLocation; Kind = kind; Name = name; FieldName = fieldName; EntryLocation = entryLocation; }
        public AnalysisLocation AlarmLocation { get; }
        public string Kind { get; }
        public string Name { get; }
        public string FieldName { get; }
        public AnalysisLocation EntryLocation { get; }
    }

    [Serializable]
    internal sealed class AnalysisExplanationNode
    {
        public AnalysisExplanationNode(int id, string kind, string name, string rawValue, string value,
            string canonicalValue, string state, string type, string scope, string definingAlarm,
            AnalysisLocation location, AnalysisLibraryIdentity library, IEnumerable<int> inputs,
            string requestedCulture, string supplyingCulture)
        {
            Id = id; Kind = kind; Name = name; RawValue = rawValue; Value = value; CanonicalValue = canonicalValue;
            State = state; Type = type; Scope = scope; DefiningAlarm = definingAlarm; Location = location; Library = library;
            Inputs = Array.AsReadOnly(inputs.ToArray()); RequestedCulture = requestedCulture; SupplyingCulture = supplyingCulture;
        }
        public int Id { get; }
        public string Kind { get; }
        public string Name { get; }
        public string RawValue { get; }
        public string Value { get; }
        public string CanonicalValue { get; }
        public string State { get; }
        public string Type { get; }
        public string Scope { get; }
        public string DefiningAlarm { get; }
        public AnalysisLocation Location { get; }
        public AnalysisLibraryIdentity Library { get; }
        public IReadOnlyList<int> Inputs { get; }
        public string RequestedCulture { get; }
        public string SupplyingCulture { get; }
    }

    [Serializable]
    internal sealed class AnalysisExplanation
    {
        public AnalysisExplanation(int contractVersion, string toolingFingerprint, AnalysisExplanationTarget target,
            string projectPath, string configuration, string platform, string culture, int? rootId,
            IEnumerable<AnalysisExplanationNode> nodes, IEnumerable<AnalysisDiagnostic> diagnostics, string message, bool isAvailable)
        {
            ContractVersion = contractVersion; ToolingFingerprint = toolingFingerprint; Target = target;
            ProjectPath = projectPath; Configuration = configuration; Platform = platform; Culture = culture; RootId = rootId;
            Nodes = Array.AsReadOnly(nodes.ToArray()); Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
            Message = message; IsAvailable = isAvailable;
        }
        public int ContractVersion { get; }
        public string ToolingFingerprint { get; }
        public AnalysisExplanationTarget Target { get; }
        public string ProjectPath { get; }
        public string Configuration { get; }
        public string Platform { get; }
        public string Culture { get; }
        public int? RootId { get; }
        public IReadOnlyList<AnalysisExplanationNode> Nodes { get; }
        public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; }
        public string Message { get; }
        public bool IsAvailable { get; }
    }

}
