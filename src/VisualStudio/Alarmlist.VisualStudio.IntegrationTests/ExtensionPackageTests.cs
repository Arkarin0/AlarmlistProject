// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Alarmlist.MSBuild.SDK.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.IntegrationTests
{
    public class ExtensionPackageTests
    {
        private static readonly XNamespace s_templateNamespace = "http://schemas.microsoft.com/developer/vstemplate/2005";
        private static readonly string s_assets = Path.Combine(AppContext.BaseDirectory, "testassets");
        private readonly ITestOutputHelper _output;

        public ExtensionPackageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void VsixContainsRegistrationAndTemplatesWithoutHostAssemblies()
        {
            using (var package = OpenPackage())
            {
                Assert.Equal(new[] { "Alarmlist.VisualStudio.dll" }, package.Entries
                    .Where(entry => entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.FullName).ToArray());
                Assert.DoesNotContain(package.Entries, entry => entry.FullName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
                var manifest = XDocument.Parse(Read(package.GetEntry("extension.vsixmanifest")));
                XNamespace ns = manifest.Root.Name.Namespace;
                Assert.Equal("[17.9,)", manifest.Descendants(ns + "InstallationTarget").Single().Attribute("Version").Value);
                Assert.All(manifest.Descendants(ns + "Prerequisite"), prerequisite =>
                    Assert.Equal("[17.9,)", prerequisite.Attribute("Version").Value));
                Assert.Equal("amd64", manifest.Descendants(ns + "ProductArchitecture").Single().Value);
                var registration = Read(package.GetEntry("Alarmlist.VisualStudio.pkgdef"));
                Assert.Contains("\"DefaultProjectExtension\"=\"almproj\"", registration);
                Assert.Contains("\"CodeBase\"=\"$PackageFolder$\\Alarmlist.VisualStudio.dll\"", registration);
                Assert.Contains("\"Language(VsTemplate)\"=\"Alarmlist\"", registration);
                Assert.Contains("\"ProjectFactoryPackage\"=\"{3347bee8-d7a1-4082-95e4-38a439553cc2}\"", registration);
                Assert.Contains("ItemTemplates", registration);
                Assert.Contains("\"almx\"=", Read(package.GetEntry("Alarmlist.XmlEditor.pkgdef")));
                Assert.Equal(2, package.Entries.Count(entry => entry.FullName.EndsWith(".vstemplate")));
                var projectTemplate = XDocument.Parse(Read(package.Entries.Single(entry =>
                    entry.FullName.StartsWith("ProjectTemplates/", StringComparison.Ordinal)
                    && entry.FullName.EndsWith(".vstemplate", StringComparison.Ordinal))));
                Assert.Equal("true", projectTemplate.Descendants(s_templateNamespace + "CreateInPlace").Single().Value);
                var catalogs = package.Entries.Where(entry => entry.FullName.EndsWith(".vstman")).ToArray();
                Assert.Equal(2, catalogs.Length);
                foreach (var catalog in catalogs)
                {
                    var document = XDocument.Parse(Read(catalog));
                    XNamespace catalogNamespace = document.Root.Name.Namespace;
                    string directory = catalog.FullName.Substring(0, catalog.FullName.LastIndexOf('/') + 1);
                    foreach (var container in document.Descendants(catalogNamespace + "VSTemplateContainer"))
                    {
                        string templatePath = directory + container.Element(catalogNamespace + "RelativePathOnDisk").Value.Replace('\\', '/')
                            + "/" + container.Element(catalogNamespace + "TemplateFileName").Value;
                        Assert.NotNull(package.GetEntry(templatePath));
                    }
                }
            }
        }

        [Fact]
        public void ProjectTemplatePinsTheAuthoritativeSdkPackage()
        {
            using (var vsix = OpenPackage())
            {
                var project = XDocument.Parse(Read(vsix.Entries.Single(entry => entry.FullName.EndsWith(".almproj"))));
                string sdk = project.Root.Attribute("Sdk").Value;
                const string prefix = "Alarmlist.MSBuild.SDK/";
                Assert.StartsWith(prefix, sdk);
                string version = sdk.Substring(prefix.Length);
                using (var package = ZipFile.OpenRead(Path.Combine(s_assets, "boilerplate", "feed", "Alarmlist.MSBuild.SDK." + version + ".nupkg")))
                {
                    var nuspec = XDocument.Parse(Read(package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec"))));
                    XNamespace ns = nuspec.Root.Name.Namespace;
                    var metadata = nuspec.Root.Element(ns + "metadata");
                    Assert.Equal("Alarmlist.MSBuild.SDK", metadata.Element(ns + "id").Value);
                    Assert.Equal(version, metadata.Element(ns + "version").Value);
                    Assert.NotNull(package.GetEntry("tasks/net472/Alarmlist.Core.dll"));
                    Assert.NotNull(package.GetEntry("tasks/net8.0/Alarmlist.Core.dll"));
                }
            }
        }

        [Fact]
        public void PackagedProjectAndItemTemplatesBuildAndReflectFileLifecycle()
        {
            using (var app = CreateApp(nameof(PackagedProjectAndItemTemplatesBuildAndReflectFileLifecycle)))
            using (var package = OpenPackage())
            {
                Instantiate(package, "ProjectTemplates/", app.WorkingDirectory, "Plant");
                app.Build(_output);
                Assert.Single(XDocument.Load(app.OutputPath).Root.Elements("Alarm"));
                var project = XDocument.Load(Path.Combine(app.WorkingDirectory, "Plant.almproj"));
                Assert.Empty(project.Descendants("TargetFramework"));
                Assert.DoesNotContain("__ALARMLIST_SDK_VERSION__", project.ToString());

                var folder = Path.Combine(app.WorkingDirectory, "sources");
                Directory.CreateDirectory(folder);
                Instantiate(package, "ItemTemplates/", folder, "Additional");
                app.Build(_output);
                Assert.Equal(2, XDocument.Load(app.OutputPath).Root.Elements("Alarm").Count());
                File.Move(Path.Combine(folder, "Additional.almx"), Path.Combine(folder, "Renamed.almx"));
                app.Build(_output);
                Assert.Equal(2, XDocument.Load(app.OutputPath).Root.Elements("Alarm").Count());
                File.Delete(Path.Combine(folder, "Renamed.almx"));
                app.Build(_output);
                Assert.Single(XDocument.Load(app.OutputPath).Root.Elements("Alarm"));
                Assert.Equal(0, app.Run(_output, "/t:Clean").ExitCode);
                Assert.False(File.Exists(app.OutputPath));
                Assert.Equal(0, app.Run(_output, "/t:Rebuild").ExitCode);
                Assert.True(File.Exists(app.OutputPath));
            }
        }

        [Fact]
        public void TemplateBuildReportsCompilerErrorWithoutReplacingOutput()
        {
            using (var app = CreateApp(nameof(TemplateBuildReportsCompilerErrorWithoutReplacingOutput)))
            using (var package = OpenPackage())
            {
                Instantiate(package, "ProjectTemplates/", app.WorkingDirectory, "Plant");
                app.Build(_output);
                byte[] original = File.ReadAllBytes(app.OutputPath);
                File.WriteAllText(Path.Combine(app.WorkingDirectory, "Invalid.almx"),
                    "<Alarmlist><Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>");
                var result = app.Run(_output, "/t:Build");
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("ALM0002", result.Output);
                Assert.Equal(original, File.ReadAllBytes(app.OutputPath));
            }
        }

        private static TestApp CreateApp(string testName)
        {
            var logDirectory = typeof(ExtensionPackageTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "LogOutputDir").Value;
            return new TestApp(s_assets, "boilerplate", Path.Combine(logDirectory, testName));
        }

        private static ZipArchive OpenPackage()
        {
            return ZipFile.OpenRead(Path.Combine(s_assets, "Alarmlist.VisualStudio.vsix"));
        }

        private static string Read(ZipArchiveEntry entry)
        {
            Assert.NotNull(entry);
            using (var reader = new StreamReader(entry.Open()))
            {
                return reader.ReadToEnd();
            }
        }

        // Expand the actual files shipped in the VSIX using the documented VS template tokens.
        // IDE command routing and CPS hierarchy operations are covered by the experimental smoke test.
        private static void Instantiate(ZipArchive package, string prefix, string destination, string name)
        {
            var entry = package.Entries.Single(item => item.FullName.StartsWith(prefix, StringComparison.Ordinal)
                && item.FullName.EndsWith(".vstemplate", StringComparison.Ordinal));
            string directory = entry.FullName.Substring(0, entry.FullName.LastIndexOf('/') + 1);
            var template = XDocument.Parse(Read(entry));
            foreach (var item in template.Descendants().Where(element => element.Name == s_templateNamespace + "Project"
                || element.Name == s_templateNamespace + "ProjectItem"))
            {
                string source = item.Attribute("File")?.Value ?? item.Value;
                string target = Replace(item.Attribute("TargetFileName")?.Value ?? source, name);
                File.WriteAllText(Path.Combine(destination, target), Replace(Read(package.GetEntry(directory + source)), name));
            }
        }

        private static string Replace(string text, string name)
        {
            return text.Replace("$safeprojectname$", name).Replace("$safeitemname$", name).Replace("$fileinputname$", name);
        }
    }
}
