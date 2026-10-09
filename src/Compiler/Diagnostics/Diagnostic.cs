// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alarmlist.Text;

namespace Alarmlist.Diagnostics
{
    public sealed class Diagnostic
    {
        public Diagnostic(DiagnosticDescriptor descriptor, params object[] arguments)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Arguments = arguments ?? Array.Empty<object>();
            Severity = descriptor.Severity;
        }

        public DiagnosticDescriptor Descriptor { get; }

        public object[] Arguments { get; }

        public SourceLocation Location { get; private set; }

        public IReadOnlyList<SourceLocation> RelatedLocations { get; private set; } = Array.Empty<SourceLocation>();

        /// <summary>Returns a diagnostic with source information, preserving its descriptor and message.</summary>
        public Diagnostic WithLocation(SourceLocation location, IEnumerable<SourceLocation> relatedLocations = null)
        {
            return new Diagnostic(Descriptor, (object[])Arguments.Clone())
            {
                Location = location,
                Severity = Severity,
                RelatedLocations = Array.AsReadOnly((relatedLocations ?? Enumerable.Empty<SourceLocation>())
                    .Where(related => related != null).ToArray())
            };
        }

        public string Id => Descriptor.Id;

        public DiagnosticSeverity Severity { get; private set; }

        internal Diagnostic WithSeverity(DiagnosticSeverity severity)
        {
            Diagnostic result = WithLocation(Location, RelatedLocations);
            result.Severity = severity;
            return result;
        }

        public string Message => string.Format(CultureInfo.InvariantCulture, Descriptor.MessageFormat, Arguments);

        public override string ToString()
        {
            return $"{Id}: {Message}";
        }
    }
}
