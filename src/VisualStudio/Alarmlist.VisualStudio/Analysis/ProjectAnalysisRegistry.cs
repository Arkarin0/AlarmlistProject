// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    // Views can choose among owning projects for a linked file. No arbitrary
    // project is selected for loose/excluded files or shared project items.
    [Export]
    internal sealed class ProjectAnalysisRegistry
    {
        private readonly object _gate = new object();
        private readonly HashSet<ProjectAnalysisService> _projects = new HashSet<ProjectAnalysisService>();

        public void Add(ProjectAnalysisService project) { lock (_gate) _projects.Add(project); }
        public void Remove(ProjectAnalysisService project) { lock (_gate) _projects.Remove(project); }

        public IReadOnlyList<ProjectAnalysisService> GetContexts(string documentPath)
        {
            lock (_gate)
                return _projects.Where(project => project.Current.Project?.Inputs.Contains(documentPath, System.StringComparer.OrdinalIgnoreCase) == true).ToArray();
        }
    }
}
