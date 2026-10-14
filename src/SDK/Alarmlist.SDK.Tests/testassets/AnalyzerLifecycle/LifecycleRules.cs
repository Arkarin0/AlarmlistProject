// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Alarmlist.Analyzers;
using Alarmlist.Diagnostics;

public sealed class LifecycleRule : IAlarmAnalyzer, IDisposable
{
    private string _project;
    public int ContractVersion => 3;
    public IReadOnlyList<DiagnosticDescriptor> SupportedDiagnostics => Array.Empty<DiagnosticDescriptor>();

    public IEnumerable<Diagnostic> Analyze(AlarmAnalyzerContext context, CancellationToken cancellationToken)
    {
        _project = context.ProjectPath;
        File.AppendAllText(_project + ".analyzed", context.Culture + Environment.NewLine);
        return Array.Empty<Diagnostic>();
    }

    public void Dispose()
    {
        File.AppendAllText(_project + ".disposed", "disposed" + Environment.NewLine);
        if (File.Exists(_project + ".fail-disposal")) throw new InvalidOperationException("Test analyzer disposal failure");
    }
}
