// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Linq;

namespace Alarmlist.VisualStudio.Analysis
{
    /// <summary>Resolves a captured location without disk access or compiler objects.</summary>
    internal sealed class AnalysisNavigationTarget
    {
        private AnalysisNavigationTarget(AnalysisLocation location, string text, string caption, bool readOnly, bool xml = true)
        { Location = location; Text = text; Caption = caption; IsReadOnlyDefinition = readOnly; IsXml = xml; }

        public AnalysisLocation Location { get; }
        public string Text { get; }
        public string Caption { get; }
        public bool IsReadOnlyDefinition { get; }
        public bool IsXml { get; }

        internal static AnalysisNavigationTarget ReadOnlyDocument(string text, string caption, bool xml) =>
            new AnalysisNavigationTarget(new AnalysisLocation("package-content:" + caption, 1, 1, 1, 1, 0), text, caption, true, xml);

        public static AnalysisNavigationTarget Resolve(ProjectAnalysisState state, AnalysisLocation location)
        {
            if (state == null || location == null) return null;
            string path = location.FilePath;
            string text = state.Inputs.FirstOrDefault(input => SamePath(input.Input.FilePath, path))?.Input.Text
                ?? state.Resources.FirstOrDefault(input => SamePath(input.FilePath, path))?.Text
                ?? state.Lookups.FirstOrDefault(input => SamePath(input.FilePath, path))?.Text;
            if (text != null) return new AnalysisNavigationTarget(location, text, null, false);

            foreach (AnalysisLibrary library in state.Response?.Snapshot?.Libraries ?? Array.Empty<AnalysisLibrary>())
            {
                AnalysisLibrarySource source = library.Sources.FirstOrDefault(item => item.FilePath == path);
                AnalysisLibraryDocument document = library.Documents.FirstOrDefault(item => item.FilePath == path);
                if (source == null && document == null) continue;
                string name = source?.Name ?? document.Name;
                // A valid combined graph rejects conflicting identities. Multiple
                // physical projects with the same identity still need one unique
                // local destination; never choose an arbitrary source.
                var projects = state.Libraries?.Projects.Where(project => string.Equals(project.PackageId,
                    library.Identity.Id, StringComparison.OrdinalIgnoreCase)).ToArray() ?? Array.Empty<CapturedLibraryProject>();
                if (projects.Length != 0)
                {
                    var candidates = projects.SelectMany(project => project.PortableNames.Where(pair => pair.Value == name)
                        .Select(pair => new { Project = project, Path = pair.Key })).ToArray();
                    if (candidates.Length != 1) return null;
                    var candidate = candidates[0];
                    text = candidate.Project.Sources.FirstOrDefault(input => SamePath(input.FilePath, candidate.Path))?.Text
                        ?? candidate.Project.Context.Resources.FirstOrDefault(input => SamePath(input.FilePath, candidate.Path))?.Text
                        ?? candidate.Project.Context.Lookups.FirstOrDefault(input => SamePath(input.FilePath, candidate.Path))?.Text;
                    if (text == null) return null;
                    return new AnalysisNavigationTarget(Relocate(location, candidate.Path), text, null, false);
                }

                var origins = source?.Origins ?? document.Origins;
                var matches = origins.Where(origin => SamePosition(origin.OriginalLocation, location))
                    .Select(origin => origin.DefinitionLocation).ToArray();
                if (matches.Length == 0) return null;
                AnalysisLocation target = matches[0];
                if (matches.Any(match => !SamePosition(match, target))) return null;
                return new AnalysisNavigationTarget(target, source?.Definition ?? document.Definition,
                    library.Identity.Id + " " + library.Identity.Version + " — " + name, true);
            }
            return null;
        }

        private static bool SamePath(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        private static bool SamePosition(AnalysisLocation left, AnalysisLocation right) => left != null && right != null
            && left.FilePath == right.FilePath && left.LineNumber == right.LineNumber && left.ColumnNumber == right.ColumnNumber;
        private static AnalysisLocation Relocate(AnalysisLocation location, string path) => new AnalysisLocation(path,
            location.LineNumber, location.ColumnNumber, location.EndLineNumber, location.EndColumnNumber, 0);
    }
}
