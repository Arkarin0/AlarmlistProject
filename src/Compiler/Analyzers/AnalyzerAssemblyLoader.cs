// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Alarmlist.Diagnostics;

namespace Alarmlist.Analyzers
{
    /// <summary>Loads trusted analyzer code from captured bytes; dispose after all compilation results have been copied.</summary>
    public sealed class AnalyzerAssemblyLoader : IDisposable
    {
        private readonly List<Extensibility.CapturedAssemblyScope> _scopes = new List<Extensibility.CapturedAssemblyScope>();
        private readonly List<IAlarmAnalyzer> _instances = new List<IAlarmAnalyzer>();
        private readonly List<MetadataSource> _metadataSources = new List<MetadataSource>();
        private IReadOnlyList<IAlarmAnalyzer> _analyzers;
        public IReadOnlyList<IAlarmAnalyzer> Analyzers => _analyzers ?? throw new ObjectDisposedException(nameof(AnalyzerAssemblyLoader));

        public AnalyzerAssemblyLoader(IEnumerable<AnalyzerAssemblyReference> references)
        {
            if (references == null) throw new ArgumentNullException(nameof(references));
            var analyzers = new List<IAlarmAnalyzer>();
            foreach (AnalyzerAssemblyReference reference in references.ToArray())
            {
                string path = reference?.FilePath ?? "<null>";
                string identity = null;
                try
                {
                    if (reference == null) throw new ArgumentException("An analyzer reference must not be null.");
                    var scope = new Extensibility.CapturedAssemblyScope(reference.Images.Select(image => new KeyValuePair<string, byte[]>(image.FilePath, image.GetImage())), "Analyzer");
                    _scopes.Add(scope);
                    Assembly assembly = scope.LoadRoot(reference.FilePath);
                    identity = assembly.FullName;
                    Type[] types = assembly.GetExportedTypes().Where(type => !type.IsAbstract && !type.ContainsGenericParameters
                        && typeof(IAlarmAnalyzer).IsAssignableFrom(type)).OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
                    if (types.Length == 0) throw new NotSupportedException("No public IAlarmAnalyzer implementation was found.");
                    var created = new List<IAlarmAnalyzer>();
                    foreach (Type type in types)
                    {
                        var instance = (IAlarmAnalyzer)Activator.CreateInstance(type);
                        _instances.Add(instance);
                        created.Add(instance);
                    }
                    analyzers.AddRange(created);
                    _metadataSources.Add(new MetadataSource(path, identity, created, null));
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
                    analyzers.Add(new UnavailableAnalyzer(path + ": " + exception.Message));
                    _metadataSources.Add(new MetadataSource(path, identity, Array.Empty<IAlarmAnalyzer>(), exception.Message));
                }
            }
            _analyzers = analyzers.AsReadOnly();
        }

        /// <summary>Reads declarations without calling Analyze. Returned data remains usable after disposal.</summary>
        public IReadOnlyList<AnalyzerAssemblyMetadata> GetMetadata()
        {
            if (_analyzers == null) throw new ObjectDisposedException(nameof(AnalyzerAssemblyLoader));
            var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return Array.AsReadOnly(_metadataSources.Select(source => AnalyzerAssemblyMetadata.Capture(
                source.Path, source.Identity, source.Analyzers, owners, source.Failure)).ToArray());
        }

        public void Dispose()
        {
            if (_analyzers == null) return;
            Exception failure = null;
            try
            {
                foreach (IAlarmAnalyzer analyzer in _instances)
                {
                    try { if (analyzer is IDisposable disposable) disposable.Dispose(); }
                    catch (Exception exception) when (IsRecoverable(exception)) { failure = failure ?? exception; }
                }
            }
            finally
            {
                _analyzers = null;
                try
                {
                    foreach (Extensibility.CapturedAssemblyScope scope in _scopes)
                    {
                        try { scope.Dispose(); }
                        catch (Exception exception) when (IsRecoverable(exception)) { failure = failure ?? exception; }
                    }
                }
                finally
                {
                    _scopes.Clear();
                    _instances.Clear();
                    _metadataSources.Clear();
                }
            }
            if (failure != null) throw new InvalidOperationException("Analyzer disposal failed: " + failure.Message, failure);
        }

        internal static bool IsRecoverable(Exception exception) => Extensibility.CapturedAssemblyScope.IsRecoverable(exception);

        private sealed class MetadataSource
        {
            internal readonly string Path, Identity, Failure;
            internal readonly IReadOnlyList<IAlarmAnalyzer> Analyzers;
            internal MetadataSource(string path, string identity, IReadOnlyList<IAlarmAnalyzer> analyzers, string failure)
            { Path = path; Identity = identity; Analyzers = analyzers; Failure = failure; }
        }

        private sealed class UnavailableAnalyzer : IAlarmAnalyzer
        {
            private readonly string _message;
            internal UnavailableAnalyzer(string message) { _message = message; }
            public int ContractVersion => AlarmAnalyzerContext.CurrentContractVersion;
            public IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics => Array.Empty<DiagnosticDescriptor>();
            public IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken) => throw new NotSupportedException(_message);
        }
    }
}
