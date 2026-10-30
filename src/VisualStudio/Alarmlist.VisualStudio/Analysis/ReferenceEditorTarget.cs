// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    internal static class ReferenceEditorTarget
    {
        // ALMX inputs use open buffers; auxiliary inputs retain saved-input
        // rules. Compare the entire text before adopting a captured version for
        // a caret offset. This does not identify a symbol or parse XML.
        internal static AnalysisReferenceTarget Resolve(AnalysisReferenceRequest request, AnalysisInput editor, int offset)
        {
            var documents = request.Sources.Concat(request.ProjectDocuments)
                .Concat(request.Context.Resources.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)))
                .Concat(request.Context.Lookups.Select(input => new AnalysisInput(input.Text, input.FilePath, input.Version)))
                .Where(input => string.Equals(input.FilePath, editor.FilePath, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (documents.Length == 0) return null;
            if (documents.Any(input => input.Text != editor.Text || input.Version != documents[0].Version))
                throw new InvalidOperationException("The XML buffer differs from the captured inputs. Save resources, lookup files and project declarations before finding references or renaming them.");
            if (request.Sources.Any(input => string.Equals(input.FilePath, editor.FilePath, StringComparison.OrdinalIgnoreCase) && input.Version != editor.Version))
                throw new InvalidOperationException("The ALMX buffer version changed. Invoke the command again against its current snapshot.");
            if (offset < 0 || offset > editor.Text.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            var captured = documents[0];
            return new AnalysisReferenceTarget("Token", new AnalysisLocation(captured.FilePath, 1, 1, 1, 1, captured.Version), offset: offset);
        }
    }
}
