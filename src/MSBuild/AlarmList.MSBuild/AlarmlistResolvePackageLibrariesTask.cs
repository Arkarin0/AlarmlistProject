// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace Alarmlist.MSBuild
{
    /// <summary>Discovers library files only from NuGet's selected restore graph.</summary>
    public sealed class AlarmlistResolvePackageLibrariesTask : AlarmlistBaseTask
    {
        public string AssetsFile { get; set; }
        public string FrameworkMoniker { get; set; } = "native,Version=v0.0";
        public ITaskItem[] PackageReferences { get; set; } = Array.Empty<ITaskItem>();
        [Output] public ITaskItem[] LibraryReferences { get; private set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            LibraryReferences = Array.Empty<ITaskItem>();
            try
            {
                ITaskItem[] references = PackageReferences ?? Array.Empty<ITaskItem>();
                if (references.Length == 0) return true;
                if (string.IsNullOrWhiteSpace(AssetsFile) || !File.Exists(AssetsFile))
                    throw new InvalidDataException("Package assets are missing. Run Restore before building alarm libraries.");
                LockFile assets = new LockFileFormat().Read(AssetsFile);
                if (assets.Version < 3 || assets.Version > LockFileFormat.Version || assets.LogMessages.Any(message => message.Level == LogLevel.Error))
                    throw new InvalidDataException("Package assets are invalid or contain restore errors. Run Restore.");
                NuGetFramework framework = NuGetFramework.Parse(FrameworkMoniker);
                LockFileTarget target = assets.Targets.SingleOrDefault(candidate => candidate.TargetFramework.Equals(framework)
                    && string.IsNullOrEmpty(candidate.RuntimeIdentifier));
                if (target == null) throw new InvalidDataException("Package assets contain no matching framework: " + FrameworkMoniker);
                var selected = target.Libraries.ToDictionary(library => library.Name, StringComparer.OrdinalIgnoreCase);
                foreach (ITaskItem reference in references)
                {
                    if (!selected.TryGetValue(reference.ItemSpec, out LockFileTargetLibrary library) || library.Type != "package")
                        throw new InvalidDataException("Package reference is not restored: " + reference.ItemSpec);
                    string requested = reference.GetMetadata("Version");
                    if (!string.IsNullOrEmpty(requested) && (!VersionRange.TryParse(requested, out VersionRange range) || !range.Satisfies(library.Version)))
                        throw new InvalidDataException("Restored version does not satisfy package reference " + reference.ItemSpec + ". Run Restore.");
                }

                var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var queue = new Queue<string>(references.Select(reference => reference.ItemSpec));
                while (queue.Count != 0)
                {
                    string id = queue.Dequeue();
                    if (!reachable.Add(id)) continue;
                    if (!selected.TryGetValue(id, out LockFileTargetLibrary library))
                        throw new InvalidDataException("A restored dependency is missing: " + id);
                    foreach (var dependency in library.Dependencies) queue.Enqueue(dependency.Id);
                }

                var artifacts = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
                foreach (LockFileTargetLibrary selection in target.Libraries.Where(library => reachable.Contains(library.Name) && library.Type == "package"))
                {
                    LockFileLibrary library = assets.Libraries.Single(candidate => candidate.Type == "package"
                        && StringComparer.OrdinalIgnoreCase.Equals(candidate.Name, selection.Name) && candidate.Version.Equals(selection.Version));
                    string[] files = library.Files.Where(file => file.StartsWith("alarmlist/", StringComparison.Ordinal)
                        && file.EndsWith(".almlib", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (files.Length == 0) continue;
                    if (files.Length != 1) throw new InvalidDataException("A library package must contain one .almlib artifact: " + library.Name);
                    string packageDirectory = assets.PackageFolders.Select(folder => ChildPath(folder.Path, library.Path))
                        .FirstOrDefault(Directory.Exists);
                    if (packageDirectory == null) throw new InvalidDataException("Restored package directory is missing: " + library.Name);
                    string artifactPath = ChildPath(packageDirectory, files[0]);
                    if (!File.Exists(artifactPath)) throw new InvalidDataException("Restored library artifact is missing: " + artifactPath);
                    var item = new TaskItem(artifactPath);
                    item.SetMetadata("PackageId", library.Name);
                    item.SetMetadata("PackageVersion", library.Version.ToNormalizedString());
                    item.SetMetadata("Direct", "false");
                    artifacts.Add(library.Name, item);
                }

                // Non-library packages may aggregate libraries. The first library on each
                // direct package path is a root; its artifact describes further library edges.
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                queue = new Queue<string>(references.Select(reference => reference.ItemSpec));
                while (queue.Count != 0)
                {
                    string id = queue.Dequeue();
                    if (!visited.Add(id)) continue;
                    if (artifacts.TryGetValue(id, out ITaskItem artifact)) artifact.SetMetadata("Direct", "true");
                    else foreach (var dependency in selected[id].Dependencies) queue.Enqueue(dependency.Id);
                }
                LibraryReferences = target.Libraries.Where(library => artifacts.ContainsKey(library.Name)).Select(library => artifacts[library.Name]).ToArray();
                return true;
            }
            catch (Exception exception)
            {
                BuildOutputPlan.LogFailure(Log, exception);
                return false;
            }
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
}
