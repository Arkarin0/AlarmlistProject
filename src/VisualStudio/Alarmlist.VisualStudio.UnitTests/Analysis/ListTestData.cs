// Created/modified by Arkarin0 under one ore more license(s).

namespace Alarmlist.VisualStudio.UnitTests
{
    internal static class ListTestData
    {
        public const string Catalogue = "<Lookup xmlns='urn:alarmlist:lookup:1'>"
            + "<List Key='Wires'><Entry Key='W1' Value='=EC-120W1' DescriptionKey='Wire.Description'><Metadata Name='DrawingPage' Value='023'/></Entry>"
            + "<Entry Key='W2' Value='=EC-120W2' Description='Spare'/></List>"
            + "<List Key='Terminals'><Entry Key='K1' Value='=EC-120K1:12'/></List>"
            + "<Template Key='Disconnect'><Argument Name='wire' List='Wires'/><Argument Name='connection' List='Terminals'/></Template></Lookup>";
        public const string Resources = "<root><data name='Disconnect'><value>Disconnect wire {wire} from {connection}.</value></data>"
            + "<data name='Wire.Description'><value>Outlet motor cable</value></data></root>";
        public const string Expression = "$(loc:Disconnect, wire=$(list:Wires#W1), connection=$(list:Terminals#K1))";
        public const string Source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>" + Expression + "</Name></Alarm></Alarmlist>";
    }
}
