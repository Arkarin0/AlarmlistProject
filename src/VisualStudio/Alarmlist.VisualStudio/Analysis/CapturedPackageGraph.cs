// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.ConfigurationInput;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class CapturedPackageGraph
    {
        private readonly string _path;
        private readonly string _text;
        private readonly PackageGraphResponse _response;

        private CapturedPackageGraph(string path, string text, PackageGraphResponse response)
        { _path = path; _text = text; _response = response;
            WatchedPaths = Array.AsReadOnly(response.WatchedPaths.Concat(path == null ? Array.Empty<string>() : new[] { path })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()); }
        internal IReadOnlyList<string> WatchedPaths { get; }

        internal PackageGraphResponse CurrentResponse
        {
            get
            {
                try
                {
                    if (_path != null && (File.Exists(_path) ? File.ReadAllText(_path) : null) != _text) return null;
                    foreach (var content in _response.Packages.SelectMany(package => package.Contents))
                    {
                        if (File.Exists(content.FilePath) != (content.ContentHash != null)) return null;
                        if (content.Kind == "Manifest" && PackageContentPath.Hash(PackageContentPath.Read(content.PackageDirectory, content.RelativePath)) != content.ContentHash)
                            return null;
                    }
                    return _response;
                }
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
                        project.PackageReferences.Select(item => item.GetMetadata("Version")).ToArray(), token, project.Analyzers.ToArray())
                    : new PackageGraphResponse(AnalysisAvailability.UnsupportedContract, "The selected SDK does not expose package graph data.");
                foreach (string observed in response.WatchedPaths) observe(observed);
                return new CapturedPackageGraph(path, text, response);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            { return new CapturedPackageGraph(path, text, new PackageGraphResponse(AnalysisAvailability.InvalidInput, exception.Message)); }
        }
    }
}
