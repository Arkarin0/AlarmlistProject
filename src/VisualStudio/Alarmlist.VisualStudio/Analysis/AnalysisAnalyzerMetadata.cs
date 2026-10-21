// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisAnalyzerMetadata
    {
        public string FilePath { get; }
        public string AssemblyIdentity { get; }
        public string Failure { get; }
        public IReadOnlyList<AnalysisAnalyzerRule> Rules { get; }

        internal AnalysisAnalyzerMetadata(string path, string identity, string failure, IEnumerable<AnalysisAnalyzerRule> rules = null)
        {
            FilePath = path; AssemblyIdentity = identity; Failure = failure;
            Rules = Array.AsReadOnly((rules ?? Array.Empty<AnalysisAnalyzerRule>()).ToArray());
        }

        // Called only in the selected SDK domain. No reflected object crosses the bridge.
        internal static IReadOnlyList<AnalysisAnalyzerMetadata> Read(object loader, AnalysisContext context)
        {
            var method = loader.GetType().GetMethod("GetMetadata", Type.EmptyTypes);
            if (method == null) return context.Analyzers.Select(reference => new AnalysisAnalyzerMetadata(
                reference.FilePath, null, "This SDK does not expose analyzer declarations.")).ToArray();
            object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
            return ((IEnumerable)method.Invoke(loader, null)).Cast<object>().Select(assembly => new AnalysisAnalyzerMetadata(
                (string)Property(assembly, "FilePath"), (string)Property(assembly, "AssemblyIdentity"), (string)Property(assembly, "Failure"),
                ((IEnumerable)Property(assembly, "Rules")).Cast<object>().Select(rule => new AnalysisAnalyzerRule(
                    (string)Property(rule, "Id"), (string)Property(rule, "Title"), Property(rule, "Severity").ToString(),
                    (bool)Property(rule, "IsConfigurable"))))).ToArray();
        }
    }

    [Serializable]
    internal sealed class AnalysisAnalyzerRule
    {
        public string Id { get; }
        public string Title { get; }
        public string DefaultSeverity { get; }
        public bool IsConfigurable { get; }
        internal AnalysisAnalyzerRule(string id, string title, string defaultSeverity, bool configurable)
        { Id = id; Title = title; DefaultSeverity = defaultSeverity; IsConfigurable = configurable; }
    }
}
