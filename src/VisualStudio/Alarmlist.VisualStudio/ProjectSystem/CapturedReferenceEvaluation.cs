// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.VisualStudio.Analysis;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed class CapturedReferenceEvaluation
    {
        private CapturedReferenceEvaluation(SavedProjectEvaluation evaluation, IEnumerable<AnalysisInput> documents)
        { Evaluation = evaluation; ProjectDocuments = Array.AsReadOnly(documents.ToArray()); }

        internal SavedProjectEvaluation Evaluation { get; }
        internal IReadOnlyList<AnalysisInput> ProjectDocuments { get; }

        // The first evaluation discovers imports. Replay from their captured text
        // makes declaration locations/membership authoritative for precisely those
        // bytes, rather than pairing a prior evaluation with later disk contents.
        // This work is opt-in; ordinary project analysis does not retain this XML.
        internal static CapturedReferenceEvaluation Capture(SavedProjectEvaluation discovered,
            IDictionary<string, string> globals, CancellationToken token, ReferenceInputOverlay overlay = null)
        {
            if (discovered == null) throw new ArgumentNullException(nameof(discovered));
            var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in discovered.Imports)
            {
                token.ThrowIfCancellationRequested();
                texts.Add(path, overlay == null ? ProjectAnalysisInputSource.ReadXmlText(path) : overlay.Read(path));
            }
            var replay = new Dictionary<string, string>(texts, StringComparer.OrdinalIgnoreCase);
            foreach (var proposed in overlay?.ProjectDocuments ?? new Dictionary<string, string>()) replay[proposed.Key] = proposed.Value;
            var evaluation = SavedProjectEvaluation.Capture(discovered.Project.ProjectPath, globals, token, projectOverlays: replay, captureReferences: true);
            if (!new HashSet<string>(texts.Keys, StringComparer.OrdinalIgnoreCase).SetEquals(evaluation.Imports))
                throw new IOException("Project imports changed during reference capture. Refresh the request: " + discovered.Project.ProjectPath);
            foreach (var document in texts)
            {
                token.ThrowIfCancellationRequested();
                if (overlay?.ProjectDocuments.ContainsKey(document.Key) == true) continue;
                if (document.Value != ProjectAnalysisInputSource.ReadXmlText(document.Key))
                    throw new IOException("Project/import text changed during reference capture: " + document.Key);
            }
            token.ThrowIfCancellationRequested();
            return new CapturedReferenceEvaluation(evaluation, evaluation.Imports.Select(path => new AnalysisInput(texts[path], path, 0)));
        }
    }
}
