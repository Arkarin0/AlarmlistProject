// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Alarmlist.VisualStudio.Analysis
{
    internal enum AnalysisAvailability { Available, SdkUnavailable, UnsupportedContract, LoadFailed, InvalidInput, AnalysisFailed }

    [Serializable]
    internal sealed class AnalysisInput
    {
        public string Text { get; }
        public string FilePath { get; }
        public int Version { get; }

        public AnalysisInput(string text, string filePath, int version)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A source path is required.", nameof(filePath));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            FilePath = filePath;
            Version = version;
        }
    }

    [Serializable]
    internal sealed class AnalysisResponse
    {
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public AnalysisSnapshot Snapshot { get; }
        public string CompiledXml { get; }
        public string XmlPreviewMessage { get; }

        public AnalysisResponse(AnalysisAvailability availability, string message, AnalysisSnapshot snapshot = null,
            string compiledXml = null, string xmlPreviewMessage = null)
        {
            Availability = availability; Message = message; Snapshot = snapshot;
            CompiledXml = availability == AnalysisAvailability.Available && snapshot?.Success == true ? compiledXml : null;
            XmlPreviewMessage = xmlPreviewMessage;
        }
    }

    internal sealed class BridgeOpenResult
    {
        public AnalysisAvailability Availability { get; }
        public string Message { get; }
        public ISdkAnalysisBridge Bridge { get; }

        public BridgeOpenResult(AnalysisAvailability availability, string message, ISdkAnalysisBridge bridge = null)
        { Availability = availability; Message = message; Bridge = bridge; }
    }

    internal interface ISdkAnalysisBridgeFactory
    {
        BridgeOpenResult Open(string evaluatedCompilerPath);
    }

    internal interface ISdkAnalysisBridge : IDisposable
    {
        string CompilerPath { get; }
        string CompilerIdentity { get; }
        AnalysisResponse Analyze(IEnumerable<AnalysisInput> inputs, CancellationToken cancellationToken = default, AnalysisContext context = null);
    }

    [Export(typeof(ISdkAnalysisBridgeFactory))]
    internal sealed class SdkAnalysisBridgeFactory : ISdkAnalysisBridgeFactory
    {
        public BridgeOpenResult Open(string evaluatedCompilerPath) => SdkAnalysisBridge.Open(evaluatedCompilerPath);
    }

    /// <summary>Owned by one project/configuration. Call off the UI thread and dispose on SDK change or project unload.</summary>
    internal sealed class SdkAnalysisBridge : ISdkAnalysisBridge
    {
        private readonly object _gate = new object();
        private AppDomain _domain;
        private readonly SdkAnalysisWorker _worker;
        public string CompilerPath { get; }
        public string CompilerIdentity { get; }

        private SdkAnalysisBridge(AppDomain domain, SdkAnalysisWorker worker, string compilerPath, string identity)
        { _domain = domain; _worker = worker; CompilerPath = compilerPath; CompilerIdentity = identity; }

        internal static BridgeOpenResult Open(string evaluatedCompilerPath)
        {
            if (string.IsNullOrWhiteSpace(evaluatedCompilerPath))
                return new BridgeOpenResult(AnalysisAvailability.SdkUnavailable,
                    "The project has no AlarmlistAnalysisCompilerAssembly property. Restore a matching Alarmlist.MSBuild.SDK with analysis contract 3.");

            AppDomain domain = null;
            try
            {
                string root = Path.GetPathRoot(evaluatedCompilerPath);
                if (string.IsNullOrEmpty(root) || root == "\\" || root == "/" || root.EndsWith(":", StringComparison.Ordinal))
                    return new BridgeOpenResult(AnalysisAvailability.SdkUnavailable, "The evaluated analysis compiler path must be absolute: " + evaluatedCompilerPath);
                string path = Path.GetFullPath(evaluatedCompilerPath);
                if (!File.Exists(path))
                    return new BridgeOpenResult(AnalysisAvailability.SdkUnavailable, "Restore the project's Alarmlist.MSBuild.SDK. Its analysis compiler was not found: " + path);

                string hostAssembly = typeof(SdkAnalysisWorker).Assembly.Location;
                domain = AppDomain.CreateDomain("Alarmlist analysis " + Guid.NewGuid().ToString("N"), null, new AppDomainSetup
                {
                    ApplicationBase = Path.GetDirectoryName(hostAssembly),
                    ConfigurationFile = hostAssembly + ".config",
                    LoaderOptimization = LoaderOptimization.SingleDomain,
                    DisallowBindingRedirects = true,
                    DisallowCodeDownload = true
                });
                SdkAnalysisWorker worker = WithHostAssemblyResolution(() =>
                    (SdkAnalysisWorker)domain.CreateInstanceFromAndUnwrap(hostAssembly, typeof(SdkAnalysisWorker).FullName));
                AnalysisResponse initialized = WithHostAssemblyResolution(() => worker.Initialize(path));
                if (initialized.Availability != AnalysisAvailability.Available)
                    return new BridgeOpenResult(initialized.Availability, initialized.Message);

                var bridge = new SdkAnalysisBridge(domain, worker, path, worker.CompilerIdentity);
                domain = null; // Ownership transfers only after successful initialization.
                return new BridgeOpenResult(AnalysisAvailability.Available, null, bridge);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                return new BridgeOpenResult(AnalysisAvailability.LoadFailed,
                    "Unable to load the project's analysis compiler at " + evaluatedCompilerPath + ": " + exception.Message);
            }
            finally
            {
                if (domain != null) AppDomain.Unload(domain);
            }
        }

        public AnalysisResponse Analyze(IEnumerable<AnalysisInput> inputs, CancellationToken cancellationToken = default, AnalysisContext context = null)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            cancellationToken.ThrowIfCancellationRequested();
            AnalysisInput[] snapshots = inputs.ToArray();
            if (snapshots.Any(input => input == null)) throw new ArgumentException("An input must not be null.", nameof(inputs));
            lock (_gate)
            {
                if (_domain == null) throw new ObjectDisposedException(nameof(SdkAnalysisBridge));
                cancellationToken.ThrowIfCancellationRequested();
                AnalysisResponse response;
                try { response = WithHostAssemblyResolution(() => _worker.Analyze(snapshots, context, new AnalysisCancellationProbe(cancellationToken))); }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    response = new AnalysisResponse(AnalysisAvailability.AnalysisFailed, "SDK analysis failed: " + exception.Message);
                }
                // Cooperative cancellation crosses through a host-owned probe; never abort analyzer threads.
                cancellationToken.ThrowIfCancellationRequested();
                return response;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_domain == null) return;
                AppDomain.Unload(_domain);
                _domain = null;
            }
        }

        private static T WithHostAssemblyResolution<T>(Func<T> operation)
        {
            // VS loads the extension outside its application directory. Remoting
            // resolves proxy/DTO types by assembly name, so direct path loading
            // alone is insufficient. Resolve only our already loaded host image,
            // and only for the duration of a boundary call; never resolve an SDK
            // assembly into the VS domain.
            Assembly assembly = typeof(SdkAnalysisWorker).Assembly;
            ResolveEventHandler resolve = (sender, args) => args.Name == assembly.FullName ? assembly : null;
            AppDomain.CurrentDomain.AssemblyResolve += resolve;
            try { return operation(); }
            finally { AppDomain.CurrentDomain.AssemblyResolve -= resolve; }
        }

        internal static bool IsRecoverable(Exception exception)
        {
            return !(exception is OutOfMemoryException) && !(exception is StackOverflowException)
                && !(exception is ThreadAbortException) && !(exception is AccessViolationException);
        }
    }

    internal sealed class AnalysisCancellationProbe : MarshalByRefObject
    {
        private readonly CancellationToken _token;
        internal AnalysisCancellationProbe(CancellationToken token) { _token = token; }
        public bool IsCancellationRequested => _token.IsCancellationRequested;
        public override object InitializeLifetimeService() => null;
    }
}
