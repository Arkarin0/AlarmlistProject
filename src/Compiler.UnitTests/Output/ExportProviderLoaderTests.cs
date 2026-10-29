// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.Output;
using Xunit;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif

namespace Alarmlist.Output.Tests
{
    public class ExportProviderLoaderTests
    {
        private static ExportProviderReference Reference(string id = "Fixture", string type = "Alarmlist.Output.Tests.CapturedFixtureExporter")
        {
            string original = typeof(ExportProviderLoaderTests).Assembly.Location;
            string absent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), Path.GetFileName(original));
            return new ExportProviderReference(id, absent, type, new[] { new ExportAssemblyImage(absent, File.ReadAllBytes(original)) });
        }

        [Fact]
        public void ExplicitEntryTypeLoadsFromBytesWithoutOriginalPathAndReturnsBinaryArtifacts()
        {
            var reference = Reference();
            Assert.False(File.Exists(reference.FilePath));
            var loader = new ExportProviderLoader(new[] { reference });
            var provider = Assert.Single(loader.Providers);
            try
            {
                var document = ExportDocument.Create(new CompilationResult(new AlarmList(), Array.Empty<Diagnostic>()), "keyword");
                var artifacts = provider.Export(new ExportRequest(document, "alarms.bin"), CancellationToken.None);
                Assert.Equal(2, artifacts.Count);
                Assert.Equal(new byte[] { 0, 255, 128 }, artifacts[0].GetContent());
                Assert.Equal("sidecar.txt", artifacts[1].RelativePath);
#if NET8_0_OR_GREATER
                Assert.True(AssemblyLoadContext.GetLoadContext(provider.GetType().Assembly).IsCollectible);
                Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(provider.GetType().Assembly));
#endif
            }
            finally { loader.Dispose(); }
            loader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => loader.Providers);
        }

        [Theory]
        [InlineData("Wrong", "Alarmlist.Output.Tests.CapturedFixtureExporter", typeof(NotSupportedException))]
        [InlineData("Fixture", "Alarmlist.Output.Tests.IncompatibleFixtureExporter", typeof(NotSupportedException))]
        [InlineData("Fixture", "Alarmlist.Output.Tests.ExportProviderLoaderTests", typeof(NotSupportedException))]
        [InlineData("Fixture", "Missing.EntryType", typeof(TypeLoadException))]
        public void RejectsMismatchedIdsContractsAndUnsupportedEntryTypes(string id, string type, Type expected)
        {
            Assert.Throws(expected, () => new ExportProviderLoader(new[] { Reference(id, type) }));
            using (var recovered = new ExportProviderLoader(new[] { Reference() })) Assert.Single(recovered.Providers);
        }

        [Fact]
        public void DuplicateAndReservedIdsFailBeforeLoadingAndImagesAreDefensive()
        {
            var reference = Reference();
            Assert.Throws<ArgumentException>(() => new ExportProviderLoader(new[] { reference, reference }));
            Assert.Throws<ArgumentException>(() => new ExportProviderLoader(new[] { Reference("XML") }));
            Assert.Throws<ArgumentException>(() => Reference("bad;id"));
            byte[] bytes = { 1, 2, 3 };
            var image = new ExportAssemblyImage("bad.dll", bytes);
            bytes[0] = 9;
            image.GetImage()[1] = 9;
            Assert.Equal(new byte[] { 1, 2, 3 }, image.GetImage());
            Assert.Throws<BadImageFormatException>(() => new ExportProviderLoader(new[]
            { new ExportProviderReference("Fixture", "bad.dll", "Entry", new[] { image }) }));
        }
    }

    public class CapturedFixtureExporter : IAlarmlistExportProvider
    {
        public string Id => "Fixture";
        public virtual int ContractVersion => ExportDocument.ContractVersion;
        public string RequiredDefinitionFingerprint => null;
        public IReadOnlyList<ExportArtifact> Export(ExportRequest request, CancellationToken cancellationToken)
            => Array.AsReadOnly(new[] { new ExportArtifact(request.FileName, new byte[] { 0, 255, 128 }), ExportArtifact.FromText("sidecar.txt", "detached") });
    }

    public sealed class IncompatibleFixtureExporter : CapturedFixtureExporter
    {
        public override int ContractVersion => -1;
    }
}
