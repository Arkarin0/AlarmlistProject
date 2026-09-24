// Created/modified by Arkarin0 under one ore more license(s).

using Alarmlist.Text;

namespace Alarmlist.Syntax
{
    public sealed class Clear : IReferenceableCollectionItem, ITestProcedureItem
    {
        public SourceLocation Location { get; internal set; }
    }
}
