// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.Diagnostics
{
    public static class DiagnosticWellKnownIds
    {
        public const string DuplicateAlarmName = "ALM0001";

        public const string MissingReference = "ALM0002";

        public const string SelfReference = "ALM0003";

        public const string CircularReference = "ALM0004";
        public const string InvalidAbstract = "ALM0005";
        public const string InvalidExpression = "ALM0006";
        public const string UnknownProperty = "ALM0007";
        public const string UnassignedProperty = "ALM0008";
        public const string MissingPropertyContext = "ALM0009";
        public const string CircularFieldDependency = "ALM0010";
        public const string InvalidProperty = "ALM0011";
        public const string DuplicateProperty = "ALM0012";
    }
}
