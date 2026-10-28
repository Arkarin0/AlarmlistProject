using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Alarmlist.Output;

namespace Fixture
{
    public sealed class Exporter : IAlarmlistExportProvider
    {
        public Exporter() { File.AppendAllText("provider-constructor.marker", "constructed\n"); }
        public string Id => "Fixture";
        public int ContractVersion => ExportDocument.ContractVersion;
        public string RequiredDefinitionFingerprint => null;
        public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken)
        {
            if (request.Options.TryGetValue("FailCulture", out string culture) && culture == request.Document.Culture)
                throw new InvalidOperationException("fixture later-culture failure");
            string path = request.Options.TryGetValue("RelativePath", out string configured) ? configured : "bundle/data.bin";
            return Array.AsReadOnly(new[]
            {
                new ExportArtifact(path, Support.Payload()),
                ExportArtifact.FromText("runtime.txt", Support.Runtime + ":" + request.Document.Culture)
            });
        }
    }
}
