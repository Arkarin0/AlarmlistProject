// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Threading;
using Alarmlist.Compiler;
using Alarmlist.Diagnostics;
using Alarmlist.MSBuild;
using Alarmlist.Output;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public class ExportConfigurationTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("Application")]
        [InlineData("Exports")]
        [InlineData("exports")]
        public void ApplicationAndExportsDefaultToXml(string type)
        {
            var configuration = new ExportConfiguration(type, null, null, null);
            Assert.Equal("XML", Assert.Single(configuration.SelectedIds));
            Assert.False(BuildOutputPlan.IsLibrary(type));
            Assert.Empty(configuration.Capture(CancellationToken.None));
            Assert.Equal("XML", Assert.Single(new ExportConfiguration(type, "", null, null).SelectedIds));
        }

        [Fact]
        public void RejectsInvalidSelectionAndDuplicateOrReplacedProviders()
        {
            Assert.Throws<ArgumentException>(() => new ExportConfiguration("Library", "XML", null, null));
            Assert.Empty(new ExportConfiguration("Library", null, null, null).SelectedIds);
            foreach (string value in new[] { ";", "XML;xml", "Missing", "bad/id" })
                Assert.Throws<ArgumentException>(() => new ExportConfiguration("Exports", value, null, null));
            var first = new TaskItem("XML");
            Assert.Throws<ArgumentException>(() => new ExportConfiguration("Exports", null, new[] { first, first }, null));
            first.SetMetadata("TypeName", "Replacement");
            Assert.Throws<ArgumentException>(() => new ExportConfiguration("Exports", null, new[] { first }, null));
        }

        [Fact]
        public void OnlySelectedExternalDeclarationsAreCapturedAndOptionsAreExplicit()
        {
            var provider = new TaskItem("Fixture");
            provider.SetMetadata("AssemblyPath", typeof(ExportConfigurationTests).Assembly.Location);
            provider.SetMetadata("TypeName", "Fixture.Entry");
            provider.SetMetadata("OutputFileName", "result.bin");
            provider.SetMetadata("Option_Format", "binary");
            var unused = new TaskItem("Unused");
            unused.SetMetadata("AssemblyPath", "missing.dll");
            var configuration = new ExportConfiguration("Exports", "XML;Fixture", new[] { provider, unused }, null);
            var reference = Assert.Single(configuration.Capture(CancellationToken.None));
            Assert.Equal("Fixture", reference.Id);
            Assert.Equal("Fixture.Entry", reference.TypeName);
            Assert.Single(reference.Images);
            var request = configuration.Request("Fixture", ExportDocument.Create(new CompilationResult(new Alarmlist.Compiler.AlarmList(), Array.Empty<Diagnostic>()), "keyword"), "old.xml");
            Assert.Equal("result.bin", request.FileName);
            Assert.Equal("binary", request.Options["Format"]);
            Assert.Single(request.Options);
            Assert.Throws<OperationCanceledException>(() => configuration.Capture(new CancellationToken(true)));
        }

        [Fact]
        public void DependenciesAreDeclaredRelativeToProviderAndCannotReplaceCore()
        {
            var provider = new TaskItem("Fixture");
            provider.SetMetadata("AssemblyPath", typeof(ExportConfigurationTests).Assembly.Location);
            provider.SetMetadata("TypeName", "Fixture.Entry");
            provider.SetMetadata("Dependencies", "Alarmlist.Core.dll");
            var configuration = new ExportConfiguration("Exports", "Fixture", new[] { provider }, null);
            Assert.Throws<ArgumentException>(() => configuration.Capture(CancellationToken.None));
            provider.SetMetadata("Dependencies", "missing.dll");
            Assert.Throws<FileNotFoundException>(() => configuration.Capture(CancellationToken.None));
        }
    }
}
