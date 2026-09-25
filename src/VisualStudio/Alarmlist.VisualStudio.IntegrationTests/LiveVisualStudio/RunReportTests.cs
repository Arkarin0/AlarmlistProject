// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using Xunit;

namespace Alarmlist.VisualStudio.IntegrationTests.LiveVisualStudio
{
    public sealed class RunReportTests
    {
        [Fact]
        public void AcceptsCompleteSamplesAndUnavailableClrCounters()
        {
            LiveVisualStudioRun.Validate(Report(), 17, true, 2, 1000);
        }

        [Fact]
        public void FunctionalSmokeDoesNotRequireMeasurements()
        {
            XElement report = Report();
            report.SetAttributeValue("Measurements", false);
            report.SetAttributeValue("SourcesPreserved", false);
            report.RemoveNodes();
            LiveVisualStudioRun.Validate(report, 17, false, 2, 1000);
        }

        [Theory]
        [InlineData("valid"), InlineData("driver"), InlineData("executable"), InlineData("outside")]
        [InlineData("prefix"), InlineData("traversal"), InlineData("pid"), InlineData("start"), InlineData("schema")]
        public void CleanupRequiresTheOwnedFixtureProcess(string fault)
        {
            string root = Path.Combine(Path.GetTempPath(), "Alarmlist smoke");
            string executable = Path.Combine(Path.GetTempPath(), "VS", "devenv.exe");
            var receipt = new XElement("OwnedIde", new XAttribute("DriverProcessId", 123),
                new XAttribute("ProcessId", 456), new XAttribute("StartUtcTicks", 789),
                new XAttribute("Executable", executable), new XAttribute("Solution", Path.Combine(root, "run", "Smoke.sln")));
            switch (fault)
            {
                case "valid": break;
                case "driver": receipt.SetAttributeValue("DriverProcessId", 999); break;
                case "executable": receipt.SetAttributeValue("Executable", Path.Combine(Path.GetTempPath(), "OtherVS", "devenv.exe")); break;
                case "outside": receipt.SetAttributeValue("Solution", Path.Combine(Path.GetTempPath(), "User.sln")); break;
                case "prefix": receipt.SetAttributeValue("Solution", Path.Combine(root + "-other", "User.sln")); break;
                case "traversal": receipt.SetAttributeValue("Solution", Path.Combine(root, "..", "User.sln")); break;
                case "pid": receipt.SetAttributeValue("ProcessId", 0); break;
                case "start": receipt.SetAttributeValue("StartUtcTicks", -1); break;
                case "schema": receipt.Name = "OtherProcess"; break;
                default: throw new ArgumentException(fault);
            }
            if (fault == "valid")
                Assert.Equal(Tuple.Create(456, 789L), LiveVisualStudioRun.ValidateOwnership(receipt, 123, executable, root));
            else
                Assert.Throws<InvalidDataException>(() => LiveVisualStudioRun.ValidateOwnership(receipt, 123, executable, root));
        }

        [Theory]
        [InlineData("failed"), InlineData("cleanup"), InlineData("host"), InlineData("mode")]
        [InlineData("count"), InlineData("source"), InlineData("missing"), InlineData("duplicate")]
        [InlineData("warmup"), InlineData("nan"), InlineData("negative"), InlineData("documents")]
        [InlineData("memory"), InlineData("counter"), InlineData("schema"), InlineData("bitness")]
        public void RejectsIncompleteOrInvalidEvidence(string fault)
        {
            XElement report = Report();
            XElement latency = report.Element("Latencies").Elements().Last();
            XElement memory = report.Element("Memory").Elements().Last();
            switch (fault)
            {
                case "failed": report.SetAttributeValue("Succeeded", false); break;
                case "cleanup": report.SetAttributeValue("CleanupCompleted", false); break;
                case "host": report.SetAttributeValue("HostVersion", "18.0.0.0"); break;
                case "mode": report.SetAttributeValue("Measurements", false); break;
                case "count": report.SetAttributeValue("Iterations", 1); break;
                case "source": report.SetAttributeValue("SourcesPreserved", false); break;
                case "missing": latency.Remove(); break;
                case "duplicate": latency.SetAttributeValue("Sample", 1); break;
                case "warmup": latency.SetAttributeValue("Warmup", true); break;
                case "nan": latency.SetAttributeValue("Milliseconds", "NaN"); break;
                case "negative": latency.SetAttributeValue("Milliseconds", "-1"); break;
                case "documents": memory.SetAttributeValue("OpenDocuments", 1); break;
                case "memory": memory.SetAttributeValue("PrivateBytes", 0); break;
                case "counter": memory.SetAttributeValue("ManagedHeapBytes", "invalid"); break;
                case "schema": report.SetAttributeValue("SchemaVersion", "2"); break;
                case "bitness": report.SetAttributeValue("DriverBitness", 32); break;
                default: throw new ArgumentException(fault);
            }
            Assert.Throws<InvalidDataException>(() => LiveVisualStudioRun.Validate(report, 17, true, 2, 1000));
        }

        private static XElement Report()
        {
            return new XElement("LiveVisualStudioRun", new XAttribute("SchemaVersion", 1), new XAttribute("Succeeded", true),
                new XAttribute("CleanupCompleted", true), new XAttribute("DriverBitness", 64), new XAttribute("HostVersion", "17.14.1.0"), new XAttribute("Measurements", true),
                new XAttribute("Iterations", 2), new XAttribute("AdditionalAlarms", 1000), new XAttribute("SourcesPreserved", true),
                new XElement("Latencies", new[] { "XmlBuffer", "DesignerReopen" }.SelectMany(mode => Enumerable.Range(-1, 4).Select(sample =>
                    new XElement("Sample", new XAttribute("Mode", mode), new XAttribute("Sample", sample),
                        new XAttribute("Warmup", sample <= 0), new XAttribute("Milliseconds", "123.45"))))),
                new XElement("Memory", Enumerable.Range(-1, 4).Select(sample => new XElement("Sample", new XAttribute("Sample", sample),
                    new XAttribute("Warmup", sample <= 0), new XAttribute("PrivateBytes", 1000000), new XAttribute("WorkingSetBytes", 2000000),
                    new XAttribute("Handles", 100), new XAttribute("OpenDocuments", 0), new XAttribute("CapturedUtc", "2026-09-25T12:00:00Z"),
                    new XAttribute("ManagedHeapBytes", ""), new XAttribute("Gen2Collections", "")))));
        }
    }
}
