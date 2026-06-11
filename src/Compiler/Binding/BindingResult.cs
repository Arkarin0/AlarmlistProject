// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using System.Linq;
using Alarmlist.Diagnostics;
using Alarmlist.Syntax;

namespace Alarmlist.Binding
{
    public sealed class BindingResult
    {
        public BindingResult(AlarmSyntaxTree syntaxTree, IEnumerable<Diagnostic> diagnostics)
        {
            SyntaxTree = syntaxTree;
            Diagnostics = diagnostics.ToArray();
        }

        public AlarmSyntaxTree SyntaxTree { get; }

        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        public bool Success => !Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }
}
