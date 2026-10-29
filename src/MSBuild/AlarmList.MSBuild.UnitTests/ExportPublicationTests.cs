// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.MSBuild;
using Xunit;

namespace AlarmList.MSBuild.UnitTests
{
    public class ExportPublicationTests
    {
        [Theory]
        [InlineData("../outside.bin")]
        [InlineData("nested/../../outside.bin")]
        [InlineData("/absolute.bin")]
        [InlineData("nested\\..\\outside.bin")]
        [InlineData("nested//empty.bin")]
        [InlineData("CON")]
        [InlineData("data:stream")]
        public void RejectsEscapingAndUnsupportedArtifactPaths(string path)
        {
            string root = Path.Combine(Path.GetTempPath(), "exports");
            Assert.ThrowsAny<ArgumentException>(() => ExportOutputPaths.Artifact(root, "keyword", path));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void BinaryRollbackRestoresPreviousBytesAfterCancellationOrInstallationFailure(bool cancel)
        {
            string root = Path.Combine(Path.GetTempPath(), "AlarmlistTransactionTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string first = Path.Combine(root, "first.bin");
                string second = Path.Combine(root, "second.bin");
                File.WriteAllBytes(first, new byte[] { 0, 128 });
                if (!cancel) Directory.CreateDirectory(second);
                var transaction = new OutputTransaction(new[]
                {
                    OutputTransaction.Change.FromBytes(first, new byte[] { 255 }),
                    OutputTransaction.Change.FromBytes(second, new byte[] { 1, 2 })
                });
                bool observedInstallation = false;
                Action checkpoint = () =>
                {
                    if (File.ReadAllBytes(first).SequenceEqual(new byte[] { 255 }))
                    {
                        observedInstallation = true;
                        if (cancel) throw new OperationCanceledException();
                    }
                };
                Assert.ThrowsAny<Exception>(() => transaction.Commit(checkpoint, _ => { }));
                Assert.True(observedInstallation);
                Assert.Equal(new byte[] { 0, 128 }, File.ReadAllBytes(first));
                Assert.False(File.Exists(second));
                Assert.Empty(Directory.GetFiles(root, "*.alarmlist-*"));
            }
            finally { DeleteRoot(root); }
        }

        [Fact]
        public void VersionThreeManifestRejectsEscapingPathsAndRetainsNestedOwnership()
        {
            string root = Path.Combine(Path.GetTempPath(), "AlarmlistManifestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string project = Path.Combine(root, "Plant.almproj");
                string manifest = Path.Combine(root, "outputs.xml");
                string output = Path.Combine(root, "out", "keyword", "nested", "data.bin");
                string text = OutputManifest.WriteExports(project, "Debug", "AnyCPU", Path.Combine(root, "out"),
                    new[] { new OutputManifest.ExportOutput(output, "keyword", "Fixture") }, Array.Empty<string>());
                File.WriteAllText(manifest, text);
                var parsed = OutputManifest.Read(manifest, project, "Debug", "AnyCPU");
                Assert.Equal(output, Assert.Single(parsed.Files));
                Assert.Equal("Fixture", Assert.Single(parsed.Exports).ProviderId);
                var document = XDocument.Parse(text);
                document.Root.Element("File").SetAttributeValue("path", Path.Combine(root, "outside.bin"));
                document.Save(manifest);
                Assert.Throws<InvalidDataException>(() => OutputManifest.Read(manifest, project, "Debug", "AnyCPU"));
            }
            finally { DeleteRoot(root); }
        }

        [Fact]
        public void RetiredLibraryTemporaryOwnershipSurvivesExportTransition()
        {
            string root = Path.Combine(Path.GetTempPath(), "AlarmlistManifestTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string project = Path.Combine(root, "Plant.almproj");
                string manifest = Path.Combine(root, "outputs.xml");
                string oldLibrary = Path.Combine(root, "old", "Plant.almlib");
                string backup = oldLibrary + ".alarmlist-" + Guid.NewGuid().ToString("N") + ".backup";
                File.WriteAllText(manifest, OutputManifest.Write(project, "Debug", "AnyCPU", new[] { oldLibrary }, new[] { backup }, "Library"));
                var previous = OutputManifest.Read(manifest, project, "Debug", "AnyCPU");
                string outputRoot = Path.Combine(root, "new");
                string output = Path.Combine(outputRoot, "keyword", "data.bin");
                File.WriteAllText(manifest, OutputManifest.WriteExports(project, "Debug", "AnyCPU", outputRoot,
                    new[] { new OutputManifest.ExportOutput(output, "keyword", "Fixture") }, new[] { backup }, manifest, previous));
                var current = OutputManifest.Read(manifest, project, "Debug", "AnyCPU");
                Assert.Equal(backup, Assert.Single(current.TemporaryFiles));
                Assert.Equal(output, Assert.Single(current.Files));
                var forged = XDocument.Load(manifest);
                forged.Root.Element("Temporary").SetAttributeValue("path", Path.Combine(root, "escaping.almlib") + ".alarmlist-" + Guid.NewGuid().ToString("N") + ".backup");
                forged.Save(manifest);
                Assert.Throws<InvalidDataException>(() => OutputManifest.Read(manifest, project, "Debug", "AnyCPU"));
            }
            finally { DeleteRoot(root); }
        }

        private static void DeleteRoot(string root)
        {
            string path = Path.GetFullPath(root);
            string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(temporary, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(Path.GetDirectoryName(path)).StartsWith("Alarmlist", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the owned test directory.");
            Directory.Delete(path, true);
        }

        [Fact]
        public void ExistingJunctionCannotRedirectArtifactPublication()
        {
            string root = Path.Combine(Path.GetTempPath(), "AlarmlistJunctionTests", Guid.NewGuid().ToString("N"));
            string outside = Path.Combine(root, "outside");
            string output = Path.Combine(root, "output");
            string junction = Path.Combine(output, "keyword");
            Directory.CreateDirectory(outside);
            Directory.CreateDirectory(output);
            try
            {
#if NET8_0_OR_GREATER
                if (Path.DirectorySeparatorChar != '\\') Directory.CreateSymbolicLink(junction, outside);
                else
#endif
                using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + junction + "\" \"" + outside + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    string message = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                    Assert.True(process.WaitForExit(10000) && process.ExitCode == 0, message);
                }
                Assert.Throws<IOException>(() => ExportOutputPaths.Artifact(output, "keyword", "data.bin"));
                Assert.Empty(Directory.GetFiles(outside));
            }
            finally
            {
                // Remove only the owned junction itself before recursively deleting the test root.
                if (Directory.Exists(junction)) Directory.Delete(junction);
                DeleteRoot(root);
            }
        }
    }
}
