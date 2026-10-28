// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.MSBuild.SDK.Tests
{
    [Collection(TestProjectCollection.Name)]
    public sealed class ExportProviderTests
    {
        private readonly TestProjectFixture _fixture;
        private readonly ITestOutputHelper _output;
        public ExportProviderTests(TestProjectFixture fixture, ITestOutputHelper output) { _fixture = fixture; _output = output; }

        [Fact]
        public void PackagedContractSupportsDllAndNuGetBinaryExportsWithoutDesignTimeExecution()
        {
            var app = _fixture.CreateTestApp("Exports");
            AssertSuccess(app.Run(_output, "/restore", "/t:GetTargetPath"));
            Assert.Empty(File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt")));
            BuildProvider(app);
            AssertSuccess(app.Run(_output, "/t:Build"));
            AssertExports(app);
            string manifest = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "obj"), "*.outputs.xml", SearchOption.AllDirectories).Single();
            var previous = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "bin"), "*", SearchOption.AllDirectories)
                .Concat(new[] { manifest }).ToDictionary(path => path, File.ReadAllBytes);
            string projectPath = Path.Combine(app.WorkingDirectory, "Plant.almproj");
            var project = XDocument.Load(projectPath);
            var registration = project.Descendants("AlarmlistExportProvider").Single();
            registration.Add(new XElement("Option_FailCulture", "en"));
            project.Save(projectPath);
            var failure = app.Run(_output, "/t:Build");
            Assert.NotEqual(0, failure.ExitCode);
            Assert.Contains("fixture later-culture failure", failure.Output);
            foreach (var item in previous) Assert.Equal(item.Value, File.ReadAllBytes(item.Key));
            registration.Element("Option_FailCulture").Remove();
            project.Save(projectPath);

            string runtime = Runtime;
            string assembly = Path.Combine(app.WorkingDirectory, "Provider", "bin", "Debug", runtime, "Fixture.Exporter.dll");
            byte[] image = File.ReadAllBytes(assembly);
            string marker = Path.Combine(app.WorkingDirectory, "provider-constructor.marker");
            File.Delete(marker);
            File.WriteAllText(assembly, "invalid provider image");
            try
            {
                AssertSuccess(app.Run(_output, "/t:GetTargetPath"));
                AssertSuccess(app.Run(_output, "/t:Build", "/p:DesignTimeBuild=true"));
                Assert.False(File.Exists(marker));
                Assert.NotEqual(0, app.Run(_output, "/t:Build").ExitCode);
                foreach (var item in previous) Assert.Equal(item.Value, File.ReadAllBytes(item.Key));
            }
            finally { File.WriteAllBytes(assembly, image); }

            PackageProvider(app);
            registration.Remove();
            var reference = new XElement("PackageReference", new XAttribute("Include", "Fixture.Exporter"), new XAttribute("Version", "1.0.0"), new XAttribute("PrivateAssets", "all"));
            project.Root.Add(new XElement("ItemGroup", reference));
            project.Root.Add(new XElement("Target", new XAttribute("Name", "RecordProviderSelection"), new XAttribute("BeforeTargets", "Build"),
                new XElement("WriteLinesToFile", new XAttribute("File", "provider-selection.txt"),
                    new XAttribute("Lines", "@(AlarmlistExportProvider->'%(AssemblyPath)')"), new XAttribute("Overwrite", "true"))));
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/restore", "/t:Build"));
            AssertExports(app);
            string selected = Path.GetFullPath(Assert.Single(File.ReadAllLines(Path.Combine(app.WorkingDirectory, "provider-selection.txt")))).Replace('\\', '/');
            Assert.Contains("/fixture.exporter/1.0.0/tools/" + runtime + "/Fixture.Exporter.dll", selected);
            File.Delete(marker);
            AssertSuccess(app.Run(_output, "/t:GetTargetPath"));
            Assert.False(File.Exists(marker));
            Assert.Equal(6, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt")).Length);

            reference.Remove();
            project.Save(projectPath);
            Assert.NotEqual(0, app.Run(_output, "/restore", "/t:Build").ExitCode);
            AssertSuccess(app.Run(_output, "/t:GetTargetPath"));
            Assert.Equal(6, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt")).Length);
            project.Descendants("AlarmlistExportTypes").Single().Value = "XML";
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/t:Build"));
            Assert.False(File.Exists(Path.Combine(app.WorkingDirectory, "bin", "Debug", "keyword", "bundle", "data.bin")));
            string unrelated = Path.Combine(app.WorkingDirectory, "bin", "Debug", "keyword", "unrelated.bin");
            File.WriteAllText(unrelated, "keep");
            File.Delete(marker);
            AssertSuccess(app.Run(_output, "/t:Clean"));
            Assert.False(File.Exists(app.OutputPath));
            Assert.Equal("keep", File.ReadAllText(unrelated));
            Assert.False(File.Exists(marker));
            project.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistExportProvider", new XAttribute("Include", "XML"), new XElement("OutputFileName", "resolved.xml"))));
            project.Save(projectPath);
            AssertSuccess(app.Run(_output, "/t:Build"));
            Assert.True(File.Exists(Path.Combine(app.WorkingDirectory, "bin", "Debug", "keyword", "resolved.xml")));
            AssertSuccess(app.Run(_output, "/t:GetTargetPath"));
            Assert.Equal(2, File.ReadAllLines(Path.Combine(app.WorkingDirectory, "outputs.txt")).Length);
        }

        private void BuildProvider(TestApp app)
        {
            // Verification code references the extracted SDK contract, never the compiler project.
            // Compile both runtime assets with dotnet even when the consuming build uses desktop MSBuild.
            string previous = Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH");
            try
            {
                Environment.SetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH", null);
                AssertSuccess(app.RunProject(_output, Path.Combine(app.WorkingDirectory, "Provider", "Provider.csproj"),
                    "/restore", "/t:Build", "/p:ContractRoot=" + Path.Combine(app.PackageDirectory, "tasks")));
            }
            finally { Environment.SetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH", previous); }
        }

        private static void PackageProvider(TestApp app)
        {
            string path = Path.Combine(app.WorkingDirectory, "feed", "Fixture.Exporter.1.0.0.nupkg");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("Fixture.Exporter.nuspec").Open()))
                    writer.Write("<package><metadata><id>Fixture.Exporter</id><version>1.0.0</version><authors>Tests</authors><description>Isolated export verification provider</description></metadata></package>");
                foreach (string framework in new[] { "net472", "net8.0" })
                    foreach (string name in new[] { "Fixture.Exporter.dll", "Fixture.Support.dll" })
                        archive.CreateEntryFromFile(Path.Combine(app.WorkingDirectory, "Provider", "bin", "Debug", framework, name), "tools/" + framework + "/" + name);
                using (var writer = new StreamWriter(archive.CreateEntry("build/Fixture.Exporter.targets").Open()))
                    writer.Write("""
                        <Project><ItemGroup><AlarmlistExportProvider Include="Fixture">
                        <AssemblyPath Condition="'$(MSBuildRuntimeType)' == 'Core'">$(MSBuildThisFileDirectory)../tools/net8.0/Fixture.Exporter.dll</AssemblyPath>
                        <AssemblyPath Condition="'$(MSBuildRuntimeType)' != 'Core'">$(MSBuildThisFileDirectory)../tools/net472/Fixture.Exporter.dll</AssemblyPath>
                        <Dependencies>Fixture.Support.dll</Dependencies><TypeName>Fixture.Exporter</TypeName>
                        </AlarmlistExportProvider></ItemGroup></Project>
                        """);
            }
        }

        private static string Runtime => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ALARMLIST_TEST_MSBUILD_PATH")) ? "net8.0" : "net472";
        private void AssertExports(TestApp app)
        {
            foreach (string culture in new[] { "keyword", "en" })
            {
                string directory = Path.Combine(app.WorkingDirectory, "bin", "Debug", culture);
                Assert.True(File.Exists(Path.Combine(directory, "Plant.Alarmlist.xml")));
                Assert.Equal(new byte[] { 0, 255, 128 }, File.ReadAllBytes(Path.Combine(directory, "bundle", "data.bin")));
                Assert.Equal(Runtime + ":" + culture, File.ReadAllText(Path.Combine(directory, "runtime.txt")));
            }
        }
        private void AssertSuccess(BuildResult result) => Assert.True(result.ExitCode == 0, result.Output);
    }
}
