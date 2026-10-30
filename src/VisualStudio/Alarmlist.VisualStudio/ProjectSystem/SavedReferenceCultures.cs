// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.Build.Exceptions;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed class SavedReferenceCultures
    {
        private SavedReferenceCultures(IEnumerable<CapturedReferenceEvaluation> captured, IEnumerable<string> coverage)
        {
            Captured = Array.AsReadOnly(captured.ToArray());
            Evaluations = Array.AsReadOnly(Captured.Select(item => item.Evaluation).ToArray()); Coverage = Array.AsReadOnly(coverage.ToArray());
        }

        internal IReadOnlyList<CapturedReferenceEvaluation> Captured { get; }
        internal IReadOnlyList<SavedProjectEvaluation> Evaluations { get; }
        internal IReadOnlyList<string> Coverage { get; }

        // The solution coordinator supplies the project's active configuration and
        // platform. Culture changes reevaluate MSBuild membership and metadata;
        // swapping only the compiler culture would miss conditional inputs.
        internal static SavedReferenceCultures Capture(string projectPath, IDictionary<string, string> activeGlobals,
            CancellationToken token, ReferenceInputOverlay overlay = null)
        {
            token.ThrowIfCancellationRequested();
            var globals = new Dictionary<string, string>(activeGlobals ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            var selected = SavedProjectEvaluation.Capture(projectPath, globals, token, projectOverlays: overlay?.ProjectDocuments);
            var evaluations = new List<CapturedReferenceEvaluation>();
            var coverage = new List<string>();
            var cultures = selected.Project.BuildCultures.Split(';').Select(value => value.Trim()).Where(value => value.Length != 0)
                .Concat(new[] { selected.Project.Context.Culture }).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string culture in cultures)
            {
                token.ThrowIfCancellationRequested();
                var cultureGlobals = new Dictionary<string, string>(globals, StringComparer.OrdinalIgnoreCase) { ["AlarmlistCulture"] = culture };
                try
                {
                    bool isSelected = string.Equals(culture, selected.Project.Context.Culture, StringComparison.OrdinalIgnoreCase);
                    var discovered = isSelected ? selected : SavedProjectEvaluation.Capture(projectPath, cultureGlobals, token, projectOverlays: overlay?.ProjectDocuments);
                    evaluations.Add(CapturedReferenceEvaluation.Capture(discovered, isSelected ? globals : cultureGlobals, token, overlay));
                }
                catch (Exception exception) when (exception is InvalidProjectFileException || exception is IOException
                    || exception is UnauthorizedAccessException || exception is XmlException || exception is ArgumentException || exception is InvalidOperationException)
                {
                    coverage.Add("Unavailable culture: " + selected.Project.ProjectPath + " [" + selected.Project.Configuration + ", " + culture + "]: " + exception.Message);
                }
            }
            token.ThrowIfCancellationRequested();
            return new SavedReferenceCultures(evaluations, coverage);
        }
    }
}
