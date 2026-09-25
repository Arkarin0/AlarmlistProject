// Created/modified by Arkarin0 under one ore more license(s).

using System;
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

        public event EventHandler Changed;

        public void Add(ProjectAnalysisService project)
        {
            lock (_gate)
            {
                if (!_projects.Add(project)) return;
                project.Changed += OnChanged;
            }
            OnChanged(this, EventArgs.Empty);
        }
        public void Remove(ProjectAnalysisService project)
        {
            lock (_gate)
            {
                if (!_projects.Remove(project)) return;
                project.Changed -= OnChanged;
            }
            OnChanged(this, EventArgs.Empty);
        }
        private void OnChanged(object sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

        public IReadOnlyList<ProjectAnalysisService> GetContexts(string documentPath)
        {
            lock (_gate)
                return _projects.Where(project => project.Current.Project?.Inputs.Contains(documentPath, System.StringComparer.OrdinalIgnoreCase) == true).ToArray();
        }
    }
}
