// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Alarmlist.ConfigurationInput;
using Alarmlist.VisualStudio.Analysis;
using Alarmlist.VisualStudio.ProjectSystem;
using Xunit;

namespace Alarmlist.VisualStudio.UnitTests
{
    public sealed class PackageContentTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "AlarmlistPackageContent", Guid.NewGuid().ToString("N"));
        public PackageContentTests() { Directory.CreateDirectory(_root); }
        public void Dispose() => Directory.Delete(_root, true);
        private AnalysisPackageContent File(string relative, string kind, string text)
        {
            string path = PackageContentPath.Resolve(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, text, new UTF8Encoding(false));
            return new AnalysisPackageContent(_root, relative, kind, PackageContentPath.Hash(System.IO.File.ReadAllBytes(path)));
        }

        [Fact]
        public void ContentGroupsAreNonemptyLocalizedSortedAndHaveStableIdentities()
        {
            var document = File("docs/z.html", "Document", "html");
            var first = File("docs/a.md", "Document", "markdown");
            var build = File("build/Library.targets", "Build", "<Project />");
            var manifest = File("Library.nuspec", "Manifest", "manifest");
            var original = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
                var groups = PackageContentGroup.Create(new[] { document, first, first, build, manifest });
                Assert.Equal(new[] { "Builddateien", "Dokumente" }, groups.Select(group => group.Caption));
                Assert.Equal(new[] { "docs/a.md", "docs/z.html" }, groups[1].Files.Select(file => file.RelativePath));
                Assert.Equal(PackageContentGroup.Identity(first), PackageContentGroup.Identity(new AnalysisPackageContent(_root, "DOCS/A.MD", "Document", "changed")));
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                Assert.Equal("Documents", Assert.Single(PackageContentGroup.Create(new[] { first })).Caption);
                Assert.Empty(PackageContentGroup.Create(new[] { manifest }));
            }
            finally { CultureInfo.CurrentUICulture = original; }
        }

        [Fact]
        public void HtmlAndBuildFilesArePlainReadOnlyTextAndChangedBytesCannotOpen()
        {
            const string html = "<html><script>document.title='EXECUTED';</script>raw content</html>";
            var file = File("docs/Guide.html", "Document", html);
            var package = new AnalysisPackageNode("Library", "1.0.0", true, Array.Empty<string>(), new[] { file });
            var target = PackageContentDocument.Create(null, package, file);
            Assert.Equal(html, target.Text);
            Assert.True(target.IsReadOnlyDefinition);
            Assert.False(target.IsXml);
            var build = File("build/Library.targets", "Build", "<Project><Target Name='NeverExecute'/></Project>");
            Assert.False(PackageContentDocument.Create(null, package, build).IsXml);
            System.IO.File.WriteAllText(file.FilePath, html.Replace("raw", "new"));
            Assert.Throws<InvalidOperationException>(() => PackageContentDocument.Create(null, package, file));
            System.IO.File.Delete(file.FilePath);
            Assert.Throws<InvalidOperationException>(() => file.ReadCurrentBytes());
        }

        [Fact]
        public void AnalyzerInfoUsesDetachedIdentityWithoutLoadingTheDll()
        {
            var file = File("tools/Rules.dll", "Analyzer", "these are not assembly bytes");
            var package = new AnalysisPackageNode("Library", "1.0.0", true, Array.Empty<string>(), new[] { file });
            var state = new ProjectAnalysisState(1, null, ProjectAnalysisStatus.Available, null,
                new AnalysisResponse(AnalysisAvailability.Available, null, analyzers: new[] { new AnalysisAnalyzerMetadata(file.FilePath, "Captured.Identity", null) }));
            var target = PackageContentDocument.Create(state, package, file);
            Assert.Contains("Captured.Identity", target.Text);
            Assert.Contains(file.FilePath, target.Text);
            Assert.False(target.IsXml);
        }

        [Fact]
        public void PackageNavigationRejectsDirectoryJunctions()
        {
            string outside = Path.Combine(_root, "outside");
            string package = Path.Combine(_root, "package");
            string link = Path.Combine(package, "docs");
            Directory.CreateDirectory(outside);
            Directory.CreateDirectory(package);
            System.IO.File.WriteAllText(Path.Combine(outside, "Guide.txt"), "outside package");
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + outside + "\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                process.WaitForExit();
                Assert.True(process.ExitCode == 0, process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
            }
            try { Assert.Throws<InvalidDataException>(() => PackageContentPath.Read(package, "docs/Guide.txt")); }
            finally { Directory.Delete(link); }
            Assert.Equal("outside package", System.IO.File.ReadAllText(Path.Combine(outside, "Guide.txt")));
        }

        [Fact]
        public void LibraryViewUsesVirtualXmlAndRejectsDtds()
        {
            var package = new AnalysisPackageNode("Library", "1.0.0", true, Array.Empty<string>());
            var file = File("alarmlist/Library.almlib", "Library", "<Library><Definition /></Library>");
            var target = PackageContentDocument.Create(null, package, file);
            Assert.True(target.IsReadOnlyDefinition);
            Assert.True(target.IsXml);
            Assert.Contains("Definition", target.Text);
            var malicious = File("alarmlist/Bad.almlib", "Library", "<!DOCTYPE Library [<!ENTITY x SYSTEM 'file:///outside'>]><Library>&x;</Library>");
            Assert.Throws<System.Xml.XmlException>(() => PackageContentDocument.Create(null, package, malicious));
        }
    }
}
