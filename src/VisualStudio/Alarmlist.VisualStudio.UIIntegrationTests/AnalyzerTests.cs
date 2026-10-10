// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Alarmlist.VisualStudio.Testing;
using EnvDTE80;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio"), Trait("Area", "Analyzers")]
    public sealed class AnalyzerTests
    {
        private readonly ITestOutputHelper _output;
        public AnalyzerTests(ITestOutputHelper output) { _output = output; }
        [UiFact("2022"), Trait("VSVersion", "2022")]
        public Task ConfiguredAnalyzer2022() => UiTestRun.ExecuteAsync("2022", "ConfiguredAnalyzer", _output, CheckAnalyzer);
        [UiFact("2026"), Trait("VSVersion", "2026")]
        public Task ConfiguredAnalyzer2026() => UiTestRun.ExecuteAsync("2026", "ConfiguredAnalyzer", _output, CheckAnalyzer);

        private static void CheckAnalyzer(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide);
            VisualStudioAutomation.CloseSolution(ide.Automation, false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>Pump</FullyQualifiedName><Code>P-001</Code></Alarm></Alarmlist>";
            File.WriteAllText(workspace.AlarmPath, source);
            string analyzerPath = Path.Combine(workspace.ProjectDirectory, "Analyzers", "Alarmlist.SampleAnalyzer.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(analyzerPath));
            File.Copy(Path.Combine(AppContext.BaseDirectory, "testassets", "Alarmlist.SampleAnalyzer.dll"), analyzerPath);
            string projectPath = Path.Combine(workspace.ProjectDirectory, "Plant.almproj");
            XDocument project = XDocument.Load(projectPath);
            project.Root.Add(new XElement("ItemGroup", new XElement("AlarmlistAnalyzer", new XAttribute("Include", "Analyzers/Alarmlist.SampleAnalyzer.dll"))));
            project.Save(projectPath);
            string config = Path.Combine(workspace.ProjectDirectory, ".editorconfig");
            WriteConfiguration(config, "warning", "AL-");
            VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            ide.OpenDesigner(workspace.AlarmPath);
            const string message = "Alarm 'Pump' has code 'P-001'; expected prefix 'AL-'.";
            VisualStudioSession.Wait(() => HasDiagnostic(ide, message, vsBuildErrorLevel.vsBuildErrorLevelMedium), "external analyzer live warning before Build");
            Assert.True(VisualStudioAutomation.TryNavigateError(ide.Automation, message));

            WriteConfiguration(config, "none", "AL-");
            VisualStudioSession.Wait(() => !HasDiagnostic(ide, message, null), "saved configuration suppresses the live analyzer rule");
            WriteConfiguration(config, "silent", "AL-");
            // Observe a subsequent published state, rather than treating transient cleared rows as success.
            WriteConfiguration(config, "warning", "PLANT-");
            const string changed = "Alarm 'Pump' has code 'P-001'; expected prefix 'PLANT-'.";
            VisualStudioSession.Wait(() => HasDiagnostic(ide, changed, vsBuildErrorLevel.vsBuildErrorLevelMedium), "saved analyzer option refresh");
            Assert.False(HasDiagnostic(ide, message, null));

            WriteConfiguration(config, "error", "P-");
            VisualStudioSession.Wait(() => !HasDiagnostic(ide, changed, null), "valid alarm clears the rule");
            workspace.Build(ide, 1);
            byte[] previousOutput = File.ReadAllBytes(workspace.OutputPath);
            WriteConfiguration(config, "error", "AL-");
            VisualStudioSession.Wait(() => HasDiagnostic(ide, message, vsBuildErrorLevel.vsBuildErrorLevelHigh), "saved configuration promotes the live rule to error");
            Assert.Equal(previousOutput, File.ReadAllBytes(workspace.OutputPath));
            ide.Automation.Solution.SolutionBuild.Build(true);
            Assert.NotEqual(0, ide.Automation.Solution.SolutionBuild.LastBuildInfo);
            Assert.Equal(previousOutput, File.ReadAllBytes(workspace.OutputPath));
            Assert.Equal(source, File.ReadAllText(workspace.AlarmPath));
            // Capturing assemblies must not keep the user's analyzer file locked.
            using (File.Open(analyzerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        private static void WriteConfiguration(string path, string severity, string prefix)
        {
            string text = "root = true\n[*.almx]\nalarmlist_diagnostic.SAMPLE001.severity = " + severity
                + "\nalarmlist_sample.code_prefix = " + prefix + "\n";
            for (int attempt = 0; ; attempt++)
            {
                try { File.WriteAllText(path, text); return; }
                // The IDE can briefly hold the saved configuration while processing
                // the previous change. Retry only sharing/lock violations, for 5s.
                catch (IOException exception) when (attempt < 100
                    && (exception.HResult == unchecked((int)0x80070020) || exception.HResult == unchecked((int)0x80070021)))
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        private static bool HasDiagnostic(VisualStudioSession ide, string description, vsBuildErrorLevel? level)
        {
            var items = ((DTE2)ide.Automation).ToolWindows.ErrorList.ErrorItems;
            for (int i = 1; i <= items.Count; i++)
            {
                var item = items.Item(i);
                if (item.Description == description && (!level.HasValue || item.ErrorLevel == level.Value)) return true;
            }
            return false;
        }
    }
}
