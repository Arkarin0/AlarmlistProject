// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Diagnostics
{
    public static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor DuplicateAlarmName = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.DuplicateAlarmName,
            "Duplicate alarm name",
            "Alarm '{0}' is declared more than once.",
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor MissingReference = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.MissingReference,
            "Missing reference",
            "Alarm '{0}' references missing alarm '{1}'.",
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor SelfReference = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.SelfReference,
            "Self reference",
            "Alarm '{0}' references itself.",
            DiagnosticSeverity.Error);

        public static readonly DiagnosticDescriptor CircularReference = new DiagnosticDescriptor(
            DiagnosticWellKnownIds.CircularReference,
            "Circular reference",
            "Circular alarm reference detected: {0}.",
            DiagnosticSeverity.Error);
    }
}
