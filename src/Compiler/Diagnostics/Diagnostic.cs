// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Globalization;

namespace Alarmlist.Diagnostics
{
    public sealed class Diagnostic
    {
        public Diagnostic(DiagnosticDescriptor descriptor, params object[] arguments)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Arguments = arguments ?? Array.Empty<object>();
        }

        public DiagnosticDescriptor Descriptor { get; }

        public object[] Arguments { get; }

        public string Id => Descriptor.Id;

        public DiagnosticSeverity Severity => Descriptor.Severity;

        public string Message => string.Format(CultureInfo.InvariantCulture, Descriptor.MessageFormat, Arguments);

        public override string ToString()
        {
            return $"{Id}: {Message}";
        }
    }
}
