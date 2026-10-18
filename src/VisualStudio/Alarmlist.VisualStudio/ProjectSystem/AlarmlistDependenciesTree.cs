// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    [Export(ExportContractNames.ProjectTreeProviders.PhysicalViewRootGraft, typeof(IProjectTreeProvider))]
    [AppliesTo(AlarmlistProject.Capability)]
    internal sealed class AlarmlistDependenciesTree : ProjectTreeProviderBase
    {
        private readonly IActiveConfiguredProjectSubscriptionService _subscriptions;
        private readonly IActiveConfiguredProjectProvider _active;
        private DependencyTreeSubscription<IProjectVersionedValue<IProjectSnapshot>> _subscription;
        internal static readonly ProjectTreeFlags RootFlags = ProjectTreeFlags.Create("AlarmlistDependencies")
            | ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.ReferencesFolder;

        [ImportingConstructor]
        public AlarmlistDependenciesTree(IProjectThreadingService threading,
            UnconfiguredProject project, IActiveConfiguredProjectSubscriptionService subscriptions,
            IActiveConfiguredProjectProvider active)
            : base(threading, project, configuredProjectExportsRequired: false)
        { _subscriptions = subscriptions; _active = active; }

        protected override void Initialize()
        {
            base.Initialize();
            _subscription = new DependencyTreeSubscription<IProjectVersionedValue<IProjectSnapshot>>(
                _subscriptions.ProjectSource.SourceBlock, UpdateAsync, DefaultCancellationToken);
            ProjectFaultHandlerService.RegisterFaultHandler(_subscription.Completion, project: UnconfiguredProject);
        }

        private Task UpdateAsync(IProjectVersionedValue<IProjectSnapshot> update)
        {
            var project = update.Value.ProjectInstance;
            if (project == null) return Task.CompletedTask;
            // The base provider's configured exports can still be uninitialized
            // when ProjectSource first publishes. Use CPS's live provider here.
            ConfiguredProject active = _active.ActiveConfiguredProject;
            if (active == null || _active.ConfigurationActiveCancellationToken.IsCancellationRequested
                || active.ProjectConfiguration.Dimensions.Any(dimension =>
                !string.Equals(project.GetPropertyValue(dimension.Key), dimension.Value, StringComparison.OrdinalIgnoreCase)))
                return Task.CompletedTask;
            ImmutableDictionary<string, bool> references = CaptureReferences(project, update.Value.FullPath);
            return SubmitTreeUpdateAsync((previous, exports, token) =>
            {
                token.ThrowIfCancellationRequested();
                var icon = new ProjectImageMoniker(KnownMonikers.Reference.Guid, KnownMonikers.Reference.Id);
                IProjectTree root = previous?.Value.Tree ?? NewTree(DependencyTreeLabels.Dependencies, icon: icon, flags: RootFlags);
                if (!ReferenceEquals(active, _active.ActiveConfiguredProject)) return Task.FromResult(new TreeUpdateResult(root));
                IProjectTree group = root.Children.FirstOrDefault(child => child.Flags.Contains("AlarmlistProjectReferences"));
                if (references.Count != 0)
                {
                    group = group ?? root.Add(NewTree(DependencyTreeLabels.Projects, icon: icon,
                        flags: ProjectTreeFlags.VirtualFolder | ProjectTreeFlags.Create("AlarmlistProjectReferences")));
                    foreach (IProjectTree removed in group.Children.Where(child => !references.ContainsKey(child.FilePath)).ToArray())
                        group = group.Remove(removed);
                    foreach (string path in references.Keys.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        bool exists = references[path];
                        var itemIcon = exists ? KnownMonikers.Reference : KnownMonikers.ReferenceWarning;
                        var flags = ProjectTreeFlags.Create("AlarmlistProjectReference") | ProjectTreeFlags.Reference
                            | (exists ? ProjectTreeFlags.ResolvedReference : ProjectTreeFlags.BrokenReference);
                        var moniker = new ProjectImageMoniker(itemIcon.Guid, itemIcon.Id);
                        IProjectTree existing = group.Children.FirstOrDefault(child => string.Equals(child.FilePath, path, StringComparison.OrdinalIgnoreCase));
                        group = existing == null
                            ? group.Add(NewTree(Path.GetFileNameWithoutExtension(path), filePath: path, icon: moniker, flags: flags)).Parent
                            : existing.SetProperties(icon: moniker, flags: flags).Parent;
                    }
                    root = group.Parent;
                }
                else if (group != null) root = root.Remove(group);
                return Task.FromResult(new TreeUpdateResult(root));
            }, update.DataSourceVersions, TreeSubmissionOptions.SkipIfUpdateHasOldDataSources, DefaultCancellationToken);
        }

        // Use the evaluated direct items, including missing and unloaded projects.
        // Capturing this data never loads a dependency or runs a build target.
        internal static ImmutableDictionary<string, bool> CaptureReferences(ProjectInstance project, string projectPath)
        {
            string directory = Path.GetDirectoryName(projectPath);
            return project.GetItems("ProjectReference")
                .Select(item => Path.GetFullPath(Path.Combine(directory, item.EvaluatedInclude)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableDictionary(path => path, File.Exists, StringComparer.OrdinalIgnoreCase);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _subscription?.Dispose();
            base.Dispose(disposing);
        }

        public override string GetPath(IProjectTree node)
        {
            for (IProjectTree ancestor = node; ancestor != null; ancestor = ancestor.Parent)
                if (ancestor.Flags.Contains("AlarmlistDependencies")) return node.FilePath;
            return null;
        }
        public override bool CanRemove(IImmutableSet<IProjectTree> nodes, DeleteOptions options = DeleteOptions.None)
            => !options.HasFlag(DeleteOptions.DeleteFromStorage) && nodes.Count != 0
                && nodes.All(node => node.Flags.Contains("AlarmlistProjectReference"));

        public override async Task RemoveAsync(IImmutableSet<IProjectTree> nodes, DeleteOptions options = DeleteOptions.None)
        {
            if (!CanRemove(nodes, options)) throw new InvalidOperationException("Only project references can be removed here.");
            ConfiguredProject active = _active.ActiveConfiguredProject;
            if (active == null || _active.ConfigurationActiveCancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("The active project is unavailable.");
            foreach (IProjectTree node in nodes)
            {
                DefaultCancellationToken.ThrowIfCancellationRequested();
                if (!ReferenceEquals(active, _active.ActiveConfiguredProject)) throw new InvalidOperationException("The active configuration changed.");
                await active.Services.ProjectReferences.RemoveAsync(node.FilePath);
            }
        }

        protected override ConfiguredProjectExports GetActiveConfiguredProjectExports(ConfiguredProject project)
            => GetActiveConfiguredProjectExports<ConfiguredProjectExports>(project);
        public override IProjectTree FindByPath(IProjectTree root, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (root.Flags.Contains("AlarmlistProjectReference")
                && string.Equals(root.FilePath, path, StringComparison.OrdinalIgnoreCase)) return root;
            foreach (IProjectTree child in root.Children)
            {
                IProjectTree match = FindByPath(child, path);
                if (match != null) return match;
            }
            return null;
        }
    }
}
