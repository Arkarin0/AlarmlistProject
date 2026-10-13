// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    [Serializable]
    internal sealed class AnalysisProcedureCreation
    {
        public IReadOnlyList<AnalysisGroupDefault> Groups { get; }
        public string Error { get; }
        public AnalysisProcedureCreation(IEnumerable<AnalysisGroupDefault> groups, string error)
        { Groups = Array.AsReadOnly(groups.ToArray()); Error = error; }
    }

    [Serializable]
    internal sealed class AnalysisGroupDefault
    {
        public string Name { get; }
        public string Title { get; }
        public AnalysisGroupDefault(string name, string title) { Name = name; Title = title; }
    }
}
