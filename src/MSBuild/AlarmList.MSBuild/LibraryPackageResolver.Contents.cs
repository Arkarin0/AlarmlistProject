// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Alarmlist.ConfigurationInput;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Alarmlist.MSBuild
{
    public static partial class LibraryPackageResolver
    {
        public static IReadOnlyList<ResolvedPackageNode> ResolveContentGraph(string assetsText, string assetsPath, string frameworkMoniker,
            string[] packageIds, string[] packageVersions, string[] analyzerPaths, Action<string> observePath)
        {
            if (packageIds == null || packageVersions == null || packageIds.Length != packageVersions.Length)
                throw new ArgumentException("Package identifiers and versions must have matching lengths.");
            if (packageIds.Length == 0) return Array.Empty<ResolvedPackageNode>();
            SelectGraph(assetsText, assetsPath, frameworkMoniker, packageIds, packageVersions,
                out LockFile assets, out LockFileTarget target, out _, out HashSet<string> reachable);
            var analyzers = new HashSet<string>((analyzerPaths ?? Array.Empty<string>()).Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            var result = new List<ResolvedPackageNode>();
            foreach (var selected in target.Libraries.Where(item => item.Type == "package" && reachable.Contains(item.Name)))
            {
                var library = assets.Libraries.Single(item => item.Type == "package" && item.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase)
                    && item.Version.Equals(selected.Version));
                string[] directories = assets.PackageFolders.Select(folder => ChildPath(folder.Path, library.Path)).ToArray();
                string directory = directories.FirstOrDefault(Directory.Exists) ?? directories.FirstOrDefault()
                    ?? throw new InvalidDataException("The package assets contain no package folder.");
                var files = new HashSet<string>(library.Files.Select(file => file.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
                // Validate every declared path before using manifests or selecting entries.
                foreach (string file in files) PackageContentPath.Resolve(directory, file);
                var contents = new List<ResolvedPackageContent>();
                var manifests = files.Where(file => file.IndexOf('/') < 0 && file.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (manifests.Length > 1) throw new InvalidDataException("The selected package declares multiple manifests.");
                string readme = null;
                if (manifests.Length == 1)
                {
                    byte[] bytes = Capture(manifests[0], "Manifest");
                    if (bytes != null)
                        using (var stream = new MemoryStream(bytes))
                        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                            readme = new NuspecReader(XDocument.Load(reader)).GetReadme()?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(readme))
                    {
                        PackageContentPath.Resolve(directory, readme);
                        if (!files.Contains(readme)) throw new InvalidDataException("The package README is not declared in the restored file list.");
                    }
                }
                var build = new HashSet<string>(selected.Build.Concat(selected.BuildMultiTargeting).Select(item => item.Path.Replace('\\', '/')),
                    StringComparer.OrdinalIgnoreCase);
                foreach (string file in files.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
                {
                    string extension = Path.GetExtension(file);
                    string kind = build.Contains(file) && (extension.Equals(".props", StringComparison.OrdinalIgnoreCase) || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase)) ? "Build"
                        : file.StartsWith("alarmlist/", StringComparison.OrdinalIgnoreCase) && extension.Equals(".almlib", StringComparison.OrdinalIgnoreCase) ? "Library"
                        : string.Equals(file, readme, StringComparison.OrdinalIgnoreCase) ||
                            ((file.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) || file.StartsWith("documentation/", StringComparison.OrdinalIgnoreCase))
                            && new[] { ".md", ".txt", ".html" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) ? "Document"
                        : extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) && analyzers.Contains(PackageContentPath.Resolve(directory, file)) ? "Analyzer" : null;
                    if (kind != null) Capture(file, kind);
                }
                result.Add(new ResolvedPackageNode(library.Name, library.Version.ToNormalizedString(),
                    packageIds.Contains(library.Name, StringComparer.OrdinalIgnoreCase), selected.Dependencies.Select(item => item.Id), contents));

                byte[] Capture(string relative, string kind)
                {
                    foreach (string candidate in directories) observePath?.Invoke(PackageContentPath.Resolve(candidate, relative));
                    byte[] bytes = PackageContentPath.Read(directory, relative);
                    contents.Add(new ResolvedPackageContent(directory, relative, kind, PackageContentPath.Hash(bytes)));
                    return bytes;
                }
            }
            return result.AsReadOnly();
        }
    }

    public sealed class ResolvedPackageContent
    {
        internal ResolvedPackageContent(string directory, string relative, string kind, string hash)
        { PackageDirectory = directory; RelativePath = relative; Kind = kind; ContentHash = hash; FilePath = PackageContentPath.Resolve(directory, relative); }
        public string PackageDirectory { get; }
        public string RelativePath { get; }
        public string FilePath { get; }
        public string Kind { get; }
        public string ContentHash { get; }
    }
}
