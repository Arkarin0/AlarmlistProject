// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Process = System.Diagnostics.Process;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class LocalizationResourceTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "XliffImport")]
        public Task ImportSavedResources2022() => UiTestRun.ExecuteAsync("2022", "XliffImport", _output, (ide, workspace) => CheckImport(ide, workspace, "2022"));
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "XliffImport")]
        public Task ImportSavedResources2026() => UiTestRun.ExecuteAsync("2026", "XliffImport", _output, (ide, workspace) => CheckImport(ide, workspace, "2026"));

        private static void CheckImport(VisualStudioSession ide, UiTestWorkspace workspace, string version)
        {
            workspace.CreateProject(ide);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            string resource = Path.Combine(workspace.ProjectDirectory, "Labels.de.resx");
            WriteResource(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "Inspect {item}");
            WriteResource(resource, "Alt {item}");
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>$(loc:Inspection, item=\"001\")</Name></Alarm></Alarmlist>";
            File.WriteAllText(workspace.AlarmPath, source);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("PropertyGroup", new XElement("AlarmlistCulture", "de"), new XElement("AlarmlistCultures", "keyword;en;de")));
            project.Save(projectPath);
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 1);
            string outputPath = Path.Combine(workspace.ProjectDirectory, "bin", "Debug", "de", "Plant.Alarmlist.xml");
            byte[] oldOutput = File.ReadAllBytes(outputPath), saved = File.ReadAllBytes(resource);
            RunExchange(workspace, version, "export", true, "/t:ExportAlarmlistXliff", "/p:AlarmlistXliffTargetCultures=de");
            string exchangePath = Path.Combine(workspace.ProjectDirectory, "xliff", "Alarmlist.en.de.xlf");
            XDocument exchange = XDocument.Load(exchangePath, LoadOptions.PreserveWhitespace);
            XNamespace ns = "urn:oasis:names:tc:xliff:document:2.0";
            XElement target = exchange.Descendants(ns + "target").Single();
            target.ReplaceNodes(new XText("Prüfen "), new XElement(target.Element(ns + "ph")));
            exchange.Save(exchangePath, SaveOptions.DisableFormatting);
            string selection = "/p:AlarmlistXliffImportFiles=" + exchangePath;

            // XML-buffer edits are protected independently of the native cell editor.
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, resource, File.ReadAllText(resource).Replace("Alt", "Private XML")));
            RunExchange(workspace, version, "dry-run-open-xml", true, "/t:ImportAlarmlistXliff", selection);
            string rejected = RunExchange(workspace, version, "refuse-dirty-xml", false, "/t:ImportAlarmlistXliff", selection, "/p:AlarmlistXliffApply=true");
            Assert.Contains("Resource is open in Visual Studio", rejected);
            Assert.Contains("Private XML", ide.ReadText(resource)); Assert.Equal(saved, File.ReadAllBytes(resource));
            ide.Automation.Documents.Item(resource).Close(vsSaveChanges.vsSaveChangesNo);

            using var automation = new UIA3Automation();
            using var application = FlaUI.Core.Application.Attach(ide.ProcessId);
            var window = application.GetMainWindow(automation, TimeSpan.FromSeconds(60));
            // The experimental profile can restore a floating Preview window over
            // the resource cells. Hide it only in this test's owned IDE.
            foreach (EnvDTE.Window toolWindow in ide.Automation.Windows)
                if (toolWindow.Caption == "Alarmlist Preview") toolWindow.Visible = false;
            ide.Automation.Solution.Projects.Item(1).ProjectItems.Item("Labels.de.resx").Open(Constants.vsViewKindPrimary).Activate();
            window.SetForeground();
            AutomationElement table = null;
            VisualStudioSession.Wait(() => (table = window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable").And(cf.ByControlType(ControlType.DataGrid))))
                ?.FindFirstDescendant(cf => cf.ByName("Alt {item}").And(cf.ByControlType(ControlType.Text))) != null, "native target resource table");
            window.SetForeground();
            table.Focus();
            TextBox draft = ResourceEditorTests.BeginEdit(ide, table, "Alt {item}");
            ResourceEditorTests.ReplaceDraft(ide, draft, "Private native {item}");
            rejected = RunExchange(workspace, version, "refuse-native-draft", false, "/t:ImportAlarmlistXliff", selection, "/p:AlarmlistXliffApply=true");
            Assert.Contains("Resource is open in Visual Studio", rejected);
            Assert.Equal("Private native {item}", draft.Text); Assert.Equal(saved, File.ReadAllBytes(resource));
            ResourceEditorTests.RequireFieldFocus(ide, draft);
            Keyboard.Type(VirtualKeyShort.ESCAPE);
            ide.Automation.Documents.Item(resource).Close(vsSaveChanges.vsSaveChangesNo);

            ide.OpenDesigner(workspace.AlarmPath);
            SelectDesigner(window);
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Alt 001", "pre-import saved preview");
            RunExchange(workspace, version, "apply", true, "/t:ImportAlarmlistXliff", selection, "/p:AlarmlistXliffApply=true");
            VisualStudioSession.Wait(() => Box(window, "Name")?.Text == "Prüfen 001", "saved import refreshes designer preview");
            Assert.Equal(oldOutput, File.ReadAllBytes(outputPath)); Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            byte[] imported = File.ReadAllBytes(resource);
            RunExchange(workspace, version, "repeat", true, "/t:ImportAlarmlistXliff", selection, "/p:AlarmlistXliffApply=true");
            Assert.Equal(imported, File.ReadAllBytes(resource));
            RunExchange(workspace, version, "build", true, "/t:Build");
            Assert.Contains("Prüfen 001", File.ReadAllText(outputPath));
            ide.Automation.Solution.Projects.Item(1).ProjectItems.Item("Labels.de.resx").Open(Constants.vsViewKindPrimary).Activate();
            VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("resourceTable"))
                ?.FindFirstDescendant(cf => cf.ByName("Prüfen {item}").And(cf.ByControlType(ControlType.Text))) != null, "native editor opens imported translation");
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "imported-resource.png"));
        }

        private static string RunExchange(UiTestWorkspace workspace, string version, string label, bool success, params string[] arguments)
        {
            bool desktop = version == "2022";
            string executable = desktop ? Path.Combine(Environment.GetEnvironmentVariable("ALARMLIST_VS2022_PATH"), "MSBuild", "Current", "Bin", "MSBuild.exe") : "dotnet";
            var start = new ProcessStartInfo(executable) { WorkingDirectory = workspace.ProjectDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            if (!desktop) start.ArgumentList.Add("msbuild");
            foreach (string argument in new[] { "Plant.almproj", "/nologo", "/nr:false", "/v:minimal" }.Concat(arguments)) start.ArgumentList.Add(argument);
            using (Process process = Process.Start(start))
            {
                Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { process.Kill(true); throw new TimeoutException("The owned XLIFF process did not finish."); }
                string log = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(workspace.DirectoryPath, label + ".log"), log);
                Assert.True(success ? process.ExitCode == 0 : process.ExitCode != 0, log);
                return log;
            }
        }
    }
}
