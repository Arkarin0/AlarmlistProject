// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alarmlist.VisualStudio.Analysis
{
    // Views can choose among owning projects for a linked file. No arbitrary
    // project is selected for loose/excluded files or shared project items.
    [Export]
    internal sealed class ProjectAnalysisRegistry
    {
        private readonly object _gate = new object();
        private readonly HashSet<ProjectAnalysisService> _projects = new HashSet<ProjectAnalysisService>();
        private readonly Dictionary<ProjectAnalysisService, Func<AnalysisProject, string, CancellationToken, Task>> _cultureWriters
            = new Dictionary<ProjectAnalysisService, Func<AnalysisProject, string, CancellationToken, Task>>();

        public event EventHandler Changed;

        public void Add(ProjectAnalysisService project, Func<AnalysisProject, string, CancellationToken, Task> cultureWriter = null)
        {
            lock (_gate)
            {
                if (!_projects.Add(project)) return;
                if (cultureWriter != null) _cultureWriters.Add(project, cultureWriter);
                project.Changed += OnChanged;
            }
            OnChanged(this, EventArgs.Empty);
        }
        public void Remove(ProjectAnalysisService project)
        {
            lock (_gate)
            {
                if (!_projects.Remove(project)) return;
                _cultureWriters.Remove(project);
                project.Changed -= OnChanged;
            }
            OnChanged(this, EventArgs.Empty);
        }
        private void OnChanged(object sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);

        public Task SetPreviewCultureAsync(AnalysisProject project, string culture, CancellationToken token)
        {
            Func<AnalysisProject, string, CancellationToken, Task> writer;
            lock (_gate)
            {
                ProjectAnalysisService owner = _projects.FirstOrDefault(service => ReferenceEquals(service.Current.Project, project));
                if (owner == null || !_cultureWriters.TryGetValue(owner, out writer))
                    throw new InvalidOperationException("The project context changed. Choose the preview language again.");
            }
            return writer(project, culture, token);
        }

        public IReadOnlyList<ProjectAnalysisService> GetContexts(string documentPath)
        {
            lock (_gate)
                return _projects.Where(project => project.Current.Project?.Inputs.Contains(documentPath, System.StringComparer.OrdinalIgnoreCase) == true).ToArray();
        }
    }
}
