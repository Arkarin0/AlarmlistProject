// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Alarmlist.MSBuild
{
    // Shared by the build task and the isolated IDE worker; no MSBuild task types in this API.
    public static class LibraryPackageResolver
    {
        public static IReadOnlyList<ResolvedLibraryPackage> Resolve(string assetsText, string assetsPath, string frameworkMoniker,
            string[] packageIds, string[] packageVersions)
            => Resolve(assetsText, assetsPath, frameworkMoniker, packageIds, packageVersions, null);

        public static IReadOnlyList<ResolvedLibraryPackage> Resolve(string assetsText, string assetsPath, string frameworkMoniker,
            string[] packageIds, string[] packageVersions, Action<string> observeArtifactPath)
        {
            if (packageIds == null || packageVersions == null || packageIds.Length != packageVersions.Length)
                throw new ArgumentException("Package identifiers and versions must have matching lengths.");
            if (packageIds.Length == 0) return Array.Empty<ResolvedLibraryPackage>();
            var logger = new AssetLogger();
            LockFile assets = new LockFileFormat().Parse(assetsText, logger, assetsPath);
            if (assets.Version < 3 || assets.Version > LockFileFormat.Version || assets.LogMessages.Any(message => message.Level == LogLevel.Error))
                throw new InvalidDataException("Package assets are invalid or contain restore errors. Run Restore. " + string.Join("; ", logger.Errors));
            NuGetFramework framework = NuGetFramework.Parse(frameworkMoniker);
            LockFileTarget target = assets.Targets.SingleOrDefault(candidate => candidate.TargetFramework.Equals(framework)
                && string.IsNullOrEmpty(candidate.RuntimeIdentifier));
            if (target == null) throw new InvalidDataException("Package assets contain no matching framework: " + frameworkMoniker);
            var selected = target.Libraries.ToDictionary(library => library.Name, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < packageIds.Length; index++)
            {
                if (!selected.TryGetValue(packageIds[index], out LockFileTargetLibrary library) || library.Type != "package")
                    throw new InvalidDataException("Package reference is not restored: " + packageIds[index]);
                string requested = packageVersions[index];
                if (!string.IsNullOrEmpty(requested) && (!VersionRange.TryParse(requested, out VersionRange range) || !range.Satisfies(library.Version)))
                    throw new InvalidDataException("Restored version does not satisfy package reference " + packageIds[index] + ". Run Restore.");
            }

            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(packageIds);
            while (queue.Count != 0)
            {
                string id = queue.Dequeue();
                if (!reachable.Add(id)) continue;
                if (!selected.TryGetValue(id, out LockFileTargetLibrary library))
                    throw new InvalidDataException("A restored dependency is missing: " + id);
                foreach (var dependency in library.Dependencies) queue.Enqueue(dependency.Id);
            }

            var artifacts = new Dictionary<string, ResolvedLibraryPackage>(StringComparer.OrdinalIgnoreCase);
            foreach (LockFileTargetLibrary selection in target.Libraries.Where(library => reachable.Contains(library.Name) && library.Type == "package"))
            {
                LockFileLibrary library = assets.Libraries.Single(candidate => candidate.Type == "package"
                    && StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, selection.Name) && candidate.Version.Equals(selection.Version));
                string[] files = library.Files.Where(file => file.StartsWith("alarmlist/", StringComparison.Ordinal)
                    && file.EndsWith(".almlib", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (files.Length == 0) continue;
                if (files.Length != 1) throw new InvalidDataException("A library package must contain one .almlib artifact: " + library.Name);
                string[] packageDirectories = assets.PackageFolders.Select(folder => ChildPath(folder.Path, library.Path)).ToArray();
                foreach (string directory in packageDirectories) observeArtifactPath?.Invoke(ChildPath(directory, files[0]));
                string packageDirectory = packageDirectories.FirstOrDefault(Directory.Exists);
                if (packageDirectory == null) throw new InvalidDataException("Restored package directory is missing: " + library.Name);
                string artifactPath = ChildPath(packageDirectory, files[0]);
                if (!File.Exists(artifactPath)) throw new InvalidDataException("Restored library artifact is missing: " + artifactPath);
                var item = new ResolvedLibraryPackage(artifactPath, library.Name, library.Version.ToNormalizedString());
                artifacts.Add(library.Name, item);
            }

            // Non-library packages may aggregate libraries. The first library on each
            // direct package path is a root; its artifact describes further library edges.
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            queue = new Queue<string>(packageIds);
            while (queue.Count != 0)
            {
                string id = queue.Dequeue();
                if (!visited.Add(id)) continue;
                if (artifacts.TryGetValue(id, out ResolvedLibraryPackage artifact)) artifact.IsDirect = true;
                else foreach (var dependency in selected[id].Dependencies) queue.Enqueue(dependency.Id);
            }
            return target.Libraries.Where(library => artifacts.ContainsKey(library.Name)).Select(library => artifacts[library.Name]).ToArray();
        }

        private sealed class AssetLogger : LoggerBase
        {
            internal List<string> Errors { get; } = new List<string>();
            public override void Log(ILogMessage message) { if (message.Level == LogLevel.Error) Errors.Add(message.Message); }
            public override System.Threading.Tasks.Task LogAsync(ILogMessage message)
            { Log(message); return System.Threading.Tasks.Task.CompletedTask; }
        }

        private static string ChildPath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)
                || relative.Split('/', '\\').Any(segment => segment == ".." || segment == "." || segment.Length == 0))
                throw new InvalidDataException("Invalid path in package assets: " + relative);
            string directory = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(directory, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Package asset escapes its package directory.");
            return path;
        }
    }

    public sealed class ResolvedLibraryPackage
    {
        internal ResolvedLibraryPackage(string filePath, string packageId, string packageVersion)
        { FilePath = filePath; PackageId = packageId; PackageVersion = packageVersion; }
        public string FilePath { get; }
        public string PackageId { get; }
        public string PackageVersion { get; }
        public bool IsDirect { get; internal set; }
    }
}
