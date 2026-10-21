// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed partial class AlarmlistDependenciesTree
    {
        private IProjectTree UpdatePackageContents(IProjectTree package, IReadOnlyList<PackageContentGroup> groups)
        {
            string Identity(string kind) => "AlarmlistPackageContentKind=" + kind;
            foreach (var removed in package.Children.Where(child => child.Flags.Contains("AlarmlistPackageContentGroup")
                && !groups.Any(group => child.Flags.Contains(Identity(group.Kind)))).ToArray()) package = package.Remove(removed);
            foreach (var group in groups)
            {
                var groupImage = group.Kind == "Analyzer" ? KnownMonikers.CodeAnalysisWindow : KnownMonikers.FolderClosed;
                var groupIcon = new ProjectImageMoniker(groupImage.Guid, groupImage.Id);
                var flags = ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Create("AlarmlistPackageContentGroup", Identity(group.Kind));
                var existing = package.Children.FirstOrDefault(child => child.Flags.Contains(Identity(group.Kind)));
                var node = existing == null ? package.Add(NewTree(group.Caption, icon: groupIcon, flags: flags))
                    : existing.SetProperties(caption: group.Caption, icon: groupIcon, flags: flags);
                foreach (var removed in node.Children.Where(child => !group.Files.Any(file => child.Flags.Contains(PackageContentGroup.Identity(file)))).ToArray())
                    node = node.Remove(removed);
                foreach (var file in group.Files)
                {
                    var image = file.ContentHash == null ? KnownMonikers.ReferenceWarning
                        : file.Kind == "Analyzer" ? KnownMonikers.CodeAnalysisWindow : KnownMonikers.Document;
                    var icon = new ProjectImageMoniker(image.Guid, image.Id);
                    var fileFlags = ProjectTreeFlags.Create("AlarmlistPackageContentFile", PackageContentGroup.Identity(file));
                    if (file.ContentHash == null) fileFlags |= ProjectTreeFlags.BrokenReference;
                    string caption = file.RelativePath + (file.ContentHash == null ? " — " + DependencyTreeLabels.Unresolved : "");
                    var previous = node.Children.FirstOrDefault(child => child.Flags.Contains(PackageContentGroup.Identity(file)));
                    node = (previous == null ? node.Add(NewTree(caption, icon: icon, flags: fileFlags))
                        : previous.SetProperties(caption: caption, icon: icon, flags: fileFlags)).Parent;
                }
                package = node.Parent;
            }
            return package;
        }
    }
}
