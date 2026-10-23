// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Alarmlist.Diagnostics;

namespace Alarmlist.Analyzers
{
    internal static class AnalyzerDriver
    {
        internal static IReadOnlyList<Diagnostic> Run(IEnumerable<IAlarmAnalyzer> analyzers, AlarmAnalyzerContext context, CancellationToken token)
        {
            var diagnostics = new List<Diagnostic>();
            var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IAlarmAnalyzer analyzer in analyzers)
            {
                token.ThrowIfCancellationRequested();
                string name = analyzer.GetType().FullName;
                try
                {
                    var rules = ReadRules(analyzer, owners);
                    var reported = new List<Diagnostic>();
                    foreach (Diagnostic diagnostic in analyzer.Analyze(context, token) ?? throw new InvalidOperationException("The analyzer returned null diagnostics."))
                    {
                        token.ThrowIfCancellationRequested();
                        if (diagnostic == null || !rules.TryGetValue(diagnostic.Id, out DiagnosticDescriptor declared))
                            throw new InvalidOperationException("The analyzer reported an undeclared diagnostic.");
                        // Freeze formatted text and use the registered rule's policy, not an ad-hoc descriptor.
                        reported.Add(new Diagnostic(new DiagnosticDescriptor(declared.Id, declared.Title, "{0}", declared.Severity, declared.IsConfigurable),
                            diagnostic.Message).WithLocation(diagnostic.Location, diagnostic.RelatedLocations));
                    }
                    token.ThrowIfCancellationRequested();
                    diagnostics.AddRange(reported);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception exception) when (!(exception is OutOfMemoryException) && !(exception is StackOverflowException)
                    && !(exception is AccessViolationException) && !(exception is AppDomainUnloadedException) && !(exception is ThreadAbortException))
                {
                    diagnostics.Add(new Diagnostic(DiagnosticDescriptors.AnalyzerFailure, name, exception.Message));
                }
            }
            return diagnostics.AsReadOnly();
        }

        internal static Dictionary<string, DiagnosticDescriptor> ReadRules(IAlarmAnalyzer analyzer, HashSet<string> owners)
        {
            if (analyzer.ContractVersion != AlarmAnalyzerContext.CurrentContractVersion)
                throw new NotSupportedException("Unsupported analyzer contract " + analyzer.ContractVersion + ". Rebuild this analyzer for contract 5 with the matching SDK.");
            var rules = new Dictionary<string, DiagnosticDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (DiagnosticDescriptor descriptor in analyzer.SupportedDiagnostics ?? throw new InvalidOperationException("SupportedDiagnostics is null."))
            {
                if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Id)
                    || descriptor.Id.StartsWith("ALM", StringComparison.OrdinalIgnoreCase)
                    || !char.IsLetter(descriptor.Id[0]) || descriptor.Id.Any(character => !char.IsLetterOrDigit(character) && character != '_')
                    || !Enum.IsDefined(typeof(DiagnosticSeverity), descriptor.Severity))
                    throw new InvalidOperationException("Analyzer rules require a valid ID outside the reserved ALM prefix and a supported severity.");
                if (rules.ContainsKey(descriptor.Id) || owners.Contains(descriptor.Id))
                    throw new InvalidOperationException("Duplicate analyzer diagnostic ID '" + descriptor.Id + "'.");
                rules.Add(descriptor.Id, descriptor);
            }
            foreach (string id in rules.Keys) owners.Add(id);
            return rules;
        }
    }
}
