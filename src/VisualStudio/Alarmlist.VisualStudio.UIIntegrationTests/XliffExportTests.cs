// Created/modified by Arkarin0 under one ore more license(s).

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Host", "VisualStudio"), Trait("Area", "Xliff")]
    public sealed class XliffExportTests
    {
        private readonly ITestOutputHelper _output;
        public XliffExportTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ExportSavedResources2022() => UiTestRun.ExecuteAsync("2022", "XliffSavedExport", _output, CheckExport);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ExportSavedResources2026() => UiTestRun.ExecuteAsync("2026", "XliffSavedExport", _output, CheckExport);

        private static void CheckExport(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string path = Path.Combine(workspace.ProjectDirectory, "Labels.en.resx");
            new XElement("root", new XElement("data", new XAttribute("name", "Inspection"), new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", "  Inspect $(par1) & 😀\n$$(loc:literal)  "), new XElement("comment", "Operator instruction"))).Save(path);
            string saved = File.ReadAllText(path);
            byte[] output = File.ReadAllBytes(workspace.OutputPath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, path, saved.Replace("Inspect", "Unsealed")));
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workspace.ProjectDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in new[] { "msbuild", "Plant.almproj", "/nologo", "/nr:false", "/v:minimal", "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de" })
                start.ArgumentList.Add(argument);
            using (Process process = Process.Start(start))
            {
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill(true);
                    throw new System.TimeoutException("The owned XLIFF export process did not finish.");
                }
                string log = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, "export.log"), log);
                Assert.True(process.ExitCode == 0, log);
            }
            string xliff = File.ReadAllText(Path.Combine(workspace.ProjectDirectory, "xliff", "Alarmlist.en.de.xlf"));
            Assert.Contains("Inspect", xliff);
            Assert.DoesNotContain("Unsealed", xliff);
            Assert.Equal(saved, File.ReadAllText(path));
            Assert.Contains("Unsealed", ide.ReadText(path));
            EnvDTE.Document document = ide.Automation.Documents.Item(path);
            try { Assert.False(document.Saved); }
            finally { Marshal.ReleaseComObject(document); }
            Assert.Equal(output, File.ReadAllBytes(workspace.OutputPath));
        }
    }
}
