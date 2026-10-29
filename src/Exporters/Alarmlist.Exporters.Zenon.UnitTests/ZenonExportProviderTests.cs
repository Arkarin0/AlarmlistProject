// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Alarmlist.Compiler;
using Alarmlist.Output;
using Alarmlist.Text;
using Xunit;

namespace Alarmlist.Exporters.Zenon.UnitTests
{
    public class ZenonExportProviderTests
    {
        private static ExportRequest Compile(string alarms, string culture = "keyword", params ResxTextSnapshot[] resources)
        {
            var result = new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot("<Alarmlist>" + alarms + "</Alarmlist>", "alarms.almx", 1) }, new CompilationContext(resources, culture));
            Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return new ExportRequest(ExportDocument.Create(result, culture), "alarms.zenon.xml");
        }

        private static XElement Export(ExportRequest request)
        {
            var artifact = Assert.Single(new ZenonExportProvider().Export(request, CancellationToken.None));
            Assert.Equal("alarms.zenon.xml", artifact.RelativePath);
            return XDocument.Parse(Encoding.UTF8.GetString(artifact.GetContent()).TrimStart('\ufeff')).Root;
        }

#if NET8_0_OR_GREATER
        [Fact]
        public void DisposedCapturedProviderUnloadsWhileDetachedArtifactsRemainUsable()
        {
            var retained = ExportAndRelease();
            for (int attempt = 0; attempt < 20 && retained.Context.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.False(retained.Context.IsAlive);
            Assert.Contains("@Ready@", Encoding.UTF8.GetString(retained.Artifact.GetContent()));
            Assert.Throws<ObjectDisposedException>(() => retained.Loader.Providers);
            GC.KeepAlive(retained);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static (WeakReference Context, ExportArtifact Artifact, ExportProviderLoader Loader) ExportAndRelease()
        {
            string path = typeof(ZenonExportProvider).Assembly.Location;
            var reference = new ExportProviderReference("Zenon", path, typeof(ZenonExportProvider).FullName,
                new[] { new ExportAssemblyImage(path, File.ReadAllBytes(path)) });
            var loader = new ExportProviderLoader(new[] { reference });
            var provider = Assert.Single(loader.Providers);
            var context = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(provider.GetType().Assembly);
            Assert.True(context.IsCollectible);
            var artifact = Assert.Single(provider.Export(Compile("<Alarm><Name>$(loc:Ready)</Name></Alarm>"), CancellationToken.None));
            loader.Dispose();
            return (new WeakReference(context), artifact, loader);
        }
#endif

        [Theory]
        [InlineData("keyword")]
        [InlineData("en")]
        public void InheritedKeysChildValuesAndProcedureReferencesUseCompilerBoundaries(string culture)
        {
            var request = Compile("""
                <Alarm abstract="true"><FullyQualifiedName>Base</FullyQualifiedName>
                <Properties><Property Name="N" Value="base"/></Properties>
                <Name>$(loc:YouPlacedAtPositionKeyword) $(N) $(loc:atLocationkeyword)</Name>
                <TestProcedure><Group Name="Verify"><Title>$(AlarmlistAlarmName)</Title><Step>$(AlarmlistAlarmName)</Step></Group></TestProcedure>
                </Alarm><Alarm><FullyQualifiedName>Child</FullyQualifiedName><ReferenceName>Base</ReferenceName>
                <Properties><Property Name="N" Value="1"/></Properties><Code>001</Code></Alarm>
                """, culture, new ResxTextSnapshot("<root><data name='YouPlacedAtPositionKeyword'><value>Position</value></data><data name='atLocationkeyword'><value>Location</value></data></root>", "values.en.resx", 1, "en"));
            var alarm = Assert.Single(Export(request).Elements("Alarm"));
            const string expected = "@YouPlacedAtPositionKeyword@ 1 @atLocationkeyword@";
            Assert.Equal(expected, alarm.Element("Name").Value);
            Assert.Equal("001", alarm.Element("Code").Value);
            Assert.Equal("Child", alarm.Element("FullyQualifiedName").Value);
            var group = alarm.Element("TestProcedure").Element("Group");
            Assert.Equal("Verify", (string)group.Attribute("Name"));
            Assert.Equal(expected, group.Element("Title").Value);
            Assert.Equal(expected, group.Element("TestProcedureItem").Value);
        }

        [Fact]
        public void EscapedAndParameterTokenLookingTextRemainLiteral()
        {
            var request = Compile("""
                <Alarm><Properties><Property Name="P" Value="$(loc:Injected)"/></Properties>
                <Name>$$(loc:Escaped)|$(P)|$(loc:Real)</Name></Alarm>
                """);
            Assert.Equal("$(loc:Escaped)|$(loc:Injected)|@Real@", Export(request).Element("Alarm").Element("Name").Value);
            Assert.Throws<OperationCanceledException>(() => new ZenonExportProvider().Export(request, new CancellationToken(true)));
        }

        [Theory]
        [InlineData("text@literal")]
        [InlineData("$(loc:A)$(loc:B)")]
        [InlineData("$(loc:A)$(Empty)$(loc:B)")]
        [InlineData("$(loc:A, value=&quot;1&quot;)")]
        public void UnrepresentableDelimiterAndFormattedTextFailExplicitly(string expression)
        {
            var request = Compile("<Alarm><Properties><Property Name='Empty' Value=''/></Properties><Name>" + expression + "</Name></Alarm>");
            Assert.Throws<NotSupportedException>(() => new ZenonExportProvider().Export(request, CancellationToken.None));
        }

        [Fact]
        public void ConcreteTranslationWithParameterSubstitutionIsNotSilentlyFlattened()
        {
            var request = Compile("<Alarm><Properties><Property Name='P' Value='1'/></Properties><Name>$(loc:A)</Name></Alarm>", "en",
                new ResxTextSnapshot("<root><data name='A'><value>Inspect $(P)</value></data></root>", "values.en.resx", 1, "en"));
            Assert.Contains("substitution", Assert.Throws<NotSupportedException>(() => new ZenonExportProvider().Export(request, CancellationToken.None)).Message);
        }

        [Fact]
        public void DirectLookupLabelsAndLiteralMetadataWorkInKeywordMode()
        {
            const string source = "<Alarmlist><Alarm><Name>$(path:Machine) $(property:Machine#Speed) $(meta:Machine#Speed#Default)</Name></Alarm></Alarmlist>";
            const string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Machine'><Segment LabelKey='Hmi.Machine'/><Property Key='Speed' LabelKey='Hmi.Speed'><Metadata Name='Default' Value='060'/></Property></Path></Lookup>";
            var result = new AlarmCompiler().CompileSnapshots(new[] { new AlmxTextSnapshot(source, "alarms.almx", 1) },
                new CompilationContext(Array.Empty<ResxTextSnapshot>(), new[] { new LookupTextSnapshot(lookup, "hmi.almlookup", 1) }, "keyword"));
            Assert.True(result.Success);
            Assert.Equal("@Hmi.Machine@ @Hmi.Speed@ 060", Export(new ExportRequest(ExportDocument.Create(result, "keyword"), "alarms.zenon.xml")).Element("Alarm").Element("Name").Value);
        }
    }
}
