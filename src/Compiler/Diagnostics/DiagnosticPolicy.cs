// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Diagnostics
{
    /// <summary>Applies file severity, suppression, and warning promotion before compilation success is determined.</summary>
    public static class DiagnosticPolicy
    {
        public static IReadOnlyList<Diagnostic> Apply(IEnumerable<Diagnostic> diagnostics, DiagnosticOptions options)
        {
            if (diagnostics == null) throw new ArgumentNullException(nameof(diagnostics));
            if (options == null) throw new ArgumentNullException(nameof(options));
            return Array.AsReadOnly(diagnostics.Select(diagnostic => Apply(diagnostic, options)).Where(diagnostic => diagnostic != null).ToArray());
        }

        /// <summary>Returns null for a suppressed diagnostic. The input and its default descriptor remain unchanged.</summary>
        public static Diagnostic Apply(Diagnostic diagnostic, DiagnosticOptions options)
        {
            if (diagnostic == null) throw new ArgumentNullException(nameof(diagnostic));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!diagnostic.Descriptor.IsConfigurable) return diagnostic;

            DiagnosticSeverity severity = diagnostic.Descriptor.Severity;
            IReadOnlyDictionary<string, string> fileOptions = options.Configuration.GetOptionsForSourcePath(diagnostic.Location?.FilePath);
            if (fileOptions.TryGetValue("alarmlist_diagnostic." + diagnostic.Id + ".severity", out string value))
            {
                switch (value.ToLowerInvariant())
                {
                    case "none": return null;
                    case "error": severity = DiagnosticSeverity.Error; break;
                    case "warning": severity = DiagnosticSeverity.Warning; break;
                    case "suggestion": severity = DiagnosticSeverity.Info; break;
                    case "silent": severity = DiagnosticSeverity.Hidden; break;
                    // 'default' and invalid values retain the descriptor's default severity.
                }
            }
            if (options.NoWarn.Contains(diagnostic.Id, StringComparer.OrdinalIgnoreCase)) return null;
            if (severity == DiagnosticSeverity.Warning
                && (options.WarningsAsErrors.Contains(diagnostic.Id, StringComparer.OrdinalIgnoreCase)
                    || (options.TreatWarningsAsErrors && !options.WarningsNotAsErrors.Contains(diagnostic.Id, StringComparer.OrdinalIgnoreCase))))
                severity = DiagnosticSeverity.Error;
            return diagnostic.WithSeverity(severity);
        }
    }
}
