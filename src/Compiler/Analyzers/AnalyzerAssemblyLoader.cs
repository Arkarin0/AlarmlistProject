// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Alarmlist.Diagnostics;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace Alarmlist.Analyzers
{
    /// <summary>Loads trusted analyzer code from captured bytes; dispose after all compilation results have been copied.</summary>
    public sealed class AnalyzerAssemblyLoader : IDisposable
    {
        private readonly List<Scope> _scopes = new List<Scope>();
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
                    var scope = new Scope(reference);
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
                    foreach (Scope scope in _scopes)
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

        internal static bool IsRecoverable(Exception exception) => !(exception is OutOfMemoryException) && !(exception is StackOverflowException)
            && !(exception is AccessViolationException) && !(exception is AppDomainUnloadedException) && !(exception is ThreadAbortException);

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

        private sealed class Scope : IDisposable
        {
            private readonly Dictionary<string, byte[]> _images;
#if NET8_0_OR_GREATER
            private readonly LoadContext _context;
#else
            private static readonly object s_cacheGate = new object();
            private static readonly Dictionary<string, Assembly> s_cache = new Dictionary<string, Assembly>(StringComparer.Ordinal);
            private static readonly Dictionary<string, string> s_imageIdentities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<Assembly> _owned = new HashSet<Assembly>();
            private readonly string _identity;
#endif
            internal Scope(AnalyzerAssemblyReference reference)
            {
                _images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (AnalyzerAssemblyImage image in reference.Images)
                {
                    string name = Path.GetFileNameWithoutExtension(image.FilePath);
                    if (name == typeof(IAlarmAnalyzer).Assembly.GetName().Name) continue;
                    if (_images.ContainsKey(name)) throw new ArgumentException("Duplicate analyzer dependency name '" + name + "'.");
                    _images.Add(name, image.GetImage());
                }
#if NET8_0_OR_GREATER
                _context = new LoadContext(_images);
#else
                using (var hash = SHA256.Create())
                using (var contents = new MemoryStream())
                {
                    var identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pair in _images.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        byte[] name = Encoding.UTF8.GetBytes(pair.Key + "\0");
                        contents.Write(name, 0, name.Length);
                        byte[] digest = hash.ComputeHash(pair.Value);
                        identities.Add(pair.Key, Convert.ToBase64String(digest));
                        contents.Write(digest, 0, digest.Length);
                    }
                    _identity = Convert.ToBase64String(hash.ComputeHash(contents.ToArray()));
                    // The desktop CLR can reuse bindings by assembly identity even for
                    // byte-loaded copies. Never let a changed dependency run stale code.
                    // VS renews its SDK domain; a desktop build must renew its node.
                    lock (s_cacheGate)
                    {
                        foreach (var pair in identities)
                            if (s_imageIdentities.TryGetValue(pair.Key, out string previous) && previous != pair.Value)
                                throw new NotSupportedException("Analyzer assembly '" + pair.Key
                                    + "' changed or conflicts with an assembly already captured in this .NET Framework process. Reload the analysis AppDomain or restart desktop MSBuild with /nodeReuse:false.");
                        foreach (var pair in identities) s_imageIdentities[pair.Key] = pair.Value;
                    }
                }
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
#endif
            }

            internal Assembly LoadRoot(string path) => Load(Path.GetFileNameWithoutExtension(path))
                ?? throw new FileNotFoundException("The analyzer image was not captured: " + path);

            private Assembly Load(string name)
            {
                if (!_images.TryGetValue(name, out byte[] image)) return null;
#if NET8_0_OR_GREATER
                using (var stream = new MemoryStream(image, false)) return _context.LoadFromStream(stream);
#else
                Assembly assembly;
                lock (s_cacheGate)
                {
                    string key = _identity + ":" + name;
                    if (!s_cache.TryGetValue(key, out assembly)) s_cache.Add(key, assembly = Assembly.Load(image));
                }
                lock (_owned) _owned.Add(assembly);
                return assembly;
#endif
            }

#if !NET8_0_OR_GREATER
            private Assembly Resolve(object sender, ResolveEventArgs args)
            {
                lock (_owned)
                    if (args.RequestingAssembly == null || !_owned.Contains(args.RequestingAssembly)) return null;
                string name = new AssemblyName(args.Name).Name;
                if (name == typeof(IAlarmAnalyzer).Assembly.GetName().Name) return typeof(IAlarmAnalyzer).Assembly;
                return Load(name);
            }
#endif
            public void Dispose()
            {
                try
                {
#if NET8_0_OR_GREATER
                    try { _context.Unload(); }
                    catch (Exception exception) when (IsRecoverable(exception))
                    {
                        // .NET 8 clears Unloading handlers and suppresses finalization
                        // before invoking callbacks. A thrown callback can therefore
                        // prevent native release; retry once and retain the failure.
                        try { _context.Unload(); }
                        catch (Exception retry) when (IsRecoverable(retry)) { }
                        throw;
                    }
#else
                    AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
                    lock (_owned) _owned.Clear();
#endif
                }
                finally { _images.Clear(); }
            }
        }

#if NET8_0_OR_GREATER
        private sealed class LoadContext : AssemblyLoadContext
        {
            private readonly Dictionary<string, byte[]> _images;
            internal LoadContext(Dictionary<string, byte[]> images) : base(isCollectible: true) { _images = images; }
            protected override Assembly Load(AssemblyName name)
            {
                if (name.Name == typeof(IAlarmAnalyzer).Assembly.GetName().Name) return typeof(IAlarmAnalyzer).Assembly;
                if (!_images.TryGetValue(name.Name, out byte[] image)) return null;
                using (var stream = new MemoryStream(image, false)) return LoadFromStream(stream);
            }
        }
#endif
    }
}
