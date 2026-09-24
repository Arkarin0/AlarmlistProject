// Created/modified by Arkarin0 under one ore more license(s).

using System.Collections.Generic;
using Alarmlist.Text;

namespace Alarmlist.Diagnostics
{
    internal sealed class DiagnosticBag : List<Diagnostic>
    {
        public void ReportDuplicateAlarmName(string alarmName, SourceLocation location, SourceLocation previousLocation)
        {
            Add(new Diagnostic(DiagnosticDescriptors.DuplicateAlarmName, alarmName).WithLocation(location, new[] { previousLocation }));
        }

        public void ReportMissingReference(string alarmName, string referenceName, SourceLocation location)
        {
            Add(new Diagnostic(DiagnosticDescriptors.MissingReference, alarmName, referenceName).WithLocation(location));
        }

        public void ReportSelfReference(string alarmName, SourceLocation location)
        {
            Add(new Diagnostic(DiagnosticDescriptors.SelfReference, alarmName).WithLocation(location));
        }

        public void ReportCircularReference(IEnumerable<string> trace, SourceLocation location, IEnumerable<SourceLocation> relatedLocations)
        {
            Add(new Diagnostic(DiagnosticDescriptors.CircularReference, string.Join(" -> ", trace)).WithLocation(location, relatedLocations));
        }
    }
}
