// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;
using Alarmlist.Configuration;

namespace Alarmlist.Diagnostics
{
    /// <summary>Immutable diagnostic configuration shared by compilation and snapshot analysis.</summary>
    public sealed class DiagnosticOptions
    {
        public static DiagnosticOptions Default { get; } = new DiagnosticOptions();

        public DiagnosticOptions(AnalyzerConfiguration configuration = null, bool treatWarningsAsErrors = false,
            IEnumerable<string> noWarn = null, IEnumerable<string> warningsAsErrors = null, IEnumerable<string> warningsNotAsErrors = null)
        {
            Configuration = configuration ?? AnalyzerConfiguration.Empty;
            TreatWarningsAsErrors = treatWarningsAsErrors;
            NoWarn = Capture(noWarn);
            WarningsAsErrors = Capture(warningsAsErrors);
            WarningsNotAsErrors = Capture(warningsNotAsErrors);
        }

        public AnalyzerConfiguration Configuration { get; }
        public bool TreatWarningsAsErrors { get; }
        public IReadOnlyList<string> NoWarn { get; }
        public IReadOnlyList<string> WarningsAsErrors { get; }
        public IReadOnlyList<string> WarningsNotAsErrors { get; }

        private static IReadOnlyList<string> Capture(IEnumerable<string> ids)
            => Array.AsReadOnly((ids ?? Array.Empty<string>()).Where(id => id != null)
                .SelectMany(id => id.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(id => id.Trim()).Where(id => id.Length != 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
