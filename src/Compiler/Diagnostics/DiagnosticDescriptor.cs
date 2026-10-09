// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Diagnostics
{
    public sealed class DiagnosticDescriptor
    {
        public DiagnosticDescriptor(string id, string title, string messageFormat, DiagnosticSeverity severity)
            : this(id, title, messageFormat, severity, severity != DiagnosticSeverity.Error)
        {
        }

        /// <summary>Mandatory diagnostics cannot be suppressed, demoted, or promoted by project policy.</summary>
        public DiagnosticDescriptor(string id, string title, string messageFormat, DiagnosticSeverity severity, bool isConfigurable)
        {
            Id = id;
            Title = title;
            MessageFormat = messageFormat;
            Severity = severity;
            IsConfigurable = isConfigurable;
        }

        public string Id { get; }

        public string Title { get; }

        public string MessageFormat { get; }

        public DiagnosticSeverity Severity { get; }

        public bool IsConfigurable { get; }
    }
}
