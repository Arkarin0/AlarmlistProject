// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests.Analysis
{
    public partial class SdkAnalysisBridgeTests
    {
        [Fact]
        public void CapturedConfigurationCrossesTheBridgeWithoutSuppressingMandatoryErrors()
        {
            using (var sdk = new SdkAsset())
            using (SdkAnalysisBridge bridge = Open(sdk.CompilerPath))
            {
                string path = Path.Combine(sdk.DirectoryPath, "A.almx");
                var context = new AnalysisContext(diagnosticOptions: new AnalysisDiagnosticOptions(true, "ALM0002"),
                    configurations: new[] { new CapturedEditorConfig(Path.Combine(sdk.DirectoryPath, ".editorconfig"),
                        "root=true\n[*.almx]\nAlarmlist_diagnostic.ALM0002.severity=none"), new CapturedEditorConfig(Path.Combine(sdk.DirectoryPath, "absent", ".editorconfig"), null) });
                AnalysisResponse response = bridge.Analyze(new[] { new AnalysisInput(
                    "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>", path, 7) }, context: context);
                Assert.Equal(AnalysisAvailability.Available, response.Availability);
                Assert.False(response.Snapshot.Success);
                Assert.Null(response.CompiledXml);
                Assert.Equal("ALM0002", Assert.Single(response.Snapshot.Diagnostics).Id);
                Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly => assembly.GetName().Name == "Alarmlist.Core");
            }
        }
    }
}
