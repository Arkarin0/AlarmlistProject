// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Alarmlist.VisualStudio.Analysis
{
    internal sealed class ReferenceCatalogueSnapshot
    {
        internal ReferenceCatalogueSnapshot(string solutionPath, string configuration, IEnumerable<ReferenceProjectSelection> projects, IEnumerable<string> coverage)
        {
            SolutionPath = solutionPath; Configuration = configuration;
            Projects = Array.AsReadOnly(projects.OrderBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase).ToArray());
            Coverage = Array.AsReadOnly(coverage.OrderBy(message => message, StringComparer.Ordinal).ToArray());
        }
        internal string SolutionPath { get; }
        internal string Configuration { get; }
        internal IReadOnlyList<ReferenceProjectSelection> Projects { get; }
        internal IReadOnlyList<string> Coverage { get; }
        internal bool Matches(ReferenceCatalogueSnapshot other) => other != null && SolutionPath == other.SolutionPath && Configuration == other.Configuration
            && Projects.Count == other.Projects.Count && Projects.Select((project, index) => project.Key == other.Projects[index].Key && project.Unavailable == other.Projects[index].Unavailable).All(equal => equal)
            && Coverage.SequenceEqual(other.Coverage, StringComparer.Ordinal);
    }

    internal interface IReferenceSolutionCatalogue
    {
        event EventHandler Changed;
        Task<ReferenceCatalogueSnapshot> CaptureAsync(CancellationToken token);
    }

    // COM stays on the UI thread. Only paths, active configuration selections and
    // detached CPS snapshots leave this boundary; no project targets are executed.
    [Export(typeof(IReferenceSolutionCatalogue))]
    internal sealed class VisualStudioReferenceCatalogue : IReferenceSolutionCatalogue, IVsSolutionEvents, IVsUpdateSolutionEvents, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ProjectAnalysisRegistry _registry;
        private IVsSolution _solution;
        private IVsSolutionBuildManager _build;
        private uint _solutionCookie, _buildCookie;
        private bool _disposed;
        public event EventHandler Changed;

        [ImportingConstructor]
        public VisualStudioReferenceCatalogue([Import(typeof(SVsServiceProvider))] IServiceProvider services, ProjectAnalysisRegistry registry)
        { _services = services; _registry = registry; _registry.Changed += OnRegistryChanged; }

        public async Task<ReferenceCatalogueSnapshot> CaptureAsync(CancellationToken token)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
            if (_disposed) throw new ObjectDisposedException(nameof(VisualStudioReferenceCatalogue));
            EnsureEvents();
            var dte = _services.GetService(typeof(SDTE)) as DTE;
            if (dte == null) return new ReferenceCatalogueSnapshot(null, null, Array.Empty<ReferenceProjectSelection>(), new[] { "The solution automation service is unavailable." });
            if (dte.Solution?.IsOpen != true) return new ReferenceCatalogueSnapshot(null, null, Array.Empty<ReferenceProjectSelection>(), new[] { "No open solution is available for reference search." });
            string solutionPath = dte.Solution.FullName;
            if (string.IsNullOrWhiteSpace(solutionPath)) return new ReferenceCatalogueSnapshot(null, null, Array.Empty<ReferenceProjectSelection>(),
                new[] { "Save the solution before capturing its project configuration mappings." });
            string directory = Path.GetDirectoryName(solutionPath);
            var active = dte.Solution.SolutionBuild.ActiveConfiguration;
            if (active == null) return new ReferenceCatalogueSnapshot(solutionPath, null, Array.Empty<ReferenceProjectSelection>(),
                new[] { "The active solution configuration is unavailable." });
            string activeKey = ConfigurationKey(active);
            var loaded = _registry.GetProjects().Select(service => service.Current).Where(state => state.Project != null)
                .GroupBy(state => Path.GetFullPath(state.Project.ProjectPath), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
            var selections = new Dictionary<string, ReferenceProjectSelection>(StringComparer.OrdinalIgnoreCase);
            var coverage = new List<string>();
            var mappings = new List<XElement>();
            foreach (SolutionContext context in active.SolutionContexts)
            {
                token.ThrowIfCancellationRequested();
                string path = ResolveProjectPath(directory, context.ProjectName);
                if (!IsApplicationProject(path)) continue;
                loaded.TryGetValue(path, out var state);
                var globals = state?.Project.EvaluationGlobalProperties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globals["Configuration"] = context.ConfigurationName; globals["Platform"] = context.PlatformName;
                globals["SolutionPath"] = solutionPath; globals["SolutionDir"] = directory + Path.DirectorySeparatorChar;
                var configurationSlot = new IVsProjectCfg[1];
                if (ErrorHandler.Succeeded(_solution.GetProjectOfUniqueName(context.ProjectName, out var hierarchy)) && hierarchy != null
                    && ErrorHandler.Succeeded(_build.FindActiveProjectCfg(IntPtr.Zero, IntPtr.Zero, hierarchy, configurationSlot)) && configurationSlot[0] != null
                    && ErrorHandler.Succeeded(configurationSlot[0].get_CanonicalName(out string canonical)))
                {
                    int separator = canonical.IndexOf('|');
                    if (separator > 0) { globals["Configuration"] = canonical.Substring(0, separator); globals["Platform"] = canonical.Substring(separator + 1); }
                }
                string unavailable = state != null && state.Status == ProjectAnalysisStatus.Pending ? "Project inputs are refreshing. Wait for the active context." : null;
                if (state != null && (!string.Equals(state.Project.Context.Configuration, globals["Configuration"], StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(state.Project.Context.Platform, globals["Platform"], StringComparison.OrdinalIgnoreCase)))
                    unavailable = "The active project configuration is changing. Wait for the current project context.";
                if (hierarchy != null && ErrorHandler.Succeeded(_solution.GetGuidOfProject(hierarchy, out Guid id)))
                    mappings.Add(new XElement("ProjectConfiguration", new XAttribute("Project", id.ToString("B")), new XAttribute("AbsolutePath", path),
                        new XAttribute("BuildProjectInSolution", context.ShouldBuild), globals["Configuration"] + "|" + globals["Platform"]));
                else coverage.Add("Dependency configuration mapping unavailable: " + path);
                selections[path] = new ReferenceProjectSelection(path, globals, liveProject: state?.Project, unavailable: unavailable);
                if (!context.ShouldBuild) coverage.Add("Excluded from the active solution build: " + path + " [" + context.ConfigurationName + "|" + context.PlatformName + "]; saved reference inputs remain included.");
            }
            // Inventory includes unloaded hierarchies. A project without an active
            // mapping is a coverage gap, never silently assigned Debug/AnyCPU.
            Guid all = Guid.Empty;
            if (ErrorHandler.Succeeded(_solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_ALLINSOLUTION, ref all, out var projects)))
            {
                var item = new IVsHierarchy[1];
                while (projects.Next(1, item, out uint fetched) == VSConstants.S_OK && fetched == 1)
                {
                    token.ThrowIfCancellationRequested();
                    if (!ErrorHandler.Succeeded(item[0].GetCanonicalName(VSConstants.VSITEMID_ROOT, out string moniker)))
                    {
                        if (!ErrorHandler.Succeeded(_solution.GetUniqueNameOfProject(item[0], out string unique))) continue;
                        moniker = ResolveProjectPath(directory, unique);
                    }
                    if (!IsApplicationProject(moniker)) continue;
                    string path = ResolveProjectPath(directory, moniker);
                    if (!selections.ContainsKey(path)) selections[path] = new ReferenceProjectSelection(path, null, unavailable: "No active solution configuration mapping is available.");
                }
            }
            foreach (SolutionConfiguration configuration in dte.Solution.SolutionBuild.SolutionConfigurations)
            {
                token.ThrowIfCancellationRequested();
                if (string.Equals(ConfigurationKey(configuration), activeKey, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (SolutionContext context in configuration.SolutionContexts)
                {
                    token.ThrowIfCancellationRequested();
                    string path = ResolveProjectPath(directory, context.ProjectName);
                    if (IsApplicationProject(path)) coverage.Add("Excluded configuration: " + path + " [" + context.ConfigurationName + "|" + context.PlatformName + "]; only the active project configuration is searched.");
                }
            }
            // A CPS snapshot may still describe the previous solution mapping
            // even when this project's own configuration name stayed the same.
            // Use the current native mappings for loaded and unloaded consumers.
            string configurationContents = new XElement("SolutionConfiguration", mappings).ToString(SaveOptions.DisableFormatting);
            foreach (string path in selections.Keys.ToArray())
            {
                var selection = selections[path];
                if (selection.Unavailable != null) continue;
                var globals = selection.Globals.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                globals["CurrentSolutionConfigurationContents"] = configurationContents;
                selections[path] = new ReferenceProjectSelection(path, globals, selection.UseOpenBuffers, selection.LiveProject);
            }
            token.ThrowIfCancellationRequested();
            return new ReferenceCatalogueSnapshot(solutionPath, activeKey, selections.Values, coverage.Distinct(StringComparer.Ordinal));
        }

        private static string ConfigurationKey(SolutionConfiguration configuration)
        { ThreadHelper.ThrowIfNotOnUIThread(); return configuration.Name + "|" + (configuration as EnvDTE80.SolutionConfiguration2)?.PlatformName; }
        private static string ResolveProjectPath(string directory, string name) => Path.GetFullPath(Path.IsPathRooted(name) ? name : Path.Combine(directory, name));
        private static bool IsApplicationProject(string path) => !string.IsNullOrWhiteSpace(path) && string.Equals(Path.GetExtension(path), ".almproj", StringComparison.OrdinalIgnoreCase);
        private void EnsureEvents()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_solution != null) return;
            var solution = _services.GetService(typeof(SVsSolution)) as IVsSolution ?? throw new InvalidOperationException("The Visual Studio solution service is unavailable.");
            var build = _services.GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager ?? throw new InvalidOperationException("The solution configuration service is unavailable.");
            uint solutionCookie = 0, buildCookie = 0;
            try
            {
                ErrorHandler.ThrowOnFailure(solution.AdviseSolutionEvents(this, out solutionCookie));
                ErrorHandler.ThrowOnFailure(build.AdviseUpdateSolutionEvents(this, out buildCookie));
            }
            catch
            {
                if (solutionCookie != 0) solution.UnadviseSolutionEvents(solutionCookie);
                throw;
            }
            _solution = solution; _build = build; _solutionCookie = solutionCookie; _buildCookie = buildCookie;
        }
        private void OnRegistryChanged(object sender, EventArgs args) => Changed?.Invoke(this, EventArgs.Empty);
        private int Invalidate() { if (!_disposed) Changed?.Invoke(this, EventArgs.Empty); return VSConstants.S_OK; }
        public int OnAfterOpenProject(IVsHierarchy hierarchy, int added) => Invalidate();
        public int OnAfterLoadProject(IVsHierarchy stub, IVsHierarchy real) => Invalidate();
        public int OnBeforeUnloadProject(IVsHierarchy real, IVsHierarchy stub) => Invalidate();
        public int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed) => Invalidate();
        public int OnAfterOpenSolution(object reserved, int newSolution) => Invalidate();
        public int OnBeforeCloseSolution(object reserved) => Invalidate();
        public int OnAfterCloseSolution(object reserved) => Invalidate();
        public int OnQueryCloseProject(IVsHierarchy hierarchy, int removing, ref int cancel) => VSConstants.S_OK;
        public int OnQueryUnloadProject(IVsHierarchy hierarchy, ref int cancel) => VSConstants.S_OK;
        public int OnQueryCloseSolution(object reserved, ref int cancel) => VSConstants.S_OK;
        public int OnActiveProjectCfgChange(IVsHierarchy hierarchy) => Invalidate();
        public int UpdateSolution_Begin(ref int cancel) => VSConstants.S_OK;
        public int UpdateSolution_Done(int succeeded, int modified, int cancelled) => VSConstants.S_OK;
        public int UpdateSolution_StartUpdate(ref int cancel) => VSConstants.S_OK;
        public int UpdateSolution_Cancel() => VSConstants.S_OK;
        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_disposed) return;
            _disposed = true; _registry.Changed -= OnRegistryChanged;
            if (_solutionCookie != 0) _solution?.UnadviseSolutionEvents(_solutionCookie);
            if (_buildCookie != 0) _build?.UnadviseUpdateSolutionEvents(_buildCookie);
            _solutionCookie = _buildCookie = 0;
        }
    }
}
