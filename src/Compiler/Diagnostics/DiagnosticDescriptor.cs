// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Diagnostics
{
    public sealed class DiagnosticDescriptor
    {
        public DiagnosticDescriptor(string id, string title, string messageFormat, DiagnosticSeverity severity)
        {
            Id = id;
            Title = title;
            MessageFormat = messageFormat;
            Severity = severity;
        }

        public string Id { get; }

        public string Title { get; }

        public string MessageFormat { get; }

        public DiagnosticSeverity Severity { get; }
    }
}
