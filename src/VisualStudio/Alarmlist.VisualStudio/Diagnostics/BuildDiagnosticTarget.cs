// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.Diagnostics
{
    internal sealed class BuildDiagnosticTarget
    {
        private readonly string _path, _id, _message;
        private readonly int _line, _column;
        public BuildDiagnosticTarget(string path, string id, string message, int line, int column)
        { _path = path; _id = id; _message = message; _line = line; _column = column; }

        internal bool Matches(LiveDiagnostic diagnostic) => diagnostic.IsCompilerDiagnostic && diagnostic.Id == _id
            && diagnostic.Message == _message && diagnostic.Location != null
            && string.Equals(diagnostic.Location.FilePath, _path, StringComparison.OrdinalIgnoreCase)
            && diagnostic.Location.LineNumber == _line + 1 && diagnostic.Location.ColumnNumber == _column + 1;

        public async Task<bool> TryNavigateAsync(ProjectAnalysisRegistry registry, IProjectAnalysisInputSource inputs,
            Action<AnalysisLocation, string> navigate, CancellationToken token)
        {
            // Build rows do not contain a captured source version. Only navigate
            // when the same diagnostic still exists in current validated inputs.
            foreach (ProjectAnalysisService project in registry.GetContexts(_path))
            {
                ProjectAnalysisState state = project.Current;
                LiveDiagnostic diagnostic = LiveDiagnostic.Create(state).FirstOrDefault(Matches);
                if (diagnostic == null || !diagnostic.TryGetSourceText(diagnostic.Location, out string text)) continue;
                if (!await ProjectAnalysisValidation.InputsMatchAsync(inputs, state, token)
                    || !ReferenceEquals(project.Current, state) || !registry.GetContexts(_path).Contains(project)) continue;
                token.ThrowIfCancellationRequested();
                navigate(diagnostic.Location, text);
                return true;
            }
            return false;
        }
    }
}
