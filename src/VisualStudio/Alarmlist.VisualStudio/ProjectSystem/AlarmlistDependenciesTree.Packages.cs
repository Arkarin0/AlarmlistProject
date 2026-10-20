// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    internal sealed partial class AlarmlistDependenciesTree
    {
        private readonly ProjectAnalysisRegistry _registry;
        private readonly BufferBlock<bool> _packageSignals = new BufferBlock<bool>();
        private DependencyTreeSubscription<bool> _packageSubscription;
        private AnalysisProject _treeProject;
        private ConfiguredProject _treeConfigured;

        private void InitializePackages()
        {
            _packageSubscription = new DependencyTreeSubscription<bool>(_packageSignals, _ =>
                SubmitTreeUpdateAsync((previous, exports, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var root = previous?.Value.Tree;
                    if (root == null) root = NewTree(DependencyTreeLabels.Dependencies,
                        icon: new ProjectImageMoniker(KnownMonikers.Reference.Guid, KnownMonikers.Reference.Id), flags: RootFlags);
                    if (_treeProject != null)
                        root = UpdatePackages(root);
                    return Task.FromResult(new TreeUpdateResult(root));
                }, DefaultCancellationToken), DefaultCancellationToken);
            ProjectFaultHandlerService.RegisterFaultHandler(_packageSubscription.Completion, project: UnconfiguredProject);
            _registry.Changed += OnPackagesChanged;
            _active.Changed += OnPackagesChanged;
        }

        private void OnPackagesChanged(object sender, EventArgs args) => _packageSignals.Post(true);

        private IProjectTree UpdatePackages(IProjectTree root)
        {
            ProjectAnalysisState state = _registry.GetProjects().Select(service => service.Current)
                .FirstOrDefault(candidate => string.Equals(candidate.Project?.ProjectPath, _treeProject.ProjectPath, StringComparison.OrdinalIgnoreCase));
            if (!ReferenceEquals(_treeConfigured, _active.ActiveConfiguredProject) || _active.ConfigurationActiveCancellationToken.IsCancellationRequested)
                state = null;
            IReadOnlyList<PackageTreeEntry> entries = PackageTreeEntry.Create(_treeProject, state);
            IProjectTree group = root.Children.FirstOrDefault(child => child.Flags.Contains("AlarmlistPackages"));
            if (entries.Count == 0) return group == null ? root : root.Remove(group);
            var moniker = new ProjectImageMoniker(KnownMonikers.NuGet.Guid, KnownMonikers.NuGet.Id);
            group = group ?? root.Add(NewTree(DependencyTreeLabels.Packages, icon: moniker,
                flags: ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Create("AlarmlistPackages")));
            return UpdatePackageChildren(group, entries).Parent;
        }

        private IProjectTree UpdatePackageChildren(IProjectTree parent, IReadOnlyList<PackageTreeEntry> entries)
        {
            string Identity(string id) => "AlarmlistPackageId=" + id.ToUpperInvariant();
            foreach (var removed in parent.Children.Where(child => !entries.Any(entry => child.Flags.Contains(Identity(entry.Id)))).ToArray())
                parent = parent.Remove(removed);
            foreach (PackageTreeEntry entry in entries)
            {
                var image = entry.Version == null ? KnownMonikers.ReferenceWarning : KnownMonikers.NuGet;
                var moniker = new ProjectImageMoniker(image.Guid, image.Id);
                var flags = ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Reference | ProjectTreeFlags.Create(Identity(entry.Id))
                    | ProjectTreeFlags.Create(entry.IsDirect ? "AlarmlistDirectPackage" : "AlarmlistTransitivePackage")
                    | (entry.Version == null ? ProjectTreeFlags.BrokenReference : ProjectTreeFlags.ResolvedReference);
                var existing = parent.Children.FirstOrDefault(child => child.Flags.Contains(Identity(entry.Id)));
                IProjectTree node = existing == null ? parent.Add(NewTree(entry.Caption, icon: moniker, flags: flags))
                    : existing.SetProperties(caption: entry.Caption, icon: moniker, flags: flags);
                parent = UpdatePackageChildren(node, entry.Children).Parent;
            }
            return parent;
        }
    }
}
