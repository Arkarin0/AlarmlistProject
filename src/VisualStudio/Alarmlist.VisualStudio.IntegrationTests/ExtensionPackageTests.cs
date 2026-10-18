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
                Assert.Contains("ItemTemplates\\Alarmlist", registration);
                Assert.Contains("Menus.ctmenu, 2", registration);
                Assert.Contains("ToolWindows\\{2c7f1b33-66f5-4831-96b1-c11cd1e28769}", registration);
                Assert.Contains("Editors\\{6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f}", registration);
                Assert.Contains("Editors\\{92d86a30-3de0-4f7d-bc54-3e801efb277d}", registration);
                foreach (string editor in new[] { "6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f", "92d86a30-3de0-4f7d-bc54-3e801efb277d" })
                {
                    string section = "[$RootKey$\\Editors\\{" + editor + "}]";
                    // Registration attributes can reopen the same pkgdef key.
                    string values = string.Join("\n", System.Text.RegularExpressions.Regex.Matches(registration,
                        "(?ms)^" + System.Text.RegularExpressions.Regex.Escape(section) + @"\r?\n(.*?)(?=^\[|\z)")
                        .Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value));
                    Assert.Contains("\"DeferUntilIntellisenseIsReady\"=dword:00000000", values);
                }
                Assert.Contains("\"almx\"=dword:00000060", registration);
                foreach (string logicalView in new[] { "7651a701", "7651a702", "7651a703" }) Assert.Contains(logicalView, registration);
                Assert.Contains(manifest.Descendants(ns + "Asset"), asset => (string)asset.Attribute("Type") == "Microsoft.VisualStudio.MefComponent"
                    && (string)asset.Attribute("Path") == "Alarmlist.VisualStudio.dll");
                string xmlRegistration = Read(package.GetEntry("Alarmlist.XmlEditor.pkgdef"));
                Assert.Contains("\"almx\"=dword:00000028", xmlRegistration);
                Assert.Contains("Languages\\File Extensions\\.almx", xmlRegistration);
                Assert.Contains("{F6819A78-A205-47B5-BE1C-675B3C7F0B8E}", xmlRegistration);
                Assert.Equal(4, package.Entries.Count(entry => entry.FullName.EndsWith(".vstemplate")));
                var projectTemplates = package.Entries.Where(entry =>
                    entry.FullName.StartsWith("ProjectTemplates/", StringComparison.Ordinal)
                    && entry.FullName.EndsWith(".vstemplate", StringComparison.Ordinal)).ToArray();
                Assert.Equal(2, projectTemplates.Length);
                Assert.All(projectTemplates, entry => Assert.Equal("true", XDocument.Parse(Read(entry))
                    .Descendants(s_templateNamespace + "CreateInPlace").Single().Value));
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
        public void ItemTemplatesContainTransparentMultiResolutionIconsOnlyAsTemplateAssets()
        {
            using (var package = OpenPackage())
            {
                var templates = package.Entries.Where(entry => entry.FullName.StartsWith("ItemTemplates/", StringComparison.Ordinal)
                    && entry.FullName.EndsWith(".vstemplate", StringComparison.Ordinal)).ToArray();
                Assert.Equal(2, templates.Length);
                foreach (var entry in templates)
                {
                    var template = XDocument.Parse(Read(entry));
                    string icon = template.Descendants(s_templateNamespace + "Icon").Single().Value;
                    Assert.DoesNotContain(template.Descendants(s_templateNamespace + "ProjectItem"), item => item.Value == icon);
                    string directory = entry.FullName.Substring(0, entry.FullName.LastIndexOf('/') + 1);
                    using (var stream = package.GetEntry(directory + icon).Open())
                    using (var content = new MemoryStream())
                    {
                        stream.CopyTo(content);
                        content.Position = 0;
                        using (var reader = new BinaryReader(content))
                        {
                            Assert.Equal(0, reader.ReadUInt16());
                            Assert.Equal(1, reader.ReadUInt16());
                            int[] sizes = { 16, 20, 24, 32, 48, 64, 256 };
                            Assert.Equal(sizes.Length, reader.ReadUInt16());
                            foreach (int size in sizes)
                            {
                                Assert.Equal(size % 256, reader.ReadByte());
                                Assert.Equal(size % 256, reader.ReadByte());
                                Assert.Equal(0, reader.ReadUInt16());
                                Assert.Equal(1, reader.ReadUInt16());
                                Assert.Equal(32, reader.ReadUInt16());
                                uint length = reader.ReadUInt32(), offset = reader.ReadUInt32();
                                long next = content.Position;
                                Assert.InRange((long)offset + length, 1, content.Length);
                                content.Position = offset;
                                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, reader.ReadBytes(8));
                                content.Position = offset + 25;
                                Assert.Equal(6, reader.ReadByte()); // RGBA PNG, retains transparency.
                                content.Position = next;
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void ProjectTemplatePinsTheAuthoritativeSdkPackage()
        {
            using (var vsix = OpenPackage())
            {
                var projects = vsix.Entries.Where(entry => entry.FullName.EndsWith(".almproj")).ToArray();
                Assert.Equal(2, projects.Length);
                Assert.All(projects, entry => Assert.Equal(XDocument.Parse(Read(projects[0])).Root.Attribute("Sdk").Value,
                    XDocument.Parse(Read(entry)).Root.Attribute("Sdk").Value));
                var project = XDocument.Parse(Read(projects[0]));
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
        public void PackagedLibraryTemplateBuildsAndPacksReusableDefinitions()
        {
            using (var app = CreateApp(nameof(PackagedLibraryTemplateBuildsAndPacksReusableDefinitions)))
            using (var package = OpenPackage())
            {
                Instantiate(package, "ProjectTemplates/", app.WorkingDirectory, "Plant", "AlarmlistLibrary.vstemplate");
                var result = app.Run(_output, "/t:Restore;Build;Pack");
                Assert.True(result.ExitCode == 0, result.Output);
                var project = XDocument.Load(Path.Combine(app.WorkingDirectory, "Plant.almproj"));
                Assert.Empty(project.Descendants("TargetFramework"));
                Assert.Equal("Library", project.Descendants("OutputType").Single().Value);
                string artifact = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "bin"), "Plant.almlib", SearchOption.AllDirectories).Single();
                string text = File.ReadAllText(artifact);
                Assert.Contains("Plant.Template", text);
                Assert.Contains("Plant.Example", text);
                Assert.Contains("abstract=\"true\"", text);
                Assert.Contains("ReferenceName", text);
                Assert.False(File.Exists(app.OutputPath));
                Assert.Single(Directory.GetFiles(Path.Combine(app.WorkingDirectory, "bin"), "Plant.1.0.0.nupkg", SearchOption.AllDirectories));
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
        public void ResourceItemTemplateBuildsSavedTranslationsWithAnInferredCulture()
        {
            using (var app = CreateApp(nameof(ResourceItemTemplateBuildsSavedTranslationsWithAnInferredCulture)))
            using (var package = OpenPackage())
            {
                Instantiate(package, "ProjectTemplates/", app.WorkingDirectory, "Plant");
                Instantiate(package, "ItemTemplates/", app.WorkingDirectory, "Translations.de", "Translations.vstemplate");
                string path = Path.Combine(app.WorkingDirectory, "Translations.de.resx");
                var resource = XDocument.Load(path);
                Assert.Empty(resource.Root.Elements("data"));
                Assert.Equal(4, resource.Root.Elements("resheader").Count());
                resource.Root.Add(new XElement("data", new XAttribute("name", "Greeting"), new XElement("value", "Druck prüfen")));
                resource.Save(path);
                File.WriteAllText(Path.Combine(app.WorkingDirectory, "Alarms.almx"),
                    "<Alarmlist><Alarm><FullyQualifiedName>Localized</FullyQualifiedName><Name>$(loc:Greeting)</Name></Alarm></Alarmlist>");
                var result = app.Run(_output, "/t:Build", "/p:AlarmlistCultures=de");
                Assert.Equal(0, result.ExitCode);
                Assert.Equal("Druck prüfen", XDocument.Load(Path.Combine(app.WorkingDirectory, "bin", "Debug", "de", "Plant.Alarmlist.xml"))
                    .Root.Element("Alarm").Element("Name").Value);
                var template = XDocument.Parse(Read(package.Entries.Single(entry => entry.FullName.EndsWith("/Translations.vstemplate", StringComparison.Ordinal))));
                Assert.Equal("Translations.en.resx", template.Descendants(s_templateNamespace + "DefaultName").Single().Value);
                Assert.Equal("AlarmlistResource", template.Descendants(s_templateNamespace + "ProjectItem").Single().Attribute("ItemType").Value);
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
        private static void Instantiate(ZipArchive package, string prefix, string destination, string name, string templateName = null)
        {
            templateName = templateName ?? (prefix == "ProjectTemplates/" ? "Alarmlist.vstemplate" : "AlarmList.vstemplate");
            var entry = package.Entries.Single(item => item.FullName.StartsWith(prefix, StringComparison.Ordinal)
                && item.FullName.EndsWith("/" + templateName, StringComparison.Ordinal));
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
