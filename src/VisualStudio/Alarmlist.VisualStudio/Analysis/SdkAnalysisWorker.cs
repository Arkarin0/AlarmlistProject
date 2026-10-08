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
        private Type _resourceType;
        private Type _lookupType;
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
                _propertyType = assembly.GetType("Alarmlist.Compiler.PropertyDefinition", true);
                if (_contextType.GetConstructor(new[] { typeof(IEnumerable<>).MakeGenericType(_resourceType), typeof(IEnumerable<>).MakeGenericType(_lookupType), typeof(string),
                    typeof(string), typeof(string), typeof(string), typeof(IEnumerable<>).MakeGenericType(_propertyType) }) == null
                    || _propertyType.GetConstructor(new[] { typeof(string), typeof(string), assembly.GetType("Alarmlist.Text.SourceLocation", true) }) == null)
                    throw new NotSupportedException("The SDK context constructors are incompatible.");
                _analyze = compilerType.GetMethod("AnalyzeSnapshots", new[] { typeof(IEnumerable<>).MakeGenericType(_snapshotType), _contextType, typeof(bool) });
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
                    + ". Restore an SDK supporting analysis contract 3 with lookup catalogues and named templates. " + failure.Message);
            }
        }

        public AnalysisResponse Analyze(AnalysisInput[] inputs, AnalysisContext context = null)
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
                object result = _analyze.Invoke(_compiler, new object[] { snapshots, capturedContext, true });
                AnalysisSnapshot snapshot = _reader.Read(result);
                return new AnalysisResponse(AnalysisAvailability.Available, null, snapshot,
                    snapshot.Success ? (string)_compiledXml.GetValue(result, null) : null);
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
