// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Xunit;

namespace Alarmlist.VisualStudio.UIIntegrationTests
{
    public sealed partial class ReferenceRenameTests
    {
        [UiFact("2022"), Trait("VSVersion", "2022"), Trait("Area", "AuxiliaryReferencesRename")]
        public Task AuxiliaryReferencesRename2022() => UiTestRun.ExecuteAsync("2022", "AuxiliaryReferencesRename", _output, CheckAuxiliary);
        [UiFact("2026"), Trait("VSVersion", "2026"), Trait("Area", "AuxiliaryReferencesRename")]
        public Task AuxiliaryReferencesRename2026() => UiTestRun.ExecuteAsync("2026", "AuxiliaryReferencesRename", _output, CheckAuxiliary);

        private static void CheckAuxiliary(VisualStudioSession ide, UiTestWorkspace workspace)
        {
            workspace.CreateProject(ide, build: false); ide.Automation.Solution.Close(false);
            const string source = "<Alarmlist><Alarm><FullyQualifiedName>A</FullyQualifiedName><Name>Panel literal</Name><Code>001</Code>"
                + "<Description>$(path:Panel)</Description><TestProcedure><Group Name='Inspection'><Step>Inspect Panel</Step></Group></TestProcedure></Alarm></Alarmlist>";
            const string lookup = "<Lookup xmlns='urn:alarmlist:lookup:1'><Path Key='Panel'><Segment LabelKey='PanelLabel'/></Path></Lookup>";
            string lookupPath = Path.Combine(workspace.ProjectDirectory, "Devices.almlookup");
            File.WriteAllText(workspace.AlarmPath, source); File.WriteAllText(lookupPath, lookup);
            File.WriteAllText(Path.Combine(workspace.ProjectDirectory, "Labels.en.resx"), "<root><data name='PanelLabel'><value>Panel</value></data></root>");
            Alarmlist.VisualStudio.Testing.VisualStudioAutomation.OpenSolution(ide.Automation, workspace.SolutionPath);
            workspace.Build(ide, 1);
            byte[] sourceBytes = File.ReadAllBytes(workspace.AlarmPath), lookupBytes = File.ReadAllBytes(lookupPath);
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(ide.MainWindowHandle).AsWindow();
            ide.Automation.ItemOperations.OpenFile(lookupPath, "{FA3CD31E-987B-443A-9B81-186104E8DAC1}");
            ide.Automation.Documents.Item(lookupPath).Activate();
            Position(lookupPath, "Panel'");
            Rename("Panel", "Console");
            VisualStudioSession.Wait(() => ide.ReadText(lookupPath) == lookup.Replace("Key='Panel'", "Key='Console'")
                && ide.ReadText(workspace.AlarmPath) == source.Replace("path:Panel", "path:Console"), "lookup identity and ALMX use change together");
            AssertSaved();
            ide.Automation.Documents.Item(lookupPath).Activate(); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(lookupPath) == lookup && ide.ReadText(workspace.AlarmPath) == source, "native lookup and ALMX linked Undo");
            ide.OpenDesigner(workspace.AlarmPath); ide.Automation.ExecuteCommand("View.ViewCode", "");
            Position(workspace.AlarmPath, "Inspection'");
            Rename("Inspection", "Checks");
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source.Replace("Name='Inspection'", "Name='Checks'"), "procedure-group identity rename");
            Assert.Equal(lookup, ide.ReadText(lookupPath)); AssertSaved();
            ide.Automation.Documents.Item(workspace.AlarmPath).Activate(); ide.Automation.ExecuteCommand("Edit.Undo");
            VisualStudioSession.Wait(() => ide.ReadText(workspace.AlarmPath) == source, "procedure-group rename Undo");
            AssertSaved();
            window.CaptureToFile(Path.Combine(workspace.DirectoryPath, "auxiliary-reference-rename.png"));

            AutomationElement ById(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id));
            void Position(string path, string token)
            {
                XmlCompletionTests.FocusXmlInput(ide, window, path);
                VisualStudioSession.Wait(() => (ById("Alarmlist.XmlAnalysisStatus")?.Name ?? "").Length == 0, "XML consumer analysis is ready");
                var selection = (TextSelection)ide.Automation.ActiveDocument.Selection;
                int offset = ide.ReadText(path).IndexOf(token, StringComparison.Ordinal) + 2;
                selection.MoveToAbsoluteOffset(offset); Assert.Equal(offset, selection.ActivePoint.AbsoluteCharOffset);
            }
            void Rename(string oldName, string newName)
            {
                ide.Automation.ExecuteCommand("Refactor.Rename");
                VisualStudioSession.Wait(() => ById("RenameNewName")?.IsEnabled == true && ById("RenameNewName").AsTextBox().Text == oldName, "resolved XML rename target " + oldName);
                var pane = ide.Automation.Windows.Item("{a840dc04-70c0-47c2-8618-8d3955e39ccc}");
                pane.IsFloating = true; pane.Width = 1150; pane.Height = 800;
                pane.Left = window.BoundingRectangle.Left + 20; pane.Top = window.BoundingRectangle.Top + 40;
                ById("RenameNewName").AsTextBox().Text = newName;
                window.FindAllDescendants(cf => cf.ByName("Preview rename changes").And(cf.ByControlType(ControlType.Button))).First(item => item.IsEnabled).AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Review ", StringComparison.Ordinal), "auxiliary rename preview");
                var acknowledge = window.FindFirstDescendant(cf => cf.ByName("Acknowledge partial rename and diagnostic changes"))?.AsCheckBox();
                if (acknowledge?.IsOffscreen == false) acknowledge.IsChecked = true;
                VisualStudioSession.Wait(() => ById("RenameApply")?.IsEnabled == true, "approved auxiliary rename");
                ById("RenameApply").AsButton().Invoke();
                VisualStudioSession.Wait(() => (ById("RenameStatus")?.Name ?? "").StartsWith("Rename applied.", StringComparison.Ordinal), "auxiliary rename completes");
                pane.Visible = false;
            }
            void AssertSaved()
            { Assert.Equal(sourceBytes, File.ReadAllBytes(workspace.AlarmPath)); Assert.Equal(lookupBytes, File.ReadAllBytes(lookupPath)); }
        }
    }
}
