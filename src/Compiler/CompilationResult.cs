// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;

namespace Alarmlist.Compiler
{
    public sealed class CompilationResult
    {
        public CompilationResult(AlarmList alarmList, IEnumerable<Diagnostic> diagnostics)
        {
            AlarmList = alarmList;
            Diagnostics = diagnostics.ToArray();
        }

        public AlarmList AlarmList { get; }

        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
