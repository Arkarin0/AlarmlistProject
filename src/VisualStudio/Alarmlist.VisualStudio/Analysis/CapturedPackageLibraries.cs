// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class CapturedPackageLibraries
    {
        private readonly AnalysisProject _project;
        private CapturedPackageLibraries(AnalysisProject project, string assetsPath, string assetsText, AnalysisLibraryReferences references)
        { _project = project; AssetsPath = assetsPath; AssetsText = assetsText; References = references; }

        public string AssetsPath { get; }
        public string AssetsText { get; }
        public AnalysisLibraryReferences References { get; }

        public bool IsCurrent(ISdkAnalysisBridgeFactory factory, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (AssetsPath == null) return true;
            if (File.ReadAllText(AssetsPath) != AssetsText) return false;
            BridgeOpenResult opened = factory.Open(_project.CompilerPath);
            using (ISdkAnalysisBridge bridge = opened.Bridge)
            {
                if (!(bridge is ISdkLibraryBridge libraryBridge)) return false;
                var current = Capture(_project, libraryBridge, token);
                return AssetsPath == current.AssetsPath && AssetsText == current.AssetsText && References.Matches(current.References);
            }
        }

        public static CapturedPackageLibraries Capture(AnalysisProject project, ISdkLibraryBridge bridge, CancellationToken token,
            Action<System.Collections.Generic.IEnumerable<string>> observe = null)
        {
            token.ThrowIfCancellationRequested();
            if (project.PackageReferences.Count == 0)
                return new CapturedPackageLibraries(project, null, null, AnalysisLibraryReferences.Empty);
            if (string.IsNullOrWhiteSpace(project.AssetsFile))
                throw new InvalidDataException("Package references require restored assets: " + project.ProjectPath);
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.ProjectPath), project.AssetsFile));
            observe?.Invoke(new[] { path });
            string text = File.ReadAllText(path);
            var result = bridge.ResolvePackages(text, path, project.FrameworkMoniker,
                project.PackageReferences.Select(item => item.Include).ToArray(),
                project.PackageReferences.Select(item => item.GetMetadata("Version")).ToArray(), token);
            observe?.Invoke(result.WatchedPaths);
            if (result.Availability != AnalysisAvailability.Available)
                throw new InvalidDataException("Cannot resolve saved packages for " + project.ProjectPath + ": " + result.Message);
            observe?.Invoke(result.Packages.Select(package => package.FilePath));
            var artifacts = result.Packages.Select(package =>
            {
                token.ThrowIfCancellationRequested();
                return new AnalysisInput(ProjectAnalysisInputSource.ReadXmlText(package.FilePath), package.FilePath, 0);
            }).ToArray();
            return new CapturedPackageLibraries(project, path, text, new AnalysisLibraryReferences(
                result.Packages.Where(package => package.IsDirect).Select(package => package.Identity), artifacts));
        }
    }
}
