// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio")]
    public sealed class XmlCompletionPerformanceTests
    {
        private readonly ITestOutputHelper _output;
        public XmlCompletionPerformanceTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "XmlCompletionPerformance")]
        public Task CompletionPerformance2022() => UiTestRun.ExecuteAsync("2022", "XmlCompletionPerformance", _output, Measure);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "XmlCompletionPerformance")]
        public Task CompletionPerformance2026() => UiTestRun.ExecuteAsync("2026", "XmlCompletionPerformance", _output, Measure);

        private static void Measure(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false);
            string alarms = "<Alarmlist>" + string.Concat(Enumerable.Range(0, 1000).Select(index =>
                "<Alarm><FullyQualifiedName>Perf.Alarm" + index + "</FullyQualifiedName><Name>Alarm " + index + "</Name></Alarm>")) + "</Alarmlist>";
            Assert.True(VisualStudioAutomation.ReplaceXmlBuffer(ide.Automation, workspace.AlarmPath, alarms));
            ide.Automation.Documents.Item(workspace.AlarmPath).Save();
            workspace.Build(ide, 1000);
            string savedAlarms = File.ReadAllText(workspace.AlarmPath);
            string project = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            string lookup = Path.Combine(workspace.ProjectDirectory, "Performance.almlookup");
            File.WriteAllText(lookup, "<Lookup xmlns='urn:alarmlist:lookup:1'/>");
            byte[] savedProject = File.ReadAllBytes(project), savedLookup = File.ReadAllBytes(lookup);
            using var automation = new UIA3Automation(); var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            var reports = new List<object>();
            ide.OpenDesigner(workspace.AlarmPath);
            window.FindFirstDescendant(cf => cf.ByName("XML").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            Run("ALMX embedded", workspace.AlarmPath, alarms.Replace("</Alarmlist>", "<Al¦</Alarmlist>"), "Alarm");
            VisualStudioAutomation.OpenAdditionalEditorView(ide.Automation, workspace.AlarmPath, "FA3CD31E-987B-443A-9B81-186104E8DAC1");
            Run("ALMX native XML", workspace.AlarmPath, alarms.Replace("</Alarmlist>", "<Al¦</Alarmlist>"), "Alarm");
            ide.Automation.ItemOperations.OpenFile(lookup, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
            Run("Lookup native XML", lookup, "<Lookup xmlns='urn:alarmlist:lookup:1'><Li¦", "List");
            ide.Automation.Documents.Item(lookup).Close(vsSaveChanges.vsSaveChangesNo);
            ide.Automation.ItemOperations.OpenFile(project, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
            Run("Project native XML", project, "<Project><ItemGroup><AlarmlistR¦", "AlarmlistResource");
            ide.Automation.Documents.Item(project).Close(vsSaveChanges.vsSaveChangesNo);
            ide.Automation.Documents.Item(workspace.AlarmPath).Close(vsSaveChanges.vsSaveChangesNo);
            Assert.Equal(savedAlarms, File.ReadAllText(workspace.AlarmPath));
            Assert.Equal(savedProject, File.ReadAllBytes(project)); Assert.Equal(savedLookup, File.ReadAllBytes(lookup));
            ide.OpenDesigner(workspace.AlarmPath);
            window.FindFirstDescendant(cf => cf.ByName("XML").And(cf.ByControlType(ControlType.RadioButton))).Patterns.SelectionItem.Pattern.Select();
            Run("ALMX reopened lifecycle probe", workspace.AlarmPath, alarms.Replace("</Alarmlist>", "<Al¦</Alarmlist>"), "Alarm", warmRequests: 0);
            ide.Automation.Documents.Item(workspace.AlarmPath).Close(vsSaveChanges.vsSaveChangesNo);
            File.WriteAllText(Path.Combine(workspace.DirectoryPath, "completion-latency.json"), JsonSerializer.Serialize(new
            {
                AlarmCount = 1000, WarmRequestsPerSurface = 20, NativePollingResolutionMilliseconds = 100,
                Metric = "Command to visible candidate; UI automation and polling included. Descriptive, no timing threshold.",
                Baseline = "Milestone 12 iteration 1 first-use probes; smaller workload, not a controlled latency comparison.",
                Lifecycle = "Closed/reopened ALMX, invoked and dismissed completion; saved files unchanged. View/session detachment has separate unit/native evidence.",
                Surfaces = reports
            }, new JsonSerializerOptions { WriteIndented = true }));

            void Run(string surface, string path, string marked, string candidate, int warmRequests = 20)
            {
                var selection = (TextSelection)ide.Automation.Documents.Item(path).Selection;
                string text = marked.Replace("¦", ""); selection.SelectAll(); selection.Insert(text);
                selection.MoveToAbsoluteOffset(marked.IndexOf('¦') + 1);
                if (Path.GetExtension(path) == ".almx") selection.ActivePoint.TryToShow(vsPaneShowHow.vsPaneShowCentered);
                XmlCompletionTests.FocusXmlInput(ide, window, path);
                var elapsed = new List<double>();
                for (int request = 0; request <= warmRequests; request++)
                {
                    ide.RequireForeground(); var timer = Stopwatch.StartNew();
                    ide.Automation.ExecuteCommand("Edit.ListMembers");
                    VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId(candidate)) != null, surface + " completion candidate");
                    timer.Stop(); elapsed.Add(timer.Elapsed.TotalMilliseconds);
                    XmlCompletionTests.RequireXmlInput(ide, window, path); Keyboard.Type(VirtualKeyShort.ESCAPE);
                    VisualStudioSession.Wait(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DefaultCompletionPresenter")) == null, surface + " dismissal");
                    Assert.Equal(text, ide.ReadText(path));
                }
                double[] warm = elapsed.Skip(1).OrderBy(value => value).ToArray();
                reports.Add(new { Surface = surface, FirstUseMilliseconds = elapsed[0], WarmRequests = warm.Length,
                    P50Milliseconds = warm.Length == 0 ? (double?)null : warm[(int)Math.Ceiling(warm.Length * .5) - 1],
                    P95Milliseconds = warm.Length == 0 ? (double?)null : warm[(int)Math.Ceiling(warm.Length * .95) - 1], SamplesMilliseconds = elapsed });
            }
        }
    }
}
