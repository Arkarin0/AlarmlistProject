using Xunit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Alarmlist.MSBuild.IntegrationTests
{
    public class AlarmlistTaskTests
    {
        [Fact]
        public void TestBuildTargetWithSampledata()
        {
            var rootDir = FindSampleDataDirectory();
            var projectFilePath = Path.Combine(rootDir, "SampleData.almproj");
            var outputFile = Path.Combine(rootDir, "bin", "SampleData.Alarmlist.xml");
            if (File.Exists(outputFile))
                File.Delete(outputFile);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"msbuild \"{projectFilePath}\" /t:Build /p:Configuration=Debug /v:minimal",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = rootDir
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, output + Environment.NewLine + error);
            Assert.True(File.Exists(outputFile), output + Environment.NewLine + error);

            var document = XDocument.Load(outputFile);
            var alarms = document.Root.Elements("Alarm").ToArray();
            Assert.Equal("AlarmList", document.Root.Name.LocalName);
            Assert.Equal(3, alarms.Length);
            Assert.Contains(alarms, alarm => alarm.Element("Code")?.Value == "F-002");
        }

        private static string FindSampleDataDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var sampleDataDirectory = Path.Combine(directory.FullName, "src", "MSBuild", "SampleData");
                if (Directory.Exists(sampleDataDirectory))
                    return sampleDataDirectory;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate src\\MSBuild\\SampleData.");
        }
    }
}
