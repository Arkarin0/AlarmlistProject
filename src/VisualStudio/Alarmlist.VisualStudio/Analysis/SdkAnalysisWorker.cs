// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Xml;

namespace Alarmlist.VisualStudio.Analysis
{
    // Activated only in the isolated domain. Never return reflection objects,
    // SDK instances, or SDK exceptions across the remoting boundary.
    internal sealed class SdkAnalysisWorker : MarshalByRefObject
    {
        private Type _snapshotType;
        private Type _resourceType;
        private Type _lookupType;
        private Type _editorConfigType;
        private Type _analyzerConfigurationType;
        private Type _diagnosticOptionsType;
        private MethodInfo _withDiagnosticOptions;
        private Type _analyzerImageType;
        private Type _analyzerReferenceType;
        private Type _analyzerLoaderType;
        private MethodInfo _withAnalyzers;
        private MethodInfo _inferResourceCulture;
        private Type _contextType;
        private Type _propertyType;
        private object _compiler;
        private MethodInfo _analyze;
        private PropertyInfo _compiledXml;
        private AnalysisContractReader _reader;
        public string CompilerIdentity { get; private set; }

        public SdkAnalysisWorker() { }

        public override object InitializeLifetimeService() => null;

        public AnalysisResponse Initialize(string compilerPath)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(compilerPath);
                if (!string.Equals(Path.GetFullPath(assembly.Location), compilerPath, StringComparison.OrdinalIgnoreCase)
                    || assembly.GetName().Name != "Alarmlist.Core")
                    throw new NotSupportedException("The requested SDK compiler identity/path was not loaded.");
                string framework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
                if (framework == null || new FrameworkName(framework).Identifier != ".NETFramework")
                    throw new NotSupportedException("The Visual Studio bridge requires the SDK's .NET Framework compiler.");

                _reader = new AnalysisContractReader(assembly);
                _snapshotType = assembly.GetType("Alarmlist.Text.AlmxTextSnapshot", true);
                if (_snapshotType.GetConstructor(new[] { typeof(string), typeof(string), typeof(int) }) == null)
                    throw new NotSupportedException("The SDK snapshot constructor is incompatible.");
                Type compilerType = assembly.GetType("Alarmlist.Compiler.AlarmCompiler", true);
                _resourceType = assembly.GetType("Alarmlist.Text.ResxTextSnapshot", true);
                _lookupType = assembly.GetType("Alarmlist.Text.LookupTextSnapshot", true);
                if (_lookupType.GetConstructor(new[] { typeof(string), typeof(string), typeof(int) }) == null)
                    throw new NotSupportedException("The SDK lookup input contract is incompatible.");
                _inferResourceCulture = assembly.GetType("Alarmlist.Localization.LocalizationCulture", true)
                    .GetMethod("InferResourceCulture", new[] { typeof(string) });
                if (_resourceType.GetConstructor(new[] { typeof(string), typeof(string), typeof(int), typeof(string) }) == null
                    || _inferResourceCulture == null || !_inferResourceCulture.IsStatic || _inferResourceCulture.ReturnType != typeof(string))
                    throw new NotSupportedException("The SDK resource input contract is incompatible.");
                _contextType = assembly.GetType("Alarmlist.Compiler.CompilationContext", true);
                _editorConfigType = assembly.GetType("Alarmlist.Text.EditorConfigTextSnapshot", true);
                _analyzerConfigurationType = assembly.GetType("Alarmlist.Configuration.AnalyzerConfiguration", true);
                _diagnosticOptionsType = assembly.GetType("Alarmlist.Diagnostics.DiagnosticOptions", true);
                _analyzerImageType = assembly.GetType("Alarmlist.Analyzers.AnalyzerAssemblyImage", true);
                _analyzerReferenceType = assembly.GetType("Alarmlist.Analyzers.AnalyzerAssemblyReference", true);
                _analyzerLoaderType = assembly.GetType("Alarmlist.Analyzers.AnalyzerAssemblyLoader", true);
                _withAnalyzers = _contextType.GetMethod("WithAnalyzers", new[] { typeof(IEnumerable<>).MakeGenericType(assembly.GetType("Alarmlist.Analyzers.IAlarmAnalyzer", true)) });
                if (_withAnalyzers == null || _withAnalyzers.ReturnType != _contextType || !typeof(IDisposable).IsAssignableFrom(_analyzerLoaderType))
                    throw new NotSupportedException("The SDK has no compatible analyzer loading contract.");
                _withDiagnosticOptions = _contextType.GetMethod("WithDiagnosticOptions", new[] { _diagnosticOptionsType });
                if (_editorConfigType.GetConstructor(new[] { typeof(string), typeof(string), typeof(int) }) == null
                    || _analyzerConfigurationType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(_editorConfigType) }) == null
                    || _diagnosticOptionsType.GetConstructor(new[] { _analyzerConfigurationType, typeof(bool), typeof(IEnumerable<string>), typeof(IEnumerable<string>), typeof(IEnumerable<string>) }) == null
                    || _withDiagnosticOptions == null || _withDiagnosticOptions.ReturnType != _contextType)
                    throw new NotSupportedException("The SDK has no compatible captured diagnostic configuration contract.");
                _propertyType = assembly.GetType("Alarmlist.Compiler.PropertyDefinition", true);
                if (_contextType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(_resourceType), typeof(IEnumerable<>).MakeGenericType(_lookupType), typeof(string),
                    typeof(string), typeof(string), typeof(string), typeof(IEnumerable<>).MakeGenericType(_propertyType) }) == null
                    || _propertyType.GetConstructor(new[] { typeof(string), typeof(string), assembly.GetType("Alarmlist.Text.SourceLocation", true) }) == null)
                    throw new NotSupportedException("The SDK context constructors are incompatible.");
                _analyze = compilerType.GetMethod("AnalyzeSnapshots", new[] { typeof(IEnumerable<>).MakeGenericType(_snapshotType), _contextType, typeof(bool), typeof(CancellationToken) });
                if (_analyze == null || _analyze.IsStatic || _analyze.ReturnType != assembly.GetType("Alarmlist.Analysis.AnalysisResult"))
                    throw new NotSupportedException("The SDK has no compatible AnalyzeSnapshots method.");
                PropertyInfo xml = _analyze.ReturnType.GetProperty("CompiledXml", BindingFlags.Public | BindingFlags.Instance);
                if (xml?.PropertyType != typeof(string) || xml.GetGetMethod() == null || xml.GetIndexParameters().Length != 0)
                    throw new NotSupportedException("The SDK has no compatible compiled XML preview.");
                _compiledXml = xml;
                _compiler = Activator.CreateInstance(compilerType);
                CompilerIdentity = assembly.FullName;
                // Prove that the method can execute before advertising availability.
                return Analyze(Array.Empty<AnalysisInput>());
            }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
            {
                Exception failure = Unwrap(exception);
                AnalysisAvailability state = failure is NotSupportedException || failure is TypeLoadException || failure is MissingMemberException
                    ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.LoadFailed;
                return new AnalysisResponse(state, "Cannot use analysis compiler " + compilerPath
                    + ". Restore an SDK supporting analysis contract 3 with captured diagnostic configuration and external analyzers. " + failure.Message);
            }
        }

        public AnalysisResponse Analyze(AnalysisInput[] inputs, AnalysisContext context = null, AnalysisCancellationProbe cancellation = null)
        {
            using (var cancellationScope = new WorkerCancellation(cancellation))
                return AnalyzeCore(inputs, context, cancellationScope.Token);
        }

        private AnalysisResponse AnalyzeCore(AnalysisInput[] inputs, AnalysisContext context, CancellationToken token)
        {
            try
            {
                Array snapshots = Array.CreateInstance(_snapshotType, inputs.Length);
                for (int i = 0; i < inputs.Length; i++)
                    snapshots.SetValue(Activator.CreateInstance(_snapshotType, inputs[i].Text, inputs[i].FilePath, inputs[i].Version), i);
                context = context ?? AnalysisContext.Empty;
                Array properties = Array.CreateInstance(_propertyType, context.Properties.Count);
                for (int i = 0; i < context.Properties.Count; i++)
                    properties.SetValue(Activator.CreateInstance(_propertyType, new object[] { context.Properties[i].Name, context.Properties[i].Value, null }), i);
                Array resources = Array.CreateInstance(_resourceType, context.Resources.Count);
                for (int i = 0; i < context.Resources.Count; i++)
                {
                    AnalysisResourceInput input = context.Resources[i];
                    string culture = input.Culture ?? (string)_inferResourceCulture.Invoke(null, new object[] { input.FilePath });
                    resources.SetValue(Activator.CreateInstance(_resourceType, input.Text, input.FilePath, input.Version, culture), i);
                }
                Array lookups = Array.CreateInstance(_lookupType, context.Lookups.Count);
                for (int i = 0; i < context.Lookups.Count; i++)
                {
                    AnalysisLookupInput input = context.Lookups[i];
                    lookups.SetValue(Activator.CreateInstance(_lookupType, input.Text, input.FilePath, input.Version), i);
                }
                object capturedContext = Activator.CreateInstance(_contextType, resources, lookups, context.Culture, context.ProjectPath, context.Configuration, context.Platform, properties);
                var configurations = context.Configurations.Where(input => input.Text != null).ToArray();
                Array configurationInputs = Array.CreateInstance(_editorConfigType, configurations.Length);
                for (int i = 0; i < configurations.Length; i++)
                    configurationInputs.SetValue(Activator.CreateInstance(_editorConfigType, configurations[i].Text, configurations[i].FilePath, 0), i);
                object configuration = Activator.CreateInstance(_analyzerConfigurationType, new object[] { configurationInputs });
                AnalysisDiagnosticOptions policy = context.DiagnosticOptions;
                object options = Activator.CreateInstance(_diagnosticOptionsType, configuration, policy.TreatWarningsAsErrors,
                    new[] { policy.NoWarn }, new[] { policy.WarningsAsErrors }, new[] { policy.WarningsNotAsErrors });
                capturedContext = _withDiagnosticOptions.Invoke(capturedContext, new[] { options });
                Array analyzerReferences = Array.CreateInstance(_analyzerReferenceType, context.Analyzers.Count);
                for (int i = 0; i < context.Analyzers.Count; i++)
                {
                    var reference = context.Analyzers[i];
                    Array images = Array.CreateInstance(_analyzerImageType, reference.Images.Count);
                    for (int j = 0; j < reference.Images.Count; j++)
                        images.SetValue(Activator.CreateInstance(_analyzerImageType, reference.Images[j].FilePath, reference.Images[j].GetImage()), j);
                    analyzerReferences.SetValue(Activator.CreateInstance(_analyzerReferenceType, reference.FilePath, images), i);
                }
                using (var loader = (IDisposable)Activator.CreateInstance(_analyzerLoaderType, new object[] { analyzerReferences }))
                {
                    capturedContext = _withAnalyzers.Invoke(capturedContext, new[] { _analyzerLoaderType.GetProperty("Analyzers").GetValue(loader) });
                    object result = _analyze.Invoke(_compiler, new object[] { snapshots, capturedContext, true, token });
                    AnalysisSnapshot snapshot = _reader.Read(result);
                    return new AnalysisResponse(AnalysisAvailability.Available, null, snapshot,
                        snapshot.Success ? (string)_compiledXml.GetValue(result, null) : null);
                }
            }
            catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(Unwrap(exception)))
            {
                Exception failure = Unwrap(exception);
                AnalysisAvailability state = failure is XmlException ? AnalysisAvailability.InvalidInput
                    : failure is NotSupportedException ? AnalysisAvailability.UnsupportedContract : AnalysisAvailability.AnalysisFailed;
                return new AnalysisResponse(state, "SDK analysis is incomplete: " + failure.Message);
            }
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
            return exception;
        }

        private sealed class WorkerCancellation : IDisposable
        {
            private readonly CancellationTokenSource _source = new CancellationTokenSource();
            private readonly Timer _timer;
            private readonly object _gate = new object();
            private bool _disposed;
            internal WorkerCancellation(AnalysisCancellationProbe probe)
            {
                if (probe != null) _timer = new Timer(_ =>
                {
                    lock (_gate)
                    {
                        if (_disposed || _source.IsCancellationRequested) return;
                        try { if (probe.IsCancellationRequested) _source.Cancel(); }
                        catch (Exception exception) when (SdkAnalysisBridge.IsRecoverable(exception)) { _source.Cancel(); }
                    }
                }, null, 0, 50);
            }
            internal CancellationToken Token => _source.Token;
            public void Dispose()
            {
                _timer?.Dispose();
                lock (_gate) { _disposed = true; _source.Dispose(); }
            }
        }
    }
}
