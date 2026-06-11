// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;

namespace Alarmlist.Diagnostics
{
    internal sealed class DiagnosticBag : List<Diagnostic>
    {
        public void ReportDuplicateAlarmName(string alarmName)
        {
            Add(new Diagnostic(DiagnosticDescriptors.DuplicateAlarmName, alarmName));
        }

        public void ReportMissingReference(string alarmName, string referenceName)
        {
            Add(new Diagnostic(DiagnosticDescriptors.MissingReference, alarmName, referenceName));
        }

        public void ReportSelfReference(string alarmName)
        {
            Add(new Diagnostic(DiagnosticDescriptors.SelfReference, alarmName));
        }

        public void ReportCircularReference(IEnumerable<string> trace)
        {
            Add(new Diagnostic(DiagnosticDescriptors.CircularReference, string.Join(" -> ", trace)));
        }
    }
}
