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
            BuildProject(rootDir, projectFilePath, outputFile);

            var document = XDocument.Load(outputFile);
            var alarms = document.Root.Elements("Alarm").ToArray();
            Assert.Equal("AlarmList", document.Root.Name.LocalName);
            Assert.Equal(3, alarms.Length);
            Assert.Contains(alarms, alarm => alarm.Element("Code")?.Value == "F-002");
            Assert.DoesNotContain(alarms, alarm => alarm.Element("Code")?.Value == "C-001");
        }

        [Fact]
        public void TestBuildTargetResolvesReferenceFromAnotherFile()
        {
            var rootDir = FindSampleDataDirectory();
            var projectFilePath = Path.Combine(rootDir, "SampleData.almproj");
            var outputFile = Path.Combine(rootDir, "bin", "SampleData.Alarmlist.xml");
            BuildProject(rootDir, projectFilePath, outputFile);

            var document = XDocument.Load(outputFile);
            var referencedAlarm = document.Root
                .Elements("Alarm")
                .Single(alarm => alarm.Element("Code")?.Value == "F-002");

            Assert.Equal("Fire", referencedAlarm.Element("Category")?.Value);
            Assert.Contains(
                referencedAlarm.Element("TestProcedure")?.Element("Instructions")?.Elements("TestProcedureItem") ?? Enumerable.Empty<XElement>(),
                item => item.Value == "Inspect the affected detector and surrounding area.");
            Assert.Contains(
                referencedAlarm.Element("TestProcedure")?.Element("Instructions")?.Elements("TestProcedureItem") ?? Enumerable.Empty<XElement>(),
                item => item.Value == "Confirm the panel zone and detector address.");
        }

        [Fact]
        public void TestBuildTargetWithConditionallyCompiledSampleData()
        {
            var rootDir = FindSampleDataDirectory();
            var projectFilePath = Path.Combine(rootDir, "SampleData.almproj");
            var outputFile = Path.Combine(rootDir, "bin", "SampleData.Alarmlist.xml");
            BuildProject(rootDir, projectFilePath, outputFile, "/p:IncludeConditionalAlarms=true");

            var document = XDocument.Load(outputFile);
            var alarms = document.Root.Elements("Alarm").ToArray();
            Assert.Equal("AlarmList", document.Root.Name.LocalName);
            Assert.Equal(4, alarms.Length);
            Assert.Contains(alarms, alarm => alarm.Element("Code")?.Value == "C-001");
        }

        [Fact]
        public void TestBuildTargetReportsDiagnosticWhenReferencedAlarmFromAnotherFileIsMissing()
        {
            var rootDir = FindSampleDataDirectory();
            var projectFilePath = Path.Combine(rootDir, "SampleData.almproj");
            var outputFile = Path.Combine(rootDir, "bin", "SampleData.Alarmlist.xml");

            var result = RunBuildProject(rootDir, projectFilePath, outputFile, "/p:IncludeMissingReferenceAlarm=true");

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(File.Exists(outputFile));
            Assert.Contains("ALM0002", result.Output);
            Assert.Contains("Sample.Plant.Reference.DoesNotExist", result.Output);
        }

        private static void BuildProject(string rootDir, string projectFilePath, string outputFile, params string[] additionalArguments)
        {
            var result = RunBuildProject(rootDir, projectFilePath, outputFile, additionalArguments);

            Assert.True(result.ExitCode == 0, result.Output);
            Assert.True(File.Exists(outputFile), result.Output);
        }

        private static BuildResult RunBuildProject(string rootDir, string projectFilePath, string outputFile, params string[] additionalArguments)
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);

            var arguments = string.Join(" ", new[]
            {
                $"msbuild \"{projectFilePath}\"",
                "/t:Build",
                "/p:Configuration=Debug",
                "/v:minimal"
            }.Concat(additionalArguments));

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
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

            return new BuildResult(process.ExitCode, output + Environment.NewLine + error);
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

        private sealed class BuildResult
        {
            public BuildResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }

            public string Output { get; }
        }
    }
}
