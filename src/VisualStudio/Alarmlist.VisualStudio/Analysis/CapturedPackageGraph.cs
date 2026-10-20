// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class CapturedPackageGraph
    {
        private readonly string _path;
        private readonly string _text;
        private readonly PackageGraphResponse _response;

        private CapturedPackageGraph(string path, string text, PackageGraphResponse response)
        { _path = path; _text = text; _response = response; }

        internal PackageGraphResponse CurrentResponse
        {
            get
            {
                try { return _path == null || (File.Exists(_path) ? File.ReadAllText(_path) : null) == _text ? _response : null; }
                catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { return null; }
            }
        }

        internal static CapturedPackageGraph Capture(AnalysisProject project, ISdkAnalysisBridge bridge,
            Action<string> observe, CancellationToken token)
        {
            if (project.PackageReferences.Count == 0) return null;
            string path = null, text = null;
            try
            {
                if (string.IsNullOrWhiteSpace(project.AssetsFile)) throw new InvalidDataException("Restore the project to select package versions.");
                path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project.ProjectPath), project.AssetsFile));
                // Register before reading so a failed or missing restore can recover.
                observe(path);
                text = File.ReadAllText(path);
                token.ThrowIfCancellationRequested();
                var response = bridge is ISdkPackageGraphBridge packages
                    ? packages.ResolvePackageGraph(text, path, project.FrameworkMoniker,
                        project.PackageReferences.Select(item => item.Include).ToArray(),
                        project.PackageReferences.Select(item => item.GetMetadata("Version")).ToArray(), token)
                    : new PackageGraphResponse(AnalysisAvailability.UnsupportedContract, "The selected SDK does not expose package graph data.");
                return new CapturedPackageGraph(path, text, response);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { return new CapturedPackageGraph(path, text, new PackageGraphResponse(AnalysisAvailability.InvalidInput, exception.Message)); }
        }
    }
}
