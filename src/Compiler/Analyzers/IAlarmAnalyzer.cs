// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Threading;
using Alarmlist.Diagnostics;

namespace Alarmlist.Analyzers
{
    /// <summary>Runs once per successful bound/evaluated project and selected culture. Implementations must honor cancellation.</summary>
    public interface IAlarmAnalyzer
    {
        int ContractVersion { get; }
        IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics { get; }
        IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken);
    }
}
