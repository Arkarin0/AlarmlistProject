// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.Build.Execution;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed partial class AlarmlistDependenciesTree
    {
        // ProjectInstance already owns import discovery and evaluated membership.
        // Do not enumerate directories or execute a target to populate this tree.
        internal sealed class SharedInputNode
        {
            internal string Path { get; }
            internal string Caption { get; }
            internal SharedInputNode(string path, string caption) { Path = path; Caption = caption; }
        }

        internal static ImmutableDictionary<string, ImmutableArray<SharedInputNode>> CaptureSharedInputs(ProjectInstance project, string projectPath)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var imports = project.ImportPaths.Where(path => string.Equals(Path.GetExtension(path), ".projitems", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath).Distinct(comparer).ToArray();
            string directory = Path.GetDirectoryName(projectPath);
            return imports.ToImmutableDictionary(path => path, path => project.GetItems("Compile")
                .Concat(project.GetItems("AlarmlistResource")).Concat(project.GetItems("AlarmlistLookup"))
                .Where(item => string.Equals(item.GetMetadataValue("DefiningProjectFullPath"), path, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(item.GetMetadataValue("Visible"), "false", StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude)), comparer)
                .Select(group =>
                {
                    string link = group.First().GetMetadataValue("Link");
                    string caption = string.IsNullOrWhiteSpace(link)
                        ? Uri.UnescapeDataString(new Uri(Path.GetDirectoryName(path) + Path.DirectorySeparatorChar).MakeRelativeUri(new Uri(group.Key)).ToString())
                        : link.Replace('\\', '/');
                    return new SharedInputNode(group.Key, caption);
                }).ToImmutableArray(), comparer);
        }

        private IProjectTree UpdateSharedInputs(IProjectTree root, IReadOnlyDictionary<string, ImmutableArray<SharedInputNode>> inputs)
        {
            var group = root.Children.FirstOrDefault(child => child.Flags.Contains("AlarmlistSharedProjects"));
            if (inputs.Count == 0) return group == null ? root : root.Remove(group);
            var folder = new ProjectImageMoniker(KnownMonikers.FolderClosed.Guid, KnownMonikers.FolderClosed.Id);
            group = group ?? root.Add(NewTree(DependencyTreeLabels.SharedProjects, icon: folder,
                flags: ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Create("AlarmlistSharedProjects")));
            foreach (var removed in group.Children.Where(child => !inputs.ContainsKey(child.FilePath)).ToArray()) group = group.Remove(removed);
            foreach (var shared in inputs.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                var node = group.Children.FirstOrDefault(child => string.Equals(child.FilePath, shared.Key, StringComparison.OrdinalIgnoreCase));
                node = node ?? group.Add(NewTree(Path.GetFileNameWithoutExtension(shared.Key), filePath: shared.Key, icon: folder,
                    flags: ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Create("AlarmlistSharedProject")));
                foreach (var removed in node.Children.Where(child => !shared.Value.Any(input => string.Equals(input.Path, child.FilePath, StringComparison.OrdinalIgnoreCase))).ToArray()) node = node.Remove(removed);
                foreach (var input in shared.Value)
                {
                    string path = input.Path;
                    bool exists = File.Exists(path);
                    var image = exists ? KnownMonikers.Document : KnownMonikers.ReferenceWarning;
                    var icon = new ProjectImageMoniker(image.Guid, image.Id);
                    var flags = ProjectTreeFlags.Create("AlarmlistSharedInput") | ProjectTreeFlags.ImportedSharedItem
                        | (exists ? ProjectTreeFlags.FileOnDisk | ProjectTreeFlags.SourceFile : ProjectTreeFlags.BrokenReference);
                    var previous = node.Children.FirstOrDefault(child => string.Equals(child.FilePath, path, StringComparison.OrdinalIgnoreCase));
                    string caption = input.Caption;
                    node = (previous == null ? node.Add(NewTree(caption, filePath: path, icon: icon, flags: flags))
                        : previous.SetProperties(caption: caption, icon: icon, flags: flags)).Parent;
                }
                group = node.Parent;
            }
            return group.Parent;
        }
    }
}
