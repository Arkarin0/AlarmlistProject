// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Analysis;
using Microsoft.VisualStudio.Shell.TableManager;

namespace Alarmlist.VisualStudio.Diagnostics
{
    [Export]
    internal sealed class LiveDiagnosticTableSource : ITableDataSource, ITableEntriesSnapshotFactory, IDisposable
    {
        public const string SourceIdentifier = "Alarmlist.LiveDiagnostics";
        public const string EntryKey = "Alarmlist.LiveDiagnosticEntry";
        private readonly object _gate = new object();
        // Serialize sink callbacks separately from state reads. Callbacks may read
        // GetCurrentSnapshot on another thread; never hold _gate while calling them.
        private readonly object _notifications = new object();
        private readonly ITableManager _manager;
        private readonly HashSet<ProjectAnalysisService> _projects = new HashSet<ProjectAnalysisService>();
        private readonly HashSet<ITableDataSink> _sinks = new HashSet<ITableDataSink>();
        private LiveDiagnosticTableSnapshot _snapshot = new LiveDiagnosticTableSnapshot(0, Array.Empty<LiveDiagnosticTableEntry>());
        private bool _stable = true;
        private bool _disposed;
        public string Identifier => SourceIdentifier;
        public string DisplayName => "Alarmlist live analysis";
        public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;
        public int CurrentVersionNumber { get { lock (_gate) return _snapshot.VersionNumber; } }

        [ImportingConstructor]
        public LiveDiagnosticTableSource(ITableManagerProvider provider) : this(provider.GetTableManager(StandardTables.ErrorsTable)) { }

        internal LiveDiagnosticTableSource(ITableManager manager)
        {
            _manager = manager;
            _manager.AddSource(this, StandardTableKeyNames.ErrorSeverity, StandardTableKeyNames.ErrorCode,
                StandardTableKeyNames.Text, StandardTableKeyNames.ProjectName, StandardTableKeyNames.DocumentName,
                StandardTableKeyNames.Line, StandardTableKeyNames.Column, StandardTableKeyNames.ErrorSource, StandardTableKeyNames.BuildTool);
        }

        public IDisposable Attach(ProjectAnalysisService project)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(LiveDiagnosticTableSource));
                if (!_projects.Add(project)) throw new InvalidOperationException("The project already publishes live diagnostics.");
                project.Changed += OnChanged;
            }
            Update();
            return new Registration(() =>
            {
                lock (_gate) { _projects.Remove(project); project.Changed -= OnChanged; }
                Update();
            });
        }

        private void OnChanged(object sender, EventArgs args) => Update();

        private void Update()
        {
            lock (_gate)
            {
                if (_disposed) return;
                var states = _projects.Select(project => new { Project = project, State = project.Current }).ToArray();
                _stable = states.All(item => item.State.Status != ProjectAnalysisStatus.Pending);
                _snapshot = new LiveDiagnosticTableSnapshot(_snapshot.VersionNumber + 1, states
                    .OrderBy(item => item.State.Project?.ProjectPath, StringComparer.OrdinalIgnoreCase)
                    .SelectMany(item => LiveDiagnostic.Create(item.State).Select(diagnostic => new LiveDiagnosticTableEntry(this, item.Project, diagnostic))).ToArray());
            }
            lock (_notifications)
            {
                ITableDataSink[] sinks;
                bool stable;
                lock (_gate) { if (_disposed) return; sinks = _sinks.ToArray(); stable = _stable; }
                foreach (ITableDataSink sink in sinks) { sink.IsStable = stable; sink.FactorySnapshotChanged(this); }
            }
        }

        public IDisposable Subscribe(ITableDataSink sink)
        {
            lock (_notifications)
            {
                bool stable;
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(LiveDiagnosticTableSource));
                    _sinks.Add(sink);
                    stable = _stable;
                }
                sink.AddFactory(this, true);
                sink.IsStable = stable;
            }
            return new Registration(() => { lock (_notifications) { lock (_gate) _sinks.Remove(sink); } });
        }

        internal bool IsCurrent(LiveDiagnosticTableEntry entry)
        {
            lock (_gate) return !_disposed && _projects.Contains(entry.Project) && ReferenceEquals(entry.Project.Current, entry.Diagnostic.State);
        }

        public ITableEntriesSnapshot GetCurrentSnapshot() { lock (_gate) return _snapshot; }
        public ITableEntriesSnapshot GetSnapshot(int versionNumber)
        {
            lock (_gate) return _snapshot.VersionNumber == versionNumber ? _snapshot : null;
        }

        public void Dispose()
        {
            lock (_notifications)
            {
                ITableDataSink[] sinks;
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    foreach (ProjectAnalysisService project in _projects) project.Changed -= OnChanged;
                    _projects.Clear();
                    _snapshot = new LiveDiagnosticTableSnapshot(_snapshot.VersionNumber + 1, Array.Empty<LiveDiagnosticTableEntry>());
                    sinks = _sinks.ToArray();
                    _sinks.Clear();
                }
                foreach (ITableDataSink sink in sinks) { sink.IsStable = true; sink.RemoveFactory(this); }
                _manager.RemoveSource(this);
            }
        }

        private sealed class Registration : IDisposable
        {
            private Action _dispose;
            public Registration(Action dispose) { _dispose = dispose; }
            public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }

    internal sealed class LiveDiagnosticTableEntry
    {
        private readonly LiveDiagnosticTableSource _source;
        public ProjectAnalysisService Project { get; }
        public LiveDiagnostic Diagnostic { get; }
        public LiveDiagnosticTableEntry(LiveDiagnosticTableSource source, ProjectAnalysisService project, LiveDiagnostic diagnostic)
        { _source = source; Project = project; Diagnostic = diagnostic; }

        public async Task<bool> TryNavigateAsync(IProjectAnalysisInputSource inputs, Action<AnalysisLocation, string> navigate, CancellationToken token)
            => await TryNavigateAsync(inputs, target => navigate(target.Location, target.Text), token);

        public async Task<bool> TryNavigateAsync(IProjectAnalysisInputSource inputs, Action<AnalysisNavigationTarget> navigate, CancellationToken token)
        {
            if (!_source.IsCurrent(this) || !Diagnostic.TryGetNavigationTarget(Diagnostic.Location, out AnalysisNavigationTarget target)) return false;
            if (!await ProjectAnalysisValidation.InputsMatchAsync(inputs, Diagnostic.State, token) || !_source.IsCurrent(this)) return false;
            token.ThrowIfCancellationRequested();
            navigate(target);
            return true;
        }
    }
}
