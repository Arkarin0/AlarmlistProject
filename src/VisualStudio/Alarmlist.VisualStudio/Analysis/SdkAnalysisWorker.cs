// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml;

namespace Alarmlist.VisualStudio.Analysis
{
    // Activated only in the isolated domain. Never return reflection objects,
    // SDK instances, or SDK exceptions across the remoting boundary.
    internal sealed class SdkAnalysisWorker : MarshalByRefObject
    {
        private Type _snapshotType;
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
                _analyze = compilerType.GetMethod("AnalyzeSnapshots", new[] { typeof(IEnumerable<>).MakeGenericType(_snapshotType) });
                if (_analyze == null || _analyze.ReturnType != assembly.GetType("Alarmlist.Analysis.AnalysisResult"))
                    throw new NotSupportedException("The SDK has no compatible AnalyzeSnapshots method.");
                // XML is an optional addition to contract 1. Older SDKs still
                // provide provenance/diagnostics without supporting this preview.
                MethodInfo preview = compilerType.GetMethod("AnalyzeSnapshots", new[]
                    { typeof(IEnumerable<>).MakeGenericType(_snapshotType), typeof(bool) });
                PropertyInfo xml = _analyze.ReturnType.GetProperty("CompiledXml", BindingFlags.Public | BindingFlags.Instance);
                if (preview != null && !preview.IsStatic && preview.ReturnType == _analyze.ReturnType
                    && xml?.PropertyType == typeof(string) && xml.GetGetMethod() != null && xml.GetIndexParameters().Length == 0)
                {
                    _analyze = preview;
                    _compiledXml = xml;
                }
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
                    + ". Restore an SDK supporting analysis contract 1. " + failure.Message);
            }
        }

        public AnalysisResponse Analyze(AnalysisInput[] inputs)
        {
            try
            {
                Array snapshots = Array.CreateInstance(_snapshotType, inputs.Length);
                for (int i = 0; i < inputs.Length; i++)
                    snapshots.SetValue(Activator.CreateInstance(_snapshotType, inputs[i].Text, inputs[i].FilePath, inputs[i].Version), i);
                object result = _analyze.Invoke(_compiler, _compiledXml == null
                    ? new object[] { snapshots } : new object[] { snapshots, true });
                AnalysisSnapshot snapshot = _reader.Read(result);
                return new AnalysisResponse(AnalysisAvailability.Available, null, snapshot,
                    snapshot.Success ? (string)_compiledXml?.GetValue(result, null) : null,
                    _compiledXml == null ? "This project's SDK does not support compiled XML preview. Update the project's SDK to enable it." : null);
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
    }
}
