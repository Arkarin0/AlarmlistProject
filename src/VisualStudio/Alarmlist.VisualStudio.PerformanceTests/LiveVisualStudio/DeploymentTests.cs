// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Alarmlist.VisualStudio.Testing;
using Xunit;

namespace Alarmlist.VisualStudio.PerformanceTests.LiveVisualStudio
{
    public sealed class DeploymentTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "AlarmlistDeploymentTests", Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("missing"), InlineData("matching"), InlineData("assembly"), InlineData("template"), InlineData("dependency")]
        public void ChecksTheActualInstalledPayloadRatherThanVersionOrReceipt(string state)
        {
            var payload = new Dictionary<string, byte[]>
            {
                ["Alarmlist.VisualStudio.dll"] = new byte[] { 1 },
                ["ProjectTemplates/Project.almproj"] = new byte[] { 2 },
                ["Dependency.dll"] = new byte[] { 3 }
            };
            if (state != "missing")
                foreach (KeyValuePair<string, byte[]> file in payload)
                {
                    string path = Path.Combine(_directory, file.Key.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, file.Value);
                }
            switch (state)
            {
                case "assembly": File.WriteAllBytes(Path.Combine(_directory, "Alarmlist.VisualStudio.dll"), new byte[] { 9 }); break;
                case "template": File.Delete(Path.Combine(_directory, "ProjectTemplates", "Project.almproj")); break;
                case "dependency": File.WriteAllBytes(Path.Combine(_directory, "Dependency.dll"), new byte[] { 9 }); break;
            }
            Assert.Equal(state == "matching", VisualStudioTestDeployment.Matches(_directory, payload));
        }

        [Theory]
        [InlineData("/RootSuffix", "AlarmlistExp", true)]
        [InlineData("/rootsuffix", "alarmlistexp", true)]
        [InlineData("-RootSuffix", "AlarmlistExp", true)]
        [InlineData("/RootSuffix:AlarmlistExp", "", true)]
        [InlineData("/RootSuffix", "OtherProfile", false)]
        [InlineData("/RootSuffix", "AlarmlistExpOther", false)]
        [InlineData("UserAlarmlistExp.sln", "", false)]
        public void ExistingIdeGuardMatchesOnlyTheSelectedExperimentalProfile(string argument, string value, bool expected)
        {
            Assert.Equal(expected, VisualStudioTestDeployment.UsesProfile(new[] { "devenv.exe", argument, value }, "AlarmlistExp"));
        }

        [Theory]
        [InlineData("ProjectTemplates/Example%20Project.almproj", true)]
        [InlineData("../outside.dll", false)]
        [InlineData("%2e%2e/outside.dll", false)]
        [InlineData("/outside.dll", false)]
        public void ReadsEncodedAssetNamesAndRejectsEscapingPaths(string entry, bool valid)
        {
            Directory.CreateDirectory(_directory);
            string vsix = Path.Combine(_directory, "test.vsix");
            using (var archive = ZipFile.Open(vsix, ZipArchiveMode.Create))
            {
                foreach (string name in new[] { "extension.vsixmanifest", "Alarmlist.VisualStudio.dll", entry })
                    using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write("test");
            }
            if (valid) Assert.Contains("ProjectTemplates/Example Project.almproj", VisualStudioTestDeployment.ReadPayload(vsix).Keys);
            else Assert.Throws<InvalidDataException>(() => VisualStudioTestDeployment.ReadPayload(vsix));
        }

        public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    }
}
