// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.VisualStudio.UnitTests
{
    internal static class LookupTestData
    {
        public const string Catalogue = "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Service.Vakuum'>"
            + "<Segment LabelKey='Hmi.Service'/><Segment LabelKey='Hmi.Vakuum'/>"
            + "<Property Key='Ein' LabelKey='Hmi.Ein'><Metadata Name='Minimum' Value='0'/>"
            + "<Metadata Name='Maximum' Value='100'/><Metadata Name='Default' Value='060'/></Property>"
            + "<Button Key='Reset' LabelKey='Hmi.Reset'/></Path></Lookup>";
        public const string Resources = "<root><data name='Set'><value>In {path}, set {property} to {value}.</value></data>"
            + "<data name='Hmi.Service'><value>Service</value></data><data name='Hmi.Vakuum'><value>Vacuum</value></data>"
            + "<data name='Hmi.Ein'><value>ON</value></data><data name='Hmi.Reset'><value>Reset</value></data></root>";
        public const string Expression = "$(loc:Set, path=$(path:Service.Vakuum), property=$(property:Service.Vakuum#Ein), value=$(meta:Service.Vakuum#Ein#Default))";
        public const string Source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>" + Expression + "</Name></Alarm></Alarmlist>";
    }
}
