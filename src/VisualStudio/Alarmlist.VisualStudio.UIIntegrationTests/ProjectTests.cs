// Created/modified by Arkarin0 under one ore more license(s).

using System.IO;
using System.Threading.Tasks;
using Alarmlist.VisualStudio.Testing;
using EnvDTE;
using Xunit;
using Xunit.Abstractions;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    [Trait("Category", "UIIntegration"), Trait("Host", "VisualStudio")]
    public sealed class ProjectTests
    {
        private readonly ITestOutputHelper _output;
        public ProjectTests(ITestOutputHelper output) { _output = output; }

        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "ProjectItems")]
        public Task TemplatesAndFileLifecycle2022() => Run("2022", nameof(TemplatesAndFileLifecycle), TemplatesAndFileLifecycle);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "ProjectItems")]
        public Task TemplatesAndFileLifecycle2026() => Run("2026", nameof(TemplatesAndFileLifecycle), TemplatesAndFileLifecycle);
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SharedEditor")]
        public Task SharedEditor2022() => Run("2022", "SharedEditor", SharedEditor);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SharedEditor")]
        public Task SharedEditor2026() => Run("2026", "SharedEditor", SharedEditor);
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SaveAs")]
        public Task DesignerSaveAs2022() => SaveAs("2022", false);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SaveAs")]
        public Task DesignerSaveAs2026() => SaveAs("2026", false);
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "SaveAs")]
        public Task XmlSaveAs2022() => SaveAs("2022", true);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "SaveAs")]
        public Task XmlSaveAs2026() => SaveAs("2026", true);
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "LiveDiagnostics")]
        public Task LiveDiagnostics2022() => Run("2022", "LiveDiagnostics", LiveDiagnostics);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "LiveDiagnostics")]
        public Task LiveDiagnostics2026() => Run("2026", "LiveDiagnostics", LiveDiagnostics);
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "Build")]
        public Task BuildFailureCleanAndRebuild2022() => Run("2022", nameof(BuildFailureCleanAndRebuild), BuildFailureCleanAndRebuild);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "Build")]
        public Task BuildFailureCleanAndRebuild2026() => Run("2026", nameof(BuildFailureCleanAndRebuild), BuildFailureCleanAndRebuild);

        private Task Run(string version, string scenario, System.Action<VisualStudioSession, UiTestWorkspace> action) =>
            UiTestRun.ExecuteAsync(version, scenario, _output, (ide, workspace) =>
            {
                workspace.CreateProject(ide);
                action(ide, workspace);
            });

        private Task SaveAs(string version, bool xml) => Run(version, xml ? "XmlSaveAs" : "DesignerSaveAs", (ide, workspace) =>
        {
            VisualStudioAutomation.VerifySaveAs(ide.Automation, workspace.AlarmPath,
                xml ? "fa3cd31e-987b-443a-9b81-186104e8dac1" : "6b8cc287-1eb4-4b75-a8cb-4b8e13b60c2f");
            // Save As must not introduce duplicate compiled inputs through default globs.
            workspace.Build(ide, 1);
        });

        private static void TemplatesAndFileLifecycle(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            DTE dte = ide.Automation;
            VisualStudioAutomation.AddItem(dte, VisualStudioAutomation.GetTemplate(dte, true));
            VisualStudioSession.Wait(() => VisualStudioAutomation.FindItem(dte, "sources", "Additional.almx", ""), "new item");
            workspace.Build(ide, 2);
            Assert.True(VisualStudioAutomation.FindItem(dte, "sources", "Additional.almx", "rename"));
            VisualStudioAutomation.CloseSolution(dte, true);
            VisualStudioAutomation.OpenSolution(dte, workspace.SolutionPath);
            VisualStudioSession.Wait(() => dte.Solution.Projects.Count == 1
                && VisualStudioAutomation.FindItem(dte, "sources", "Renamed.almx", ""), "renamed item after solution reopening");
            Assert.True(VisualStudioAutomation.FindItem(dte, "sources", "Renamed.almx", "delete"));
            workspace.Build(ide, 1);
        }

        private static void SharedEditor(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            VisualStudioAutomation.VerifyEditor(ide.Automation, workspace.AlarmPath);
            workspace.Build(ide, 1);
        }

        private static void LiveDiagnostics(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            DTE dte = ide.Automation;
            string outputBefore = File.ReadAllText(workspace.OutputPath);
            string path = Path.Combine(workspace.ProjectDirectory, "Live.almx");
            const string source = "<Alarmlist>\r\n  <Alarm><FullyQualifiedName>Live</FullyQualifiedName>\r\n    <ReferenceName>MissingLive</ReferenceName></Alarm>\r\n</Alarmlist>";
            const string message = "Alarm 'Live' references missing alarm 'MissingLive'.";
            File.WriteAllText(path, source);
            VisualStudioSession.Wait(() => VisualStudioAutomation.FindItem(dte, null, "Live.almx", ""), "live input discovery");
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(dte, message), "live diagnostic before Build");
            VisualStudioSession.Wait(() =>
            {
                if (VisualStudioAutomation.IsAtLocation(dte, path, 3, 6)) return true;
                VisualStudioAutomation.TryNavigateError(dte, message);
                return false;
            }, "live diagnostic source position");
            VisualStudioAutomation.CloseSolution(dte, false);
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(dte, message), "unload clears live diagnostics");
            VisualStudioAutomation.OpenSolution(dte, workspace.SolutionPath);
            VisualStudioSession.Wait(() => dte.Solution.Projects.Count == 1
                && VisualStudioAutomation.FindItem(dte, null, "Live.almx", ""), "reopened input tree");
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(dte, message), "reopen republishes live diagnostics");
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(dte, path,
                source.Replace("<ReferenceName>MissingLive</ReferenceName>", "")), "XML-only unsaved correction");
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(dte, message), "unsaved correction clears error");
            Assert.Equal(source, File.ReadAllText(path));
            Assert.Equal(outputBefore, File.ReadAllText(workspace.OutputPath));
            dte.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(dte, message), "discard restores disk diagnostics");
            Assert.True(VisualStudioAutomation.FindItem(dte, null, "Live.almx", "delete"));
            VisualStudioSession.Wait(() => !VisualStudioAutomation.HasErrorDescription(dte, message), "removed input clears error");
        }

        private static void BuildFailureCleanAndRebuild(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            DTE dte = ide.Automation;
            string before = File.ReadAllText(workspace.OutputPath);
            string path = Path.Combine(workspace.ProjectDirectory, "Invalid.almx");
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>Invalid</FullyQualifiedName><ReferenceName>Missing</ReferenceName></Alarm></Alarmlist>";
            File.WriteAllText(path, source);
            VisualStudioSession.Wait(() => VisualStudioAutomation.FindItem(dte, null, "Invalid.almx", ""), "globbed invalid input");
            dte.Solution.SolutionBuild.Build(true);
            Assert.NotEqual(0, dte.Solution.SolutionBuild.LastBuildInfo);
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasCompilerError(dte), "Build error in Error List");
            Assert.Equal(before, File.ReadAllText(workspace.OutputPath));
            VisualStudioSession.Wait(() => VisualStudioAutomation.ReplaceXmlBuffer(dte, path,
                source.Replace(">Missing<", ">NewMissing<")), "XML-only reference edit");
            VisualStudioSession.Wait(() => VisualStudioAutomation.HasErrorDescription(dte,
                "Alarm 'Invalid' references missing alarm 'NewMissing'."), "updated live error beside Build error");
            Assert.True(VisualStudioAutomation.HasCompilerError(dte), "Live analysis removed a Build error.");
            Assert.Equal(source, File.ReadAllText(path));
            Assert.Equal(before, File.ReadAllText(workspace.OutputPath));
            dte.Documents.Item(path).Close(vsSaveChanges.vsSaveChangesNo);
            Assert.True(VisualStudioAutomation.FindItem(dte, null, "Invalid.almx", "delete"));
            dte.Solution.SolutionBuild.Clean(true);
            Assert.Equal(0, dte.Solution.SolutionBuild.LastBuildInfo);
            Assert.False(File.Exists(workspace.OutputPath));
            dte.ExecuteCommand("Build.RebuildSolution", "");
            VisualStudioSession.Wait(() => File.Exists(workspace.OutputPath)
                && dte.Solution.SolutionBuild.BuildState != vsBuildState.vsBuildStateInProgress, "Rebuild completion");
            Assert.Equal(0, dte.Solution.SolutionBuild.LastBuildInfo);
            workspace.AssertAlarmCount(1);
        }
    }
}
