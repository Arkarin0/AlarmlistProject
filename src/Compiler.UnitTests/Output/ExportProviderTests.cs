// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Output;
using Xunit;

namespace Alarmlist.Output.Tests
{
    public class ExportProviderTests
    {
        private static CompilationResult Result(AlarmList alarms) => new CompilationResult(alarms, Array.Empty<Diagnostic>());

        [Fact]
        public void SnapshotDoesNotRetainMutableAlarmListOrOptions()
        {
            var alarms = new AlarmList { new Alarm("Plant.A", null, "", "Ü 😀", "Name") };
            ExportDocument document = ExportDocument.Create(Result(alarms), "keyword");
            alarms.Clear();
            var options = new Dictionary<string, string> { ["prefix"] = "original" };
            var request = new ExportRequest(document, "alarms.xml", options);
            options["prefix"] = "changed";
            Assert.Single(document.Alarms);
            Assert.Equal("Plant.A", document.Alarms[0].FullyQualifiedName);
            Assert.Equal("original", request.Options["prefix"]);
            Assert.Equal("keyword", document.Culture);
            Assert.Equal(4, document.Fields.Count);
            Assert.Equal(Alarmlist.Definition.AlarmDefinition.SchemaFingerprint, document.DefinitionFingerprint);
        }

        [Fact]
        public void XmlProviderMatchesLegacyFileBytesAndSupportsCancellation()
        {
            var procedure = new TestProcedure(new[] { new ProcedureGroup("Custom", "Inspect <&>", new[]
            {
                new TestProcedureItem(TestProcedureItemKind.Hint, "Ü 😀\r\ntext"),
                new TestProcedureItem(TestProcedureItemKind.Instruction, ""),
                new TestProcedureItem(TestProcedureItemKind.Warning, null)
            }) });
            var alarms = new AlarmList { new Alarm("Plant.A", null, "", "<&>", "Name", procedure) };
            var request = new ExportRequest(ExportDocument.Create(Result(alarms), "en"), "alarms.xml");
            var provider = new XmlAlarmlistExportProvider();
            string path = Path.GetTempFileName();
            try
            {
                new XmlAlarmlistOutputWriter().Write(alarms, new AlarmlistOutputOptions(path));
                ExportArtifact artifact = Assert.Single(provider.Export(request, CancellationToken.None));
                Assert.Equal(File.ReadAllBytes(path), artifact.GetContent());
                Assert.Equal("alarms.xml", artifact.RelativePath);
                Assert.Throws<OperationCanceledException>(() => provider.Export(request, new CancellationToken(true)));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void IncompatibleProviderFailsWithRebuildGuidance()
        {
            var request = new ExportRequest(ExportDocument.Create(Result(new AlarmList()), "keyword"), "a.xml");
            Assert.Contains("Rebuild", Assert.Throws<NotSupportedException>(() => request.ValidateCompatibility(new Stub(999, null))).Message);
            Assert.Contains("definition", Assert.Throws<NotSupportedException>(() => request.ValidateCompatibility(new Stub(1, "other"))).Message);
            request.ValidateCompatibility(new Stub(1, null));
            request.ValidateCompatibility(new Stub(1, request.Document.DefinitionFingerprint));
            Assert.Throws<ArgumentException>(() => ExportDocument.Create(Result(new AlarmList()), "not-a-culture-!"));
            Assert.Throws<ArgumentException>(() => ExportDocument.Create(new CompilationResult(null, Array.Empty<Diagnostic>()), "en"));
        }

        private sealed class Stub : IAlarmlistExportProvider
        {
            internal Stub(int version, string fingerprint) { ContractVersion = version; RequiredDefinitionFingerprint = fingerprint; }
            public string Id => "test";
            public int ContractVersion { get; }
            public string RequiredDefinitionFingerprint { get; }
            public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken) => Array.Empty<ExportArtifact>();
        }
    }
}
