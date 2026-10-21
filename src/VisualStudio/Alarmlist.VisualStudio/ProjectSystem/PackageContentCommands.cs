// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Alarmlist.VisualStudio.ProjectSystem
{
    // Follow CPS command routing for Open and UIHierarchy Enter/double-click.
    // Nodes deliberately have no physical FilePath, so ordinary editable-file
    // handlers cannot bypass the captured read-only viewer.
    [ExportCommandGroup("5efc7975-14bc-11cf-9b2b-00aa00573819"), AppliesTo(AlarmlistProject.Capability), Order(10)]
    internal sealed class OpenPackageContentCommand : PackageContentCommand
    {
        [ImportingConstructor]
        public OpenPackageContentCommand(UnconfiguredProject project, IActiveConfiguredProjectProvider active, ProjectAnalysisRegistry registry,
            [Import(typeof(SVsServiceProvider))] IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
            : base(project, active, registry, services, adapters, (long)VSConstants.VSStd97CmdID.Open) { }
    }

    [ExportCommandGroup("60481700-078b-11d1-aaf8-00a0c9055a90"), AppliesTo(AlarmlistProject.Capability), Order(10)]
    internal sealed class ActivatePackageContentCommand : PackageContentCommand
    {
        [ImportingConstructor]
        public ActivatePackageContentCommand(UnconfiguredProject project, IActiveConfiguredProjectProvider active, ProjectAnalysisRegistry registry,
            [Import(typeof(SVsServiceProvider))] IServiceProvider services, IVsEditorAdaptersFactoryService adapters)
            : base(project, active, registry, services, adapters,
                (long)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick, (long)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey) { }
    }

    internal abstract class PackageContentCommand : IAsyncCommandGroupHandler
    {
        private readonly UnconfiguredProject _project;
        private readonly IActiveConfiguredProjectProvider _active;
        private readonly ProjectAnalysisRegistry _registry;
        private readonly IServiceProvider _services;
        private readonly IVsEditorAdaptersFactoryService _adapters;
        private readonly long[] _commands;
        protected PackageContentCommand(UnconfiguredProject project, IActiveConfiguredProjectProvider active, ProjectAnalysisRegistry registry,
            IServiceProvider services, IVsEditorAdaptersFactoryService adapters, params long[] commands)
        { _project = project; _active = active; _registry = registry; _services = services; _adapters = adapters; _commands = commands; }

        private bool Handles(IImmutableSet<IProjectTree> nodes, long id, bool focused) => focused && _commands.Contains(id)
            && nodes.Count == 1 && nodes.Single().Flags.Contains("AlarmlistPackageContentFile");

        public Task<CommandStatusResult> GetCommandStatusAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused,
            string commandText, CommandStatus progressiveStatus) => !Handles(nodes, commandId, focused)
            ? Task.FromResult(new CommandStatusResult(false, commandText, progressiveStatus))
            : Task.FromResult(new CommandStatusResult(true, commandText, CommandStatus.Supported
                | (nodes.Single().Flags.Contains(ProjectTreeFlags.BrokenReference) ? 0 : CommandStatus.Enabled)));

        public async Task<bool> TryHandleCommandAsync(IImmutableSet<IProjectTree> nodes, long commandId, bool focused,
            long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
        {
            if (!Handles(nodes, commandId, focused)) return false;
            try
            {
                var token = _active.ConfigurationActiveCancellationToken;
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                var configured = _active.ActiveConfiguredProject;
                var service = _registry.GetProjects().FirstOrDefault(item => string.Equals(item.Current.Project?.ProjectPath, _project.FullPath, StringComparison.OrdinalIgnoreCase));
                var state = service?.Current;
                if (state == null || configured == null || state.Project.Configuration != configured.ProjectConfiguration.Name)
                    throw new InvalidOperationException("The package selection is no longer current.");
                var node = nodes.Single();
                var packageNode = node.Parent?.Parent;
                var target = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var graph = state.PackageGraph?.CurrentResponse;
                    if (graph?.Availability != AnalysisAvailability.Available) throw new InvalidOperationException("Restore or wait for package analysis before opening this item.");
                    var package = graph.Packages.SingleOrDefault(item => packageNode != null
                        && packageNode.Flags.Contains("AlarmlistPackageId=" + item.Id.ToUpperInvariant())
                        && packageNode.Flags.Contains("AlarmlistPackageVersion=" + item.Version));
                    var content = package?.Contents.SingleOrDefault(item => node.Flags.Contains(PackageContentGroup.Identity(item)));
                    if (content == null) throw new InvalidOperationException("The selected package item is no longer available.");
                    return PackageContentDocument.Create(state, package, content);
                }, token);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!ReferenceEquals(service.Current, state) || !ReferenceEquals(configured, _active.ActiveConfiguredProject))
                    throw new InvalidOperationException("Package analysis changed while opening the item. Try again.");
                GeneratedLibraryDefinition.Navigate(_services, _adapters, target);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception))
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                (_services.GetService(typeof(SVsStatusbar)) as IVsStatusbar)?.SetText(exception.Message);
            }
            return true;
        }
    }
}
