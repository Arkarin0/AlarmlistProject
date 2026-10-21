// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;

namespace Alarmlist.Analyzers
{
    /// <summary>Detached declarations from one captured assembly, independent of compilation results.</summary>
    public sealed class AnalyzerAssemblyMetadata
    {
        public string FilePath { get; }
        public string AssemblyIdentity { get; }
        public string Failure { get; }
        public IReadOnlyList<DiagnosticDescriptor> Rules { get; }

        internal AnalyzerAssemblyMetadata(string path, string identity, string failure, IEnumerable<DiagnosticDescriptor> rules)
        {
            FilePath = path;
            AssemblyIdentity = identity;
            Failure = failure;
            Rules = Array.AsReadOnly(rules.OrderBy(rule => rule.Id, StringComparer.OrdinalIgnoreCase).ToArray());
        }

        internal static AnalyzerAssemblyMetadata Capture(string path, string identity, IEnumerable<IAlarmAnalyzer> analyzers,
            HashSet<string> owners, string loadFailure = null)
        {
            var rules = new List<DiagnosticDescriptor>();
            string failure = loadFailure;
            foreach (IAlarmAnalyzer analyzer in analyzers)
            {
                try { rules.AddRange(AnalyzerDriver.ReadRules(analyzer, owners).Values); }
                catch (Exception exception) when (AnalyzerAssemblyLoader.IsRecoverable(exception))
                { failure = failure ?? exception.Message; }
            }
            return new AnalyzerAssemblyMetadata(path, identity, failure,
                failure == null ? rules : Enumerable.Empty<DiagnosticDescriptor>());
        }
    }
}
